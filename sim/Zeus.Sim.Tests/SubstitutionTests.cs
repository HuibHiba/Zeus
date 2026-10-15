using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class SubstitutionTests
{
    internal static Tactic Tactic(string name = "laag_blok_442") => TacticLoader.LoadFile(TacticLoaderTests.TacticPath(name));
    private static Squad Squad(string name) => SquadLoader.LoadFile(SquadTests.SquadPath(name));

    internal static Match Create(ulong seed = 5, MatchConfig? config = null, string tactic = "laag_blok_442") =>
        Match.Create(config ?? new MatchConfig(HalfSeconds: 300), Tactic(tactic), Tactic(tactic), seed,
            homeSquad: Squad("prof"), awaySquad: Squad("amateur"));

    /// <summary>Steps until play is stopped (throw-in, goal kick, ...); fails the test if that never happens.</summary>
    internal static void RunToRest(Match match, int maxSteps = 20000)
    {
        for (int i = 0; i < maxSteps && !(match.Tick > 0 && match.IsRestMoment); i++) match.Step();
        Assert.True(match.IsRestMoment, "no rest moment reached");
    }

    /// <summary>Steps until play is running again after a rest moment.</summary>
    internal static void RunToPlay(Match match)
    {
        for (int i = 0; i < 2000 && match.IsRestMoment; i++) match.Step();
        Assert.False(match.IsRestMoment);
    }

    private static string State(Match m) => m.SnapshotHash() + m.EventHash() + m.Commands.Count + m.SubstitutionsUsed(Side.Home);

    private static void AssertRejected(Match match, Action action, string messagePart)
    {
        string before = State(match);
        var error = Assert.Throws<MatchCommandException>(action);
        Assert.Contains(messagePart, error.Message);
        Assert.Equal(before, State(match));
    }

    [Fact]
    public void SubstitutionAtARestMomentWorksAndRecordsEventAndCommand()
    {
        var match = Create();
        RunToRest(match);
        var bench = match.BenchOf(Side.Home).First(b => b.Role != "keeper");
        match.Substitute(Side.Home, "RB", bench.Id);
        Assert.Equal("RB", match.GetSnapshot().Players.Single(p => p.Team == Side.Home && p.Id == "RB").Id);
        Assert.Equal(bench.Id, match.SquadIdOf(Side.Home, "RB"));
        Assert.Equal(bench, match.SquadPlayerOf(Side.Home, "RB"));
        Assert.Equal(1, match.SubstitutionsUsed(Side.Home));
        Assert.Equal(4, match.SubstitutionsLeft(Side.Home));
        Assert.DoesNotContain(match.BenchOf(Side.Home), b => b.Id == bench.Id);
        var e = Assert.Single(match.Events.Events, e => e.Type == EventType.Substitution);
        Assert.Equal(("RB", Side.Home, "RB->" + bench.Id, match.Tick), (e.PlayerId, e.Team, e.Outcome, e.Tick));
        var command = Assert.Single(match.Commands);
        Assert.Equal((MatchCommand.SubstituteKind, match.Tick, "RB", bench.Id), (command.Kind, command.Tick, command.OutPlayerId, command.InPlayerId));
        match.Step(); // the match goes on
    }

    [Fact]
    public void SubstituteTakesTheSlotAndStartsFresh()
    {
        var match = Create();
        for (int i = 0; i < 3000; i++) match.Step();
        RunToRest(match);
        var tired = match.FatigueOf(Side.Home, "LCM")!.Value;
        Assert.True(tired.LongEnergy < 1);
        var before = match.GetSnapshot().Players.Single(p => p.Team == Side.Home && p.Id == "LCM");
        match.Substitute(Side.Home, "LCM", "B3");
        Assert.Equal(FatigueState.Fresh, match.FatigueOf(Side.Home, "LCM"));
        var after = match.GetSnapshot().Players.Single(p => p.Team == Side.Home && p.Id == "LCM");
        Assert.Equal((before.Pos, 1.0, 1.0), (after.Pos, after.ShortEnergy, after.LongEnergy));
        Assert.Equal(11, match.GetSnapshot().Players.Count(p => p.Team == Side.Home));
        Assert.Equal("B3", match.SquadIdOf(Side.Home, "LCM"));
        for (int i = 0; i < 50; i++) match.Step();
        Assert.True(match.FatigueOf(Side.Home, "LCM")!.Value.LongEnergy > .9);
    }

    [Fact]
    public void SubstituteCanBeSubstitutedOutByHisSquadIdButTheOriginalCannotReturn()
    {
        var match = Create();
        match.Substitute(Side.Home, "LS", "B2"); // tick 0 is a rest moment
        AssertRejected(match, () => match.Substitute(Side.Home, "LS", "B4"), "al gewisseld");
        AssertRejected(match, () => match.Substitute(Side.Home, "B2", "LS"), "kan niet terugkomen");
        match.Substitute(Side.Home, "B2", "B4");
        Assert.Equal("B4", match.SquadIdOf(Side.Home, "LS"));
        Assert.Equal(2, match.SubstitutionsUsed(Side.Home));
    }

    [Fact]
    public void SixthSubstitutionIsRejectedWithoutChangingState()
    {
        var match = Create();
        foreach (var (o, i) in new[] { ("RB", "B2"), ("LB", "B3"), ("RC", "B4"), ("LC", "B5"), ("RS", "B6") }) match.Substitute(Side.Home, o, i);
        Assert.Equal(0, match.SubstitutionsLeft(Side.Home));
        AssertRejected(match, () => match.Substitute(Side.Home, "LS", "B7"), "Maximaal aantal wissels");
        // The other team has its own allowance.
        match.Substitute(Side.Away, "RB", "B2");
    }

    [Fact]
    public void MaxSubstitutionsIsConfigurable()
    {
        var match = Create(config: new MatchConfig(HalfSeconds: 300, MaxSubstitutions: 1));
        match.Substitute(Side.Home, "RB", "B2");
        AssertRejected(match, () => match.Substitute(Side.Home, "LB", "B3"), "Maximaal aantal wissels (1)");
        Assert.Throws<ArgumentException>(() => Create(config: new MatchConfig(MaxSubstitutions: 8)));
    }

    [Fact]
    public void SentOffPlayerCannotBeSubstituted()
    {
        var match = Create();
        match.ReportIncident(Side.Home, "RC", Vec2.Zero, new RefereeIncident(ContactAction.Charge, Conduct.ExcessiveForce, true, true,
            TacticalContext: TacticalContext.NoTacticalOffence, AgainstOpponent: true));
        Assert.True(match.GetSnapshot().Players.Single(p => p.Team == Side.Home && p.Id == "RC").SentOff);
        Assert.True(match.IsRestMoment);
        AssertRejected(match, () => match.Substitute(Side.Home, "RC", "B2"), "uitgesloten");
    }

    [Fact]
    public void KeeperOnlyBySubstituteKeeper()
    {
        var match = Create();
        AssertRejected(match, () => match.Substitute(Side.Home, "GK", "B2"), "alleen door een keeper");
        AssertRejected(match, () => match.Substitute(Side.Home, "RB", "B1"), "kan geen veldspeler");
        var keeper = match.BenchOf(Side.Home).Single(b => b.Role == "keeper");
        match.Substitute(Side.Home, "GK", keeper.Id);
        Assert.Equal(keeper.Id, match.SquadIdOf(Side.Home, "GK"));
        Assert.True(match.GetSnapshot().Players.Single(p => p.Team == Side.Home && p.Id == "GK").IsKeeper);
        for (int i = 0; i < 100; i++) match.Step();
    }

    [Fact]
    public void InvalidIdsAreRejectedWithClearErrors()
    {
        var match = Create();
        AssertRejected(match, () => match.Substitute(Side.Home, "XX", "B2"), "Onbekende speler XX");
        AssertRejected(match, () => match.Substitute(Side.Home, "RB", "Z9"), "Onbekende reservespeler Z9");
        AssertRejected(match, () => match.Substitute(Side.Home, "RB", "LB"), "staat al op het veld");
        AssertRejected(match, () => match.Substitute(Side.Home, "B2", "B3"), "zit op de bank");
        AssertRejected(match, () => match.Substitute(Side.Home, "", ""), "Onbekende speler");
    }

    [Fact]
    public void CommandsWhilePlayIsRunningAreRejected()
    {
        var match = Create();
        RunToRest(match);
        RunToPlay(match);
        AssertRejected(match, () => match.Substitute(Side.Home, "RB", "B2"), "rustmoment");
        AssertRejected(match, () => match.Adjust(Side.Home, new TacticAdjustment(Tempo: .5)), "rustmoment");
    }

    [Fact]
    public void SubstitutionAfterTheFinalWhistleIsRejected()
    {
        var match = Create(config: new MatchConfig(HalfSeconds: 1, RestartPauseSeconds: .3));
        for (int i = 0; i < 400 && match.Status != MatchStatus.Finished; i++) match.Step();
        Assert.Equal(MatchStatus.Finished, match.Status);
        AssertRejected(match, () => match.Substitute(Side.Home, "RB", "B2"), "afgelopen");
    }

    [Fact]
    public void SubstitutionWithoutASuppliedSquadUsesTheNeutralReserves()
    {
        var tactic = Tactic();
        var match = Match.Create(new MatchConfig(), tactic, tactic, 3);
        match.Substitute(Side.Home, "LS", "B2");
        Assert.Equal("B2", match.SquadIdOf(Side.Home, "LS"));
        Assert.Equal(1, match.SubstitutionsUsed(Side.Home));
    }

    [Fact]
    public void MatchesWithoutCommandsRecordNothing()
    {
        var tactic = Tactic();
        var match = Match.Create(new MatchConfig(HalfSeconds: 60), tactic, tactic, 11);
        for (int i = 0; i < 400; i++) match.Step();
        Assert.Empty(match.Commands);
        Assert.DoesNotContain(match.Events.Events, e => e.Type is EventType.Substitution or EventType.TacticChange);
    }
}
