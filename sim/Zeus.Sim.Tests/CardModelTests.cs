using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class CardModelTests
{
    [Fact]
    public void ProbabilitiesAreBoundedAndSumToOneIncludingExtremeConfiguration()
    {
        foreach (double speed in new[] { -100.0, 0, 2, 8, 100 })
        foreach (bool behind in new[] { false, true })
        foreach (var config in new[] { CardConfig.Default,
            new CardConfig(BaseRed: 1, BaseYellow: 1, MaxRed: 1, MaxYellow: 1) })
        {
            var c = CardModel.Chances(speed, behind, config);
            Assert.InRange(c.Red, 0, 1);
            Assert.InRange(c.Yellow, 0, 1);
            Assert.InRange(c.None, 0, 1);
            Assert.Equal(1, c.Red + c.Yellow + c.None, 12);
        }
    }

    [Fact]
    public void DefaultBaseRisksAreExplicitAndSpeedAndBehindIncreaseRisk()
    {
        var baseline = CardModel.Chances(0, false);
        Assert.Equal(0.0005, baseline.Red);
        Assert.Equal(0.04, baseline.Yellow);
        var fast = CardModel.Chances(8, false);
        var behind = CardModel.Chances(8, true);
        Assert.True(fast.Red > baseline.Red);
        Assert.True(fast.Yellow > baseline.Yellow);
        Assert.True(behind.Red > fast.Red);
        Assert.True(behind.Yellow > fast.Yellow);
        Assert.True(behind.None < fast.None);
        Assert.Equal(baseline, CardModel.Chances(-20, false));
        Assert.Equal(behind, CardModel.Chances(100, true));
    }

    [Fact]
    public void OneSeededDrawMatchesProbabilityIntervalsAndConsumesExactlyOneDraw()
    {
        var chances = CardModel.Chances(5, true);
        var outcomes = new HashSet<CardDecision>();
        for (ulong seed = 0; seed < 100; seed++)
        {
            var rng = new Rng(seed);
            var reference = new Rng(seed);
            double draw = reference.NextDouble();
            var expected = draw < chances.Red ? CardDecision.Red
                : draw < chances.Red + chances.Yellow ? CardDecision.Yellow : CardDecision.None;
            var actual = CardModel.ChooseCard(5, true, false, rng);
            Assert.Equal(expected, actual);
            Assert.Equal(reference.NextUInt(), rng.NextUInt());
            outcomes.Add(actual);
        }
        Assert.Equal(3, outcomes.Count);
    }

    [Fact]
    public void ExistingYellowConvertsOnlyYellowIntoSecondYellowDismissal()
    {
        var outcomes = new HashSet<CardDecision>();
        for (ulong seed = 0; seed < 100; seed++)
        {
            var first = CardModel.ChooseCard(5, true, false, new Rng(seed));
            var second = CardModel.ChooseCard(5, true, true, new Rng(seed));
            Assert.Equal(first == CardDecision.Yellow ? CardDecision.SecondYellowRed : first, second);
            outcomes.Add(second);
        }
        Assert.Contains(CardDecision.Red, outcomes);
        Assert.Contains(CardDecision.SecondYellowRed, outcomes);
        Assert.Contains(CardDecision.None, outcomes);
    }

    [Fact]
    public void SameSeedProducesSameSequence()
    {
        var a = new Rng(42); var b = new Rng(42);
        for (int i = 0; i < 100; i++)
            Assert.Equal(CardModel.ChooseCard(6, true, true, a), CardModel.ChooseCard(6, true, true, b));
    }

    [Fact]
    public void InvalidSpeedAndConfigurationAreRejectedBeforeDrawing()
    {
        var rng = new Rng(42); var reference = new Rng(42);
        Assert.Throws<ArgumentException>(() => CardModel.ChooseCard(double.NaN, false, false, rng));
        Assert.Throws<ArgumentException>(() => CardModel.Chances(double.PositiveInfinity, false));
        Assert.Throws<ArgumentException>(() => new CardConfig(SpeedScale: 0).Validate());
        Assert.Throws<ArgumentException>(() => new CardConfig(MaxRed: 2).Validate());
        Assert.Equal(reference.NextUInt(), rng.NextUInt());
    }
}
