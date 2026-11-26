using System.Collections.Generic;
using Xunit;
using YxCanju;

public sealed class PreviewCardGuardTests
{
    [Theory]
    [InlineData(7009001)] [InlineData(7019007)] [InlineData(10009001)] [InlineData(10029008)]
    public void RejectsBothSectsAndAllRanks(int id)
    {
        Assert.True(PreviewCardGuard.Contains(new List<int> { 0, id }));
        Assert.True(PreviewCardGuard.Contains(new List<object> { 7000001, id }));
    }
    [Fact] public void AllowsOriginalCardsAndEmptyHands()
    {
        Assert.False(PreviewCardGuard.Contains(new List<int> { 0, 7000001, 10020045 }));
        Assert.False(PreviewCardGuard.Contains((List<int>)null));
        Assert.False(PreviewCardGuard.Contains(new List<object>()));
    }
}
