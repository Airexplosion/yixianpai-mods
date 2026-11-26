using System.Collections.Generic;
using Xunit;
using Yx.Shared;

namespace YxCanju.Tests
{
    public class SolutionCardsTests
    {
        [Fact]
        public void Selecting_later_solutions_preserves_empty_slots_and_duplicate_cards()
        {
            var report = Json.ParseObject("{\"best\":[{\"cards\":[11,12,13]},{\"cards\":[21,0,21]},{\"cards\":[31,32,0]}]}");
            Assert.Equal(new[] { 21, 0, 21 }, SolutionCards.Get(report, 1));
            Assert.Equal(new[] { 31, 32, 0 }, SolutionCards.Get(report, 2));
            Assert.Equal(new[] { 11, 12, 13 }, SolutionCards.Get(report, 0));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(1)]
        [InlineData(100)]
        public void Invalid_selection_does_not_fall_back_to_the_first_solution(int index)
        {
            var report = Json.ParseObject("{\"best\":[{\"cards\":[11]}]}");
            Assert.Null(SolutionCards.Get(report, index));
        }

        [Fact]
        public void Empty_or_missing_results_cannot_supply_a_placement_target()
        {
            Assert.Null(SolutionCards.Get(null, 0));
            Assert.Null(SolutionCards.Get(new Dictionary<string, object>(), 0));
            Assert.Null(SolutionCards.Get(Json.ParseObject("{\"best\":[{\"cards\":[]}]}") , 0));
        }
    }
}
