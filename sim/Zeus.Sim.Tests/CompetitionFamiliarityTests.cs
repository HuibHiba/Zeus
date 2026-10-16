using System.Text.Json.Nodes;
using Zeus.Sim;

namespace Zeus.Sim.Tests;

public sealed class CompetitionFamiliarityTests
{
    static string Tactics => Path.GetDirectoryName(TacticLoaderTests.TacticPath("laag_blok_442"))!;
    static string Squads => Path.Combine(Path.GetDirectoryName(Tactics)!, "squads");
    static Tactic Tactic => TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433"));
    static readonly MatchConfig Short = new(HalfSeconds: 60, EnableFamiliarity: true);

    static SeasonState Career(double initial = 0)
    {
        var s = new SeasonState { PlayerTactic = "man_om_man_433.json" };
        var squad = SquadFactory.ForLevel("amateur", Tactic, Squads)!;
        Competition.EnsureFamiliarity(s, squad);
        foreach (var p in s.Familiarity!.Players.Values)
            foreach (var c in TacticalConcepts.All) p.Concepts[c] = initial;
        return s;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RoundsAreDeterministicWithAndWithoutFamiliarity(bool enabled)
    {
        var a = enabled ? Career() : new SeasonState();
        var b = SeasonState.Parse(a.ToJson());
        Competition.PlayRound(a, Tactics, Squads, config: Short with { EnableFamiliarity = enabled });
        Competition.PlayRound(b, Tactics, Squads, config: Short with { EnableFamiliarity = enabled });
        Assert.Equal(a.ToJson(), b.ToJson());
    }

    [Fact]
    public void DisabledStatePreservesCompetitionResultsAndDoesNotLearn()
    {
        var a = Career();
        var b = new SeasonState { PlayerTactic = a.PlayerTactic };
        var before = a.Familiarity!.Players["ST"].Concepts.ToDictionary(k => k.Key, k => k.Value);
        Competition.PlayRound(a, Tactics, Squads, config: Short with { EnableFamiliarity = false });
        Competition.PlayRound(b, Tactics, Squads, config: Short with { EnableFamiliarity = false });
        Assert.Equal(a.Results, b.Results);
        Assert.Equal(before, a.Familiarity.Players["ST"].Concepts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RoundAppliesAfterMatchOnceAndStartsANewLearningPeriod(bool live)
    {
        var state = Career(.2);
        Competition.Train(state, "man_om_man", 1);
        var expected = FamiliarityState.FromJson(state.Familiarity!.ToJson());
        var demand = TacticDemand.From(Tactic);
        // Live processing uses the saved demand, even if the selected tactic changes afterwards.
        if (live)
        {
            state.Pending = new PendingResult(0, 3, 2);
            state.PendingDemand = demand;
            state.PlayerTactic = "counter_532.json";
            state = SeasonState.Parse(state.ToJson());
        }
        FamiliarityState.AfterMatch(expected, demand);
        expected.BeginPeriod(1);
        Competition.PlayRound(state, Tactics, Squads, config: Short);
        Assert.Equal(expected.ToJson(), state.Familiarity!.ToJson());
        Assert.False(state.TrainedThisRound);
        Assert.Null(state.PendingDemand);
        Assert.Null(state.Pending);
        Assert.Equal(1, state.Round);
    }

    [Fact]
    public void TrainingAndBudgetSurviveReloadAndOldCareersRemainReadable()
    {
        var old = SeasonState.Parse("""{"Version":1}""");
        Assert.Equal(2, old.Version);
        Assert.Null(old.Familiarity);
        var state = Career();
        Competition.Train(state, "counter", 1);
        var back = SeasonState.Parse(state.ToJson());
        Assert.Equal(state.ToJson(), back.ToJson());
        Assert.Throws<InvalidOperationException>(() => Competition.Train(back, "counter", 1));
        Assert.True(back.Familiarity!.Players["ST"].Learned > 0);
        Assert.Throws<ArgumentException>(() => Competition.Train(Career(), "counter", double.NaN));
        var corrupt = JsonNode.Parse(state.ToJson())!;
        corrupt["Familiarity"]!["Period"] = 99;
        Assert.Throws<FormatException>(() => SeasonState.Parse(corrupt.ToJsonString()));
        corrupt = JsonNode.Parse(state.ToJson())!;
        corrupt["Familiarity"]!["Players"]!["ST"]!["Learned"] = -1;
        Assert.Throws<FormatException>(() => SeasonState.Parse(corrupt.ToJsonString()));
    }

    [Fact]
    public void LearningContinuesAcrossSeasonsWithoutResettingFamiliarity()
    {
        var state = Career(.3);
        Competition.PlayRest(state, Tactics, Squads, config: Short);
        string before = state.Familiarity!.ToJson();
        Competition.StartNextSeason(state);
        Assert.Equal(before, state.Familiarity.ToJson());
        Assert.Equal(Competition.Rounds, state.Familiarity.Period);
        Assert.Equal(state.ToJson(), SeasonState.Parse(state.ToJson()).ToJson());
    }

    [Fact]
    public void LowFamiliarityScoresWorseOnTacticalExecutionAndOnlyPlayerSideIsAffected()
    {
        var tactic = Tactic;
        var actions = tactic.Roster.Where(p => p.Role != "keeper").Select(p => new TriggerAction(p.Id, "start_pressing")).ToArray();
        tactic = tactic with { Triggers = new[] { TriggerRules.BallToKeeper, TriggerRules.BadTouch,
            TriggerRules.BackPassToMidfielder, TriggerRules.OpponentOnFlank }.Select(c =>
                new Trigger(c, GamePhase.Verdedigen, c, null, actions)).ToArray() };
        var observations = new List<FamiliarityTriggerObservation>[] { [], [] };
        for (ulong seed = 1; seed <= 4; seed++)
        for (int initial = 0; initial <= 1; initial++)
        {
            var state = FamiliarityState.ForTeam(Competition.PlayerClubName, tactic.Roster.Select(p => p.Id), initial);
            bool home = seed % 2 == 0;
            var match = Match.Create(new(EnableFamiliarity: true), tactic, tactic, seed,
                homeFamiliarity: home ? state : null, awayFamiliarity: home ? null : state);
            for (int tick = 0; tick < 3000; tick++) match.Step();
            Assert.All(match.FamiliarityTriggers, o => Assert.Equal(home ? Side.Home : Side.Away, o.Team));
            observations[initial].AddRange(match.FamiliarityTriggers);
        }
        Assert.All(observations, o => Assert.True(o.Count > 50));
        double MissRate(int i) => observations[i].Count(o => o.Missed) / (double)observations[i].Count;
        double Delay(int i) => observations[i].Where(o => !o.Missed).Average(o => o.DelaySeconds);
        Assert.True(MissRate(0) > MissRate(1) + .03, $"low={MissRate(0)}, high={MissRate(1)}");
        Assert.True(Delay(0) > Delay(1) + .1);
        Assert.All(Enumerable.Range(0, 3), d => Assert.Equal(.7, Competition.AiFamiliarity(d)));
    }
}
