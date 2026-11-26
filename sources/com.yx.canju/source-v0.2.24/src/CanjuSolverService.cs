using System;
using Yx.ModSdk;
using Yx.Shared;

namespace YxCanju
{
    /// <summary>Other mods reuse the provider's native library and data, on the main thread.</summary>
    public sealed class CanjuSolverService
    {
        const string Lib = "solver";
        readonly ModContext _ctx;
        bool _active = true;

        public CanjuSolverService(ModContext ctx) { _ctx = ctx; }
        public bool IsAvailable { get { return _active && _ctx.Native.IsAvailable(Lib); } }
        public void Stop() { _active = false; }
        public void Start(string request, Action<string> complete) { Call("solve_start", request, complete); }
        public void Poll(string request, Action<string> complete) { Call("solve_poll", request, complete); }
        public void Cancel(string request, Action<string> complete)
        {
            var cleanup = new CancelCleanup(this, request, complete);
            Call("solve_cancel", request, cleanup.OnCancelled);
        }

        void Call(string export, string request, Action<string> complete)
        {
            if (!IsAvailable) { complete("{\"ok\":false,\"error\":\"残局求解服务已停用或不可用\"}"); return; }
            _ctx.Native.Call(Lib, export, request, complete);
        }

        sealed class CancelCleanup
        {
            readonly CanjuSolverService _service;
            readonly string _request;
            readonly Action<string> _complete;
            int _attempts;
            public CancelCleanup(CanjuSolverService service, string request, Action<string> complete)
            { _service = service; _request = request; _complete = complete; }
            public void OnCancelled(string json)
            {
                // Native jobs are released only by a terminal poll, including cancelled jobs.
                _service._ctx.MainThread.Delay(0.3f, Poll);
                _complete(json);
            }
            void Poll()
            {
                if (_service.IsAvailable) _service.Poll(_request, OnPoll);
            }
            void OnPoll(string json)
            {
                try
                {
                    var report = Json.ParseObject(json);
                    string state = Json.GetString(report, "state", "");
                    if (!Json.GetBool(report, "ok", false) || state == "done" || state == "cancelled" || state == "error") return;
                    if (_attempts++ < 600) _service._ctx.MainThread.Delay(0.3f, Poll);
                }
                catch (Exception) { }
            }
        }
    }
}
