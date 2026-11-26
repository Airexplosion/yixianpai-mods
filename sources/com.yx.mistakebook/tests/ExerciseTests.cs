using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Proto;
using Xunit;
using Yx.Shared;
using YxCanju;

namespace YxMistakeBook
{
    public class ExerciseTests
    {
        [Fact]
        public void SnapshotRoundTripsWithDuplicateCopiesAndIntentionalEmptySlots()
        {
            var sample = Sample.Create();
            var side = Json.GetObject(sample.Request, "me");
            side["cards"] = Exercise.ListOf(new[] { 8, 8, 3 });
            side["initial"] = Exercise.ListOf(new[] { 8, 0 });
            var book = Json.ParseObject(Json.Serialize(sample.Data));
            var item = new Exercise(book);
            var pred = Json.ParseObject(item.Prediction(new[] { 0, 8 }));
            Assert.Equal(new[] { 8, 3 }, Exercise.Cards(Json.GetObject(pred, "me"), "handCards"));
            Assert.Equal(new[] { 0, 8 }, Exercise.Cards(Json.GetObject(pred, "me"), "cards"));
            Assert.Equal(new[] { 8, 0 }, item.Original);
            Assert.Equal(new[] { 8, 8, 3 }, item.Pool);
            Assert.Throws<ArgumentException>(() => item.Prediction(new[] { 3, 3 }));
            Assert.Throws<ArgumentException>(() => item.Prediction(new[] { 8 }));
            book["rules"] = "older";
            Assert.Throws<ArgumentException>(() => new Exercise(book));
        }

        [Theory]
        [InlineData("001.0008.0006")]
        [InlineData("001.0008.0007")]
        public void MixedNotebookKeepsLegacyDataWithoutRegradingIt(string legacyRules)
        {
            var legacy = Json.ParseObject(Json.Serialize(Sample.Create().Data));
            legacy["rules"] = legacyRules;
            legacy["attempts"] = 3;
            legacy["passed"] = true;
            var original = Json.Serialize(legacy);
            var old = Exercise.TryRead(legacy);
            var current = Exercise.TryRead(Sample.Create().Data);
            Assert.NotNull(old);
            Assert.Equal(3, old.Attempts);
            Assert.True(Json.GetBool(old.Data, "passed", false));
            Assert.False(old.CanGrade);
            Assert.Throws<InvalidOperationException>(() => old.Prediction(old.Original));
            Assert.Throws<InvalidOperationException>(() => old.Search());
            Assert.Throws<InvalidOperationException>(() => old.HistoricalRun(null));
            Assert.Null(Exercise.TryRead(new Dictionary<string, object>()));
            Assert.Equal(original, Json.Serialize(legacy));
            Assert.True(current.CanGrade);
            Assert.Equal("001.0008.0008", current.RulesVersion);
        }

        [Theory]
        [InlineData("foe", true)]
        [InlineData("me", false)]
        public void RejectsRandomOrWrongInitiativeEvidence(string first, bool random)
        {
            var e = Sample.Create();
            var note = Json.ParseObject("{\"canWin\":true,\"hasRandom\":false,\"best\":[{\"cards\":[8,3],\"orders\":[{}]}]}");
            var entry = (Dictionary<string, object>)Json.GetArray(note,"best")[0];
            var order = (Dictionary<string, object>)Json.GetArray(entry,"orders")[0];
            order["first"] = first; order["result"] = "win"; order["hadRandom"] = random;
            Assert.Throws<ArgumentException>(() => e.WinningBoard(note));
            order["first"] = "foe"; order["hadRandom"] = false;
            Assert.Equal(new[] { 8, 3 }, e.WinningBoard(note));
            note["hasRandom"] = true;
            Assert.Throws<ArgumentException>(() => e.WinningBoard(note));
        }

        [Fact]
        public void RandomSubmissionDoesNotReceiveDeterministicVerdict()
        {
            var report = Json.ParseObject("{\"runs\":[{\"meFirst\":true,\"random\":false,\"winner\":\"L\"},{\"meFirst\":false,\"random\":true,\"winner\":\"L\"}]}");
            Assert.Throws<ArgumentException>(() => Sample.Create().HistoricalRun(report));
        }

