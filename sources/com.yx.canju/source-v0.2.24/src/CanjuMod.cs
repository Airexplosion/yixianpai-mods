using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using Yx.ModSdk;
using Yx.ModSdk.Unity;
using Yx.Shared;

namespace YxCanju
{
    /// <summary>
    /// 残局求解：在原生残局 / 复盘与本地练习场里可用，PvP 等其它模式不开放。
    ///   · 左上设置按钮组下方放「残局求解」按钮（热键 Ctrl+Alt+J 同一动作）；
    ///   · 入口只打开秘籍抽屉，首次 / 局面变化后列表为空，点抽屉顶部「求解」才计算；
    ///   · 局面不变时复用结果，每条方案右上方独立「一键摆牌」，留手牌行不提供摆牌按钮。
    /// </summary>
    public sealed class CanjuMod : YxMod
    {
        public const string Lib = "solver";
        public const string Hotkey = "Ctrl+Alt+J";
        const float PollSeconds = 0.3f;
        const float ToastSeconds = 1.5f;

        ConfigEntry<int> _topN;
        ConfigEntry<int> _timeLimitMs;
        ConfigEntry<int> _samples;
        ConfigEntry<int> _seed;
        ConfigEntry<int> _currentOrderWeightPercent;
        int _job;                  // 0 = 空闲；-1 = 已提交还没拿到任务号
        bool _polling;
        float _sinceToast;
        ResultView _fallback;      // 秘籍抽屉打不开时才用的文字面板
        EsotericView _eso;
        AutoPlacer _placer;
        UiButton _btn;
        UiKit _ui;
        CanjuSolverService _service;
        int _frame;
        bool _btnShown;

        // 最近一次求解：局面签名 + 结果（局面不变就复用）
        string _sig;
        string _pendingSig;
        string _pendingFirst;
        int _pendingOrderWeightPercent;
        bool _pendingWeightByCurrent;
        Dictionary<string, object> _result;
        int _charId;
        int _sect;
        readonly List<int> _pool = new List<int>();

        public override void OnLoad(ModContext ctx)
        {
            _service = new CanjuSolverService(ctx);
            ctx.Services.Register<CanjuSolverService>(_service);
            _topN = ctx.Config.Bind("solve", "topN", 5, ctx.T("显示前几名摆法", "How many arrangements to show"));
            _timeLimitMs = ctx.Config.Bind("solve", "timeLimitMs", 8000,
                ctx.T("最长求解时间（毫秒）；多数局面会提前收敛结束", "Max solve time (ms); most positions converge earlier"));
            _samples = ctx.Config.Bind("solve", "samples", 3,
                ctx.T("随机局面抽样次数；越大越稳定但更慢", "Random position samples; higher is steadier but slower"));
            _seed = ctx.Config.Bind("solve", "seed", 1,
                ctx.T("随机种子；相同种子和局面得到相同结果", "Random seed; same seed and position give the same result"));
            _currentOrderWeightPercent = ctx.Config.Bind("solve", "currentOrderWeightPercent", 50,
                ctx.T("先后手排序占比：当前顺序占百分之几（1–99），默认50等权；另一顺序仍会计算。仅原生残局 / 复盘生效",
                    "Current initiative ranking weight (1–99%); default 50 is equal. Both orders are still calculated; native review only"));
            ctx.Input.RegisterHotkey("solve", Hotkey, OnButton);
            _fallback = new ResultView(ctx);
            _eso = new EsotericView(ctx, OnPlace, OnResolve, ShowSteps);
            _eso.Install();
            _placer = new AutoPlacer(ctx, PreparePlacement);
            _ui = new UiKit(ctx);
            if (!ctx.Native.IsAvailable(Lib)) ctx.Log.Warn("求解库不可用（manifest 没声明，或管理器版本不支持原生库）");
        }

        public override void OnDisable()
        {
            if (_service != null) _service.Stop();
            if (_job != 0) Cancel();
            if (_placer != null) _placer.Stop("mod 已停用");
            if (_eso != null) _eso.Hide();
            if (_eso != null) _eso.Cleanup();
            if (_eso != null) _eso.Uninstall();
            if (_fallback != null) _fallback.Close();
            if (_btn != null && _btn.IsAlive) UnityEngine.Object.Destroy(_btn.GameObject);
            _btn = null;
        }

        public override void OnUpdate()
        {
            if (_frame++ % 20 != 0) return;
            try
            {
                InjectButton();
                _eso.Tick();
                _fallback.Tick();
            }
            catch (Exception e) { Context.Log.Warn("界面刷新失败：" + e.Message); }
        }

