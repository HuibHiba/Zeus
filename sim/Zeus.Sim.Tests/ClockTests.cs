using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class ClockTests
{
    private static Match ShortMatch(double half = 1) => Match.Create(new MatchConfig(HalfSeconds: half, RestartPauseSeconds: 0.3),
        TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442")),
        TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433")), 42, (_, _) => new FormationBrain());

    [Fact]
    public void TwoPeriodsSwapEndsIncludeRestartTimeAndFinishOnce()
    {
        var match = ShortMatch();
        for (int i = 0; i < 200 && match.Status != MatchStatus.Finished; i++) match.Step();
        Assert.Equal(MatchStatus.Finished, match.Status);
        Assert.Equal(2, match.Period);
        Assert.True(match.EndsSwapped);
        Assert.Single(match.Events.Events.Where(e => e.Type == EventType.HalfTime));
        Assert.Single(match.Events.Events.Where(e => e.Type == EventType.FullTime));
        Assert.True(match.Tick > 20); // The restart pause is added, not deducted from the playing period.
        long end = match.Tick;
        match.Step();
        Assert.Equal(end, match.Tick);
        Assert.Contains(match.Events.Events, e => e.Type == EventType.RestartTaken && e.Team == Side.Away && e.Period == 2);
    }

    [Fact]
    public void GoalAtNegativeEndBelongsToHomeAfterHalfTime()
    {
        var match = ShortMatch(5);
        for (int i = 0; i < 54; i++) match.Step();
        Assert.True(match.EndsSwapped);
        Assert.Equal(MatchStatus.Running, match.Status);
        match.SetBall(new Vec3(-52.4, 0, 0), new Vec3(-10, 0, 0));
        match.Step();
        Assert.Equal(1, match.HomeScore);
        Assert.Equal(0, match.AwayScore);
        Assert.Equal(Side.Away, match.Possession);
        Assert.Contains(match.Events.Events, e => e.Type == EventType.Goal && e.Team == Side.Home && e.Period == 2);
    }

    [Fact]
    public void PenaltyPositionAndShotDirectionMirrorAfterHalfTime()
    {
        var match = ShortMatch(5);
        for (int i = 0; i < 54; i++) match.Step();
        match.ReportIncident(Side.Away, "RC", new Vec2(-40, 0),
            new RefereeIncident(ContactAction.Tackle, Conduct.Careless, true, true,
                InsideDefendingPenaltyArea: true, TacticalContext: TacticalContext.NoTacticalOffence, AgainstOpponent: true));
        Assert.Equal(-41.5, match.Ball.Pos.X);
        for (int i = 0; i < 4; i++) match.Step();
        Assert.True(match.Ball.Vel.X < 0);
    }
}
