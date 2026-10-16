using System.Text.Json;
using System.Text.RegularExpressions;

namespace Zeus.Sim.Tests;

public sealed class LocCoverageTests
{
    static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "Zeus.sln"))) dir = Path.GetDirectoryName(dir)!;
        return dir;
    }

    // Product name and language-independent identifiers in developer overlays.
    static readonly HashSet<string> Allowed = new(StringComparer.Ordinal) { "TRIGGER ", "PASS ", "Gaffer's Notebook" };
    static readonly Regex VisibleLiteral = new(
        "(?:\\b(?:Text|TooltipText|PlaceholderText|DialogText|Title)\\s*=\\s*|\\b(?:Label|Primary|Secondary|MenuItem|Small|Wrapped|SetStatus|AddItem|AddSeparator|PanelLine|StatRow)\\s*\\(\\s*|\\bDrawString\\([^;]*?,\\s*)(?<interpolated>\\$)?\"(?<text>(?:\\\\.|[^\"\\\\])*)\"",
        RegexOptions.Compiled);

    [Fact]
    public void GodotVisibleLiteralsUseLocalization()
    {
        var failures = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(Root(), "godot"), "*.cs"))
        {
            var source = File.ReadAllText(file);
            // Preserve line numbers, ignore standalone and trailing comments without stripping res:// URLs.
            source = Regex.Replace(source, @"(?m)^\s*//[^\r\n]*", "");
            foreach (System.Text.RegularExpressions.Match m in VisibleLiteral.Matches(source))
            {
                var value = m.Groups["text"].Value;
                // Interpolations that start with an expression are checked at their text-producing source.
                if (m.Groups["interpolated"].Success) value = value.Split('{')[0];
                if (!Regex.IsMatch(value, "[A-Za-z]") || Allowed.Contains(value)) continue;
                var line = source[..m.Index].Count(c => c == '\n') + 1;
                failures.Add($"{Path.GetFileName(file)}:{line}: {value}");
            }
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Theory]
    [InlineData("new Label { Text = \"Hard coded\" }")]
    [InlineData("button.Text = \"Start\";")]
    [InlineData("UiTheme.Primary(\"Start match\")")]
    [InlineData("pick.AddItem(\"Low block\")")]
    [InlineData("TooltipText = \"Drag here\"")]
    [InlineData("label.Text = $\"Score: {score}\"")]
    [InlineData("DrawString(font, position, \"Attack\", alignment)")]
    public void GuardDetectsCommonUiEntryPoints(string source) => Assert.Matches(VisibleLiteral, source);

    [Fact]
    public void BuiltInIdentifiersHaveTranslatedDisplayNames()
    {
        var table = LanguageTable.Parse(File.ReadAllText(Path.Combine(Root(), "data/lang/en.json")));
        var names = TacticEditorModel.Roles.Concat(SquadLoader.Roles).Concat(TacticEditorModel.AllowedInstructions)
            .Concat(TacticEditorModel.Conditions).Concat(TacticEditorModel.BasePhases).Concat(TacticEditorModel.BlockHeights)
            .Concat(TacticEditorModel.PressingForms).Concat(TacticEditorModel.Standings).Concat(TacticEditorModel.Zones)
            .Concat(TacticalConcepts.All).Concat(SquadFactory.Levels).Concat(Competition.Divisions.Select(d => d.Name));
        foreach (var name in names)
            Assert.True(table.ContainsKey("common.name." + name.ToLowerInvariant().Replace(' ', '_')), name);
    }

    [Fact]
    public void BuiltInChallengesHaveBothLanguagesAndKeepLegacyFallback()
    {
        foreach (var file in Directory.GetFiles(Path.Combine(Root(), "challenges"), "*.json"))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var r = doc.RootElement;
            foreach (var field in new[] { "naam", "uitleg", "tip" })
                foreach (var language in new[] { "en", "nl" }) Assert.False(string.IsNullOrWhiteSpace(r.GetProperty(field + "_" + language).GetString()));
            var english = ChallengeEvaluator.Parse(r.GetRawText(), "en");
            var dutch = ChallengeEvaluator.Parse(r.GetRawText(), "nl");
            Assert.Equal(r.GetProperty("uitleg_en").GetString(), english.Description);
            Assert.Equal(r.GetProperty("tip_nl").GetString(), dutch.Hint);
            Assert.Equal(english.Criteria, dutch.Criteria);
            var legacy = System.Text.Json.Nodes.JsonNode.Parse(r.GetRawText())!.AsObject();
            foreach (var field in new[] { "naam", "uitleg", "tip" })
                foreach (var language in new[] { "en", "nl" }) legacy.Remove(field + "_" + language);
            Assert.Equal(r.GetProperty("uitleg").GetString(), ChallengeEvaluator.Parse(legacy.ToJsonString(), "en").Description);
        }
    }

    [Fact]
    public void AllLiteralTranslationKeysExistInEnglish()
    {
        var table = LanguageTable.Parse(File.ReadAllText(Path.Combine(Root(), "data/lang/en.json")));
        foreach (var file in Directory.GetFiles(Path.Combine(Root(), "godot"), "*.cs"))
            foreach (System.Text.RegularExpressions.Match m in Regex.Matches(File.ReadAllText(file), "Loc\\.T\\(\"([^\"]+)\"\\s*[,)]"))
                Assert.True(table.ContainsKey(m.Groups[1].Value), $"Missing key {m.Groups[1].Value} in {file}");
    }
}