        // ---------- 入口 ----------

        void OnButton()
        {
            if (!PuzzleReader.CanSolve(Context, BattleManager.Instance)) { Toast("只能在残局 / 复盘 / 本地练习场的备战界面使用"); return; }
            string error;
            Dictionary<string, object> req = PuzzleReader.Read(Context, out error);
            if (req == null) { Toast("读不到局面：" + error); return; }
            ApplySettings(req);
            string sig = PuzzleReader.Signature(req);
            if (_result != null && sig == _sig) { ShowResult(); return; }   // 局面没变：直接看上次的结果
            RememberMe(req);
            if (!_eso.Show(null, _charId, _sect, _pool)) Toast("秘籍面板暂不可用，请稍后重试");
            _eso.SetSolving(_job != 0);
        }

        void OnResolve()
        {
            if (!PuzzleReader.CanSolve(Context, BattleManager.Instance)) { Toast("只能在残局 / 复盘 / 本地练习场的备战界面使用"); return; }
            if (_job != 0) { Toast("正在求解，请稍候…"); return; }
            if (_placer.Running) { Toast("正在摆牌，请稍候…"); return; }
            string error;
            Dictionary<string, object> req = PuzzleReader.Read(Context, out error);
            if (req == null) { Toast("读不到局面：" + error); return; }
            ApplySettings(req);
            Solve(req, PuzzleReader.Signature(req));
        }

        void ShowSteps(int index)
        {
            if (_result != null) _fallback.ShowEntry(_result, index);
        }

        void OnPlace(int index)
        {
            if (_job != 0) { Toast("正在求解，请稍候…"); return; }
            if (_placer.Running) { Toast("正在摆牌…"); return; }
            _placedIndex = index;
            if (!PlacementIsCurrent()) { Toast("局面已改变，请重新求解"); return; }
            int[] target = SolutionCards.Get(_result, index);
            if (target == null) { Toast("还没有求解结果"); return; }
            _eso.Hide();
            Toast("开始按方案 " + (index + 1).ToString(CultureInfo.InvariantCulture) + " 摆牌…");
            _placer.Start(target, _sig, OnPlaced);
        }

        int _placedIndex;
        Dictionary<string, object> _startingGrids;

        bool PlacementIsCurrent()
        {
            var entries = Json.GetArray(_result, "best");
            if (entries == null || _placedIndex < 0 || _placedIndex >= entries.Count) return false;
            string error;
            var request = PuzzleReader.Read(Context, out error);
            if (request == null) return false;
            PreparePlacement(request);
            return PuzzleReader.Signature(request) == _sig;
        }

        void PreparePlacement(Dictionary<string, object> request)
        {
            ApplySettings(request);
            var entries = Json.GetArray(_result, "best");
            var entry = entries[_placedIndex] as Dictionary<string, object>;
            var live = Json.GetObject(Json.GetObject(request, "me"), "xianMoTempDatas");
            PuzzleSignature.RestoreRecommendedGrids(live, _startingGrids, Json.GetObject(entry, "enchantGrids"));
        }


        void OnPlaced(string msg)
        {
            if (msg == "摆好了")
            {
                var entry = Json.GetArray(_result, "best")[_placedIndex] as Dictionary<string, object>;
                string grids = ResultView.EnchantText(entry);
                if (grids.Length > 0) msg += "。请手动设置" + grids;
                if (grids.Length > 0 || HasSteps(entry, "refined") || HasSteps(entry, "bottle") || HasSteps(entry, "keyin"))
                    ShowSteps(_placedIndex);
            }
            Toast(msg);
            Context.Log.Info("一键摆牌：" + msg);
        }

        static bool HasSteps(Dictionary<string, object> entry, string key)
        {
            var values = Json.GetArray(entry, key);
            return values != null && values.Count > 0;
        }

        // ---------- 求解 ----------

        void Solve(Dictionary<string, object> req, string sig)
        {
            ModContext ctx = Context;
            if (!ctx.Native.IsAvailable(Lib)) { Toast("求解库不可用"); return; }
            ApplySettings(req);
            RememberMe(req);
            _result = null;
            _sig = null;
            _eso.Show(null, _charId, _sect, _pool);
            _eso.SetSolving(true);
            _pendingSig = sig;
            _startingGrids = Json.GetObject(Json.GetObject(req, "me"), "xianMoTempDatas");
            _pendingFirst = Json.GetString(req, "first", "both");
            _pendingOrderWeightPercent = (int)Json.GetNumber(req, "currentOrderWeightPercent", 50);
            _pendingWeightByCurrent = Json.GetBool(req, "weightByCurrentInitiative", false);
            string json = Json.Serialize(req);
            ctx.Log.Info("求解请求：" + json);
            Toast("开始求解，按可达到的修为计算出手顺序…");
            _sinceToast = 0f;
            _job = -1;
            try { ctx.Native.Call(Lib, "solve_start", json, OnStarted); }
            catch (Exception)
            {
                _job = 0;
                _eso.SetSolving(false);
                throw;
            }
        }

