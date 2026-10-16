using System.Text.Json.Nodes;

namespace Zeus.Sim.Tests;

public sealed class TacticExchangeTests : IDisposable
{
    readonly string _directory = Path.Combine(Path.GetTempPath(), "zeus-exchange-" + Guid.NewGuid());
    public TacticExchangeTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public void FriendRoundTripPreservesEditsAndExtensionFields()
    {
        var model = TacticTemplates.Create("433", "Friend tactic");
        var root = JsonNode.Parse(model.ToJson())!;
        root["friend_notes"] = "A custom field survives sharing";
        model = TacticEditorModel.Parse(root.ToJsonString());
        model.SetPosition("opbouw", model.PlayerIds[1], 0.35, 0.6);
        var expected = model.ToJson();
        var path = Path.Combine(_directory, "friends tactic.JSON");
        TacticExchange.Export(model, path);
        var imported = TacticExchange.Import(path);
        Assert.Equal(expected, imported.ToJson());
        Assert.Equal(expected, model.ToJson());
        imported.SetPosition("opbouw", imported.PlayerIds[1], 0.7, 0.8);
        Assert.Equal(expected, TacticExchange.Import(path).ToJson());
    }

    [Fact]
    public void InvalidAndOversizedImportsDoNotTouchAnExistingTactic()
    {
        var path = Path.Combine(_directory, "invalid.json");
        File.WriteAllText(path, "{\"naam\":\"Broken\"}");
        Assert.ThrowsAny<Exception>(() => TacticExchange.Import(path));
        File.WriteAllBytes(path, new byte[TacticExchange.MaxBytes + 1]);
        Assert.Throws<InvalidDataException>(() => TacticExchange.Import(path));
        Assert.Equal(TacticExchange.MaxBytes + 1, new FileInfo(path).Length);
    }

    [Theory]
    [InlineData("CON", "imported")]
    [InlineData("../my tactic", "mytactic")]
    [InlineData("", "imported")]
    public void ImportedNameIsSafeAndDoesNotOverwriteExistingTactics(string name, string expected)
    {
        File.WriteAllText(Path.Combine(_directory, expected + ".json"), "keep");
        var result = TacticExchange.AvailableName(name, _directory);
        Assert.Equal(expected + "_2", result);
        Assert.Equal("keep", File.ReadAllText(Path.Combine(_directory, expected + ".json")));
    }

    [Fact]
    public void DuplicateLongNameStillFitsTheFilenameLimit()
    {
        var stem = new string('a', TacticFileNames.MaxLength);
        File.WriteAllText(Path.Combine(_directory, stem + ".json"), "keep");
        var result = TacticExchange.AvailableName(stem, _directory);
        Assert.Equal(TacticFileNames.MaxLength, result.Length);
        Assert.EndsWith("_2", result);
    }

    [Fact]
    public void ExportRefusesOtherFileTypesWithoutWriting()
    {
        var path = Path.Combine(_directory, "keep.txt");
        File.WriteAllText(path, "keep");
        Assert.Throws<ArgumentException>(() => TacticExchange.Export(TacticTemplates.Create("433", "Test"), path));
        Assert.Equal("keep", File.ReadAllText(path));
    }

    [Fact]
    public void OversizedExportPreservesTheExistingDestination()
    {
        var root = JsonNode.Parse(TacticTemplates.Create("433", "Test").ToJson())!;
        root["notes"] = new string('x', TacticExchange.MaxBytes);
        var model = TacticEditorModel.Parse(root.ToJsonString());
        var path = Path.Combine(_directory, "keep.json");
        File.WriteAllText(path, "keep");
        Assert.Throws<InvalidDataException>(() => TacticExchange.Export(model, path));
        Assert.Equal("keep", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
    }
}
