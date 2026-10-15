using System.Text.Json.Nodes;
using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class TacticLoaderTests
{
    internal static string TacticsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tactics"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("tactics dir not found"), "tactics");
    }

    internal static string TacticPath(string name) => Path.Combine(TacticsDir(), name + ".json");

    [Theory]
    [InlineData("laag_blok_442")]
    [InlineData("man_om_man_433")]
    [InlineData("testcase_a_433")]
    public void LoadsExampleFiles(string name)
    {
        var t = TacticLoader.LoadFile(TacticPath(name));
        Assert.Equal(11, t.Roster.Count);
        foreach (GamePhase ph in Enum.GetValues<GamePhase>())
            Assert.Equal(11, t.Phase(ph).Players.Count);
    }

    [Fact]
    public void ResolvesGelijkAan()
    {
        var t = TacticLoader.LoadFile(TacticPath("laag_blok_442"));
        Assert.Equal(t.Phase(GamePhase.Verdedigen), t.Phase(GamePhase.Balverlies));
        Assert.Equal(t.Phase(GamePhase.Opbouw), t.Phase(GamePhase.Aanval));
    }

    [Fact]
    public void LoadsVariantsAndBlockSwitches()
    {
        var t = TacticLoader.LoadFile(TacticPath("man_om_man_433"));
        Assert.True(t.DefendVariants.ContainsKey(BlockHeight.Hoog));
        Assert.True(t.DefendVariants.ContainsKey(BlockHeight.Laag));
        Assert.Equal(2, t.BlockSwitches.Count);
        Assert.Equal(ScoreState.Voor, t.BlockSwitches[0].When.Score);
        Assert.Equal(60, t.BlockSwitches[0].When.FromMinute);
        Assert.Equal(BlockHeight.Laag, t.BlockSwitches[0].To);
    }

    private static JsonObject Valid() =>
        JsonNode.Parse(File.ReadAllText(TacticPath("laag_blok_442")))!.AsObject();

    [Fact]
    public void MissingPhaseGivesClearError()
    {
        var j = Valid();
        j["fases"]!.AsObject().Remove("balwinst");
        var e = Assert.Throws<TacticLoadException>(() => TacticLoader.Parse(j.ToJsonString()));
        Assert.Contains("balwinst", e.Message);
    }

    [Fact]
    public void PositionOutsideRangeGivesClearError()
    {
        var j = Valid();
        j["fases"]!["opbouw"]!["spelers"]![3]!["pos"] = new JsonArray(1.5, 0.5);
        var e = Assert.Throws<TacticLoadException>(() => TacticLoader.Parse(j.ToJsonString()));
        Assert.Contains("outside 0..1", e.Message);
        Assert.Contains("opbouw", e.Message);
    }

    [Fact]
    public void WrongPlayerCountGivesClearError()
    {
        var j = Valid();
        j["spelers"]!.AsArray().RemoveAt(0);
        var e = Assert.Throws<TacticLoadException>(() => TacticLoader.Parse(j.ToJsonString()));
        Assert.Contains("11", e.Message);
    }

    [Fact]
    public void InvalidJsonAndMissingFileGiveClearErrors()
    {
        Assert.Throws<TacticLoadException>(() => TacticLoader.Parse("{ nope"));
        Assert.Throws<TacticLoadException>(() => TacticLoader.LoadFile(TacticPath("bestaat_niet")));
    }

    [Fact]
    public void BlockSwitchWithoutVariantsFails()
    {
        var j = Valid();
        j["blokwissels"] = new JsonArray(new JsonObject
        {
            ["wanneer"] = new JsonObject { ["stand"] = "voor" },
            ["naar"] = "laag"
        });
        Assert.Throws<TacticLoadException>(() => TacticLoader.Parse(j.ToJsonString()));
    }
    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "RC")]
    [InlineData(true, "unknown")]
    [InlineData(true, "")]
    public void MarkingRequiresAnInstructionAndKnownTarget(bool instruction, string? target)
    {
        var json = Valid();
        var player = json["fases"]!["verdedigen"]!["spelers"]![1]!;
        player["instructies"] = instruction ? new JsonArray("dek_man") : new JsonArray();
        if (target is null) player.AsObject().Remove("dek"); else player["dek"] = target;
        var error = Assert.Throws<TacticLoadException>(() => TacticLoader.Parse(json.ToJsonString()));
        Assert.Contains("dek", error.Message);
        Assert.Contains("verdedigen.spelers[1]", error.Message);
    }

}
