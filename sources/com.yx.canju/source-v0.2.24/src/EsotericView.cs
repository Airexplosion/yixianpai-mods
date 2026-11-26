using System;
using System.Collections.Generic;
using System.Globalization;
using Proto;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yx.ModSdk;
using Yx.ModSdk.Unity;
using Yx.Shared;

namespace YxCanju
{
    /// <summary>
    /// 用游戏自己的「战斗秘籍」抽屉（BattleEsotericDetailPanel）显示求解结果 —— 外观与秘籍一致、真卡图，纯本地：
    ///   本地 new 一个 EsotericData（每套推荐摆法一行，行备注写先手 / 后手的胜负与剩血，第一名另加一行「留在手牌」），
    ///   直接调面板的 Show(data)。显示路径不发任何包；不写 lastFastBattleEsotericId、不走 OnBattleReadyEsotericButtonClick。
    ///   shortId 用一个不属于玩家的负数、complete = false → 面板上与服务器相关的按钮（编辑 / 抄录 / 公开…）都不出现；
    ///   面板自带的「秘籍主面板」按钮会打开玩家真正的秘籍库，这里把它藏掉。
    /// 顶部「求解」只在点击后提交，每个推荐行右上方单独提供「一键摆牌」，循环复用行时重新绑定目标。
    /// 这个抽屉和玩家真正的秘籍共用同一个实例：抽屉一关就删掉我们的按钮、恢复「秘籍主面板」按钮，
    /// 免得玩家之后打开自己的秘籍时还看到它们。
    /// </summary>
    public sealed class EsotericView
    {
        const int LocalShortId = -20260923;
        const string PlaceName = "YxCanjuPlace";
        const string ResolveName = "YxCanjuResolve";

        readonly ModContext _ctx;
        readonly UiKit _ui;
        readonly Action<int> _onPlace;
        readonly Action<int> _onSteps;
        readonly Action _onResolve;
        BattleEsotericDetailPanel _panel;
        UiButton _resolve;
        Transform _mainButton;
        bool _mainButtonActive;
        TextMeshProUGUI _title;
        bool _titleEnabled;
        TMP_FontAsset _buttonFont;
        EsotericData _data;
        bool _solving;
        ISubscription _cellHook;
        ISubscription _showHook;
        readonly List<RowTarget> _targets = new List<RowTarget>();
        readonly List<RowButton> _rowButtons = new List<RowButton>();

        sealed class RowTarget
        {
            public EsotericUnitData Data;
            public int Index;
        }

        // 委托由持有类保留，不能放进 CLR 泛型委托集合。复用行时更新 Target。
        sealed class RowButton
        {
            public EsotericView Owner;
            public Transform Cell;
            public RowTarget Target;
            public UiButton Button, Details;
            public void Steps() { if (Target != null) Owner._onSteps(Target.Index); }
            public void Click()
            {
                if (Target == null || Owner._solving || Owner._panel == null || Owner._panel.hiding) return;
                Owner._onPlace(Target.Index);
            }
        }

        public EsotericView(ModContext ctx, Action<int> onPlace, Action onResolve, Action<int> onSteps)
        {
            _ctx = ctx;
            _ui = new UiKit(ctx);
            _onPlace = onPlace;
            _onResolve = onResolve;
            _onSteps = onSteps;
        }

        public void Install()
        {
            _cellHook = _ctx.Hooks.TryPostfix("EsotericDetailUnitCell", "UpdateContent", 1, OnCellUpdated);
            _showHook = _ctx.Hooks.TryPrefix("BattleEsotericDetailPanel", "Show", 1, OnPanelShow);
        }

        public void Uninstall()
        {
            if (_cellHook != null) _cellHook.Cancel();
            if (_showHook != null) _showHook.Cancel();
            _cellHook = null;
            _showHook = null;
        }

        bool OnPanelShow(HookContext h)
        {
            if (_panel != null && object.ReferenceEquals(h.Instance, _panel)
                && (h.Args.Length == 0 || !object.ReferenceEquals(h.Args[0], _data))) Cleanup();
            return true;
        }

