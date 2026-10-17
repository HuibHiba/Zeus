using Zeus.Sim;

namespace Zeus.Sim.Tests;

public class TeamMovementTests
{
    sealed class StationaryBrain : IPlayerBrain
    {
        public Intent Decide(Perception view) => new(IntentKind.HoldPosition, view.Self.Pos);
    }

    static Vec2 ObservedTarget(double ballX)
    {
        var tactic = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("positiespel_4231"));
        var config = new MatchConfig(MaxReactionSeconds: 0,
            Perception: new(FieldOfViewRadians: 2 * Math.PI, SightRange: 200, ScanIntervalSeconds: .1),
            Instructions: new(EnableSupport: false, EnableStayBack: false, EnableDrop: false,
                EnableAdvance: false, EnableBoxRun: false, EnableCutInside: false, EnableWidth: false));
        var match = Match.Create(config, tactic, tactic, 1, (_, _) => new StationaryBrain());
        match.SetBall(new((ballX - .5) * Pitch.Length, 0, 10), Vec3.Zero);
        match.Step();
        var view = match.PerceptionOf(Side.Home, "RDM");
        Assert.NotNull(view);
        Assert.NotNull(view.KnownBall);
        Assert.Equal(ballX < .66 ? GamePhase.Opbouw : GamePhase.Aanval, view.Phase);
        return view.FormationTarget;
    }

    [Fact]
    public void CrossingIntoAttackDoesNotSendAnEntireLineFortyMetresForward()
    {
        Assert.InRange((ObservedTarget(.66001) - ObservedTarget(.65999)).Length, 0, .01);
    }

    [Fact]
    public void HighBlockRecoversBehindTheObservedBallInsteadOfGuardingTheOtherHalf()
    {
        var ball = new BallSnapshot(new(-30, 0, 0), Vec3.Zero);
        var view = new Perception(0, new(Side.Home, "RC", new(5.25, 8), Vec2.Zero, 0),
            GamePhase.Verdedigen, false, new(5.25, 8), [], ball, new(0, ball));
        var layout = new PhasePlayer("RC", .55, .6, null, [], null);
        var target = InstructionRules.Target(view, layout, layout, new(.5, .5, "zone", .6, .8),
            .55, new(15.75, 0), new());
        Assert.True(target.X <= ball.Pos.X - 8 + 1e-9);
        Assert.InRange(target.X, -Pitch.HalfLength, Pitch.HalfLength);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void SecondDefenderScreensBehindThePresserAndMirrorsWithEnds(int direction)
    {
        PlayerSnapshot Player(string id, double x, double y) => new(Side.Home, id,
            new Vec2(x, y) * direction, Vec2.Zero, 0);
        var ball = new BallSnapshot(new(10 * direction, 0, 0), Vec3.Zero);
        var view = new Perception(0, Player("CM", 0, 10), GamePhase.Verdedigen, false,
            new Vec2(0, 10) * direction, [Player("P", 9, 0)], ball, new(0, ball),
            AttackPositiveX: direction > 0);
        var target = DefensiveSupport.Target(view, view.FormationTarget);
        Assert.NotEqual(view.FormationTarget, target);
        Assert.InRange((target - view.FormationTarget).Length, 0, 8.000000001);
        Assert.True(target.X * direction < ball.Pos.X * direction);
        Assert.Equal(view.FormationTarget, DefensiveSupport.Target(view with { IsPresser = true }, view.FormationTarget));
        Assert.Equal(view.FormationTarget, DefensiveSupport.Target(view with { VisibleBall = null }, view.FormationTarget));
        Assert.Equal(view.FormationTarget, DefensiveSupport.Target(view with { Phase = GamePhase.Opbouw }, view.FormationTarget));
        Assert.Equal(view.FormationTarget, DefensiveSupport.Target(view with {
            VisiblePlayers = [Player("P", 9, 0), Player("OTHER", 5, 0)] }, view.FormationTarget));
    }

    [Fact]
    public void ExplicitBallWinLayoutIsPreserved()
    {
        var source = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("positiespel_4231"));
        var phases = source.Phases.ToDictionary(p => p.Key, p => p.Value);
        phases[GamePhase.Balwinst] = new(null, source.Phase(GamePhase.Opbouw).Players.Select(p =>
            p with { X = .25, FollowBall = null }).ToArray());
        var tactic = source with { Phases = phases };
        var match = Match.Create(new(MaxReactionSeconds: 0,
            Perception: new(FieldOfViewRadians: 2 * Math.PI, SightRange: 200, ScanIntervalSeconds: .1),
            Instructions: InstructionConfig.Disabled with { EnableDynamicShape = true }),
            tactic, tactic, 1, (_, _) => new StationaryBrain());
        var keeper = match.GetSnapshot().Players.Single(p => p.Team == Side.Away && p.IsKeeper);
        match.SetBall(new(keeper.Pos.X, keeper.Pos.Y, 0), Vec3.Zero);
        match.Step();
        match.Step();
        var view = match.PerceptionOf(Side.Away, "RDM")!;
        Assert.Equal(GamePhase.Balwinst, view.Phase);
        var layout = phases[GamePhase.Balwinst].Players.Single(p => p.Id == "RDM");
        Assert.Equal(Pitch.ToMeters(layout.X, layout.Y, Side.Away), view.FormationTarget);
    }
    // A defender who loses sight of the ball must keep positioning on where he last saw it, not jump to the
    // centre-circle shape (AI diagnosis 2026-10-17). The ball is moved beyond his sight range, so no glance can refresh it.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DefenderKeepsPositioningOnTheLastSeenBall(bool dynamicShape)
    {
        var tactic = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("positiespel_4231"));
        var config = new MatchConfig(MaxReactionSeconds: 0,
            Perception: new(FieldOfViewRadians: 2 * Math.PI, SightRange: 45, AllRoundMeters: 25, ScanIntervalSeconds: .1,
                BallMemorySeconds: .3),
            Instructions: new() { EnableDynamicShape = dynamicShape });
        var match = Match.Create(config, tactic, tactic, 1, (_, _) => new StationaryBrain());
        void Hold(Vec3 ball, int steps) { for (int i = 0; i < steps; i++) { match.SetBall(ball, Vec3.Zero); match.Step(); } }
        Hold(new(5, 0, 6), 3);
        var defending = match.Possession == Side.Home ? Side.Away : Side.Home;
        double towardsOwnGoal = defending == Side.Home ? -1 : 1;
        var seen = new Vec3(10 * towardsOwnGoal, 15, 6);
        Hold(seen, 5);
        var before = match.PerceptionOf(defending, "LC")!;
        Assert.NotNull(before.KnownBall);
        // Far beyond 45 m from the defender, on the opponent's half.
        Hold(new(-50 * towardsOwnGoal, -30, 6), 10);
        var after = match.PerceptionOf(defending, "LC")!;
        Assert.Null(after.KnownBall);
        Assert.Equal(before.Phase, after.Phase);
        if (dynamicShape)
        {
            Assert.Equal(seen, after.BallBelief);
            Assert.InRange((after.FormationTarget - before.FormationTarget).Length, 0, 1e-9);
        }
        else
        {
            Assert.Null(after.BallBelief);
            Assert.True((after.FormationTarget - before.FormationTarget).Length > 1);
        }
    }
}
