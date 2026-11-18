using System.Collections.Generic;
using System.Linq;
using Xunit;
using Yx.ModSdk;
using Yx.ModSdk.Testing;
using Yx.Shared;

namespace YxMistakeBook
{
    public sealed class BookHarness : YxMod
    {
        public ModContext Ctx;
        public override void OnLoad(ModContext ctx) { Ctx = ctx; }
    }

    public class JobTests
    {
        const string Id = "com.yx.mistakebook";

        [Fact]
        public void CancellingBeforeStartReplyReapsJobAndNeverSavesStaleRound()
        {
            using var host = TestHost.Start();
            host.SetNative("battle", "solve_start", _ => "{\"ok\":true,\"job\":7}");
            host.SetNative("battle", "solve_cancel", _ => "{\"ok\":true}");
            var mod = host.Load<BookHarness>(Id, "0.1.0");
            bool callback = false;
            var job = new BattleJob(mod.Ctx, "solve", _ => callback = true, _ => callback = true);
            job.Start("{}");
            job.Stop();
            host.Tick(); host.Tick();
            Assert.False(callback);
            Assert.Equal(new[] { "solve_start", "solve_cancel" }, host.NativeCalls.Select(c => c.Export));
            Assert.Equal("{\"job\":7}", host.NativeCalls[1].Input);
        }

        [Fact]
        public void PendingThenDoneSavesOnePersistentSnapshotThatSurvivesReload()
        {
            using var host = TestHost.Start();
            int polls = 0, completions = 0;
            host.SetNative("battle", "solve_start", _ => "{\"ok\":true,\"job\":1}");
            host.SetNative("battle", "solve_poll", _ => ++polls == 1 ? "{\"ok\":true,\"state\":\"pending\"}" : "{\"ok\":true,\"state\":\"done\"}");
            var mod = host.Load<BookHarness>(Id, "0.1.0");
            var job = new BattleJob(mod.Ctx, "solve", _ => {
                completions++;
                mod.Ctx.Data.Set("exercises.v1", new List<object> { Sample.Create().Data });
            }, why => throw new Xunit.Sdk.XunitException(why));
            job.Start("{}");
            for (int i = 0; i < 8; i++) host.Tick(0.4);
            Assert.Equal(1, completions);
            var persisted = Json.ParseObject(Json.Serialize(host.Find(Id).Data.Snapshot()));
            var again = host.Reload<BookHarness>(Id, "0.1.0", data: persisted);
            var items = again.Ctx.Data.Get<List<object>>("exercises.v1", null);
            var saved = new Exercise((Dictionary<string, object>)Assert.Single(items));
            Assert.Equal(Sample.Create().Pool, saved.Pool);
            Assert.Equal(Sample.Create().Answer, saved.Answer);
        }
    }
}
