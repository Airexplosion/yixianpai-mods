using System;
using System.Collections.Generic;
using System.Globalization;
using Yx.ModSdk;
using Proto;

namespace YxCanju
{
    /// <summary>
    /// 一键摆牌：把牌桌摆成目标摆法（每个已解锁格一个卡 id，0 = 空格）。
    /// 只走游戏自己的移牌入口 —— <c>CardPanel.MoveToGrid</c> / <c>MoveToHand</c>（与玩家拖牌同一入口，项目绑定规则）。
    /// 每步重新读实时牌面、只做一个动作，等这一步在牌面上生效（或超时）再做下一步 ——
    /// 游戏的移牌请求是异步的，且共用一个缓存请求对象，不能连着同步调。
    ///   第 1 段：目标里用不到的多余牌先收回手牌（这样摆的过程中消耗 / 持续牌不会超上限）；
    ///   第 2 段：从左到右找第一个不对的格：目标是空 → 收回手牌；否则优先从牌桌别的格换过来（格与格互换），没有再从手牌放上去。
    ///   从不把牌拖到同 id 的牌上（那会触发合成升级）：只在格子当前的牌 ≠ 目标牌时才动。
    /// </summary>
    public sealed class AutoPlacer
    {
        const float StepSeconds = 0.35f;
        const float PendingTimeout = 4f;
        const int MaxSteps = 60;

        readonly ModContext _ctx;
        int[] _target;
        int _steps;
        bool _running;
        int _pendingSlot = -1;
        int _pendingId;
        float _pendingWait;
        Action<string> _onDone;
        readonly Action<Dictionary<string, object>> _prepareRequest;
        string _signature, _pendingSignature;

        public AutoPlacer(ModContext ctx) : this(ctx, null) { }
        public AutoPlacer(ModContext ctx, Action<Dictionary<string, object>> prepareRequest)
        { _ctx = ctx; _prepareRequest = prepareRequest; }

        public bool Running { get { return _running; } }

        public void Start(int[] target, string signature, Action<string> onDone)
        {
            if (_running) return;
            _target = target;
            _signature = signature;
            _pendingSignature = null;
            _onDone = onDone;
            _steps = 0;
            _pendingSlot = -1;
            _running = true;
            Step();
        }

        public void Stop(string why)
        {
            if (!_running) return;
            _running = false;
            if (_onDone != null) _onDone(why);
        }

        void Next() { _ctx.MainThread.Delay(StepSeconds, Step); }

        void Step()
        {
            if (!_running) return;
            try { StepCore(); }
            catch (Exception e) { Stop("摆牌出错：" + e.Message); }
        }

