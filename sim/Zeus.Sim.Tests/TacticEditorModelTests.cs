using Xunit;
using System.Text.Json.Nodes;

namespace Zeus.Sim.Tests;

public class TacticEditorModelTests
{
    private static string TacticPath()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Zeus.sln"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir!, "tactics", "laag_blok_442.json");
    }

    [Fact]
    public void LoadsElevenPlayersAndPhases()
    {
        var m = TacticEditorModel.LoadFile(TacticPath());
        Assert.Equal(11, m.PlayerIds.Count);
        Assert.Contains("opbouw", m.Phases);
    }

    [Fact]
    public void SetPositionClampsAndRoundTripsThroughLoader()
    {
        var m = TacticEditorModel.LoadFile(TacticPath());
        m.SetPosition("opbouw", "RS", 1.4, -0.2);
        Assert.Equal((1.0, 0.0), m.GetPosition("opbouw", "RS"));
        var again = TacticEditorModel.Parse(m.ToJson());
        Assert.Equal((1.0, 0.0), again.GetPosition("opbouw", "RS"));
    }

    [Fact]
    public void EditingAnAliasPhaseDetachesItWithoutChangingTheSource()
    {
        var m = TacticEditorModel.LoadFile(TacticPath());
        Assert.True(m.IsAlias("aanval"));
        var before = m.GetPosition("opbouw", "RS");
        m.SetPosition("aanval", "RS", 0.9, 0.5);
        Assert.False(m.IsAlias("aanval"));
        Assert.Equal((0.9, 0.5), m.GetPosition("aanval", "RS"));
        Assert.Equal(before, m.GetPosition("opbouw", "RS"));
    }

    [Fact]
    public void EditingASourcePhaseDoesNotChangeAliasPhases()
    {
        var m = TacticEditorModel.LoadFile(TacticPath());
        var before = m.GetPosition("aanval", "RS");
        m.SetPosition("opbouw", "RS", 0.11, 0.22);
        Assert.False(m.IsAlias("aanval"));
        Assert.Equal(before, m.GetPosition("aanval", "RS"));
        Assert.Equal((0.11, 0.22), m.GetPosition("opbouw", "RS"));
    }

    [Fact]
    public void SaveWritesAFileTheRealLoaderAccepts()
    {
        var m = TacticEditorModel.LoadFile(TacticPath());
        m.Name = "Editor test";
        m.SetPosition("opbouw", "LS", 0.7, 0.3);
        var path = Path.Combine(Path.GetTempPath(), $"zeus_editor_{Guid.NewGuid():N}.json");
        try
        {
            m.Save(path);
            TacticLoader.LoadFile(path);
            Assert.Equal("Editor test", TacticEditorModel.LoadFile(path).Name);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void UnknownPlayerOrPhaseThrows()
    {
        var m = TacticEditorModel.LoadFile(TacticPath());
        Assert.Throws<ArgumentException>(() => m.SetPosition("opbouw", "XX", 0.5, 0.5));
        Assert.Throws<ArgumentException>(() => m.GetPosition("bestaatniet", "RS"));
    }

    [Fact]
    public void AllowedInstructionsMatchSchema()
    {
        var schema = JsonNode.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(TacticPath())!, "..", "schema", "tactic.schema.json")))!;
        var expected = schema["$defs"]!["instructie"]!["enum"]!.AsArray().Select(i => i!.GetValue<string>());
        Assert.Equal(expected, TacticEditorModel.AllowedInstructions);
    }

    [Fact]
    public void ReadsInstructionsWithoutDetachingAndReturnsASnapshot()
    {
        var m = TacticEditorModel.LoadFile(TacticPath());
        var before = m.ToJson();
        Assert.Equal(new[] { "loop_diep" }, m.GetInstructions("aanval", "RS"));
        Assert.Empty(m.GetInstructions("opbouw", "GK"));
        Assert.Equal(before, m.ToJson());
        var snapshot = m.GetInstructions("opbouw", "RS");
        m.ToggleInstruction("opbouw", "RS", "loop_diep");
        Assert.Equal(new[] { "loop_diep" }, snapshot);
    }

    [Fact]
    public void EveryAllowedInstructionCanBeToggledAndEmptyFieldIsOmitted()
    {
        var m = TacticEditorModel.LoadFile(TacticPath());
        foreach (var instruction in TacticEditorModel.AllowedInstructions)
        {
            if (instruction == "dek_man")
            {
                var before = m.ToJson();
                Assert.Throws<ArgumentException>(() => m.ToggleInstruction("opbouw", "GK", instruction));
                Assert.Equal(before, m.ToJson());
                m.SetMarking("opbouw", "GK", "RS");
            }
            else m.ToggleInstruction("opbouw", "GK", instruction);
            Assert.Equal(new[] { instruction }, m.GetInstructions("opbouw", "GK"));
            TacticEditorModel.Parse(m.ToJson());
            m.ToggleInstruction("opbouw", "GK", instruction);
            Assert.Empty(m.GetInstructions("opbouw", "GK"));
            Assert.Null(m.GetMarking("opbouw", "GK"));
            var root = JsonNode.Parse(m.ToJson())!;
            Assert.False(root["fases"]!["opbouw"]!["spelers"]!.AsArray()[0]!.AsObject().ContainsKey("instructies"));
        }
    }

    [Theory]
    [InlineData("opbouw", "aanval")]
    [InlineData("aanval", "opbouw")]
    public void EditingInstructionsDetachesAliasesAndPreservesOtherFields(string edited, string unchanged)
    {
        var m = TacticEditorModel.LoadFile(TacticPath());
        var original = JsonNode.Parse(m.ToJson())!;
        var position = m.GetPosition(edited, "RS");
        m.ToggleInstruction(edited, "RS", "start_pressing");
        Assert.False(m.IsAlias("aanval"));
        Assert.Equal(new[] { "loop_diep", "start_pressing" }, m.GetInstructions(edited, "RS"));
        Assert.Equal(new[] { "loop_diep" }, m.GetInstructions(unchanged, "RS"));
        Assert.Equal(new[] { "loop_diep" }, m.GetInstructions("balwinst", "RS"));
        Assert.Equal(position, m.GetPosition(edited, "RS"));
        var changed = JsonNode.Parse(m.ToJson())!;
        Assert.True(JsonNode.DeepEquals(original["triggers"], changed["triggers"]));
        Assert.True(JsonNode.DeepEquals(original["fases"]!["verdedigen"], changed["fases"]!["verdedigen"]));
        TacticEditorModel.Parse(m.ToJson());
    }

    [Fact]
    public void InvalidInstructionEditsThrowWithoutChangingTheModel()
    {
        var m = TacticEditorModel.LoadFile(TacticPath());
        var before = m.ToJson();
        Assert.Throws<ArgumentException>(() => m.GetInstructions("onbekend", "RS"));
        Assert.Throws<ArgumentException>(() => m.GetInstructions("aanval", "XX"));
        Assert.Throws<ArgumentException>(() => m.ToggleInstruction("aanval", "RS", "onbekend"));
        Assert.Throws<ArgumentException>(() => m.ToggleInstruction("aanval", "XX", "loop_diep"));
        Assert.Throws<ArgumentException>(() => m.ToggleInstruction("onbekend", "RS", "loop_diep"));
        Assert.Equal(before, m.ToJson());
    }

    [Theory]
    [InlineData("tempo", 0.6)]
    [InlineData("pressing_intensiteit", 0.2)]
    [InlineData("lijnhoogte", 0.25)]
    [InlineData("compactheid", 0.9)]
    public void TeamSettingsReadClampRoundAndRoundTrip(string setting, double initial)
    {
        var m = TacticEditorModel.LoadFile(TacticPath());
        Assert.Equal(initial, m.GetTeamSetting(setting));
        m.SetTeamSetting(setting, -0.2);
        Assert.Equal(0, m.GetTeamSetting(setting));
        m.SetTeamSetting(setting, 1.4);
        Assert.Equal(1, m.GetTeamSetting(setting));
        m.SetTeamSetting(setting, 0.456);
        Assert.Equal(0.46, TacticEditorModel.Parse(m.ToJson()).GetTeamSetting(setting));
        Assert.Equal("zone", JsonNode.Parse(m.ToJson())!["team"]!["pressing_vorm"]!.GetValue<string>());
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => m.SetTeamSetting(setting, value));
        Assert.Equal(0.46, m.GetTeamSetting(setting));
    }

    [Fact]
    public void MissingCompactnessIsPreservedUntilSetAndUnknownSettingsAreRejected()
    {
        var root = JsonNode.Parse(File.ReadAllText(TacticPath()))!;
        root["team"]!.AsObject().Remove("compactheid");
        var m = TacticEditorModel.Parse(root.ToJsonString());
        Assert.Null(m.GetTeamSetting("compactheid"));
        Assert.False(JsonNode.Parse(m.ToJson())!["team"]!.AsObject().ContainsKey("compactheid"));
        var before = m.ToJson();
        Assert.Throws<ArgumentException>(() => m.GetTeamSetting("pressing_vorm"));
        Assert.Throws<ArgumentException>(() => m.SetTeamSetting("onbekend", 0.2));
        Assert.Equal(before, m.ToJson());
        m.SetTeamSetting("compactheid", 0.781);
        Assert.Equal(0.78, TacticEditorModel.Parse(m.ToJson()).GetTeamSetting("compactheid"));
    }

    [Fact]
    public void SavesNewSettingsAndInstructionsAndValidatesBeforeWriting()
    {
        var m = TacticEditorModel.LoadFile(TacticPath());
        m.ToggleInstruction("aanval", "RS", "start_pressing");
        m.SetTeamSetting("tempo", 0.876);
        var path = Path.Combine(Path.GetTempPath(), $"zeus_editor_{Guid.NewGuid():N}.json");
        try
        {
            m.Save(path);
            TacticLoader.LoadFile(path);
            var saved = File.ReadAllText(path);
            var again = TacticEditorModel.LoadFile(path);
            Assert.Equal(new[] { "loop_diep", "start_pressing" }, again.GetInstructions("aanval", "RS"));
            Assert.Equal(0.88, again.GetTeamSetting("tempo"));
            m.Name = "";
            Assert.Throws<TacticLoadException>(() => m.Save(path));
            Assert.Equal(saved, File.ReadAllText(path));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally { File.Delete(path); }
    }
}