        /// <summary>显示；成功返回 true（准备层没显示时面板容器不可用，返回 false）。</summary>
        public bool Show(Dictionary<string, object> report, int charId, int sect, List<int> pool)
        {
            BattlePanel bp = ILRPanelBase.FindILRPanel<BattlePanel>();
            if (bp == null || bp.readyLayer == null) return false;
            var p = bp.FindILRSubPanelRuntime<BattleEsotericDetailPanel>(bp.readyLayer.subPanelContainer);
            if (p == null) return false;
            if (_panel != null && !object.ReferenceEquals(_panel, p)) Cleanup();
            _panel = p;
            ClearRowButtons();
            _targets.Clear();
            _data = Build(report, charId, sect, pool);
            _buttonFont = SolverButtonStyle.FontFrom(Find(p.transform, "TitleLabel"));
            p.Show(_data);
            if (_mainButton == null)
            {
                _mainButton = Find(p.transform, "EsotericMainButton");
                if (_mainButton != null) _mainButtonActive = _mainButton.gameObject.activeSelf;
            }
            if (_mainButton != null) _mainButton.gameObject.SetActive(false);
            AddButtons(p.transform);
            return true;
        }

        public void SetSolving(bool solving)
        {
            _solving = solving;
            if (_resolve != null && _resolve.IsAlive)
            {
                _resolve.SetText(solving ? "求解中…" : "求解");
                _resolve.SetInteractable(!solving);
            }
            for (int i = 0; i < _rowButtons.Count; i++)
                if (_rowButtons[i].Button != null) _rowButtons[i].Button.SetInteractable(!solving);
        }

        /// <summary>收起抽屉（开始自动摆牌时，让玩家看得到牌在动）。</summary>
        public void Hide()
        {
            try { if (_panel != null && !_panel.hiding) _panel.Hide(); } catch (Exception) { }
        }

        /// <summary>每隔几帧调一次：抽屉关了就撤掉我们的按钮、恢复原按钮。</summary>
        public void Tick()
        {
            if (_panel == null) return;
            bool closed = true;
            try { closed = _panel.hiding || !_panel.gameObject.activeInHierarchy; } catch (Exception) { }
            if (!closed) return;
            Cleanup();
        }

        public void Cleanup()
        {
            ClearRowButtons();
            _targets.Clear();
            if (_resolve != null && _resolve.IsAlive) UnityEngine.Object.Destroy(_resolve.GameObject);
            _resolve = null;
            if (_mainButton != null) { try { _mainButton.gameObject.SetActive(_mainButtonActive); } catch (Exception) { } }
            if (_title != null) _title.enabled = _titleEnabled;
            _title = null;
            _mainButton = null;
            _panel = null;
            _data = null;
        }

        void AddButtons(Transform root)
        {
            if (_resolve != null && _resolve.IsAlive) return;
            Transform t = Find(root, "TitleLabel");
            if (t == null) return;
            // Use the native header action's space so the player's title remains visible.
            var slot = _mainButton as RectTransform;
            bool useSlot = slot != null && slot.parent != null && slot.parent.gameObject.activeInHierarchy
                && slot.rect.width >= 110f && slot.rect.height >= 40f;
            var parent = useSlot ? slot.parent : t.parent;
            if (!useSlot)
            {
                _title = t.GetComponent(typeof(TextMeshProUGUI)) as TextMeshProUGUI;
                if (_title != null) { _titleEnabled = _title.enabled; _title.enabled = false; }
            }
            _resolve = SolverButtonStyle.Create(_ui, parent, ResolveName, "求解", _onResolve,
                new Vector2(0.5f, 0.5f), Vector2.zero, useSlot ? slot.rect.size : new Vector2(200f, 64f), 30f, _buttonFont, true);
            if (_resolve == null || !_resolve.IsAlive)
            {
                if (_title != null) _title.enabled = _titleEnabled;
                return;
            }
            if (useSlot)
            {
                var rt = _resolve.GameObject.transform as RectTransform;
                rt.anchorMin = slot.anchorMin;
                rt.anchorMax = slot.anchorMax;
                rt.pivot = slot.pivot;
                rt.sizeDelta = slot.sizeDelta;
                rt.anchoredPosition = slot.anchoredPosition;
                rt.localScale = slot.localScale;
                rt.SetSiblingIndex(slot.GetSiblingIndex());
            }
            SetSolving(_solving);
        }

