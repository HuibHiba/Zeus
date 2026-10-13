using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class BlockSelectionTests
{
    private static Tactic Tactic(params BlockSwitch[] rules)
    {
        var tactic = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433"));
        return tactic with { BlockSwitches = rules };
    }

    [Fact]
    public void AllConditionsMustMatchAndMinuteBoundaryIsInclusive()
    {
        var tactic = Tactic(new BlockSwitch("protect lead", new(ScoreState.Voor, 60, 0.7), BlockHeight.Laag));
        var initial = tactic.Phase(GamePhase.Verdedigen).Block;
        Assert.Equal(initial, BlockSelection.Select(tactic, ScoreState.Achter, 60, 0.8));
        Assert.Equal(initial, BlockSelection.Select(tactic, ScoreState.Voor, 59, 0.8));
        Assert.Equal(initial, BlockSelection.Select(tactic, ScoreState.Voor, 60, 0.7));
        Assert.Equal(BlockHeight.Laag, BlockSelection.Select(tactic, ScoreState.Voor, 60, 0.700001));
    }

    [Fact]
    public void LastMatchingRuleWinsAndLaterNonmatchDoesNotResetSelection()
    {
        var tactic = Tactic(
            new BlockSwitch("tired", new(null, null, 0.5), BlockHeight.Laag),
            new BlockSwitch("chase equalizer", new(ScoreState.Achter, 75, null), BlockHeight.Hoog),
            new BlockSwitch("protect lead", new(ScoreState.Voor, 80, null), BlockHeight.Laag));
        Assert.Equal(BlockHeight.Hoog, BlockSelection.Select(tactic, ScoreState.Achter, 85, 0.9));
        Assert.Equal(BlockHeight.Laag, BlockSelection.Select(tactic, ScoreState.Voor, 85, 0.9));
        Assert.Equal(BlockHeight.Laag, BlockSelection.Select(tactic, ScoreState.Gelijk, 85, 0.9));
    }

    [Fact]
    public void RecheckingWithoutMatchesReturnsBaseRatherThanRememberingPriorSwitch()
    {
        var tactic = Tactic(new BlockSwitch("lead", new(ScoreState.Voor, null, null), BlockHeight.Laag));
        Assert.Equal(BlockHeight.Laag, BlockSelection.Select(tactic, ScoreState.Voor, 80, 0.5));
        Assert.Equal(tactic.Phase(GamePhase.Verdedigen).Block,
            BlockSelection.Select(tactic, ScoreState.Gelijk, 81, 0.5));
        Assert.Empty(Tactic().BlockSwitches);
        Assert.Equal(tactic.Phase(GamePhase.Verdedigen).Block,
            BlockSelection.Select(Tactic(), ScoreState.Achter, 0, 0));
    }

    [Fact]
    public void NullableBaseRemainsNullWhenNoRuleMatches()
    {
        var tactic = Tactic();
        var phases = new Dictionary<GamePhase, PhaseLayout>(tactic.Phases);
        phases[GamePhase.Verdedigen] = phases[GamePhase.Verdedigen] with { Block = null };
        Assert.Null(BlockSelection.Select(tactic with { Phases = phases }, ScoreState.Gelijk, 0, 1));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void InvalidFatigueIsRejected(double fatigue) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => BlockSelection.Select(Tactic(), ScoreState.Gelijk, 0, fatigue));

    [Fact]
    public void InvalidTimeAndScoreAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BlockSelection.Select(Tactic(), ScoreState.Gelijk, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => BlockSelection.Select(Tactic(), (ScoreState)99, 0, 0));
    }

    [Fact]
    public void InvalidTargetOrThresholdFailsEvenWhenRuleWouldNotMatch()
    {
        Assert.Throws<ArgumentException>(() => BlockSelection.Select(Tactic(
            new BlockSwitch("missing", new(ScoreState.Achter, null, null), BlockHeight.Midden)), ScoreState.Voor, 0, 0));
        foreach (var threshold in new[] { double.NaN, double.PositiveInfinity, -0.1, 1.1 })
            Assert.Throws<ArgumentException>(() => BlockSelection.Select(Tactic(
                new BlockSwitch("invalid", new(ScoreState.Achter, null, threshold), BlockHeight.Laag)), ScoreState.Voor, 0, 0));
    }
}