        void StepCore()
        {
            // 每一步重新检查模式与局面，换边、离场、修改属性后不能继续使用旧结果。
            if (!PuzzleReader.CanSolve(_ctx, BattleManager.Instance)) { Stop("已离开可求解的备战界面"); return; }
            if (++_steps > MaxSteps) { Stop("步数超过上限，已停止（牌面可能被手动改过）"); return; }
            BattlePanel bp = ILRPanelBase.FindILRPanel<BattlePanel>();
            CardPanel cp = bp != null ? bp.FindILRSubPanel<CardPanel>() : null;
            if (cp == null) { Stop("不在摆牌界面"); return; }
            if (cp.isDealingCards) { Next(); return; }

            var grids = new List<CardGrid>();
            var items = new List<CardItem>();
            var board = new List<int>();
            List<CardGrid> all = cp.GetCardGrids();
            if (all != null)
                for (int i = 0; i < all.Count; i++)
                {
                    CardGrid g = all[i];
                    if (g == null || !g.unlocked) continue;
                    CardItem it = g.GetCard();
                    grids.Add(g);
                    items.Add(it);
                    board.Add(it != null && it.cardInfo != null ? it.cardInfo.id : 0);
                }
            List<CardItem> hand = cp.GetHandCards();
            if (grids.Count < _target.Length) { Stop("格子数和求解时不一致，停止"); return; }

            // 上一步还没在牌面上生效：等（有牌在播动画也等）
            if (_pendingSlot >= 0)
            {
                if (board[_pendingSlot] != _pendingId && _pendingWait < PendingTimeout)
                {
                    _pendingWait += StepSeconds;
                    Next();
                    return;
                }
                if (board[_pendingSlot] != _pendingId) { Stop("移牌未生效，已停止，请重试"); return; }
                _pendingSlot = -1;
            }
            if (AnyBusy(items) || AnyBusy(hand)) { Next(); return; }
            string error;
            var request = PuzzleReader.Read(_ctx, out error);
            if (request != null && _prepareRequest != null) _prepareRequest(request);
            string actual = request == null ? null : PuzzleReader.Signature(request);
            if (_pendingSignature != null && actual == _signature && _pendingWait < PendingTimeout)
            {
                _pendingWait += StepSeconds;
                Next();
                return;
            }
            if (actual == null || actual != (_pendingSignature ?? _signature))
            { Stop("局面已改变，请重新求解"); return; }

            _signature = actual;
            _pendingSignature = null;

            // 第 1 段：多余的牌收回手牌
            int excess = ExcessSlot(board);
            if (excess >= 0)
            {
                ReturnToHand(cp, items[excess], request);
                Pending(excess, 0);
                return;
            }
            // 第 2 段：第一个不对的格
            for (int i = 0; i < _target.Length; i++)
            {
                int want = _target[i];
                if (board[i] == want || want == 0) continue;
                for (int j = 0; j < board.Count; j++)
                {
                    if (j == i || board[j] != want) continue;
                    if (j < _target.Length && _target[j] == want) continue;   // 那张已经在它该在的位置
                    cp.MoveToGrid(items[j], grids[i]);                          // 格与格互换
                    Pending(i, want);
                    return;
                }
                if (hand != null)
                    for (int h = 0; h < hand.Count; h++)
                    {
                        CardItem hc = hand[h];
                        if (hc == null || hc.cardInfo == null || hc.cardInfo.id != want) continue;
                        cp.MoveToGrid(hc, grids[i]);                            // 手牌放上去（原来的牌回手牌）
                        Pending(i, want);
                        return;
                    }
                Stop("找不到要放的牌（" + want.ToString(CultureInfo.InvariantCulture) + "），停止");
                return;
            }
            for (int i = 0; i < _target.Length; i++)
                if (_target[i] == 0 && board[i] != 0)
                {
                    ReturnToHand(cp, items[i], request);
                    Pending(i, 0);
                    return;
                }
            Stop("摆好了");
        }

        void ReturnToHand(CardPanel panel, CardItem item, Dictionary<string, object> request)
        {
            if (item.cardConfig != null && item.cardConfig.subcategory == Subcategory.ZhuanHuan)
                _pendingSignature = PlacementSignature.AfterReturn(request, item.cardInfo.id, item.cardConfig.linkageId);
            panel.MoveToHand(item);
        }

        void Pending(int slot, int id)
        {
            _pendingSlot = slot;
            _pendingId = id;
            _pendingWait = 0f;
            Next();
        }

        /// <summary>牌桌上某种牌比目标多出来的那一格（优先不在目标位置上的）；没有返回 -1。</summary>
        int ExcessSlot(List<int> board)
        {
            for (int i = 0; i < board.Count; i++)
            {
                int id = board[i];
                if (id == 0) continue;
                if (Count(board, id) <= Count(_target, id)) continue;
                for (int k = 0; k < board.Count; k++)
                    if (board[k] == id && (k >= _target.Length || _target[k] != id)) return k;
            }
            return -1;
        }

        static int Count(List<int> list, int id)
        {
            int n = 0;
            for (int i = 0; i < list.Count; i++) if (list[i] == id) n++;
            return n;
        }

        static int Count(int[] arr, int id)
        {
            int n = 0;
            for (int i = 0; i < arr.Length; i++) if (arr[i] == id) n++;
            return n;
        }

        static bool AnyBusy(List<CardItem> items)
        {
            if (items == null) return false;
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null && !items[i].interactable) return true;
            return false;
        }
    }
}
