using System.Text.Json.Nodes;
using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class SquadTests
{
    internal static string SquadPath(string name) => Path.Combine(Path.GetDirectoryName(TacticLoaderTests.TacticsDir())!, "squads", name + ".json");
    private static Tactic Tactic() => TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
    private static Squad Load(string name) => SquadLoader.LoadFile(SquadPath(name));

    [Theory]
    [InlineData("amateur")]
    [InlineData("halfprof")]
    [InlineData("prof")]
    public void ExamplesAreCompleteNormalizedAndMatchSlots(string name)
    {
        var squad = Load(name);
        SquadLoader.Validate(squad, Tactic());
        Assert.Equal(11, squad.Starters.Count);
        Assert.Equal(7, squad.Bench.Count);
        Assert.Equal(18, squad.Starters.Concat(squad.Bench).Select(p => p.Id).Distinct().Count());
        Assert.NotNull(squad.Starters.Single(p => p.Id == "GK").Keeper);
        Assert.NotNull(squad.Bench.Single(p => p.Role == "keeper").Keeper);
        Assert.All(squad.Starters, p => Assert.InRange(p.Attributes.Vision, 0, 1));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("zero")]
    [InlineData("high")]
    [InlineData("fraction")]
    [InlineData("string")]
    [InlineData("duplicate")]
    [InlineData("short")]
    [InlineData("keeper")]
    [InlineData("position")]
    [InlineData("role")]
    [InlineData("name")]
    [InlineData("status")]
    public void InvalidSquadsGivePathErrors(string error)
    {
        var json = JsonNode.Parse(File.ReadAllText(SquadPath("amateur")))!;
        var player = json["starters"]![0]!;
        switch (error)
        {
            case "missing": player["attributes"]!.AsObject().Remove("passing"); break;
            case "unknown": player["attributes"]!["passng"] = 9; break;
            case "zero": player["attributes"]!["passing"] = 0; break;
            case "high": player["attributes"]!["passing"] = 21; break;
            case "fraction": player["attributes"]!["passing"] = 9.5; break;
            case "string": player["attributes"]!["passing"] = "9"; break;
            case "duplicate": json["bench"]![0]!["id"] = "GK"; break;
            case "short": json["bench"]!.AsArray().RemoveAt(0); break;
            case "keeper": player.AsObject().Remove("keeper"); break;
            case "position": player["preferredPosition"] = "??"; break;
            case "role": player["role"] = "??"; break;
            case "name": player["name"] = " "; break;
            case "status": json["status"] = "definitief"; break;
        }
        Assert.StartsWith("$", Assert.Throws<SquadLoadException>(() => SquadLoader.Parse(json.ToJsonString())).Message);
    }

    [Fact]
    public void AttributeScaleHasExactEndpoints()
    {
        var json = JsonNode.Parse(File.ReadAllText(SquadPath("prof")))!;
        json["starters"]![0]!["attributes"]!["passing"] = 1;
        json["starters"]![0]!["attributes"]!["firstTouch"] = 20;
        var player = SquadLoader.Parse(json.ToJsonString()).Starters[0];
        Assert.Equal(0, player.Attributes.Passing);
        Assert.Equal(1, player.Attributes.FirstTouch);
    }

    [Fact]
    public void WrongTacticSlotsAreNeverFilledByRosterOrder()
    {
        var other = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433"));
        Assert.Contains("tactiekslots", Assert.Throws<SquadLoadException>(() => Match.Create(new(), other, other, 1, homeSquad: Load("prof"))).Message);
    }

    [Theory]
    [InlineData(7UL)]
    [InlineData(42UL)]
    public void ExplicitNeutralAndDisabledAttributesKeepOldBits(ulong seed)
    {
        var tactic = Tactic();
        var config = new MatchConfig(HalfSeconds: 60);
        var baseline = Match.Create(config, tactic, tactic, seed);
        var neutral = Match.Create(config, tactic, tactic, seed, homeSquad: Squad.NeutralFor(tactic), awaySquad: Squad.NeutralFor(tactic));
        var disabled = Match.Create(config with { Attributes = AttributeConfig.Disabled }, tactic, tactic, seed, homeSquad: Load("amateur"), awaySquad: Load("prof"));
        for (int i = 0; i < 3000 && baseline.Status != MatchStatus.Finished; i++)
        {
            baseline.Step(); neutral.Step(); disabled.Step();
            Assert.Equal(baseline.GetSnapshot(), neutral.GetSnapshot());
            Assert.Equal(baseline.GetSnapshot(), disabled.GetSnapshot());
        }
        Assert.Equal(MatchStatus.Finished, baseline.Status);
        Assert.Equal(baseline.EventHash(), neutral.EventHash());
        Assert.Equal(baseline.EventHash(), disabled.EventHash());
    }

    [Fact]
    public void SelectionIsAttachedByIdentityAndBenchStaysOffPitch()
    {
        var tactic = Tactic(); var squad = Load("prof");
        squad = squad with { Starters = squad.Starters.Reverse().ToArray() };
        var match = Match.Create(new(), tactic, tactic, 1, homeSquad: squad);
        Assert.Equal(squad.Starters.Single(p => p.Id == "RB"), match.SquadPlayerOf(Side.Home, "RB"));
        Assert.Null(match.SquadPlayerOf(Side.Home, "B1"));
        Assert.Equal(22, match.GetSnapshot().Players.Count);
    }

    [Fact]
    public void SensingAndDirectedSpacingAreOptInAndUseKnownBall()
    {
        var tactic = Tactic();
        var config = new MatchConfig(Attributes: AttributeConfig.Disabled with { Vision = true, Positioning = true, Anticipation = true });
        var amateur = Match.Create(config, tactic, tactic, 3, (_, _) => new FormationBrain(), homeSquad: Load("amateur"));
        var prof = Match.Create(config, tactic, tactic, 3, (_, _) => new FormationBrain(), homeSquad: Load("prof"));
        for (int i = 0; i < 150; i++) { amateur.Step(); prof.Step(); }
        var a = amateur.PerceptionOf(Side.Home, "RC")!;
        var p = prof.PerceptionOf(Side.Home, "RC")!;
        Assert.True(a.MarkGapExtra > p.MarkGapExtra);
        Assert.NotEqual(a.FormationTarget, p.FormationTarget);
        Assert.True(a.PassOptionLimit < p.PassOptionLimit);
        Assert.True(a.ChoiceTemperature == 0);
    }

    [Fact]
    public void SquadMatchesAndReplayAreDeterministic()
    {
        string tacticJson = File.ReadAllText(TacticLoaderTests.TacticPath("laag_blok_442"));
        string homeJson = File.ReadAllText(SquadPath("prof")), awayJson = File.ReadAllText(SquadPath("amateur"));
        var tactic = Tactic(); var config = new MatchConfig(HalfSeconds: 20);
        var match = Match.Create(config, tactic, tactic, 29, homeSquad: Load("prof"), awaySquad: Load("amateur"));
        for (int i = 0; i < 500; i++) match.Step();
        var replay = ReplayFile.Capture(config, tacticJson, tacticJson, 29, (int)match.Tick, match.EventHash(), homeJson, awayJson);
        var rerun = replay.Run();
        Assert.Equal(match.SnapshotHash(), rerun.SnapshotHash());
        Assert.Equal(match.EventHash(), rerun.EventHash());
    }
}
