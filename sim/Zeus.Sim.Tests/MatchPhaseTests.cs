using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class MatchPhaseTests
{
    private sealed class StationaryBrain : IPlayerBrain
    {
        public Intent Decide(Perception p) => new(IntentKind.HoldPosition, p.Self.Pos);
    }

    private static Tactic DistinctLayouts()
    {
        var source = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        var original = source.Phase(GamePhase.Opbouw);
        var phases = Enum.GetValues<GamePhase>().ToDictionary(phase => phase,
            phase => new PhaseLayout(null, original.Players.Select(p => p with {
                X = .10 + .12 * (int)phase, FollowBall = null
            }).ToArray()));
        return source with { Phases = phases, BlockSwitches = Array.Empty<BlockSwitch>(),
            DefendVariants = new Dictionary<BlockHeight, PhaseLayout>() };
    }

    private static Match Make(MatchConfig? config = null)
    {
        var settings = config ?? new MatchConfig(MaxReactionSeconds: 0,
            Perception: new PerceptionConfig(ScanIntervalSeconds: .1));
        return Match.Create(settings, DistinctLayouts(), DistinctLayouts(), 4,
            (_, _) => new StationaryBrain());
    }

    private static void GiveToAwayKeeper(Match match)
    {
        var keeper = match.GetSnapshot().Players.Single(p => p.Team == Side.Away && p.Id == "GK");
        match.SetBall(new Vec3(keeper.Pos.X, keeper.Pos.Y, 0), Vec3.Zero);
        match.Step();
        Assert.Equal(Side.Away, match.Possession);
    }

    private static void CheckObservedLayout(Match match, Side team, GamePhase phase)
    {
        var tactic = DistinctLayouts();
        var layout = tactic.Phase(phase).Players.Single(p => p.Id == "GK");
        var expected = Pitch.ToMeters(layout.X, layout.Y, team);
        if (match.EndsSwapped) expected = expected * -1;
        var view = match.PerceptionOf(team, "GK");
        Assert.NotNull(view);
        Assert.Equal(phase, view.Phase);
        Assert.Equal(expected.X, view.FormationTarget.X, 9);
        Assert.Equal(expected.Y, view.FormationTarget.Y, 9);
    }

    [Fact]
    public void BallInAttackThirdSelectsAttackAfterStepAndChangesObservedFormation()
    {
        var match = Make();
        match.Step();
        CheckObservedLayout(match, Side.Home, GamePhase.Opbouw);
        CheckObservedLayout(match, Side.Away, GamePhase.Verdedigen);
        match.SetBall(new Vec3(30, 0, 10), Vec3.Zero);
        match.Step();
        Assert.Equal(GamePhase.Aanval, match.PhaseOf(Side.Home));
        Assert.Equal(GamePhase.Verdedigen, match.PhaseOf(Side.Away));
        match.Step();
        CheckObservedLayout(match, Side.Home, GamePhase.Aanval);
    }

    [Fact]
    public void OpponentControlStartsTransitionsAndTheirOwnLayoutsThenExpires()
    {
        var match = Make();
        GiveToAwayKeeper(match);
        Assert.Equal(GamePhase.Balverlies, match.PhaseOf(Side.Home));
        Assert.Equal(GamePhase.Balwinst, match.PhaseOf(Side.Away));
        match.Step();
        CheckObservedLayout(match, Side.Home, GamePhase.Balverlies);
        CheckObservedLayout(match, Side.Away, GamePhase.Balwinst);
        for (int i = 0; i < 32; i++) match.Step();
        Assert.Equal(GamePhase.Verdedigen, match.PhaseOf(Side.Home));
        Assert.Equal(GamePhase.Opbouw, match.PhaseOf(Side.Away));
        CheckObservedLayout(match, Side.Home, GamePhase.Verdedigen);
        CheckObservedLayout(match, Side.Away, GamePhase.Opbouw);
    }

    [Fact]
    public void RestartStopsBothPhasesAndResumptionDoesNotInventPossessionTransition()
    {
        var match = Make();
        match.SetBall(new Vec3(0, Pitch.HalfWidth + 2, 0), Vec3.Zero);
        match.Step();
        Assert.Equal(MatchStatus.Restart, match.Status);
        Assert.Equal(GamePhase.Stilstaand, match.PhaseOf(Side.Home));
        Assert.Equal(GamePhase.Stilstaand, match.PhaseOf(Side.Away));
        var stops = match.Events.Events.Where(e => e.Type == EventType.PhaseChange).ToArray();
        Assert.Equal(new[] { Side.Home, Side.Away }, stops.TakeLast(2).Select(e => e.Team));
        for (int i = 0; i < 40 && match.Status == MatchStatus.Restart; i++) match.Step();
        Assert.Equal(MatchStatus.Running, match.Status);
        Assert.DoesNotContain(match.PhaseOf(Side.Home), new[] { GamePhase.Balwinst, GamePhase.Balverlies });
        Assert.DoesNotContain(match.PhaseOf(Side.Away), new[] { GamePhase.Balwinst, GamePhase.Balverlies });
    }

    [Fact]
    public void PhaseEventsOnlyRecordChangesAndUseHomeFirstOrder()
    {
        var match = Make();
        match.Step();
        Assert.Empty(match.Events.Events.Where(e => e.Type == EventType.PhaseChange));
        GiveToAwayKeeper(match);
        var changes = match.Events.Events.Where(e => e.Type == EventType.PhaseChange).ToArray();
        Assert.Equal(new[] { Side.Home, Side.Away }, changes.Select(e => e.Team));
        for (int i = 0; i < 5; i++) match.Step();
        Assert.Equal(2, match.Events.Events.Count(e => e.Type == EventType.PhaseChange));
    }

    [Fact]
    public void SecondHalfMirrorsAttackThirdAndObservedFormation()
    {
        var match = Make(new MatchConfig(MaxReactionSeconds: 0, HalfSeconds: 2,
            RestartPauseSeconds: 0, TransitionSeconds: 0,
            Perception: new PerceptionConfig(ScanIntervalSeconds: .1)));
        for (int i = 0; i < 30 && match.Period == 1; i++) match.Step();
        Assert.Equal(2, match.Period);
        Assert.True(match.EndsSwapped);
        match.Step();
        Assert.Equal(Side.Away, match.Possession);
        // Away attacks +x after the change of ends; it must now be Aanval at +30 m.
        match.SetBall(new Vec3(30, 0, 10), Vec3.Zero);
        match.Step();
        Assert.Equal(GamePhase.Aanval, match.PhaseOf(Side.Away));
        match.Step();
        CheckObservedLayout(match, Side.Away, GamePhase.Aanval);
        CheckObservedLayout(match, Side.Home, GamePhase.Verdedigen);
    }

    [Fact]
    public void LegacyOptOutKeepsTwoPossessionPhasesAndProducesNoPhaseEvents()
    {
        var match = Make(new MatchConfig(MaxReactionSeconds: 0, EnableTeamPhases: false));
        match.SetBall(new Vec3(30, 0, 10), Vec3.Zero);
        match.Step();
        Assert.Equal(GamePhase.Opbouw, match.PhaseOf(Side.Home));
        GiveToAwayKeeper(match);
        Assert.Equal(GamePhase.Opbouw, match.PhaseOf(Side.Away));
        Assert.Equal(GamePhase.Verdedigen, match.PhaseOf(Side.Home));
        Assert.Empty(match.Events.Events.Where(e => e.Type == EventType.PhaseChange));
    }
}
