using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class RestartTests
{
    private static Match Create() => Match.Create(new MatchConfig(
        Passing: new PassConfig(HoldSeconds: 100),
        Duels: new DuelConfig(BaseFoul: 1, MaxFoul: 1, SpeedFoul: 0, ApproachFoul: 0,
            IndisciplineFoul: 0, FatigueFoul: 0, AttemptRate: 1)),
        TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442")),
        TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433")), 42);

    private static void UntilFoul(Match match)
    {
        for (int i = 0; i < 1000 && match.Status != MatchStatus.Restart; i++) match.Step();
        Assert.Equal(MatchStatus.Restart, match.Status);
        Assert.Contains(match.Events.Events, e => e.Type == EventType.Foul);
    }

    [Fact]
    public void FoulStopsMovementAndOpponentsKeepRestartDistance()
    {
        var match = Create();
        UntilFoul(match);
        var before = match.GetSnapshot();
        Assert.Equal(GamePhase.Stilstaand, match.PhaseOf(Side.Home));
        foreach (var p in before.Players.Where(p => p.Team != match.Possession && !p.SentOff))
            Assert.True((p.Pos - before.Ball!.Pos.XY).Length >= 9.15 - 1e-8);
        for (int i = 0; i < 10; i++) match.Step();
        Assert.Equal(before.Players.Select(p => (p.Team, p.Id, p.Pos, p.Vel, p.Heading)),
            match.GetSnapshot().Players.Select(p => (p.Team, p.Id, p.Pos, p.Vel, p.Heading)));
        Assert.Equal(before.Ball, match.GetSnapshot().Ball);
        for (int i = 0; i < 25; i++) match.Step();
        Assert.Contains(match.Events.Events, e => e.Type == EventType.RestartTaken);
    }

    private static void SendOff(Match match, string id)
    {
        match.ReportIncident(Side.Away, id, Vec2.Zero,
            new RefereeIncident(ContactAction.Charge, Conduct.ExcessiveForce, true, true,
                TacticalContext: TacticalContext.NoTacticalOffence, AgainstOpponent: true));
    }

    [Fact]
    public void DirectRedRemovesPlayerFromControlAndSightAndHeWalksOffTheField()
    {
        var match = Create();
        var offender = match.GetSnapshot().Players.First(p => p.Team == Side.Away && p.Id == "RC");
        SendOff(match, offender.Id);
        var dismissed = Assert.Single(match.GetSnapshot().Players.Where(p => p.SentOff));
        Assert.Contains(match.Events.Events, e => e.Type == EventType.Card && e.Outcome == "Red");
        for (int i = 0; i < 60; i++) match.Step();
        var after = match.GetSnapshot().Players.Single(p => p.Id == dismissed.Id && p.Team == dismissed.Team);
        Assert.NotEqual(dismissed.Pos, after.Pos); // he is walking off, not standing on the pitch
        Assert.All(match.GetSnapshot().Players.Where(p => !p.SentOff), p =>
        {
            var view = match.PerceptionOf(p.Team, p.Id);
            if (view is not null) Assert.DoesNotContain(view.VisiblePlayers, v => v.SentOff);
        });
    }

    [Fact]
    public void SentOffPlayerIsOutsideTheLinesAfterTheWalkAndStaysThereWithoutInteraction()
    {
        var match = Create();
        SendOff(match, "RC");
        // The longest walk is half the pitch width plus the exit margin: well within 30 s at 2 m/s.
        for (int i = 0; i < 300; i++) match.Step();
        var player = match.GetSnapshot().Players.Single(p => p.Team == Side.Away && p.Id == "RC");
        Assert.True(player.SentOff);
        Assert.True(Math.Abs(player.Pos.Y) > Pitch.HalfWidth, $"y {player.Pos.Y}");
        Assert.Equal(Vec2.Zero, player.Vel);
        var parked = player.Pos;
        for (int i = 0; i < 600; i++) match.Step();
        var later = match.GetSnapshot().Players.Single(p => p.Team == Side.Away && p.Id == "RC");
        Assert.Equal(parked, later.Pos);
        // Not in anyone's perception, so not in pressing/passing/offside either; the ball never gives him a touch.
        foreach (var p in match.GetSnapshot().Players.Where(p => !p.SentOff))
            if (match.PerceptionOf(p.Team, p.Id) is { } view) Assert.DoesNotContain(view.VisiblePlayers, v => v.Id == "RC" && v.Team == Side.Away);
        Assert.DoesNotContain(match.Events.Events.Skip(1), e => e.Team == Side.Away && e.PlayerId == "RC" && e.Tick > 0
            && e.Type is EventType.Pass or EventType.BallControl or EventType.Interception or EventType.Duel or EventType.Foul or EventType.Shot);
    }

    [Fact]
    public void SentOffPlayerDoesNotWalkWhenNobodyIsSentOffAndWalkSpeedIsValidated()
    {
        var match = Create();
        for (int i = 0; i < 200; i++) match.Step();
        Assert.All(match.GetSnapshot().Players, p => Assert.True(Math.Abs(p.Pos.Y) <= Pitch.HalfWidth));
        Assert.Throws<ArgumentException>(() => Match.Create(new MatchConfig(SentOffWalkSpeed: 0),
            TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442")),
            TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433")), 1));
        Assert.Throws<ArgumentException>(() => Match.Create(new MatchConfig(SentOffExitMeters: 0),
            TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442")),
            TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433")), 1));
    }

    [Fact]
    public void SentOffTeammateDoesNotBlockPressingOfTheNextDefender()
    {
        // PressingAssignment only sees the perceived (non-dismissed) players: a sent-off mate who is nearer to the ball is ignored.
        var ball = new BallSnapshot(new Vec3(10, 0, 0), Vec3.Zero);
        var self = new PlayerSnapshot(Side.Away, "A", new Vec2(15, 0), Vec2.Zero, Math.PI);
        var dismissedMate = new PlayerSnapshot(Side.Away, "B", new Vec2(11, 40), Vec2.Zero, 0, SentOff: true);
        var world = new Snapshot(0, 0, new[] { self, dismissedMate }, ball);
        var view = PerceptionBuilder.Build(world, self, GamePhase.Verdedigen, false, new PerceptionConfig(), 0.1);
        Assert.DoesNotContain(view.VisiblePlayers, p => p.SentOff);
        Assert.True(PressingAssignment.IsPresser(view));
    }

    [Fact]
    public void TwoEstablishedRecklessIncidentsCauseSecondYellowDismissal()
    {
        var match = Create();
        var incident = new RefereeIncident(ContactAction.Tackle, Conduct.Reckless, true, true,
            TacticalContext: TacticalContext.NoTacticalOffence, AgainstOpponent: true);
        Assert.Equal(CardDecision.Yellow, match.ReportIncident(Side.Away, "RC", Vec2.Zero, incident).Card);
        Assert.Equal(CardDecision.SecondYellowRed, match.ReportIncident(Side.Away, "RC", Vec2.Zero, incident with { BallInPlay = false }).Card);
        Assert.True(match.GetSnapshot().Players.Single(p => p.Team == Side.Away && p.Id == "RC").SentOff);
    }

    [Fact]
    public void MisconductDuringPauseReplacesDismissedTakerAndRetainsRestart()
    {
        var match = MatchTests.MakeMatch(42);
        match.SetBall(new Vec3(60, 20, 0), Vec3.Zero);
        match.Step();
        var awarded = match.Events.Events.Last(e => e.Type == EventType.RestartAwarded);
        var spot = match.Ball.Pos.XY;
        match.ReportIncident(awarded.Team, awarded.PlayerId, spot,
            new RefereeIncident(ContactAction.Strike, Conduct.ExcessiveForce, false, true,
                TacticalContext: TacticalContext.NoTacticalOffence, AgainstOpponent: true));
        Assert.Equal(awarded.Team, match.Possession);
        Assert.Equal(spot, match.Ball.Pos.XY);
        for (int i = 0; i < 32; i++) match.Step();
        Assert.NotEqual(awarded.PlayerId, match.CarrierId);
        var taken = match.Events.Events.Last(e => e.Type == EventType.RestartTaken);
        Assert.NotEqual(awarded.PlayerId, taken.PlayerId);
        Assert.False(match.GetSnapshot().Players.Single(p => p.Team == taken.Team && p.Id == taken.PlayerId).SentOff);
    }

    [Fact]
    public void InvalidIncidentLocationDoesNotMutateCardsOrEvents()
    {
        var match = MatchTests.MakeMatch(42);
        int events = match.Events.Events.Count;
        Assert.Throws<ArgumentException>(() => match.ReportIncident(Side.Away, "RC", new Vec2(double.NaN, 0),
            new RefereeIncident(ContactAction.Tackle, Conduct.Reckless, true, true,
                TacticalContext: TacticalContext.NoTacticalOffence, AgainstOpponent: true)));
        Assert.Equal(events, match.Events.Events.Count);
        Assert.Equal(0, match.GetSnapshot().Players.Single(p => p.Team == Side.Away && p.Id == "RC").YellowCards);
    }

    [Fact]
    public void FiveDismissalsStopMatchAtSixActivePlayers()
    {
        var match = MatchTests.MakeMatch(42);
        var players = match.GetSnapshot().Players.Where(p => p.Team == Side.Away).Take(5).ToArray();
        var incident = new RefereeIncident(ContactAction.Strike, Conduct.ExcessiveForce, true, true,
            TacticalContext: TacticalContext.NoTacticalOffence, AgainstOpponent: true);
        foreach (var player in players) match.ReportIncident(Side.Away, player.Id, Vec2.Zero,
            incident with { BallInPlay = match.Status == MatchStatus.Running });
        Assert.Equal(MatchStatus.Abandoned, match.Status);
        Assert.Contains(match.Events.Events, e => e.Type == EventType.Abandoned && e.Outcome == "fewer than seven players");
    }

    [Theory]
    [InlineData(60, 0)]
    [InlineData(0, 40)]
    [InlineData(60, 40)]
    public void BoundaryFallbackStopsBallAndRestartsForOtherTeam(double x, double y)
    {
        var match = MatchTests.MakeMatch(42);
        match.SetBall(new Vec3(x, y, 0), Vec3.Zero);
        match.Step();
        Assert.Equal(MatchStatus.Restart, match.Status);
        Assert.Equal(Side.Away, match.Possession);
        var ball = match.Ball;
        Assert.True(Math.Abs(ball.Pos.X) <= Pitch.HalfLength);
        Assert.True(Math.Abs(ball.Pos.Y) <= Pitch.HalfWidth);
        Assert.Equal(Vec3.Zero, ball.Vel);
        Assert.All(match.GetSnapshot().Players.Where(p => p.Team == Side.Home),
            p => Assert.True((p.Pos - ball.Pos.XY).Length >= 9.15 - 1e-8));
        for (int i = 0; i < 32; i++) match.Step();
        Assert.Equal(MatchStatus.Running, match.Status);
        Assert.Single(match.Events.Events.Where(e => e.Type == EventType.Out));
        Assert.Contains(match.Events.Events, e => e.Type == EventType.RestartTaken);
    }
}
