using System.Text.Json;
using Zeus.Sim;

namespace Zeus.Sim.Tests;

public class FamiliarityTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private static Tactic Load(string name) => TacticLoader.LoadFile(TacticLoaderTests.TacticPath(name));
    private static FamiliarityState State(Tactic tactic, double initial) => FamiliarityState.ForTeam("test",
        tactic.Roster.Select(p => p.Id).Concat(Enumerable.Range(1, 7).Select(i => "B" + i)), initial);

    [Theory]
    [InlineData("hoge_druk_433", "hoge_pressing", .6)]
    [InlineData("man_om_man_433", "man_om_man", .8)]
    [InlineData("counter_532", "counter", .6)]
    [InlineData("positiespel_4231", "positiespel", .6)]
    public void ExampleDemandRecognizesStyle(string name, string concept, double minimum)
    {
        var demand = TacticDemand.From(Load(name));
        Assert.InRange(demand[concept], minimum, 1);
        Assert.All(demand.Weights.Values, w => Assert.InRange(w, 0, 1));
        Assert.InRange(demand.Complexity, 0, 1);
        Assert.Equal(JsonSerializer.Serialize(demand), JsonSerializer.Serialize(TacticDemand.From(Load(name))));
    }

    [Fact]
    public void ComplexityCountsTriggersInstructionsMarkingAndPaths()
    {
        var basic = Load("laag_blok_442");
        var simple = basic with { Triggers = [], Phases = basic.Phases.ToDictionary(k => k.Key,
            k => k.Value with { Players = k.Value.Players.Select(p => p with { Instructions = [], Mark = null, Path = null }).ToArray() }),
            DefendVariants = new Dictionary<BlockHeight, PhaseLayout>() };
        Assert.Equal(0, TacticDemand.From(simple).Complexity);
        var complex = simple with { Triggers = [new("test", GamePhase.Verdedigen, TriggerRules.BadTouch, null, [])],
            Phases = simple.Phases.ToDictionary(k => k.Key, k => k.Value with { Players = k.Value.Players.Select(p => p with {
                Instructions = ["dek_man"], Mark = "ST", Path = [(.5, .5)] }).ToArray() }) };
        Assert.True(TacticDemand.From(complex).Complexity > .6);
    }

    [Fact]
    public void LearningDecayTrainingBudgetAndJsonRoundtrip()
    {
        var state = FamiliarityState.ForTeam("a", ["ST"], .4);
        var demand = new TacticDemand(TacticalConcepts.All.ToDictionary(c => c, c => c == "counter" ? 1.0 : 0), 0);
        FamiliarityState.AfterMatch(state, demand);
        Assert.Equal(.415, state.Players["ST"].Concepts["counter"], 12);
        Assert.Equal(.398, state.Players["ST"].Concepts["positiespel"], 12);
        double before = state.Players["ST"].Concepts["counter"];
        FamiliarityState.Train(state, "counter", 1);
        Assert.True(state.Players["ST"].Concepts["counter"] - before > .015);
        for (int i = 0; i < 100; i++) {
            FamiliarityState.Train(state, "counter", 1);
            FamiliarityState.Train(state, "positiespel", 1);
            FamiliarityState.AfterMatch(state, demand);
        }
        Assert.Equal(FamiliarityState.PeriodLimit, state.Players["ST"].Learned, 12);
        var restored = FamiliarityState.FromJson(state.ToJson());
        FamiliarityState.Train(restored, "counter", 1);
        Assert.Equal(state.ToJson(), restored.ToJson());
        restored.BeginPeriod(1);
        FamiliarityState.Train(restored, "counter", 1);
        Assert.True(restored.Players["ST"].Learned > 0);
        Assert.Throws<ArgumentException>(() => restored.BeginPeriod(1));
    }

    [Theory]
    [InlineData(-.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void InvalidLearningInputDoesNotMutate(double intensity)
    {
        var state = FamiliarityState.ForTeam("a", ["ST"]);
        string before = state.ToJson();
        Assert.Throws<ArgumentException>(() => FamiliarityState.Train(state, "counter", intensity));
        Assert.Equal(before, state.ToJson());
        Assert.Throws<ArgumentException>(() => FamiliarityState.Train(state, "unknown", 1));
    }

    [Fact]
    public void CapacityUsesCognitiveAttributesAndEffectiveFormula()
    {
        var a = PlayerAttributes.Neutral;
        var better = a with { Decisions = 1, Positioning = 1, Anticipation = 1, Vision = 1 };
        var demand = new TacticDemand(TacticalConcepts.All.ToDictionary(c => c, _ => 1.0), 0);
        Assert.Equal(.75, demand.Deficit(a, TacticalConcepts.All.ToDictionary(c => c, _ => 0.0)), 12);
        Assert.Equal(.5, demand.Deficit(a, TacticalConcepts.All.ToDictionary(c => c, _ => 1.0)), 12);
        Assert.Equal(0, demand.Deficit(better, TacticalConcepts.All.ToDictionary(c => c, _ => 1.0)), 12);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(42)]
    public void DisabledMissingAndEmptyStatesKeepEverySnapshot(int seed)
    {
        var t = Load("man_om_man_433");
        var baseline = Match.Create(new(), t, t, (ulong)seed);
        var disabled = Match.Create(new(), t, t, (ulong)seed, homeFamiliarity: State(t, 0));
        var missing = Match.Create(new(EnableFamiliarity: true), t, t, (ulong)seed);
        var empty = Match.Create(new(EnableFamiliarity: true), t, t, (ulong)seed,
            homeFamiliarity: FamiliarityState.ForTeam("empty", []));
        for (int i = 0; i < 1200; i++) {
            baseline.Step(); disabled.Step(); missing.Step(); empty.Step();
            Assert.Equal(baseline.GetSnapshot(), disabled.GetSnapshot());
            Assert.Equal(baseline.GetSnapshot(), missing.GetSnapshot());
            Assert.Equal(baseline.GetSnapshot(), empty.GetSnapshot());
        }
        Assert.Equal(baseline.EventHash(), disabled.EventHash());
        Assert.Equal(baseline.EventHash(), missing.EventHash());
        Assert.Equal(baseline.EventHash(), empty.EventHash());
    }

    [Fact]
    public void MissingStateAlsoPreservesSuppliedSquadBehaviour()
    {
        var t = Load("man_om_man_433");
        var squad = SquadFactory.Fit(SquadLoader.LoadFile(SquadTests.SquadPath("amateur")), t);
        var a = Match.Create(new(), t, t, 7, homeSquad: squad, awaySquad: squad);
        var b = Match.Create(new(EnableFamiliarity: true), t, t, 7, homeSquad: squad, awaySquad: squad);
        for (int i = 0; i < 1200; i++) { a.Step(); b.Step(); Assert.Equal(a.GetSnapshot(), b.GetSnapshot()); }
        Assert.Equal(a.EventHash(), b.EventHash());
    }

    private static Tactic Demanding()
    {
        var tactic = Load("man_om_man_433");
        var actions = tactic.Roster.Where(p => p.Role != "keeper").Select(p => new TriggerAction(p.Id, "start_pressing")).ToArray();
        return tactic with { Triggers = new[] { TriggerRules.BallToKeeper, TriggerRules.BadTouch,
            TriggerRules.BackPassToMidfielder, TriggerRules.OpponentOnFlank }.Select(c =>
                new Trigger(c, GamePhase.Verdedigen, c, null, actions)).ToArray() };
    }

    [Fact]
    public void LowFamiliarityMissesMoreAndReactsLaterAcrossSeeds()
    {
        var t = Demanding();
        var observations = new List<FamiliarityTriggerObservation>[2] { [], [] };
        for (ulong seed = 1; seed <= 4; seed++)
        for (int level = 0; level <= 1; level++) {
            var match = Match.Create(new(EnableFamiliarity: true), t, t, seed,
                homeFamiliarity: State(t, level), awayFamiliarity: State(t, level));
            for (int i = 0; i < 3000; i++) match.Step();
            observations[level].AddRange(match.FamiliarityTriggers);
        }
        Assert.All(observations, o => Assert.True(o.Count > 100, $"Only {o.Count} attempts"));
        double MissRate(int i) => observations[i].Count(o => o.Missed) / (double)observations[i].Count;
        double Delay(int i) => observations[i].Where(o => !o.Missed).Average(o => o.DelaySeconds);
        Assert.True(MissRate(0) > MissRate(1) + .05, $"Miss rate low={MissRate(0)}, high={MissRate(1)}");
        Assert.True(Delay(0) > Delay(1) + .1, $"Delay low={Delay(0)}, high={Delay(1)}");
        output.WriteLine($"Four seeds, both teams: low n={observations[0].Count}, missed={MissRate(0):P2}, delay={Delay(0):F3}s; high n={observations[1].Count}, missed={MissRate(1):P2}, delay={Delay(1):F3}s.");
    }

    private sealed class RecordingBrain(Action<Perception> record) : IPlayerBrain
    {
        private readonly PassingBrain _brain = new(new PassConfig(), instructions: new InstructionConfig());
        public Intent Decide(Perception view) { record(view); return _brain.Decide(view); }
    }

    [Fact]
    public void MissFallsBackToRoleAndSuccessfulTriggerReallyWaits()
    {
        var t = Demanding();
        var views = new List<(long Tick, Perception View)>();
        Match? match = null;
        // Scenario pinned to the movement and pass scoring before positional round 3 (docs/kalibratie.md).
        match = Match.Create(new(EnableFamiliarity: true, PositionJogSeconds: 0, DefendingBallFollowGain: 0.5, LateralFollowGain: null, DefendingLateralFollowGain: null,
            Passing: new PassConfig(ForwardWeight: 0.06, DistanceWeight: 0.015)), t, t, 42,
            (_, _) => new RecordingBrain(view => views.Add((match!.Tick, view))),
            homeFamiliarity: State(t, 0), awayFamiliarity: State(t, 0));
        for (int i = 0; i < 3000; i++) match.Step();
        var missed = match.FamiliarityTriggers.Where(o => o.Missed).ToArray();
        Assert.NotEmpty(missed);
        var fallbackViews = views.Where(v => v.View.Phase == GamePhase.Verdedigen && missed.Any(o =>
            o.Team == v.View.Self.Team && o.PlayerId == v.View.Self.Id && v.Tick > o.Tick && v.Tick < o.Tick + 40)).ToArray();
        Assert.NotEmpty(fallbackViews);
        Assert.All(fallbackViews, v => {
            Assert.Empty(v.View.Instructions!);
            Assert.Null(v.View.MarkId);
            Assert.False(v.View.CanRunInBehind);
            Assert.Equal("zone", v.View.PressingForm);
        });
        var successes = match.FamiliarityTriggers.Where(o => !o.Missed && o.Tick < match.Tick - 20).ToArray();
        Assert.NotEmpty(successes);
        Assert.All(successes, o => Assert.Contains(match.Events.Events, e => e.Type == EventType.TriggerFired
            && e.Team == o.Team && e.PlayerId == o.PlayerId && e.Tick == o.Tick + (long)Math.Round(o.DelaySeconds / .1)));
    }

    [Fact]
    public void SubstituteUsesReserveIdentityAndMissingStarterRemainsNeutral()
    {
        var t = Demanding();
        var state = FamiliarityState.ForTeam("reserves", ["B2"]);
        var // Scenario pinned to the movement and pass scoring before positional round 3 (docs/kalibratie.md).
        match = Match.Create(new(EnableFamiliarity: true, PositionJogSeconds: 0, DefendingBallFollowGain: 0.5, LateralFollowGain: null, DefendingLateralFollowGain: null,
            Passing: new PassConfig(ForwardWeight: 0.06, DistanceWeight: 0.015)), t, t, 42, homeFamiliarity: state);
        match.Substitute(Side.Home, "ST", "B2");
        for (int i = 0; i < 3000; i++) match.Step();
        Assert.NotEmpty(match.FamiliarityTriggers);
        Assert.All(match.FamiliarityTriggers, o => { Assert.Equal(Side.Home, o.Team); Assert.Equal("B2", o.PlayerId); });
    }

    [Fact]
    public void StateIsSnapshottedAndSameSeedAndStateGiveSameEvents()
    {
        var t = Demanding();
        var state = State(t, .2);
        string json = state.ToJson();
        var a = Match.Create(new(EnableFamiliarity: true), t, t, 42, homeFamiliarity: state);
        var b = Match.Create(new(EnableFamiliarity: true), t, t, 42, homeFamiliarity: FamiliarityState.FromJson(json));
        FamiliarityState.Train(state, "man_om_man", 1);
        for (int i = 0; i < 3000; i++) { a.Step(); b.Step(); }
        Assert.NotEmpty(a.FamiliarityTriggers);
        Assert.Equal(a.EventHash(), b.EventHash());
        Assert.Equal(a.SnapshotHash(), b.SnapshotHash());
        Assert.Equal(a.FamiliarityTriggers, b.FamiliarityTriggers);
    }

    [Fact]
    public void ReplayIncludesIndependentFamiliarityInput()
    {
        string json = File.ReadAllText(TacticLoaderTests.TacticPath("man_om_man_433"));
        var t = TacticLoader.Parse(json);
        var state = State(t, 0);
        var config = new MatchConfig(EnableFamiliarity: true);
        var match = Match.Create(config, t, t, 42, homeFamiliarity: state);
        match.Substitute(Side.Home, "ST", "B2");
        for (int i = 0; i < 1500; i++) match.Step();
        var replay = ReplayFile.Capture(config, json, json, 42, 1500, match.EventHash(), commands: match.Commands, homeFamiliarity: state);
        FamiliarityState.Train(state, "man_om_man", 1);
        replay = JsonSerializer.Deserialize<ReplayFile>(JsonSerializer.Serialize(replay))!;
        Assert.Equal(3, replay.FormatVersion);
        Assert.Equal(match.SnapshotHash(), replay.Run().SnapshotHash());
    }
}
