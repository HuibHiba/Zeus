using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class MatchRulesTests
{
    private sealed class FixtureBrain : IPlayerBrain
    {
        public Intent Decide(Perception view)
        {
            if (view.Self.Team != Side.Home || view.Self.Id != "RS") return new FormationBrain().Decide(view);
            if (view.Tick >= 200 && view.HasBall)
            {
                var receiver = view.VisiblePlayers.Single(p => p.Team == Side.Home && p.Id == "LS");
                return new Intent(IntentKind.Pass, receiver.Pos, receiver.Id);
            }
            return new Intent(IntentKind.HoldPosition, view.Self.Pos);
        }
    }

    [Fact]
    public void OffsideIsEnforcedOnlyWhenCandidateActuallyPlaysPass()
    {
        var home = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        var away = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433"));
        var phases = away.Phases.ToDictionary(p => p.Key, p => p.Value);
        phases[GamePhase.Verdedigen] = phases[GamePhase.Verdedigen] with {
            Players = phases[GamePhase.Verdedigen].Players.Select(p => p.Id == "GK" ? p : p with { X = 0.8, FollowBall = null }).ToArray()
        };
        away = away with { Phases = phases, DefendVariants = new Dictionary<BlockHeight, PhaseLayout>(), BlockSwitches = Array.Empty<BlockSwitch>() };
        var match = Match.Create(new MatchConfig(Passing: new PassConfig(Skill: 1),
            Perception: new PerceptionConfig(FieldOfViewRadians: 2 * Math.PI, SightRange: 200, ScanIntervalSeconds: 0.1)),
            home, away, 42, (_, _) => new FixtureBrain());
        for (int i = 0; i < 200; i++) match.Step();
        Assert.DoesNotContain(match.Events.Events, e => e.Type == EventType.Offside); // Position alone is not an offence.
        Assert.Contains("LS", OffsideRules.PositionsAtTouch(match.GetSnapshot(), Side.Home, true));
        for (int i = 0; i < 80; i++) match.Step();
        Assert.Contains(match.Events.Events, e => e.Type == EventType.Offside && e.Team == Side.Home && e.PlayerId == "LS");
        Assert.Contains(match.Events.Events, e => e.Type == EventType.RestartAwarded && e.Outcome == "indirect_free_kick");
    }

    [Fact]
    public void GoalChangesScoreAndAwardsKickOffToConcedingTeam()
    {
        var match = MatchTests.MakeMatch(42);
        match.SetBall(new Vec3(52.4, 0, 0), new Vec3(10, 0, 0));
        match.Step();
        Assert.Equal(1, match.HomeScore);
        Assert.Equal(0, match.AwayScore);
        Assert.Equal(Side.Away, match.Possession);
        Assert.Equal(Vec3.Zero, match.Ball.Pos);
        Assert.Equal(MatchStatus.Restart, match.Status);
        Assert.Contains(match.Events.Events, e => e.Type == EventType.Goal);
        Assert.Contains(match.Events.Events, e => e.Type == EventType.RestartAwarded && e.Outcome == "kick_off");
        for (int i = 0; i < 32; i++) match.Step();
        Assert.Contains(match.Events.Events, e => e.Type == EventType.RestartTaken && e.Team == Side.Away);
    }

    [Fact]
    public void WholeBallIsRequiredAndHighBallCannotScore()
    {
        var match = MatchTests.MakeMatch(42);
        match.SetBall(new Vec3(52.55, 0, 0), Vec3.Zero);
        match.Step();
        Assert.Equal(0, match.HomeScore);
        Assert.Equal(MatchStatus.Running, match.Status);
        match.SetBall(new Vec3(52.4, 0, 4), new Vec3(10, 0, 0));
        match.Step();
        Assert.Equal(0, match.HomeScore);
        Assert.Contains(match.Events.Events, e => e.Type == EventType.RestartAwarded && e.Outcome == "GoalKick");
    }

    [Fact]
    public void TouchlineExitCreatesThrowAndOpponentsAreTwoMetresAway()
    {
        var match = MatchTests.MakeMatch(42);
        match.SetBall(new Vec3(0, 33.9, 0), new Vec3(0, 10, 0));
        match.Step();
        Assert.Contains(match.Events.Events, e => e.Type == EventType.RestartAwarded && e.Outcome == "ThrowIn");
        Assert.All(match.GetSnapshot().Players.Where(p => p.Team == Side.Home),
            p => Assert.True((p.Pos - match.Ball.Pos.XY).Length >= 2 - 1e-8));
        for (int i = 0; i < 30; i++) match.Step();
        Assert.InRange(match.Ball.Pos.Z, 1.7, 2.1); // The first running tick already advances the upward throw.
        Assert.Contains(match.Events.Events, e => e.Type == EventType.Pass && e.Outcome == "restart ThrowIn");
    }

    [Fact]
    public void EstablishedPenaltyUsesSpotAndShotInsteadOfFreeKick()
    {
        var match = MatchTests.MakeMatch(42);
        var decision = match.ReportIncident(Side.Away, "RC", new Vec2(40, 0),
            new RefereeIncident(ContactAction.Tackle, Conduct.Careless, true, true,
                InsideDefendingPenaltyArea: true, TacticalContext: TacticalContext.NoTacticalOffence, AgainstOpponent: true));
        Assert.Equal(RefereeRestart.Penalty, decision.Restart);
        Assert.Equal(new Vec3(41.5, 0, 0), match.Ball.Pos);
        Assert.Contains(match.Events.Events, e => e.Type == EventType.RestartAwarded && e.Outcome == "penalty");
        for (int i = 0; i < 31; i++) match.Step();
        Assert.Contains(match.Events.Events, e => e.Type == EventType.Shot && e.Outcome == "restart penalty");
    }

    [Theory]
    [InlineData(-1, "GoalKick")]
    [InlineData(1, "Corner")]
    public void DirectThrowCannotScoreAtEitherEnd(double direction, string expectedRestart)
    {
        var match = MatchTests.MakeMatch(42);
        match.SetBall(new Vec3(0, 33.9, 0), new Vec3(0, 10, 0));
        for (int i = 0; i < 32; i++) match.Step();
        match.SetBall(new Vec3(direction * 52.4, 0, 0), new Vec3(direction * 10, 0, 0));
        match.Step();
        Assert.Equal(0, match.HomeScore + match.AwayScore);
        Assert.Contains(match.Events.Events, e => e.Type == EventType.RestartAwarded && e.Outcome == expectedRestart);
    }

    [Fact]
    public void RestartTakerCannotPlayAgainBeforeAnotherPlayerTouches()
    {
        var match = MatchTests.MakeMatch(42);
        match.SetBall(new Vec3(0, 33.9, 0), new Vec3(0, 10, 0));
        for (int i = 0; i < 35; i++) match.Step();
        var taken = match.Events.Events.Last(e => e.Type == EventType.RestartTaken);
        var taker = match.GetSnapshot().Players.Single(p => p.Team == taken.Team && p.Id == taken.PlayerId);
        match.SetBall(new Vec3(taker.Pos.X, taker.Pos.Y, 0), Vec3.Zero);
        match.Step();
        Assert.Contains(match.Events.Events, e => e.Type == EventType.DoubleTouch && e.PlayerId == taken.PlayerId);
        Assert.Contains(match.Events.Events, e => e.Type == EventType.RestartAwarded && e.Outcome == "indirect_free_kick");
    }
}
