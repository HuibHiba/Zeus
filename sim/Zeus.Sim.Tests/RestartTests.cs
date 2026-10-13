using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class RestartTests
{
    private static Match Create() => Match.Create(new MatchConfig(
        Passing: new PassConfig(HoldSeconds: 100),
        Duels: new DuelConfig(BaseFoul: 1, MaxFoul: 1, SpeedFoul: 0, ApproachFoul: 0,
            IndisciplineFoul: 0, FatigueFoul: 0)),
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

    [Fact]
    public void DirectRedRemovesPlayerFromMovementControlAndSight()
    {
        var match = Create();
        var offender = match.GetSnapshot().Players.First(p => p.Team == Side.Away);
        match.ReportIncident(offender.Team, offender.Id, Vec2.Zero,
            new RefereeIncident(ContactAction.Charge, Conduct.ExcessiveForce, true, true,
                TacticalContext: TacticalContext.NoTacticalOffence, AgainstOpponent: true));
        var dismissed = Assert.Single(match.GetSnapshot().Players.Where(p => p.SentOff));
        Assert.Contains(match.Events.Events, e => e.Type == EventType.Card && e.Outcome == "Red");
        for (int i = 0; i < 70; i++) match.Step();
        var after = match.GetSnapshot().Players.Single(p => p.Id == dismissed.Id && p.Team == dismissed.Team);
        Assert.Equal(dismissed.Pos, after.Pos);
        Assert.All(match.GetSnapshot().Players.Where(p => !p.SentOff), p =>
        {
            var view = match.PerceptionOf(p.Team, p.Id);
            if (view is not null) Assert.DoesNotContain(view.VisiblePlayers, v => v.SentOff);
        });
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
