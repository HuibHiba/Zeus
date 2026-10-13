using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class DuelModelTests
{
    [Fact]
    public void ChancesAreBoundedAndSumToOneAcrossExtremeInputs()
    {
        foreach (double distance in new[] { 0.0, 0.6, 1.2, 1.21, 100 })
        foreach (double speed in new[] { -100.0, 0, 8, 100 })
        foreach (double angle in new[] { -Math.PI, 0, Math.PI / 2, 20 * Math.PI })
        foreach (double skill in new[] { -1.0, 0, 0.5, 1, 2 })
        foreach (double discipline in new[] { -1.0, 0, 1, 2 })
        foreach (double fatigue in new[] { -1.0, 0, 1, 2 })
        {
            var c = DuelModel.Chances(distance, speed, angle, skill, discipline, fatigue);
            Assert.InRange(c.WonBall, 0, 1);
            Assert.InRange(c.LooseBall, 0, 1);
            Assert.InRange(c.Foul, 0, DuelConfig.Default.MaxFoul);
            Assert.InRange(c.None, 0, 1);
            Assert.Equal(1, c.WonBall + c.LooseBall + c.Foul + c.None, 12);
        }
    }

    [Fact]
    public void SkillIncreasesWinAndDistanceDecreasesWin()
    {
        var low = DuelModel.Chances(0.5, 2, 0, 0.2, 0.8, 0);
        var high = DuelModel.Chances(0.5, 2, 0, 0.8, 0.8, 0);
        var far = DuelModel.Chances(1.2, 2, 0, 0.8, 0.8, 0);
        Assert.True(high.WonBall > low.WonBall);
        Assert.Equal(high.Foul, low.Foul);
        Assert.True(high.WonBall > far.WonBall);
    }

    [Fact]
    public void SpeedRearApproachIndisciplineAndFatigueIncreaseFouls()
    {
        var baseline = DuelModel.Chances(0.5, 0, 0, 0.7, 1, 0);
        Assert.True(DuelModel.Chances(0.5, 8, 0, 0.7, 1, 0).Foul > baseline.Foul);
        var side = DuelModel.Chances(0.5, 0, Math.PI / 2, 0.7, 1, 0);
        var rear = DuelModel.Chances(0.5, 0, Math.PI, 0.7, 1, 0);
        Assert.True(side.Foul > baseline.Foul);
        Assert.True(rear.Foul > side.Foul);
        Assert.True(DuelModel.Chances(0.5, 0, 0, 0.7, 0, 0).Foul > baseline.Foul);
        var tired = DuelModel.Chances(0.5, 0, 0, 0.7, 1, 1);
        Assert.True(tired.Foul > baseline.Foul);
        Assert.True(tired.WonBall < baseline.WonBall);
        Assert.Equal(baseline, DuelModel.Chances(0.5, -10, 2 * Math.PI, 0.7, 1, 0));
    }

    [Fact]
    public void OutsideReachReturnsNoneAndPreservesRandomStream()
    {
        var rng = new Rng(42);
        var untouched = new Rng(42);
        Assert.Equal(new DuelChances(0, 0, 0, 1), DuelModel.Chances(1.20001, 8, Math.PI, 1, 0, 1));
        Assert.Equal(DuelOutcome.None, DuelModel.Resolve(1.20001, 8, Math.PI, 1, 0, 1, rng));
        Assert.Equal(untouched.NextUInt(), rng.NextUInt());
    }

    [Fact]
    public void EveryInRangeAttemptUsesExactlyOneDrawAndMatchesCategoricalProbabilities()
    {
        var seen = new HashSet<DuelOutcome>();
        var c = DuelModel.Chances(0.5, 4, Math.PI / 2, 0.7, 0.7, 0.2);
        for (ulong seed = 0; seed < 100; seed++)
        {
            var rng = new Rng(seed);
            var reference = new Rng(seed);
            double draw = reference.NextDouble();
            var expected = draw < c.Foul ? DuelOutcome.Foul
                : draw < c.Foul + c.WonBall ? DuelOutcome.WonBall
                : draw < c.Foul + c.WonBall + c.LooseBall ? DuelOutcome.LooseBall : DuelOutcome.None;
            var actual = DuelModel.Resolve(0.5, 4, Math.PI / 2, 0.7, 0.7, 0.2, rng);
            Assert.Equal(expected, actual);
            Assert.Equal(reference.NextUInt(), rng.NextUInt());
            seen.Add(actual);
        }
        Assert.Equal(4, seen.Count);
    }

    [Fact]
    public void SameSeedProducesSameSequenceAndCustomReachWorks()
    {
        var a = new Rng(42); var b = new Rng(42);
        for (int i = 0; i < 100; i++)
            Assert.Equal(DuelModel.Resolve(1.2, 5, 0, 0.75, 0.8, 0.2, a),
                DuelModel.Resolve(1.2, 5, 0, 0.75, 0.8, 0.2, b));
        Assert.Equal(new DuelChances(0, 0, 0, 1), DuelModel.Chances(0.6, 0, 0, 1, 1, 0, new DuelConfig(Reach: 0.5)));
    }

    [Fact]
    public void InvalidInputsAndConfigurationAreRejected()
    {
        Assert.Throws<ArgumentException>(() => DuelModel.Chances(-1, 0, 0, 1, 1, 0));
        Assert.Throws<ArgumentException>(() => DuelModel.Chances(0, double.NaN, 0, 1, 1, 0));
        Assert.Throws<ArgumentException>(() => new DuelConfig(Reach: 0).Validate());
        Assert.Throws<ArgumentException>(() => new DuelConfig(MaxFoul: 2).Validate());
    }
}