        void OnStarted(string json)
        {
            Dictionary<string, object> r = Parse(json);
            if (r == null || !Json.GetBool(r, "ok", false))
            {
                _job = 0;
                _eso.SetSolving(false);
                string why = json;
                if (r != null) why = Json.GetString(r, "error", json);
                Toast("求解启动失败：" + why);
                return;
            }
            _job = (int)Json.GetNumber(r, "job", 0);
            SchedulePoll();
        }

        void SchedulePoll()
        {
            if (_polling) return;
            _polling = true;
            Context.MainThread.Delay(PollSeconds, Poll);
        }

        void Poll()
        {
            _polling = false;
            if (_job <= 0) return;
            string input = "{\"job\":" + _job.ToString(CultureInfo.InvariantCulture) + "}";
            Context.Native.Call(Lib, "solve_poll", input, OnPoll);
        }

        void OnPoll(string json)
        {
            Dictionary<string, object> r = Parse(json);
            if (r == null) { _job = 0; _eso.SetSolving(false); Toast("结果解析失败"); return; }
            string state = Json.GetString(r, "state", "");
            bool finished = state == "done" || state == "cancelled" || state == "error" || !Json.GetBool(r, "ok", true);
            if (!finished)
            {
                _sinceToast += PollSeconds;
                if (_sinceToast >= ToastSeconds) { _sinceToast = 0f; Toast(Progress(r)); }
                SchedulePoll();
                return;
            }
            _job = 0;
            _eso.SetSolving(false);
            Context.Log.Info("求解结束：" + json);
            if (state != "done") { Toast("求解没有完成：" + state + " " + Json.GetString(r, "error", "")); return; }
            r["firstMode"] = _pendingFirst;
            r["currentOrderWeightPercent"] = _pendingOrderWeightPercent;
            r["weightByCurrentInitiative"] = _pendingWeightByCurrent;
            _result = r;
            _sig = _pendingSig;
            if (!ResultIsCurrent()) { Toast("求解完成，但局面已改变，请在当前局面重新求解"); return; }
            Toast("求解完成：" + BestLine(r));
            ShowResult();
        }

        void ShowResult()
        {
            bool shown = false;
            try { shown = _eso.Show(_result, _charId, _sect, _pool); }
            catch (Exception e) { Context.Log.Warn("秘籍面板显示失败，改用文字面板：" + e.Message); }
            if (!shown) _fallback.Show(_result);
            _eso.SetSolving(_job != 0);
        }

        bool ResultIsCurrent()
        {
            string error;
            Dictionary<string, object> req = PuzzleReader.Read(Context, out error);
            if (req != null) ApplySettings(req);
            return req != null && PuzzleReader.Signature(req) == _sig;
        }

        void Cancel()
        {
            if (_job > 0)
            {
                string input = "{\"job\":" + _job.ToString(CultureInfo.InvariantCulture) + "}";
                Context.Native.Call(Lib, "solve_cancel", input, OnCancelled);
            }
            _job = 0;
        }

        void OnCancelled(string json) { }

        // ---------- 摆牌界面按钮 ----------

        /// <summary>摆牌界面出现后，在左上设置按钮组下方放入口；仅在可求解的模式里显示。</summary>
        void InjectButton()
        {
            bool want = PuzzleReader.CanSolve(Context, BattleManager.Instance);
            if (_btn != null && _btn.IsAlive)
            {
                if (want != _btnShown) { _btn.GameObject.SetActive(want); _btnShown = want; }
                if (want) PositionOpenButton();
                return;
            }
            _btn = null;
            if (!want) return;
            BattlePanel bp = ILRPanelBase.FindILRPanel<BattlePanel>();
            if (bp == null || bp.readyLayer == null) return;
            Button tmpl = bp.readyLayer.FindComponent<Button>("SettingButton");
            if (tmpl == null) return;
            var fontSource = bp.readyLayer.FindComponent<Button>("RecommendDeckButton");
            // 挂在 Fixed 中，避开按钮组的横排布局，并让后面的原生抽屉正常盖住入口。
            Transform parent = tmpl.transform.parent == null ? bp.readyLayer.transform : tmpl.transform.parent.parent;
            if (parent == null) parent = bp.readyLayer.transform;
            _btn = SolverButtonStyle.Create(_ui, parent, "YxCanjuOpen", "残局求解", OnButton,
                new Vector2(0f, 1f), Vector2.zero, new Vector2(180f, 64f), 28f,
                SolverButtonStyle.FontFrom(fontSource == null ? tmpl.transform : fontSource.transform));
            if (_btn == null || !_btn.IsAlive) { _btn = null; return; }
            PositionOpenButton();
            _btn.GameObject.SetActive(true);
            _btnShown = true;
        }

