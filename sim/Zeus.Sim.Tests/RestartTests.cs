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
        Assert.Equal(before.Players, match.GetSnapshot().Players);
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
                TacticalContext: TacticalContext.NoTacticalOffence));
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
            TacticalContext: TacticalContext.NoTacticalOffence);
        Assert.Equal(CardDecision.Yellow, match.ReportIncident(Side.Away, "RC", Vec2.Zero, incident).Card);
        Assert.Equal(CardDecision.SecondYellowRed, match.ReportIncident(Side.Away, "RC", Vec2.Zero, incident).Card);
        Assert.True(match.GetSnapshot().Players.Single(p => p.Team == Side.Away && p.Id == "RC").SentOff);
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
