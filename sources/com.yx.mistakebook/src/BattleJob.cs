using System;
using System.Collections.Generic;
using Yx.ModSdk;
using Yx.Shared;

namespace YxMistakeBook
{
    internal sealed class BattleJob
    {
        readonly ModContext _ctx;
        readonly string _kind;
        readonly Action<Dictionary<string, object>> _done;
        readonly Action<string> _failed;
        int _id;
        bool _stopped;

        public BattleJob(ModContext ctx, string kind, Action<Dictionary<string, object>> done, Action<string> failed)
        {
            _ctx = ctx;
            _kind = kind;
            _done = done;
            _failed = failed;
        }

        public void Start(string request)
        {
            try { _ctx.Native.Call("battle", _kind + "_start", request, Started); }
            catch (Exception ex) { Fail(ex.Message); }
        }

        static Dictionary<string, object> Read(string json)
        {
            var data = Json.ParseObject(json);
            if (!Json.GetBool(data, "ok", false)) throw new ArgumentException(Json.GetString(data, "error", "计算失败"));
            return data;
        }

        void Started(string json)
        {
            try
            {
                _id = Exercise.Number(Read(json), "job");
                if (_id < 1) throw new ArgumentException("计算器没有返回任务编号");
                if (_stopped) CancelNative();
                else Poll();
            }
            catch (Exception ex) { Fail(ex.Message); }
        }

        string JobId() { return "{\"job\":" + Exercise.N(_id) + "}"; }

        void Poll()
        {
            if (_stopped) return;
            try { _ctx.Native.Call("battle", _kind + "_poll", JobId(), Polled); }
            catch (Exception ex) { Fail(ex.Message); }
        }

        void Polled(string json)
        {
            if (_stopped) return;
            try
            {
                var report = Read(json);
                string state = Json.GetString(report, "state", "");
                if (state == "done") { _id = 0; _done(report); }
                else if (state == "running" || state == "pending") _ctx.MainThread.Delay(0.25f, Poll);
                else throw new ArgumentException(Json.GetString(report, "error", "计算已停止"));
            }
            catch (Exception ex) { Fail(ex.Message); }
        }

        void Fail(string message)
        {
            if (_stopped) return;
            Stop();
            _failed(message);
        }

        public void Stop()
        {
            _stopped = true;
            if (_id > 0) CancelNative();
        }

        void CancelNative()
        {
            string job = JobId();
            _id = 0;
            try { _ctx.Native.Call("battle", _kind + "_cancel", job, Ignore); }
            catch (Exception) { /* The native search lease also expires when the owner unloads. */ }
        }

        static void Ignore(string json) { }
    }
}
