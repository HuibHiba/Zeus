using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class FatigueTests
{
    private static FatigueState Run(double speed, bool pressing, double endurance, double duration, double dt = 0.1)
    {
        var state = FatigueState.Fresh;
        int steps = (int)Math.Round(duration / dt);
        for (int i = 0; i < steps; i++) state = Fatigue.Step(state, speed, pressing, endurance, dt);
        return state;
    }

    [Fact]
    public void ReservoirsRemainBoundedAndOrderedAcrossExertionChanges()
    {
        var state = FatigueState.Fresh;
        for (int i = 0; i < 54000; i++)
        {
            state = Fatigue.Step(state, i % 1000 < 500 ? 7 : 1, i % 1000 < 500, 0.5, 0.1);
            Assert.InRange(state.LongEnergy, 0, 1);
            Assert.InRange(state.ShortEnergy, 0, state.LongEnergy);
        }
        Assert.Equal(new FatigueState(0, 0), Fatigue.Step(new FatigueState(5, -1), 0, false, 0.5, 0));
        Assert.Equal(FatigueState.Fresh, Fatigue.Step(new FatigueState(5, 5), 0, false, 0.5, 0));
        Assert.Equal(new FatigueState(0, 0), Fatigue.Step(FatigueState.Fresh, 100, true, 0, 100000));
    }

    [Fact]
    public void RestRecoversShortButNeverLongAndCannotExceedLongEnergy()
    {
        var tired = new FatigueState(0.2, 0.8);
        var rested = Fatigue.Step(tired, 0, false, 0.5, 60);
        Assert.Equal(0.8, rested.ShortEnergy);
        Assert.Equal(tired.LongEnergy, rested.LongEnergy);
        var walking = Fatigue.Step(tired, 1, false, 0.5, 10);
        Assert.True(walking.ShortEnergy > tired.ShortEnergy);
        Assert.True(walking.LongEnergy < tired.LongEnergy);
        var threshold = Fatigue.Step(tired, 2, false, 0.5, 10);
        Assert.Equal(tired.ShortEnergy, threshold.ShortEnergy);
    }

    [Fact]
    public void SprintingAndPressingCostMoreThanRest()
    {
        var resting = Run(0, false, 0.5, 45);
        var sprinting = Run(7, false, 0.5, 45);
        var pressing = Run(7, true, 0.5, 45);
        Assert.Equal(FatigueState.Fresh, resting);
        Assert.InRange(sprinting.ShortEnergy, 0.4, 0.6);
        Assert.True(pressing.ShortEnergy < sprinting.ShortEnergy);
        Assert.True(pressing.LongEnergy < sprinting.LongEnergy);
        var staticPressing = Fatigue.Step(new FatigueState(0.4, 0.9), 0, true, 0.5, 10);
        Assert.True(staticPressing.ShortEnergy < 0.4);
        Assert.True(staticPressing.LongEnergy < 0.9);
    }

    [Fact]
    public void EnduranceReducesConsumptionAndImprovesRecovery()
    {
        var low = Run(7, true, 0, 45);
        var high = Run(7, true, 1, 45);
        Assert.True(high.ShortEnergy > low.ShortEnergy);
        Assert.True(high.LongEnergy > low.LongEnergy);
        var tired = new FatigueState(0.1, 0.8);
        Assert.True(Fatigue.Step(tired, 0, false, 1, 10).ShortEnergy
            > Fatigue.Step(tired, 0, false, 0, 10).ShortEnergy);
        Assert.Equal(Fatigue.Step(tired, 7, true, 0, 10), Fatigue.Step(tired, 7, true, -1, 10));
        Assert.Equal(Fatigue.Step(tired, 7, true, 1, 10), Fatigue.Step(tired, 7, true, 2, 10));
    }

    [Fact]
    public void ProvisionalMatchWorkloadLeavesLongEnergyNearDesignRange()
    {
        var state = Run(2.2, false, 0.5, 5400);
        Assert.InRange(state.LongEnergy, 0.7, 0.85);
        Assert.Equal(state.LongEnergy, state.ShortEnergy);
    }

    [Fact]
    public void EffectsRespectFreshAndExhaustedEndpoints()
    {
        var profile = PlayerProfile.Default;
        Assert.Equal(profile, Fatigue.Apply(profile, FatigueState.Fresh));
        var exhausted = Fatigue.Apply(profile, new FatigueState(0, 0));
        Assert.Equal(profile.MaxSpeed * 0.85, exhausted.MaxSpeed, 12);
        Assert.Equal(profile.Acceleration, exhausted.Acceleration);
        Assert.Equal(profile.Deceleration, exhausted.Deceleration);
        Assert.Equal(profile.TurnRate, exhausted.TurnRate);
        Assert.Equal(0, Fatigue.ReactionExtra(FatigueState.Fresh));
        Assert.Equal(0.15, Fatigue.ReactionExtra(new FatigueState(0, 0)));
        Assert.Equal(profile.Acceleration * 0.8,
            Fatigue.Apply(profile, new FatigueState(0, 0), new FatigueConfig(MinAccelerationMultiplier: 0.8)).Acceleration);
    }

    [Fact]
    public void DeterministicAndDurationIndependentOfDecisionStepSize()
    {
        Assert.Equal(Run(7, true, 0.75, 40), Run(7, true, 0.75, 40));
        var small = Run(7, true, 0.75, 40, 0.02);
        var large = Run(7, true, 0.75, 40, 1);
        Assert.Equal(small.LongEnergy, large.LongEnergy, 10);
        Assert.Equal(small.ShortEnergy, large.ShortEnergy, 10);
        var state = new FatigueState(0.2, 0.8);
        var one = Fatigue.Step(state, 1, false, 0.5, 50);
        for (int i = 0; i < 500; i++) state = Fatigue.Step(state, 1, false, 0.5, 0.1);
        Assert.Equal(one.ShortEnergy, state.ShortEnergy, 10);
        Assert.Equal(one.LongEnergy, state.LongEnergy, 10);
    }

    [Fact]
    public void InvalidInputsFailClearly()
    {
        Assert.Throws<ArgumentException>(() => Fatigue.Step(FatigueState.Fresh, double.NaN, false, 1, 1));
        Assert.Throws<ArgumentException>(() => Fatigue.Step(FatigueState.Fresh, 0, false, double.NaN, 1));
        Assert.Throws<ArgumentException>(() => Fatigue.Step(FatigueState.Fresh, 0, false, 1, -1));
        Assert.Throws<ArgumentException>(() => Fatigue.Step(new FatigueState(double.NaN, 1), 0, false, 1, 1));
        Assert.Throws<ArgumentException>(() => new FatigueConfig(ShortSprintRate: -1).Validate());
        Assert.Throws<ArgumentException>(() => new FatigueConfig(MinEnduranceFactor: 0).Validate());
    }
}
