using Zeus.Sim;

namespace Zeus.Sim.Tests;

public class LooseBallRecoveryTests
{
    [Fact]
    public void CompetitionSeedRecoversWideLooseBallBeforeHalfTime()
    {
        // The release's second career round stalled after two passes until the half-time reset.
        var home = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433"));
        var away = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        string squads = Path.GetDirectoryName(SquadTests.SquadPath("amateur"))!;
        var match = Match.Create(new(), home, away, 81916775885938UL,
            homeSquad: SquadFactory.ForLevel("amateur", home, squads),
            awaySquad: SquadFactory.ForLevel("amateur", away, squads));
        for (int tick = 0; tick < 3000; tick++) match.Step();
        var stats = MatchStatistics.FromMatch(match);
        Assert.True(stats.Away.Interceptions > 0, "The defending outfield players must retrieve the loose ball.");
        Assert.True(stats.Home.Passes + stats.Away.Passes > 2, "Play must continue before the half-time reset.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void GuardingKeeperDoesNotStopOutfielderRecoveringLooseBall(int direction)
    {
        var view = LooseBall(direction);
        var intent = new PassingBrain(new()).Decide(view);
        Assert.Equal(IntentKind.MoveToPosition, intent.Kind);
        Assert.Equal(view.VisibleBall!.Pos.XY, intent.Target);
    }

    [Fact]
    public void CloserOutfielderStillTakesPriority()
    {
        var view = LooseBall(1);
        view = view with { VisiblePlayers = view.VisiblePlayers.Append(
            new PlayerSnapshot(Side.Home, "LB", new Vec2(-47, 8), Vec2.Zero, 0)).ToArray() };
        var intent = new PassingBrain(new()).Decide(view);
        Assert.NotEqual(view.VisibleBall!.Pos.XY, intent.Target);
    }

    [Fact]
    public void KeeperStillGuardsInsteadOfChasingWideBall()
    {
        var view = LooseBall(1);
        view = view with { Self = view.VisiblePlayers.Single(), VisiblePlayers = new[] { view.Self } };
        var intent = new PassingBrain(new()).Decide(view);
        Assert.Equal(IntentKind.KeeperGuard, intent.Kind);
        Assert.NotEqual(view.VisibleBall!.Pos.XY, intent.Target);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    [InlineData(-1, true)]
    public void IntendedReceiverRetrievesVisiblePassThatHasStoppedShort(int direction, bool keeper)
    {
        var view = LooseBall(direction);
        if (keeper)
            view = view with { Self = view.VisiblePlayers.Single(), VisiblePlayers = new[] { view.Self } };
        view = view with { ReceiveTarget = new Vec2(-44 * direction, 0), TeamPassInFlight = true };

        var intent = new PassingBrain(new()).Decide(view);

        Assert.Equal(IntentKind.MoveToPosition, intent.Kind);
        Assert.Equal(view.VisibleBall!.Pos.XY, intent.Target);
    }

    [Theory]
    [InlineData("moving")]
    [InlineData("airborne")]
    [InlineData("hidden")]
    public void ReceiverKeepsMeetingPointUnlessHeSeesBallAtRestOnGround(string situation)
    {
        var view = LooseBall(1);
        var ball = view.VisibleBall!;
        if (situation == "moving") ball = ball with { Vel = new Vec3(4, 0, 0) };
        if (situation == "airborne") ball = ball with { Pos = ball.Pos with { Z = 2 } };
        var meet = new Vec2(-44, 0);
        view = view with { ReceiveTarget = meet, TeamPassInFlight = true,
            VisibleBall = situation == "hidden" ? null : ball, KnownBall = new BallObservation(30, ball) };

        Assert.Equal(meet, new PassingBrain(new()).Decide(view).Target);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    [InlineData(-1, true)]
    public void BlindReceiverSearchesAfterArrivingAtMeetingPoint(int direction, bool keeper)
    {
        var view = LooseBall(direction);
        if (keeper) view = view with { Self = view.VisiblePlayers.Single(), VisiblePlayers = new[] { view.Self } };
        view = view with { ReceiveTarget = view.Self.Pos, TeamPassInFlight = true, VisibleBall = null, KnownBall = null };

        var intent = new PassingBrain(new()).Decide(view);

        Assert.Equal(IntentKind.Scan, intent.Kind);
        Assert.Equal(view.Self.Pos, intent.Target);
        Assert.Null(intent.FacingTarget);
    }

    [Fact]
    public void BlindReceiverStillRunsToMeetingPointBeforeSearching()
    {
        var view = LooseBall(1);
        var meet = view.Self.Pos + new Vec2(5, 0);
        view = view with { ReceiveTarget = meet, TeamPassInFlight = true, VisibleBall = null, KnownBall = null };
        var intent = new PassingBrain(new()).Decide(view);
        Assert.Equal(IntentKind.MoveToPosition, intent.Kind);
        Assert.Equal(meet, intent.Target);
    }

    [Fact]
    public void InvisibleOvershotPassIsRecoveredBeforeHalfTime()
    {
        // The intended receiver waited blind at the aim point, blocking his whole team from retrieving the ball.
        var home = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("positiespel_4231"));
        var away = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("counter_532"));
        var match = Match.Create(new(Instructions: new(EnableSupport: true)), home, away, 202);
        for (int tick = 0; tick < 12000; tick++) match.Step();
        Assert.Contains(match.Events.Events, e => e.Seconds > 1099.3 && e.Type == EventType.BallControl);
    }

    private static Perception LooseBall(int direction)
    {
        var self = new PlayerSnapshot(Side.Home, "LC", new Vec2(-38 * direction, 6 * direction), Vec2.Zero, 0);
        var keeper = new PlayerSnapshot(Side.Home, "GK", new Vec2(-51.7 * direction, 0), Vec2.Zero, 0);
        var ball = new BallSnapshot(new Vec3(-50 * direction, 8 * direction, 0), Vec3.Zero);
        return new Perception(30, self, GamePhase.Verdedigen, false, new Vec2(-30 * direction, 5 * direction),
            new[] { keeper }, ball, new BallObservation(30, ball), BallIsLoose: true, AttackPositiveX: direction > 0);
    }
}
