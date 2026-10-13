using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class PassingTests
{
    private static PlayerSnapshot Player(Side team, string id, double x, double y) =>
        new(team, id, new Vec2(x, y), Vec2.Zero, 0);
    private static Perception View(params PlayerSnapshot[] players) => new(0,
        Player(Side.Home, "passer", 0, 0), GamePhase.Opbouw, true, Vec2.Zero,
        players, new BallSnapshot(Vec3.Zero, Vec3.Zero), null, HeldBallSeconds: 2);

    [Fact]
    public void ChoosesOnlyVisibleTeammateInAllowedDistance()
    {
        var view = View(Player(Side.Away, "opponent", 8, 0), Player(Side.Home, "near", 1, 0),
            Player(Side.Home, "far", 45, 0), Player(Side.Home, "valid", 10, 10));
        Assert.Equal("valid", PassChoice.Choose(view, new PassConfig())!.ReceiverId);
        Assert.Null(PassChoice.Choose(view with { HasBall = false }, new PassConfig()));
        Assert.Null(PassChoice.Choose(View(), new PassConfig()));
    }

    [Fact]
    public void VisibleOpponentInLaneMakesSafeSidePassPreferable()
    {
        var view = View(Player(Side.Home, "forward", 15, 0), Player(Side.Home, "side", 0, 12),
            Player(Side.Away, "blocker", 7, 0));
        Assert.Equal("side", PassChoice.Choose(view, new PassConfig())!.ReceiverId);
        Assert.Equal("forward", PassChoice.Choose(view with {
            VisiblePlayers = view.VisiblePlayers.Where(p => p.Team == Side.Home).ToArray()
        }, new PassConfig())!.ReceiverId);
    }

    [Fact]
    public void EqualOptionsHaveStableIdentityTieBreak()
    {
        var a = Player(Side.Home, "A", 10, 10);
        var b = Player(Side.Home, "B", 10, -10);
        Assert.Equal("A", PassChoice.Choose(View(a, b), new PassConfig())!.ReceiverId);
        Assert.Equal("A", PassChoice.Choose(View(b, a), new PassConfig())!.ReceiverId);
    }

    [Fact]
    public void MovingReceiverUsesObservedVelocityAndLead()
    {
        var mate = Player(Side.Home, "runner", 10, 0) with { Vel = new Vec2(4, 0) };
        Assert.Equal(11.4, PassChoice.Choose(View(mate), new PassConfig())!.Target.X, 8);
    }

    [Fact]
    public void ExecutionIsSeededAndPerfectSkillHasNoDirectionError()
    {
        var config = new PassConfig();
        Assert.Equal(PassExecution.Velocity(Vec2.Zero, new Vec2(20, 0), 1, config, new Rng(42)),
            PassExecution.Velocity(Vec2.Zero, new Vec2(20, 0), 1, config, new Rng(42)));
        Assert.NotEqual(PassExecution.Velocity(Vec2.Zero, new Vec2(20, 0), 1, config, new Rng(42)),
            PassExecution.Velocity(Vec2.Zero, new Vec2(20, 0), 1, config, new Rng(43)));
        var ideal = PassExecution.Velocity(Vec2.Zero, new Vec2(20, 0), 3,
            config with { Skill = 1 }, new Rng(42));
        Assert.Equal(12, ideal.X, 8);
        Assert.Equal(0, ideal.Y);
        Assert.Equal(0, ideal.Z);
    }

    [Fact]
    public void NoPassOptionTriggersScanningInsteadOfOmniscientPass()
    {
        var brain = new PassingBrain(new PassConfig());
        Assert.Equal(IntentKind.Scan, brain.Decide(View()).Kind);
        Assert.Equal(IntentKind.HoldPosition, brain.Decide(View() with { HeldBallSeconds = 0 }).Kind);
    }

    private static Match Match(ulong seed) => Zeus.Sim.Match.Create(new MatchConfig(),
        TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442")),
        TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433")), seed);

    [Fact]
    public void DefaultMatchPassesAndControlsBallWithoutSuccessRoll()
    {
        var match = Match(42);
        for (int i = 0; i < 1200; i++) match.Step();
        Assert.Contains(match.Events.Events, e => e.Type == EventType.Pass);
        Assert.Contains(match.Events.Events, e => e.Type is EventType.BallControl or EventType.Interception);
        Assert.All(match.GetSnapshot().Players, p => Assert.True(double.IsFinite(p.Pos.X) && double.IsFinite(p.Pos.Y)));
    }

    [Fact]
    public void PassingMatchIsDeterministic()
    {
        var a = Match(42); var b = Match(42); var other = Match(43);
        for (int i = 0; i < 3000; i++) { a.Step(); b.Step(); other.Step(); }
        Assert.Equal(a.EventHash(), b.EventHash());
        Assert.Equal(a.SnapshotHash(), b.SnapshotHash());
        Assert.NotEqual(a.EventHash(), other.EventHash());
    }

    [Fact]
    public void StationaryPlayersScanAndMatchRemainsActiveAfterEarlyPasses()
    {
        var brain = new PassingBrain(new PassConfig());
        Assert.Equal(IntentKind.Scan, brain.Decide(View() with { HasBall = false, VisibleBall = null }).Kind);
        var match = Match(42);
        for (int i = 0; i < 6000; i++) match.Step();
        Assert.Contains(match.Events.Events, e => e.Type == EventType.Pass && e.Tick > 5000);
    }
}
