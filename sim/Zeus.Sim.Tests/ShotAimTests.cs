using Zeus.Sim;

namespace Zeus.Sim.Tests;

public class ShotAimTests
{
    static (Vec2 From, Intent Shot) Decide(int direction, double lateral = 0)
    {
        var from = new Vec2(35 * direction, lateral * direction);
        var keeper = new PlayerSnapshot(Side.Away, "GK", new(50 * direction, 0), Vec2.Zero, 0,
            IsGoalkeeper: true);
        var view = new Perception(0, new(Side.Home, "ST", from, Vec2.Zero, 0), GamePhase.Aanval, true,
            from, new[] { keeper }, new BallSnapshot(new(from.X, from.Y, 0), Vec3.Zero), null,
            HeldBallSeconds: 5, AttackPositiveX: direction > 0);
        var shot = new PassingBrain(new(), new ShotConfig(MinGoalProbability: 0)).Decide(view);
        Assert.Equal(IntentKind.Shoot, shot.Kind);
        return (from, shot);
    }

    // Isolate aim and execution: an unmoving central keeper uses the real swept contact and ball physics.
    // This is not a match-conversion calibration or a claim that a moving keeper cannot save the shot.
    static bool Scores(Vec2 from, Vec2 aim, double skill, ulong seed, int direction)
    {
        var config = new MatchConfig();
        var keeper = new[] { new PlayerSnapshot(Side.Away, "GK", new(50 * direction, 0), Vec2.Zero, 0,
            IsGoalkeeper: true) };
        var ball = new BallState(new(from.X, from.Y, 0), ShotExecution.Velocity(from, aim, skill,
            new Rng(seed), config.ShotSettings));
        for (int step = 0; step < 400; step++)
        {
            var next = BallPhysics.Step(ball, config.BallPhysics, .02);
            var contact = BallControl.FindContact(ball, next, keeper, keeper, config.KeeperSettings);
            var exit = PitchRules.Evaluate(ball, next, Side.Home, homeAttacksPositiveX: direction > 0);
            if (exit is { } boundary && (contact is null || boundary.Fraction < contact.Value.Fraction))
                return boundary.Kind == PitchExitKind.Goal;
            if (contact is not null) return false;
            ball = next;
        }
        throw new InvalidOperationException("Shot neither reached a boundary nor the keeper.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void PerfectFinishDoesNotAimInsideAStationaryCentralKeepersReach(int direction)
    {
        var (from, shot) = Decide(direction);
        Assert.True(Scores(from, shot.Target, 1, 42, direction));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void WideShooterChoosesTheLaneThatDoesNotCrossTheKeepersReach(int direction)
    {
        var (from, shot) = Decide(direction, 20);
        Assert.True(shot.Target.Y * direction > 0);
        Assert.True(Scores(from, shot.Target, 1, 42, direction));
    }

    [Fact]
    public void ShotChoiceMirrorsWithTheEnds()
    {
        foreach (double lateral in new[] { -20.0, 0, 20 })
            Assert.Equal(Decide(1, lateral).Shot.Target * -1, Decide(-1, lateral).Shot.Target);
    }

    [Fact]
    public void SentOffKeeperCannotDetermineTheShotDirection()
    {
        var from = new Vec2(35, 0);
        var active = new PlayerSnapshot(Side.Away, "replacement", new(50, -1), Vec2.Zero, 0, IsGoalkeeper: true);
        var dismissed = new PlayerSnapshot(Side.Away, "GK", new(50, 1), Vec2.Zero, 0, SentOff: true);
        var view = new Perception(0, new(Side.Home, "ST", from, Vec2.Zero, 0), GamePhase.Aanval, true,
            from, new[] { dismissed, active }, new BallSnapshot(new(from.X, from.Y, 0), Vec3.Zero), null,
            HeldBallSeconds: 5, AttackPositiveX: true);
        var brain = new PassingBrain(new(), new ShotConfig(MinGoalProbability: 0));
        Assert.Equal(brain.Decide(view with { VisiblePlayers = new[] { active } }), brain.Decide(view));
        Assert.True(brain.Decide(view).Target.Y > 0);
    }

    [Fact]
    public void HigherPrecisionImprovesConversionInsteadOfRelyingOnExecutionMistakes()
    {
        var (from, shot) = Decide(1);
        int Goals(double skill) => Enumerable.Range(1, 256)
            .Count(seed => Scores(from, shot.Target, skill, (ulong)seed, 1));
        int low = Goals(.5), high = Goals(.9), perfect = Goals(1);
        Assert.True(high > low, $"low={low}, high={high}");
        Assert.True(perfect > high, $"high={high}, perfect={perfect}");
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-.1)]
    [InlineData(3.66)]
    public void AimMustRemainFiniteAndInsideThePosts(double offset) =>
        Assert.Throws<ArgumentException>(() => new ShotConfig(AimOffsetMeters: offset).Validate());

    [Fact]
    public void AimOffsetRoundTripsThroughMatchConfiguration()
    {
        var config = MatchConfigJson.Parse("{\"Shots\":{\"AimOffsetMeters\":3.1}}");
        Assert.Equal(3.1, config.ShotSettings.AimOffsetMeters);
        new ShotConfig(AimOffsetMeters: 0).Validate();
    }
}
