using Xunit;

namespace Zeus.Sim.Tests;

public class SquadFactoryTests
{
    static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Zeus.sln"))) dir = Path.GetDirectoryName(dir);
        return dir!;
    }

    public static IEnumerable<object[]> Combinations() =>
        Directory.GetFiles(Path.Combine(Root(), "tactics"), "*.json").Select(Path.GetFileNameWithoutExtension)
            .SelectMany(t => new[] { "amateur", "halfprof", "prof" }.Select(l => new object[] { t!, l }));

    [Theory]
    [MemberData(nameof(Combinations))]
    public void EveryTacticGetsAValidSquadAtEveryLevel(string tacticName, string level)
    {
        var tactic = TacticLoader.LoadFile(Path.Combine(Root(), "tactics", tacticName + ".json"));
        var squad = SquadFactory.ForLevel(level, tactic, Path.Combine(Root(), "squads"))!;
        SquadLoader.Validate(squad, tactic);
        Assert.Equal(11, squad.Starters.Count);
        Assert.Equal(7, squad.Bench.Count);
        Assert.Equal(tactic.Roster.Select(r => r.Id), squad.Starters.Select(s => s.Id));
        Assert.Equal(11, squad.Starters.Select(s => s.Name).Distinct().Count());
        var keeper = squad.Starters.Single(s => s.Id == "GK");
        Assert.True(keeper.Keeper is not null && keeper.Role == "keeper");
        Assert.All(squad.Starters.Where(s => s.Id != "GK"), s => Assert.Null(s.Keeper));
    }

    [Fact]
    public void NeutralLevelMeansNoSquadAndUnknownLevelsAreRejected()
    {
        var tactic = TacticLoader.LoadFile(Path.Combine(Root(), "tactics", "laag_blok_442.json"));
        Assert.Null(SquadFactory.ForLevel("neutraal", tactic, Path.Combine(Root(), "squads")));
        Assert.Throws<ArgumentException>(() => SquadFactory.ForLevel("wereldklasse", tactic, Path.Combine(Root(), "squads")));
    }

    [Fact]
    public void FittedSquadsKeepTheLevelProfileAndPlayAMatchWithSubstitutions()
    {
        var squads = Path.Combine(Root(), "squads");
        var home = TacticLoader.LoadFile(Path.Combine(Root(), "tactics", "hoge_druk_433.json"));
        var away = TacticLoader.LoadFile(Path.Combine(Root(), "tactics", "counter_532.json"));
        var homeSquad = SquadFactory.ForLevel("prof", home, squads)!;
        var awaySquad = SquadFactory.ForLevel("amateur", away, squads)!;
        // the level profile survives the fitting: pro players pass better than amateurs on average
        Assert.True(homeSquad.Starters.Average(s => s.Attributes.Passing) > awaySquad.Starters.Average(s => s.Attributes.Passing));
        var match = Match.Create(new MatchConfig(), home, away, 11, homeSquad: homeSquad, awaySquad: awaySquad);
        for (int i = 0; i < 3000; i++) match.Step();
        Assert.Equal(homeSquad.Starters.Select(s => s.Id), home.Roster.Select(r => r.Id));
        Assert.Equal(5, match.SubstitutionsLeft(Side.Home));
        Assert.All(match.GetSnapshot().Players, p => Assert.InRange(Math.Abs(p.Pos.X), 0, 60));
    }
}
