using System;
using System.Collections.Generic;
using System.Globalization;
using Proto;
using UnityEngine;
using Yx.ModSdk;
using Yx.ModSdk.Unity;
using Yx.Shared;
using YxCanju;

namespace YxMistakeBook
{
    public sealed partial class BookMod : YxMod
    {
        const string StoreKey = "exercises.v1";
        List<object> _book;
        UiKit _ui;
        RecordDetailPanel _record;
        BattleResult _round;
        Exercise _capture, _active;
        BattleJob _job;
        float _nextTick;
        bool _visible, _disabled;

        public override void OnLoad(ModContext ctx)
        {
            _ui = new UiKit(ctx);
            _book = ctx.Data.Get<List<object>>(StoreKey, null) ?? new List<object>();
            ctx.Hooks.Postfix("RecordDetailPanel", "RefreshRoundInfo", 1, RoundChanged);
            ctx.Hooks.Postfix("RecordDetailPanel", "OnHide", 0, RecordClosed);
            ctx.Input.RegisterHotkey("open-book", "Ctrl+Alt+M", ToggleBook);
        }

        void RoundChanged(HookContext hook)
        {
            var panel = hook.Instance as RecordDetailPanel;
            if (panel == null || hook.Args == null || hook.Args.Length == 0 || !(hook.Args[0] is int)) return;
            int index = (int)hook.Args[0];
            var info = panel.recordItem != null && panel.recordItem.recordData != null ? panel.recordItem.recordData.info : null;
            BattleResult next = info != null && info.roundStats != null && index >= 0 && index < info.roundStats.Count ? info.roundStats[index] : null;
            if (_round != next || _record != panel) StopCapture();
            _round = next;
            _record = panel;
            RefreshEntry();
        }

        void RecordClosed(HookContext hook)
        {
            if (hook.Instance != _record) return;
            StopCapture();
            _record = null;
            _round = null;
            RefreshEntry();
        }

        bool RecordOpen { get { return _record != null && _record.panel != null && _record.panel.isShow; } }
        bool AtHome { get { var scene = SceneDetector.Detect(); return scene == GameScene.Lobby || scene == GameScene.Home; } }

        public override void OnUpdate()
        {
            if (Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + 0.35f;
            if (_capture != null && !RecordOpen) StopCapture();
            if (_visible && !AtHome && !RecordOpen) CloseBook();
            RefreshEntry();
        }

        void Run(string kind, string request, Action<Dictionary<string, object>> done)
        {
            _job = new BattleJob(Context, kind, done, Failed);
            _job.Start(request);
        }

        void StopJob()
        {
            var old = _job;
            _job = null;
            if (old != null) old.Stop();
        }

        void StopCapture()
        {
            if (_capture == null) return;
            _capture = null;
            StopJob();
        }

        void Failed(string why)
        {
            _job = null;
            _capture = null;
            if (_disabled) return;
            Ui.Toast(why);
            if (_visible) { _notice = why; Draw(); }
            RefreshEntry();
        }

        int Find(string key)
        {
            for (int i = 0; i < _book.Count; i++)
                if (Json.GetString(_book[i] as Dictionary<string, object>, "key", "") == key) return i;
            return -1;
        }

        void Collect()
        {
            if (_capture != null) { StopCapture(); RefreshEntry(); return; }
            if (_visible || !RecordOpen || _round == null) return;
            try
            {
                var record = _record.recordItem.recordData;
                var info = record.info;
                if (info == null || info.version != Exercise.Rules)
                    throw new ArgumentException("此版支持战绩 " + Exercise.Rules + "，本局为 " + (info == null ? "未知" : info.version));
                if (string.IsNullOrEmpty(record.uid) || record.uid != GameClientUtil.uid)
                    throw new ArgumentException("请打开自己的战绩");
                string p1 = _round.p1 == null || _round.p1.publicData == null ? null : _round.p1.publicData.uid;
                string p2 = _round.p2 == null || _round.p2.publicData == null ? null : _round.p2.publicData.uid;
                string foe = record.uid == p1 ? p2 : record.uid == p2 ? p1 : null;
                if (string.IsNullOrEmpty(foe) || foe == record.uid || _round.winerId != foe)
                    throw new ArgumentException("只能收藏自己确实输给对手的轮次");
                string game = info.gameId;
                if (string.IsNullOrEmpty(game) && info.codeId > 0) game = info.codeId.ToString(CultureInfo.InvariantCulture);
                if (string.IsNullOrEmpty(game)) throw new ArgumentException("战绩缺少对局编号");
                string key = game + "/" + record.uid + "/" + Exercise.N(_round.round);
                if (Find(key) >= 0) { Ui.Toast("这一轮已经在错题本里了"); return; }
                string error;
                var request = RecordPuzzleReader.Read(_round, record.uid, out error);
                if (request == null) throw new ArgumentException(error);
                _capture = Exercise.Create(key, request);
                _capture.Data["game"] = game;
                Run("predict", _capture.Prediction(_capture.Original), CheckedLoss);
                RefreshEntry();
            }
            catch (Exception ex) { Failed(ex.Message); }
        }

        void CheckedLoss(Dictionary<string, object> report)
        {
            var run = _capture.HistoricalRun(report);
            if (Json.GetString(run, "winner", "") != "R") throw new ArgumentException("原摆法复算与实战败局不符，未收录");
            Run("solve", _capture.Search(), FoundAnswer);
        }

        int[] _witness;
        void FoundAnswer(Dictionary<string, object> report)
        {
            _witness = _capture.WinningBoard(report);
            Run("predict", _capture.Prediction(_witness), CheckedWin);
        }

        void CheckedWin(Dictionary<string, object> report)
        {
            var run = _capture.HistoricalRun(report);
            if (Json.GetString(run, "winner", "") != "L") throw new ArgumentException("参考摆法未通过独立复验，未收录");
            _capture.Data["answer"] = Exercise.ListOf(_witness);
            _capture.Data["saved"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            var next = new List<object>(_book);
            if (Find(_capture.Key) < 0) next.Insert(0, _capture.Data);
            Save(next);
            _job = null;
            _capture = null;
            Ui.Toast("已收藏。回大厅打开「败局错题本」即可盲摆重做");
            RefreshEntry();
        }

        void Save(List<object> next)
        {
            // Update the visible book only after persistence accepts the complete change.
            Context.Data.Set(StoreKey, next);
            _book = next;
        }

        public override void OnDisable()
        {
            _disabled = true;
            StopJob();
            _capture = null;
            if (_ui != null) _ui.DestroyAll();
        }
    }
}
