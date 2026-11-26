using System.Collections.Generic;
using Xunit;

namespace YxCanju.Tests
{
    public class PuzzleSignatureTests
    {
        [Theory]
        [InlineData("me")]
        [InlineData("foe")]
        public void StrategySwitchOrEnchantedSlotInvalidatesCachedSolution(string side)
        {
            var a = Request(); var b = Request();
            ((Dictionary<string, object>)a[side])["xianMoTempDatas"] = new Dictionary<string, object> { { "13001", 2 } };
            ((Dictionary<string, object>)b[side])["xianMoTempDatas"] = new Dictionary<string, object> { { "13001", 6 } };
            Assert.NotEqual(PuzzleSignature.Of(a), PuzzleSignature.Of(b));
        }

        static Dictionary<string, object> Request()
        {
            return new Dictionary<string, object>
            {
                { "round", 12 }, { "slots", 6 }, { "maxConsumeSustain", 2 }, { "position", "arena:1:0" },
                { "seed", 1 }, { "samples", 3 },
                { "me", new Dictionary<string, object>
                    { { "hp", 100 }, { "cards", new List<object> { 1, 2, 2, 3 } },
                      { "initial", new List<object> { 1, 2, 0 } }, { "talentTempDatas", new Dictionary<string, object> { { "5", 2 } } } } },
                { "foe", new Dictionary<string, object> { { "cards", new List<object> { 4, 5 } }, { "hp", 90 } } }
            };
        }

        [Fact]
        public void PlacementAcceptsOnlyTheRecommendedGridAndPreservesOtherEdits()
        {
            var original = new Dictionary<string, object> { { "13008", 3 } };
            var target = new Dictionary<string, object> { { "13008", 4.0 }, { "13002", 2.0 } };
            var live = new Dictionary<string, object> { { "13008", 4 }, { "13002", 2 }, { "7128", 7 } };
            PuzzleSignature.RestoreRecommendedGrids(live, original, target);
            Assert.Equal(3, live["13008"]);
            Assert.False(live.ContainsKey("13002"));
            Assert.Equal(7, live["7128"]);
            live["13008"] = 6;
            PuzzleSignature.RestoreRecommendedGrids(live, original, target);
            Assert.Equal(6, live["13008"]);
        }

        [Fact]
        public void ReorderingTheSamePoolKeepsCachedSolution()
        {
            var a = Request();
            var b = Request();
            var me = (Dictionary<string, object>)b["me"];
            me["cards"] = new List<object> { 3, 2, 1, 2 };
            me["initial"] = new List<object> { 2, 3, 1 };
            Assert.Equal(PuzzleSignature.Of(a), PuzzleSignature.Of(b));
        }

        [Theory]
        [InlineData("round", 13)]
        [InlineData("slots", 8)]
        [InlineData("maxConsumeSustain", 3)]
        [InlineData("first", "auto")]
        [InlineData("first", "me")]
        [InlineData("first", "foe")]
        [InlineData("position", "arena:2:1")]
        [InlineData("position", "arena:3:0")]
        public void ChangingSessionSideRoundOrRulesInvalidatesResult(string field, object value)
        {
            var a = Request();
            var b = Request();
            b[field] = value;
            Assert.NotEqual(PuzzleSignature.Of(a), PuzzleSignature.Of(b));
        }

        [Fact]
        public void RemovingOneDuplicateOrEditingTalentCountsInvalidatesResult()
        {
            var a = Request();
            var b = Request();
            var me = (Dictionary<string, object>)b["me"];
            ((List<object>)me["cards"]).RemoveAt(1);
            Assert.NotEqual(PuzzleSignature.Of(a), PuzzleSignature.Of(b));
            b = Request();
            me = (Dictionary<string, object>)b["me"];
            ((Dictionary<string, object>)me["talentTempDatas"])["5"] = 3;
            Assert.NotEqual(PuzzleSignature.Of(a), PuzzleSignature.Of(b));
        }

        [Fact]
        public void OpponentCardOrderIsPartOfTheProblem()
        {
            var a = Request();
            var b = Request();
            ((Dictionary<string, object>)b["foe"])["cards"] = new List<object> { 5, 4 };
            Assert.NotEqual(PuzzleSignature.Of(a), PuzzleSignature.Of(b));
        }

        [Theory]
        [InlineData("seed", 2)]
        [InlineData("samples", 5)]
        [InlineData("currentOrderWeightPercent", 70)]
        [InlineData("weightByCurrentInitiative", true)]
        public void Changing_random_settings_invalidates_result(string field, object value)
        {
            var a = Request();
            var b = Request();
            b[field] = value;
            Assert.NotEqual(PuzzleSignature.Of(a), PuzzleSignature.Of(b));
        }
    }
}
