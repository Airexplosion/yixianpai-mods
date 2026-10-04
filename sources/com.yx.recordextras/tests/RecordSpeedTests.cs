using System;
using System.Collections.Generic;
using Proto;
using Xunit;
using YxRecordExtras;

public class RecordSpeedTests
{
    [Theory]
    [InlineData(10012)]
    [InlineData(10045)]
    [InlineData(10056)]
    [InlineData(369)]
    public void EachNativeSpeedBuffIsIncluded(int buff)
    {
        PlayerData player = Player();
        player.publicData.lastRoundData.permanentBuffTempDatas[buff] = 3;
        Assert.Equal("修为：70+3（73）", RecordSpeed.Format("修为：70", player));
        AssertNative(player);
    }

    [Fact]
    public void SnapshotConditionalBonusesAreCountedOnce()
    {
        PlayerData player = Player();
        BattlePlayerLastRoundData snapshot = player.publicData.lastRoundData;
        // Model a record whose server-resolved extra speed includes retained cards.
        snapshot.permanentBuffTempDatas[369] = 8;
        snapshot.handCards.AddRange(new[] { 1, 2, 3, 4, 5, 6, 7, 7 });
        snapshot.permanentBuffTempDatas[10045] = 3;
        snapshot.permanentBuffTempDatas[10012] = 2;
        Assert.Equal("修为：70+13（83）", RecordSpeed.Format("修为：70", player));
        AssertNative(player);
        // Changing hand data must not add that bonus a second time.
        snapshot.handCards.Clear();
        Assert.Equal(13, RecordSpeed.Extra(player));
    }

    [Fact]
    public void PlayerAndRoundChangesDoNotLeakOrUsePostBattleData()
    {
        PlayerData left = Player();
        PlayerData right = Player();
        left.publicData.exp = 999;
        left.publicData.talents.Add(128);
        left.publicData.permanentBuffTempDatas[369] = 999;
        left.publicData.lastRoundData.permanentBuffTempDatas[10045] = 3;
        right.publicData.lastRoundData.exp = 80;
        right.publicData.lastRoundData.permanentBuffTempDatas[369] = 6;
        string leftText = RecordSpeed.Format("修为：999", left);
        Assert.Equal("修为：70+3（73）", leftText);
        Assert.Equal("修为：80+6（86）", RecordSpeed.Format("修为：80", right));
        left.publicData.lastRoundData = new BattlePlayerLastRoundData { exp = 72 };
        Assert.Equal("修为：72+0（72）", RecordSpeed.Format(leftText, left));
        Assert.Equal(999, left.publicData.exp);
        Assert.Equal(999, left.publicData.permanentBuffTempDatas[369]);
        Assert.Equal(6, RecordSpeed.Extra(right));
    }

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(1, 3, 0)]
    [InlineData(3, 7, 3)]
    [InlineData(4, 7, 3)]
    [InlineData(5, 7, 3)]
    [InlineData(6, 7, 3)]
    [InlineData(7, 7, 0)]
    public void ElementTalentMatchesNative(int first, int second, int expected)
    {
        PlayerData player = Player();
        player.publicData.lastRoundData.talents.Add(128);
        player.publicData.lastRoundData.usedCards.AddRange(new[] { first, second });
        Assert.Equal(expected, RecordSpeed.Extra(player));
        AssertNative(player);
    }

    [Fact]
    public void TalentVariantAndResonanceOverrideUseRecordedSelections()
    {
        PlayerData player = Player();
        player.publicData.lastRoundData.talents.AddRange(new[] { 10128, 278 });
        player.publicData.lastRoundData.usedCards.AddRange(new[] { 1, 3 });
        Assert.Equal(5, RecordSpeed.Extra(player));
        AssertNative(player);
        player.publicData.lastRoundData.talents.Remove(278);
        Assert.Equal(0, RecordSpeed.Extra(player));
    }

    [Fact]
    public void SpeedKeyinAndBuffsStackWithoutCountingOtherEffects()
    {
        PlayerData player = Player();
        player.publicData.lastRoundData.usedKeYinCards.AddRange(new[] { 40042, 50042, 40043 });
        player.publicData.lastRoundData.permanentBuffTempDatas[10012] = 2;
        player.publicData.lastRoundData.permanentBuffTempDatas[10023] = 100;
        Assert.Equal(8, RecordSpeed.Extra(player));
        AssertNative(player);
    }

    [Theory]
    [InlineData("Cultivation:70", "Cultivation:70+0（70）")]
    [InlineData("修为：70", "修为：70+0（70）")]
    [InlineData("70", "修为：70+0（70）")]
    [InlineData(null, "修为：70+0（70）")]
    public void FormatPreservesLocalizedPrefix(string label, string expected)
    {
        Assert.Equal(expected, RecordSpeed.Format(label, Player()));
    }

    [Fact]
    public void NegativeSpeedHasOneSign()
    {
        PlayerData player = Player();
        player.publicData.lastRoundData.permanentBuffTempDatas[369] = -2;
        Assert.Equal("修为：70-2（68）", RecordSpeed.Format("修为：70", player));
    }

    [Fact]
    public void IncompleteOptionalDataDoesNotBreakDisplay()
    {
        PlayerData player = Player();
        player.publicData.lastRoundData.talents = null;
        player.publicData.lastRoundData.permanentBuffTempDatas = null;
        player.publicData.lastRoundData.usedKeYinCards = null;
        Assert.Equal(0, RecordSpeed.Extra(player));
    }

    [Fact]
    public void FourHundredMixedSnapshotsMatchActualGameMethod()
    {
        Random random = new Random(20261003);
        for (int example = 0; example < 400; example++)
        {
            PlayerData player = Player();
            BattlePlayerLastRoundData snapshot = player.publicData.lastRoundData;
            foreach (int buff in new[] { 10012, 10045, 10056, 369, 10023 })
                if (random.Next(2) == 1) snapshot.permanentBuffTempDatas[buff] = random.Next(10);
            if (random.Next(2) == 1) snapshot.talents.Add(128);
            if (random.Next(2) == 1) snapshot.talents.Add(10128);
            if (random.Next(2) == 1) snapshot.talents.Add(278);
            for (int i = 0; i < 8; i++) snapshot.usedCards.Add(random.Next(1, 9));
            if (random.Next(2) == 1) snapshot.usedKeYinCards.Add(40042);
            if (random.Next(2) == 1) snapshot.usedKeYinCards.Add(50042);
            snapshot.usedKeYinCards.Add(40043);
            AssertNative(player);
        }
    }

    private static PlayerData Player()
    {
        PlayerData player = new PlayerData();
        player.publicData.lastRoundData.exp = 70;
        return player;
    }

    private static void AssertNative(PlayerData player)
    {
        BattleTempData battle = new BattleTempData { playerData = player };
        foreach (int id in player.publicData.lastRoundData.usedCards)
            battle.battleCardInfos.Add(new BattleCardInfo { id = id });
        battle.battleKeYinCards = player.publicData.lastRoundData.usedKeYinCards;
        Assert.Equal(NativeSpeedOracle.CalExtraSpeed(battle), RecordSpeed.Extra(player));
    }
}
