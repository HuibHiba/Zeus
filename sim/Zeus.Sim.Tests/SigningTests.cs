using Xunit;

namespace Zeus.Sim.Tests;

/// <summary>Career v1: one new player per season, offered at the end, replacing the weakest starter in his role.</summary>
public class SigningTests
{
    static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Zeus.sln"))) dir = Path.GetDirectoryName(dir);
        return dir!;
    }

    static string Tactics => Path.Combine(Root(), "tactics");
    static string Squads => Path.Combine(Root(), "squads");

    [Fact]
    public void ThreeCandidatesInDistinctOutfieldRolesSlightlyAboveTheTemplateAndSeeded()
    {
        for (int division = 0; division < Competition.Divisions.Count; division++)
        {
            var picks = Signings.Candidates(1, division, Squads);
            Assert.Equal(Signings.Offered, picks.Count);
            Assert.Equal(Signings.Offered, picks.Select(p => p.Role).Distinct().Count());
            Assert.All(picks, p => Assert.True(Signings.IsValid(p), p.Name));
            var template = SquadLoader.LoadFile(Path.Combine(Squads, Competition.Level(division) + ".json"));
            foreach (var p in picks)
            {
                double peers = template.Starters.Concat(template.Bench).Where(x => x.Role == p.Role).Average(x => Signings.Mean(x.Attributes));
                Assert.InRange(Signings.Mean(p.Attributes), peers, Math.Min(1, peers + Signings.Bonus + Signings.BonusSpread) + 1e-9);
            }
            Assert.Equal(picks, Signings.Candidates(1, division, Squads));
        }
        Assert.NotEqual(Signings.Candidates(1, 1, Squads).Select(p => p.Name), Signings.Candidates(2, 1, Squads).Select(p => p.Name));
    }

    [Fact]
    public void ASigningReplacesTheWeakestStarterInHisRoleAndTheSquadStaysValid()
    {
        var tactic = TacticLoader.LoadFile(Path.Combine(Tactics, "laag_blok_442.json"));
        var squad = SquadFactory.ForLevel("halfprof", tactic, Squads)!;
        var signing = Signings.Candidates(1, 1, Squads).First(p => squad.Starters.Any(s => s.Role == p.Role));
        var weakest = squad.Starters.Where(s => s.Role == signing.Role).OrderBy(s => Signings.Mean(s.Attributes)).First();
        var after = Signings.Apply(squad, new[] { signing });
        SquadLoader.Validate(after, tactic);
        Assert.Equal(squad.Starters.Select(s => s.Id), after.Starters.Select(s => s.Id));
        var slot = after.Starters.First(s => s.Id == weakest.Id);
        Assert.Equal(signing.Name, slot.Name);
        Assert.Equal(weakest.Role, slot.Role);
        Assert.Contains(after.Bench, b => b.Name == weakest.Name);
        Assert.Equal(7, after.Bench.Count);
        Assert.Equal(after.Starters, Signings.Apply(after, new[] { signing }).Starters);      // applying twice changes nothing

        var state = new SeasonState { Division = 1, Signings = new() { signing } };
        var eleven = Competition.PlayerSquad(state, tactic, Squads)!;
        Assert.Contains(eleven.Starters, s => s.Name == signing.Name);
        state.Absences = new() { new Absence(signing.Name, Absences.Injury, 2) };
        Assert.DoesNotContain(Competition.PlayerSquad(state, tactic, Squads)!.Starters, s => s.Name == signing.Name);      // absences apply after signings
    }

    [Fact]
    public void SigningsAndTheOfferRoundTripThroughTheSaveAndOldSavesStayUntouched()
    {
        var signing = Signings.Candidates(1, 0, Squads)[0];
        var state = new SeasonState { Signings = new() { signing }, LastSigningSeason = 1 };
        var back = SeasonState.Parse(state.ToJson());
        Assert.Equal(signing, back.Signings!.Single());
        Assert.Equal(1, back.LastSigningSeason);
        Assert.DoesNotContain("Signings", new SeasonState().ToJson());
        Assert.DoesNotContain("LastSigningSeason", new SeasonState().ToJson());
        Assert.Throws<FormatException>(() => SeasonState.Parse(state.ToJson().Replace("\"LastSigningSeason\": 1", "\"LastSigningSeason\": 5")));
        Assert.Throws<FormatException>(() => SeasonState.Parse(state.ToJson().Replace("\"Role\": \"" + signing.Role + "\"", "\"Role\": \"keeper\"")));
    }
}
