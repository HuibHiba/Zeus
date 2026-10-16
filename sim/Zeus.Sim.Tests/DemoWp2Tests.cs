using System.Text.Json;

namespace Zeus.Sim.Tests;

public sealed class DemoWp2Tests : IDisposable
{
    readonly string _temp = Path.Combine(Path.GetTempPath(), "zeus-wp2-" + Guid.NewGuid());
    public DemoWp2Tests() => Directory.CreateDirectory(_temp);
    public void Dispose() => Directory.Delete(_temp, true);

    static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "Zeus.sln"))) dir = Path.GetDirectoryName(dir)!;
        return dir;
    }

    [Fact]
    public void EditorUsesRepositoryAndExportUsesExecutableDataRegardlessOfWorkingDirectory()
    {
        var repo = Path.Combine(_temp, "repo");
        var exe = Path.Combine(_temp, "demo", "game.exe");
        Assert.Equal(repo, DemoPaths.DataRoot(true, Path.Combine(repo, "godot"), exe));
        Assert.Equal(Path.Combine(_temp, "demo", "data"), DemoPaths.DataRoot(false, Path.Combine(repo, "godot"), exe));
    }

    [Fact]
    public void CatalogKeepsBothSourcesAndSameNamedFilesDistinctAndDoesNotCreateDirectories()
    {
        var builtIn = Path.Combine(_temp, "built"); var own = Path.Combine(_temp, "own");
        Assert.Empty(TacticCatalog.List(builtIn, own));
        Assert.False(Directory.Exists(own));
        Directory.CreateDirectory(builtIn); Directory.CreateDirectory(own);
        File.WriteAllText(Path.Combine(builtIn, "same.json"), "built");
        File.WriteAllText(Path.Combine(own, "same.json"), "own");
        File.WriteAllText(Path.Combine(own, "ignore.txt"), "ignored");
        var entries = TacticCatalog.List(builtIn, own);
        Assert.Equal(2, entries.Length);
        Assert.False(entries[0].IsOwn); Assert.True(entries[1].IsOwn);
        Assert.Equal("same", entries[0].Label); Assert.Equal("same (eigen)", entries[1].Label);
        Assert.NotEqual(entries[0].Id, entries[1].Id);
        foreach (var entry in entries) Assert.Equal(entry.Path, TacticCatalog.Resolve(entry.Id, builtIn, own));
        Assert.Equal("built", File.ReadAllText(entries[0].Path));
        Assert.Equal("own", File.ReadAllText(entries[1].Path));
    }

    [Theory]
    [InlineData("../x.json")]
    [InlineData("own@../x.json")]
    [InlineData("own@..")]
    [InlineData("own@")]
    [InlineData("own@C:x.json")]
    public void ReferencesCannotEscapeEitherDirectory(string id)
    {
        Assert.False(TacticFileNames.IsReference(id));
        Assert.Throws<ArgumentException>(() => TacticCatalog.Resolve(id, _temp, _temp));
        Assert.Throws<FormatException>(() => SeasonState.Parse(JsonSerializer.Serialize(new SeasonState { PlayerTactic = id })));
    }

    [Fact]
    public void OwnTacticReferenceSurvivesSeasonSaveAndLoadsInSimulatedRound()
    {
        var builtIn = Path.Combine(Root(), "tactics");
        File.Copy(Path.Combine(builtIn, "laag_blok_442.json"), Path.Combine(_temp, "friends.json"));
        var state = SeasonState.Parse(new SeasonState { PlayerTactic = "own@friends.json" }.ToJson());
        Assert.Equal("own@friends.json", state.PlayerTactic);
        Competition.PlayRound(state, builtIn, Path.Combine(Root(), "squads"),
            config: new MatchConfig(HalfSeconds: 10), ownTacticsDirectory: _temp);
        Assert.Equal(1, state.Round);
        Assert.Equal(Competition.MatchesPerRound, state.Results.Count);
    }

    [Fact]
    public void EnglishAndDutchHaveExactlyTheSameFlatStringKeysAndValidFormats()
    {
        var en = LanguageTable.Parse(File.ReadAllText(Path.Combine(Root(), "data", "lang", "en.json")));
        var nl = LanguageTable.Parse(File.ReadAllText(Path.Combine(Root(), "data", "lang", "nl.json")));
        Assert.NotEmpty(en);
        Assert.Equal(en.Keys.OrderBy(k => k), nl.Keys.OrderBy(k => k));
        foreach (var text in en.Values.Concat(nl.Values)) Assert.NotEmpty(string.Format(text, "example"));
    }

    [Fact]
    public void LanguageFallsBackToEnglishThenKeyAndSupportsArguments()
    {
        var table = new LanguageTable("{\"english\":\"Hello {0}\",\"both\":\"English\"}", "{\"both\":\"Nederlands\"}");
        Assert.Equal("Nederlands", table.T("both"));
        Assert.Equal("Hello Sam", table.T("english", "Sam"));
        Assert.Equal("missing", table.T("missing"));
    }
}
