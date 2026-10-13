using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class PerceptionTests
{
    private static readonly PlayerSnapshot Self = new(Side.Home, "RC", Vec2.Zero, Vec2.Zero, 0);
    private static Snapshot World(long tick, Vec3 ball, params PlayerSnapshot[] players) =>
        new(tick, tick * 0.1, new[] { Self }.Concat(players).ToArray(), new BallSnapshot(ball, Vec3.Zero));

    [Fact]
    public void ViewConeIncludesBoundaryButExcludesBehindAndBeyondRange()
    {
        var c = new PerceptionConfig(SightRange: 20);
        Assert.True(PerceptionBuilder.InSight(Self, new Vec2(20, 0), c));
        Assert.True(PerceptionBuilder.InSight(Self, new Vec2(5, 5 * Math.Sqrt(3)), c));
        Assert.False(PerceptionBuilder.InSight(Self, new Vec2(0, 10), c));
        Assert.False(PerceptionBuilder.InSight(Self, new Vec2(-10, 0), c));
        Assert.False(PerceptionBuilder.InSight(Self, new Vec2(20.001, 0), c));
        Assert.True(PerceptionBuilder.InSight(Self with { Heading = Math.PI }, new Vec2(-10, 0), c));
    }

    [Fact]
    public void ViewFiltersBothTeamsAndCannotBeMutated()
    {
        var front = new PlayerSnapshot(Side.Away, "RC", new Vec2(10, 0), Vec2.Zero, 0);
        var behind = new PlayerSnapshot(Side.Home, "LB", new Vec2(-10, 0), Vec2.Zero, 0);
        var view = PerceptionBuilder.Build(World(0, new Vec3(5, 0, 0), front, behind), Self,
            GamePhase.Opbouw, false, new PerceptionConfig(AllRoundMeters: 0), 0.1); // view cone only
        Assert.Equal(front, Assert.Single(view.VisiblePlayers)); // Same id on other team is not self.
        Assert.Throws<NotSupportedException>(() => ((IList<PlayerSnapshot>)view.VisiblePlayers)[0] = behind);
    }

    [Fact]
    public void PlayersWithinTheDefaultAllRoundRadiusAreKnownEvenBehindTheObserver()
    {
        // Default AllRoundMeters is 25 (docs/kalibratie.md "Ronde 3"): a player 10 m behind is known, one 30 m behind is not.
        Assert.Equal(25, new PerceptionConfig().AllRoundMeters);
        var near = new PlayerSnapshot(Side.Home, "LB", new Vec2(-10, 0), Vec2.Zero, 0);
        var far = new PlayerSnapshot(Side.Home, "LCM", new Vec2(-30, 0), Vec2.Zero, 0);
        var view = PerceptionBuilder.Build(World(0, new Vec3(5, 0, 0), near, far), Self, GamePhase.Opbouw, false, new PerceptionConfig(), 0.1);
        Assert.Equal(near, Assert.Single(view.VisiblePlayers));
    }

    [Fact]
    public void HiddenBallKeepsOldPositionThenExpires()
    {
        var c = new PerceptionConfig(BallMemorySeconds: 1);
        var initial = PerceptionBuilder.Build(World(0, new Vec3(10, 0, 0)), Self, GamePhase.Opbouw, false, c, 0.1);
        var hidden = PerceptionBuilder.Build(World(10, new Vec3(-20, 0, 0)), Self, GamePhase.Opbouw, false,
            c, 0.1, initial.KnownBall);
        Assert.Null(hidden.VisibleBall);
        Assert.Equal(new Vec3(10, 0, 0), hidden.KnownBall!.Ball.Pos);
        Assert.Equal(0, hidden.KnownBall.Tick);
        var expired = PerceptionBuilder.Build(World(11, new Vec3(-30, 0, 0)), Self, GamePhase.Opbouw, false,
            c, 0.1, hidden.KnownBall);
        Assert.Null(expired.KnownBall);
    }

    [Fact]
    public void BallNearFeetIsSensedBehindButHighBallIsNot()
    {
        var c = new PerceptionConfig();
        Assert.NotNull(PerceptionBuilder.Build(World(0, new Vec3(-0.4, 0, 0)), Self,
            GamePhase.Opbouw, false, c, 0.1).VisibleBall);
        Assert.Null(PerceptionBuilder.Build(World(0, new Vec3(-0.4, 0, 3)), Self,
            GamePhase.Opbouw, false, c, 0.1).VisibleBall);
        Assert.NotNull(PerceptionBuilder.Build(World(0, new Vec3(-0.4, 0, 0)), Self,
            GamePhase.Opbouw, true, c with { NearBallRadius = 0 }, 0.1).VisibleBall);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidScanIntervalFailsAtCreation(double interval)
    {
        var t = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        Assert.Throws<ArgumentException>(() => Match.Create(new MatchConfig(
            Perception: new PerceptionConfig(ScanIntervalSeconds: interval)), t, t, 1));
    }
}