        [Fact]
        public void RecordReaderKeepsRecordOwnersPerspectiveWhenOwnerIsSecondPlayer()
        {
            var battle = new BattleResult { round = 2, firstPlayerId = "opponent", winerId = "opponent", gameMode = GameMode.RankMode };
            PlayerData Side(string uid, int hero, int[] board, int[] hand)
            {
                var p = new PlayerData();
                p.publicData.uid = uid;
                p.publicData.characterId = hero;
                p.publicData.lastRoundData = new BattlePlayerLastRoundData { unlockGrids = 2 };
                p.publicData.lastRoundData.usedCards.AddRange(board);
                p.publicData.lastRoundData.handCards.AddRange(hand);
                return p;
            }
            battle.p1 = Side("opponent", 1000002, new[] { 19, 8 }, Array.Empty<int>());
            battle.p2 = Side("owner", 1000001, new[] { 19, 0 }, new[] { 8, 3 });
            var request = RecordPuzzleReader.Read(battle, "owner", out string why);
            Assert.True(request != null, why);
            var e = Exercise.Create("g/owner/2", request);
            Assert.Equal("foe", e.First);
            Assert.Equal(new[] { 19, 0 }, e.Original);
            Assert.Equal(new[] { 19, 8 }, e.Opponent);
            Assert.Equal(new[] { 19, 8, 3 }, e.Pool);
        }

        [Fact]
        public void ReleaseEngineConfirmsOriginalLossWitnessAndDifferentWinningSubmission()
        {
            var init = new Dictionary<string, object> { { "modDir", AppContext.BaseDirectory } };
            Check(Call(yxmod_init, Json.Serialize(init)));
            var e = Sample.Create();
            Dictionary<string, object> Play(int[] board)
            {
                return e.HistoricalRun(Job(predict_start, predict_poll, e.Prediction(board)));
            }
            Assert.Equal("R", Json.GetString(Play(e.Original), "winner", ""));
            var witness = e.WinningBoard(Job(solve_start, solve_poll, e.Search()));
            Assert.Equal("L", Json.GetString(Play(witness), "winner", ""));
            Assert.Equal("L", Json.GetString(Play(e.Answer), "winner", ""));
            Assert.NotEqual(e.Answer, new[] { 3, 8 });
            Assert.Equal("L", Json.GetString(Play(new[] { 3, 8 }), "winner", ""));
        }

        static Dictionary<string, object> Job(Func<string, IntPtr> start, Func<string, IntPtr> poll, string req)
        {
            int job = Exercise.Number(Check(Call(start, req)), "job");
            var timer = Stopwatch.StartNew();
            while (timer.Elapsed < TimeSpan.FromSeconds(10))
            {
                var r = Check(Call(poll, "{\"job\":" + job + "}"));
                if (Json.GetString(r, "state", "") == "done") return r;
                Thread.Sleep(5);
            }
            throw new TimeoutException();
        }

        static Dictionary<string, object> Check(Dictionary<string, object> data)
        {
            Assert.True(Json.GetBool(data, "ok", false), Json.GetString(data, "error", ""));
            return data;
        }

        static Dictionary<string, object> Call(Func<string, IntPtr> fn, string json)
        {
            var ptr = fn(json);
            try { return Json.ParseObject(Marshal.PtrToStringUTF8(ptr)); }
            finally { yxmod_free(ptr); }
        }

        [DllImport("YxBattleSim")] static extern IntPtr yxmod_init([MarshalAs(UnmanagedType.LPUTF8Str)] string json);
        [DllImport("YxBattleSim")] static extern IntPtr solve_start([MarshalAs(UnmanagedType.LPUTF8Str)] string json);
        [DllImport("YxBattleSim")] static extern IntPtr solve_poll([MarshalAs(UnmanagedType.LPUTF8Str)] string json);
        [DllImport("YxBattleSim")] static extern IntPtr predict_start([MarshalAs(UnmanagedType.LPUTF8Str)] string json);
        [DllImport("YxBattleSim")] static extern IntPtr predict_poll([MarshalAs(UnmanagedType.LPUTF8Str)] string json);
        [DllImport("YxBattleSim")] static extern void yxmod_free(IntPtr ptr);
    }
}