        void PositionOpenButton()
        {
            BattlePanel bp = ILRPanelBase.FindILRPanel<BattlePanel>();
            if (bp == null || bp.readyLayer == null || _btn == null || !_btn.IsAlive) return;
            Button setting = bp.readyLayer.FindComponent<Button>("SettingButton");
            if (setting == null) return;
            var src = setting.transform as RectTransform;
            var dst = _btn.GameObject.GetComponent(typeof(RectTransform)) as RectTransform;
            if (src != null && dst != null)
            {
                // 左边与设置按钮对齐，向下留空；随原生 UI 布局重新定位。
                Vector3 corner = src.TransformPoint(new Vector3(src.rect.xMin, src.rect.yMin, 0f));
                dst.position = corner + new Vector3(dst.rect.width * dst.lossyScale.x / 2f,
                    -(24f + dst.rect.height / 2f) * dst.lossyScale.y, 0f);
            }
        }

        // ---------- 小工具 ----------

        static string Progress(Dictionary<string, object> r)
        {
            string n = ((long)Json.GetNumber(r, "evaluated", 0)).ToString(CultureInfo.InvariantCulture);
            string secs = (Json.GetNumber(r, "elapsedMs", 0) / 1000.0).ToString("0.0", CultureInfo.InvariantCulture);
            return "求解中… " + secs + " 秒，已算 " + n + " 场，当前最好：" + BestLine(r);
        }

        void ApplySettings(Dictionary<string, object> req)
        {
            req["topN"] = _topN.Value;
            req["timeLimitMs"] = _timeLimitMs.Value;
            req["samples"] = Math.Max(1, _samples.Value);
            req["seed"] = _seed.Value;
            req["currentOrderWeightPercent"] = Math.Max(1, Math.Min(99, _currentOrderWeightPercent.Value));
        }

        /// <summary>第一名的先后手胜负，如「先手赢 / 后手赢」。</summary>
        static string BestLine(Dictionary<string, object> r)
        {
            List<object> best = Json.GetArray(r, "best");
            if (best == null || best.Count == 0) return "暂无";
            var e = best[0] as Dictionary<string, object>;
            List<object> orders = e != null ? Json.GetArray(e, "orders") : null;
            if (orders == null) return "暂无";
            string s = "";
            for (int k = 0; k < orders.Count; k++)
            {
                var o = orders[k] as Dictionary<string, object>;
                if (o == null) continue;
                string who = "后手";
                if (Json.GetString(o, "first", "") == "me") who = "先手";
                string verdict = "输";
                if (Json.GetString(o, "result", "") == "win") verdict = "赢";
                string random = "";
                if (Json.GetBool(o, "hadRandom", false))
                {
                    int wins = (int)Json.GetNumber(o, "randomWins", 0);
                    int runs = (int)Json.GetNumber(o, "randomRuns", 0);
                    random = "（随机 " + wins.ToString(CultureInfo.InvariantCulture) + "/" + runs.ToString(CultureInfo.InvariantCulture) + ")";
                }
                if (s.Length > 0) s = s + " / ";
                s = s + who + verdict + random;
            }
            return s;
        }

        void RememberMe(Dictionary<string, object> req)
        {
            _pool.Clear();
            _charId = 0;
            _sect = 0;
            var me = req["me"] as Dictionary<string, object>;
            if (me == null) return;
            _charId = (int)me["characterId"];
            _sect = (int)me["sect"];
            var cards = me["cards"] as List<object>;
            if (cards != null)
                for (int i = 0; i < cards.Count; i++) _pool.Add((int)cards[i]);
        }

        static void Toast(string msg)
        {
            try { Ui.Toast("残局求解：" + msg); } catch (Exception) { }
        }

        static Dictionary<string, object> Parse(string json)
        {
            try { return Json.ParseObject(json); }
            catch (Exception) { return null; }
        }
    }
}