        void OnCellUpdated(HookContext h)
        {
            var cell = h.Instance as EsotericDetailUnitCell;
            var data = h.Args.Length == 0 ? null : h.Args[0] as EsotericDetailUnitCell.CellData;
            if (cell == null) return;
            RowButton button = null;
            for (int i = 0; i < _rowButtons.Count; i++)
                if (_rowButtons[i].Cell == cell.transform) { button = _rowButtons[i]; break; }
            RowTarget target = null;
            if (_panel != null && data != null && IsUnder(cell.transform, _panel.transform))
                for (int i = 0; i < _targets.Count; i++)
                    if (object.ReferenceEquals(_targets[i].Data, data.unitData)) { target = _targets[i]; break; }
            if (target == null)
            {
                if (button != null)
                {
                    button.Target = null;
                    if (button.Details != null && button.Details.IsAlive) button.Details.GameObject.SetActive(false);
                    if (button.Button != null && button.Button.IsAlive) button.Button.GameObject.SetActive(false);
                }
                return;
            }
            if (button == null)
            {
                button = new RowButton { Owner = this, Cell = cell.transform };
                _rowButtons.Add(button);
            }
            button.Target = target;
            if (button.Button == null || !button.Button.IsAlive)
            {
                Transform parent = Find(cell.transform, "Movable");
                if (parent == null) parent = cell.transform;
                Transform cards = Find(parent, "Cards");
                var cardsRt = cards == null ? null : cards.GetComponent(typeof(RectTransform)) as RectTransform;
                if (cardsRt == null) return;
                // 以卡牌条右边为准，不用比可见区域更宽的整行右边；按钮完全在卡牌上方。
                button.Button = SolverButtonStyle.Create(_ui, parent, PlaceName, "摆到牌桌", button.Click,
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 68f), 42f, _buttonFont, true);
                button.Details = SolverButtonStyle.Create(_ui, parent, "YxCanjuSteps", "操作详情", button.Steps,
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 68f), 42f, _buttonFont);
                if (button.Button != null && button.Button.IsAlive)
                {
                    var dst = button.Button.GameObject.transform as RectTransform;
                    Vector3 corner = cardsRt.TransformPoint(new Vector3(cardsRt.rect.xMax, cardsRt.rect.yMax, 0f));
                    dst.position = corner + new Vector3(-dst.rect.width * dst.lossyScale.x / 2f,
                        (6f + dst.rect.height / 2f) * dst.lossyScale.y, 0f);
                    if (button.Details != null && button.Details.IsAlive)
                        button.Details.GameObject.transform.position = dst.position - new Vector3((dst.rect.width + 10f) * dst.lossyScale.x, 0f, 0f);
                }
            }
            if (button.Button != null && button.Button.IsAlive)
            {
                button.Button.GameObject.SetActive(true);
                button.Button.SetInteractable(!_solving);
                if (button.Details != null && button.Details.IsAlive) button.Details.GameObject.SetActive(!_solving);
            }
        }

        static bool IsUnder(Transform child, Transform root)
        {
            for (Transform t = child; t != null; t = t.parent) if (t == root) return true;
            return false;
        }

        void ClearRowButtons()
        {
            for (int i = 0; i < _rowButtons.Count; i++)
            {
                _rowButtons[i].Target = null;
                if (_rowButtons[i].Details != null && _rowButtons[i].Details.IsAlive) UnityEngine.Object.Destroy(_rowButtons[i].Details.GameObject);
                if (_rowButtons[i].Button != null && _rowButtons[i].Button.IsAlive)
                {
                    _rowButtons[i].Button.GameObject.SetActive(false);
                    UnityEngine.Object.Destroy(_rowButtons[i].Button.GameObject);
                }
            }
            _rowButtons.Clear();
        }

        EsotericData Build(Dictionary<string, object> report, int charId, int sect, List<int> pool)
        {
            var d = new EsotericData();
            d.shortId = LocalShortId;
            d.complete = false;
            d.isPrivate = true;
            d.title = "残局求解";
            d.authorName = "残局求解";
            d.createTs = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            d.version = "001.0007.0016";
            try { d.seasonId = SeasonManager.currentSeasonId; } catch (Exception) { }
            d.charId = charId;
            d.sect = (Sect)sect;
            d.remark = report == null ? "" : Summary(report);

            List<object> best = report == null ? null : Json.GetArray(report, "best");
            if (best != null)
                for (int i = 0; i < best.Count; i++)
                {
                    var e = best[i] as Dictionary<string, object>;
                    if (e == null) continue;
                    List<int> cards = Cards(e);
                    var row = new EsotericUnitData();
                    row.level = Level.InvalidLevel;
                    string grids = ResultView.EnchantText(e);
                    row.remark = "方案 " + (i + 1).ToString(CultureInfo.InvariantCulture) + "：" + (grids.Length > 0 ? grids + " · " : "") + Orders(e) + EmptySlots(cards);
                    // 0 是游戏的普通攻击卡，用它保留空位的视觉位置。
                    // -999999 会被秘籍面板隐藏，布局随即收紧，导致预览与实际摆牌错位。
                    for (int k = 0; k < cards.Count; k++) row.cards.Add(cards[k]);
                    d.unitDatas.Add(row);
                    _targets.Add(new RowTarget { Data = row, Index = i });
                    if (i == 0 && e.ContainsKey("remainingHand")) AddHandRow(d, Cards(e, "remainingHand"), new List<int>());
                    else if (i == 0) AddHandRow(d, pool, cards);
                }
            d.unitCount = d.unitDatas.Count;
            if (d.unitDatas.Count > 0) d.cover = d.unitDatas[0];
            return d;
        }

        static string EmptySlots(List<int> cards)
        {
            string slots = "";
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != 0) continue;
                if (slots.Length > 0) slots = slots + "、";
                slots = slots + (i + 1).ToString(CultureInfo.InvariantCulture);
            }
            return slots.Length == 0 ? "" : "；第 " + slots + " 格留空（图中以普通攻击占位）";
        }

        /// <summary>第一名没上桌的牌：留在手牌。</summary>
        static void AddHandRow(EsotericData d, List<int> pool, List<int> board)
        {
            var rest = new List<int>(pool);
            for (int k = 0; k < board.Count; k++)
            {
                int at = rest.IndexOf(board[k]);
                if (at >= 0) rest.RemoveAt(at);
            }
            if (rest.Count == 0) return;
            var row = new EsotericUnitData();
            row.level = Level.InvalidLevel;
            row.remark = "方案 1 留在手牌";
            for (int k = 0; k < rest.Count && k < 8; k++) row.cards.Add(rest[k]);
            d.unitDatas.Add(row);
        }

        static List<int> Cards(Dictionary<string, object> e, string field = "cards")
        {
            var r = new List<int>();
            List<object> cards = Json.GetArray(e, field);
            if (cards == null) return r;
            for (int k = 0; k < cards.Count; k++) r.Add((int)Convert.ToDouble(cards[k], CultureInfo.InvariantCulture));
            return r;
        }

        static string Orders(Dictionary<string, object> e)
        {
            string s = "";
            List<object> orders = Json.GetArray(e, "orders");
            if (orders == null) return s;
            for (int k = 0; k < orders.Count; k++)
            {
                var o = orders[k] as Dictionary<string, object>;
                if (o == null) continue;
                string who = "后手";
                if (Json.GetString(o, "first", "") == "me") who = "先手";
                string verdict = "输";
                if (Json.GetString(o, "result", "") == "win") verdict = "赢";
                string my = ((int)Json.GetNumber(o, "myHp", 0)).ToString(CultureInfo.InvariantCulture);
                string foe = ((int)Json.GetNumber(o, "foeHp", 0)).ToString(CultureInfo.InvariantCulture);
                string random = RandomText(o);
                if (s.Length > 0) s = s + "，";
                s = s + who + verdict + "（" + my + " : " + foe + random + "）";
            }
            return s;
        }

        static string RandomText(Dictionary<string, object> order)
        {
            if (!Json.GetBool(order, "hadRandom", false)) return "";
            int wins = (int)Json.GetNumber(order, "randomWins", 0);
            int runs = (int)Json.GetNumber(order, "randomRuns", 0);
            return "；随机胜 " + wins.ToString(CultureInfo.InvariantCulture) + "/" + runs.ToString(CultureInfo.InvariantCulture);
        }

        static string Summary(Dictionary<string, object> r)
        {
            string secs = (Json.GetNumber(r, "elapsedMs", 0) / 1000.0).ToString("0.0", CultureInfo.InvariantCulture);
            string n = ((long)Json.GetNumber(r, "evaluated", 0)).ToString(CultureInfo.InvariantCulture);
            string tail = "局部搜索";
            if (Json.GetBool(r, "exhaustive", false)) tail = "已穷举，全局最优";
            string order = "按摆牌与可达修为计算";
            return "本地模拟推荐 · " + order + " · 算了 " + n + " 场，" + secs + " 秒，" + tail;
        }

        static Transform Find(Transform t, string name)
        {
            if (t == null) return null;
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform r = Find(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
