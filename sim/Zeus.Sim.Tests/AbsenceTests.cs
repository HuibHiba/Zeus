using Xunit;

namespace Zeus.Sim.Tests;

/// <summary>Career v1: suspensions and injuries of the player's club, replaced from the bench.</summary>
public class AbsenceTests
{
    static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Zeus.sln"))) dir = Path.GetDirectoryName(dir);
        return dir!;
    }

    static string Tactics => Path.Combine(Root(), "tactics");
    static string Squads => Path.Combine(Root(), "squads");

    static Squad Fitted() => SquadFactory.ForLevel("halfprof", TacticLoader.LoadFile(Path.Combine(Tactics, "laag_blok_442.json")), Squads)!;

    [Fact]
    public void AbsentStartersAreReplacedFromTheBenchByRoleAndTheSquadStaysComplete()
    {
        var squad = Fitted();
        var keeper = squad.Starters.First(s => s.Keeper is not null);
        var back = squad.Starters.First(s => s.Role == "back");
        var swapped = Absences.WithoutAbsent(squad, new[] { keeper.Name, back.Name });
        Assert.Equal(11, swapped.Starters.Count);
        Assert.Equal(7, swapped.Bench.Count);
        Assert.Equal(squad.Starters.Select(s => s.Id), swapped.Starters.Select(s => s.Id));       // slots unchanged
        Assert.DoesNotContain(swapped.Starters, s => s.Name == keeper.Name || s.Name == back.Name);
        Assert.Contains(swapped.Bench, b => b.Name == keeper.Name);
        Assert.NotNull(swapped.Starters.First(s => s.Id == keeper.Id).Keeper);
        Assert.Equal("back", swapped.Starters.First(s => s.Id == back.Id).Role);
        SquadLoader.Validate(swapped);
        Assert.Same(squad, Absences.WithoutAbsent(squad, Array.Empty<string>()));
        Assert.Equal(18, swapped.Starters.Concat(swapped.Bench).Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void AfterRoundCountsDownAddsRedCardsAndRollsInjuriesDeterministically()
    {
        var current = new[] { new Absence("A", Absences.Injury, 2), new Absence("B", Absences.Red, 1) };
        var starters = new[] { "C", "D", "E" };
        var next = Absences.AfterRound(current, new[] { "C", "C" }, starters, new Rng(5));
        Assert.Contains(next, a => a.Player == "A" && a.RoundsLeft == 1);
        Assert.DoesNotContain(next, a => a.Player == "B");
        Assert.Single(next, a => a.Player == "C" && a.Reason == Absences.Red && a.RoundsLeft == Absences.RedRounds);
        Assert.Equal(next, Absences.AfterRound(current, new[] { "C", "C" }, starters, new Rng(5)));
        var cup = Absences.Suspend(current, new[] { "A", "D" });      // cup red cards: suspension only, nothing counts down
        Assert.Equal(3, cup.Count);
        Assert.Contains(cup, a => a.Player == "A" && a.Reason == Absences.Red && a.RoundsLeft == 1);
        Assert.Contains(cup, a => a.Player == "B" && a.RoundsLeft == 1);
        Assert.Contains(cup, a => a.Player == "D" && a.Reason == Absences.Red);
        Assert.All(next, a => Assert.True(Absences.IsValid(a)));
        // Over many seeds, injuries happen at about the configured rate and last 1..3 rounds.
        int injuries = 0;
        var eleven = Enumerable.Range(1, 11).Select(i => "P" + i).ToArray();
        for (ulong seed = 1; seed <= 400; seed++)
            foreach (var a in Absences.AfterRound(Array.Empty<Absence>(), Array.Empty<string>(), eleven, new Rng(seed)))
            {
                Assert.Equal(Absences.Injury, a.Reason);
                Assert.InRange(a.RoundsLeft, 1, Absences.MaxInjuryRounds);
                injuries++;
            }
        Assert.InRange(injuries / 4400.0, 0.015, 0.05);
    }

    [Fact]
    public void SaveWithAbsencesRoundTripsAndOldSavesStillParse()
    {
        var state = new SeasonState { Absences = new() { new Absence("Iemand", Absences.Red, 1) } };
        var back = SeasonState.Parse(state.ToJson());
        Assert.Equal(state.Absences, back.Absences);
        Assert.Null(SeasonState.Parse(new SeasonState().ToJson()).Absences);
        Assert.DoesNotContain("Absences", new SeasonState().ToJson());       // untouched saves stay byte-identical
        Assert.Throws<FormatException>(() => SeasonState.Parse(state.ToJson().Replace("\"rood\"", "\"vakantie\"")));
        Assert.Throws<FormatException>(() => SeasonState.Parse(state.ToJson().Replace("\"RoundsLeft\": 1", "\"RoundsLeft\": 0").Replace("\"RoundsLeft\":1", "\"RoundsLeft\":0")));
        var pending = new SeasonState { Pending = new PendingResult(0, 1, 0, new() { "Iemand" }) };
        Assert.Equal(new[] { "Iemand" }, SeasonState.Parse(pending.ToJson()).Pending!.RedCards);
    }

    [Fact]
    public void APlayedRoundUsesTheReducedSquadAndKeepsTheListValid()
    {
        var injured = Fitted().Starters[1].Name;
        var state = new SeasonState { Absences = new() { new Absence(injured, Absences.Injury, 1) } };
        var tactic = TacticLoader.LoadFile(Path.Combine(Tactics, state.PlayerTactic));
        var squad = Competition.PlayerSquad(state, tactic, Squads)!;
        Assert.DoesNotContain(squad.Starters, s => s.Name == injured);
        Competition.PlayRound(state, Tactics, Squads, config: new MatchConfig(HalfSeconds: 120));
        Assert.Equal(1, state.Round);
        Assert.NotNull(state.Absences);
        Assert.DoesNotContain(state.Absences!, a => a.Player == injured && a.Reason == Absences.Injury);      // the one-round injury is over
        Assert.All(state.Absences!, a => Assert.True(Absences.IsValid(a)));
        // A live result with a named red card (stored as pending) suspends that player for the next round.
        var sentOff = Fitted().Starters[2].Name;
        state.Pending = new PendingResult(state.Round, 1, 0, new() { sentOff });
        Competition.PlayRound(state, Tactics, Squads, config: new MatchConfig(HalfSeconds: 120));
        Assert.Contains(state.Absences!, a => a.Player == sentOff && a.Reason == Absences.Red && a.RoundsLeft == 1);
        Assert.Null(state.Pending);
    }
}
