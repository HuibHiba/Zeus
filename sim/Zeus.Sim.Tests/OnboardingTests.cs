namespace Zeus.Sim.Tests;

public sealed class OnboardingTests
{
    [Fact]
    public void FiveRealStepsCanBeCompletedInAnyOrderAndSurviveReload()
    {
        var state = new OnboardingState { WelcomeSeen = true };
        foreach (var step in OnboardingState.Steps.Reverse()) Assert.True(state.Mark(step));
        var restored = OnboardingState.Parse(state.ToJson());
        Assert.Equal(5, restored.CompletedCount);
        Assert.True(restored.WelcomeSeen);
        Assert.All(OnboardingState.Steps, step => Assert.True(restored.IsDone(step)));
    }

    [Fact]
    public void DuplicateAndUnknownStepsDoNotIncreaseCount()
    {
        var state = new OnboardingState();
        Assert.True(state.Mark("substitution"));
        Assert.False(state.Mark("substitution"));
        Assert.False(state.Mark("unknown"));
        Assert.Equal(1, state.CompletedCount);
        Assert.False(state.IsDone("unknown"));
        var restored = OnboardingState.Parse(state.ToJson());
        Assert.Equal(1, restored.CompletedCount);
        Assert.False(restored.WelcomeSeen);
        Assert.False(restored.IsDone("tactic"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"version\":99,\"welcomeSeen\":true,\"completed\":[]}")]
    [InlineData("{\"version\":1,\"welcomeSeen\":false,\"completed\":[null]}")]
    [InlineData("{\"version\":1,\"welcomeSeen\":false,\"completed\":{}}")]
    public void DamagedOrUnsupportedJsonCanBeRecoveredWithoutPartialProgress(string json)
    {
        Assert.Throws<FormatException>(() => OnboardingState.Parse(json));
        Assert.Equal(0, new OnboardingState().CompletedCount);
    }

    [Fact]
    public void JsonIgnoresUnknownStepsAndDeduplicatesKnownOnes()
    {
        var state = OnboardingState.Parse("{\"version\":1,\"welcomeSeen\":false,\"completed\":[\"meter\",\"future\",\"meter\"]}");
        Assert.Equal(1, state.CompletedCount);
        Assert.True(state.IsDone("meter"));
    }
    [Fact]
    public void DynamicOnboardingAndHelpKeysExistInBothLanguages()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "Zeus.sln"))) root = Path.GetDirectoryName(root)!;
        foreach (var language in new[] { "en", "nl" })
        {
            var table = LanguageTable.Parse(File.ReadAllText(Path.Combine(root, "data/lang", language + ".json")));
            foreach (var step in OnboardingState.Steps) Assert.True(table.ContainsKey("onboarding.step." + step));
            foreach (var screen in new[] { "menu", "challenges", "editor", "meter", "season", "squad" })
                for (int line = 1; line <= 4; line++) Assert.True(table.ContainsKey($"help.{screen}.{line}"));
        }
    }
}

public sealed class OnboardingV2Tests
{
    [Fact]
    public void VersionOneFileStaysReadableAndIsWrittenAsVersionTwo()
    {
        var old = OnboardingState.Parse("{\"version\":1,\"welcomeSeen\":true,\"completed\":[\"tactic\"]}");
        Assert.True(old.WelcomeSeen);
        Assert.True(old.IsDone("tactic"));
        Assert.Equal(0, old.TutorialStep);
        Assert.False(old.TutorialDone);
        Assert.Empty(old.Seen);
        Assert.Contains("\"version\":2", old.ToJson());
    }

    [Fact]
    public void TutorialStepAndSeenIdsSurviveReload()
    {
        var state = new OnboardingState { TutorialStep = 3 };
        Assert.True(state.MarkSeen("first.editor"));
        Assert.False(state.MarkSeen("first.editor"));
        var back = OnboardingState.Parse(state.ToJson());
        Assert.Equal(3, back.TutorialStep);
        Assert.True(back.HasSeen("first.editor"));
        back.ClearSeen();
        Assert.False(back.HasSeen("first.editor"));
        Assert.Equal(3, back.TutorialStep);
    }

    [Theory]
    [InlineData("{\"version\":3,\"welcomeSeen\":false,\"completed\":[]}")]
    [InlineData("{\"version\":2,\"welcomeSeen\":false,\"completed\":[],\"tutorial\":{\"step\":9,\"done\":false},\"seen\":[]}")]
    [InlineData("{\"version\":2,\"welcomeSeen\":false,\"completed\":[],\"tutorial\":{\"step\":1,\"done\":false},\"seen\":[\"\"]}")]
    [InlineData("{\"version\":2,\"welcomeSeen\":false,\"completed\":[]}")]
    public void InvalidFilesAreRejected(string json) => Assert.Throws<FormatException>(() => OnboardingState.Parse(json));
}

public sealed class ProfileTests
{
    [Theory]
    [InlineData(null, "user://zeus_carriere.json")]
    [InlineData("", "user://zeus_carriere.json")]
    [InlineData("2", "user://profielen/2/zeus_carriere.json")]
    [InlineData("../evil", "user://zeus_carriere.json")]
    [InlineData("A b", "user://zeus_carriere.json")]
    public void PathsFollowTheProfileAndNeverEscape(string? profile, string expected) =>
        Assert.Equal(expected, ProfileLayout.Path("zeus_carriere.json", profile));

    [Fact]
    public void ProfileRoundTripsAndCleansTheNames()
    {
        var data = new ProfileData { Name = "  Ann\u0007  ", Skin = 4, Hair = 5, Outfit = 3, Colour = 9, ClubName = "FC Test", Kit1 = 1, Kit2 = 2, Crest = 7, Created = "2026-10-27" };
        var back = ProfileData.Parse(data.ToJson());
        Assert.Equal("Ann", back.Name);
        Assert.Equal((4, 5, 3, 9), (back.Skin, back.Hair, back.Outfit, back.Colour));
        Assert.Equal(("FC Test", 1, 2, 7), (back.ClubName, back.Kit1, back.Kit2, back.Crest));
    }

    [Fact]
    public void OutOfRangeIndexIsRejected()
    {
        var json = new ProfileData { Skin = 0 }.ToJson().Replace("\"skin\":0", "\"skin\":5");
        Assert.Throws<FormatException>(() => ProfileData.Parse(json));
    }
}

public sealed class ProfilePathGuardTests
{
    // Files that stay global on purpose (settings, feedback, logs, autotest output) or that define the profile layout itself.
    static readonly string[] Allowed = { "Loc.cs", "AutoTest.cs", "Feedback.cs", "FeedbackLog.cs", "Profiles.cs", "SafeFile.cs", "FirstStart.cs" };      // FirstStart.HasOldSave looks at the main folder on purpose (the RC7 save)

    [Fact]
    public void PerProfileFilesNeverUseALiteralUserPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "godot"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var offenders = Directory.GetFiles(Path.Combine(dir!.FullName, "godot"), "*.cs")
            .Where(f => !Allowed.Contains(Path.GetFileName(f)))
            .Where(f => File.ReadLines(f).Any(l => l.Contains("\"user://") && !l.TrimStart().StartsWith("//")))
            .Select(Path.GetFileName).ToList();
        Assert.Empty(offenders);
    }
}

public sealed class MentorTests
{
    static string LangDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "data", "lang"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "data", "lang");
    }

    static Dictionary<string, string> Mentor(string lang) =>
        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(LangDir(), lang + ".json")))!
            .Where(p => p.Key.StartsWith("mentor.", StringComparison.Ordinal)).ToDictionary(p => p.Key, p => p.Value);

    [Fact]
    public void BothLanguagesHaveTheSameMentorIdsAndShortLines()
    {
        var en = Mentor("en"); var nl = Mentor("nl");
        Assert.Equal(en.Keys.Order(), nl.Keys.Order());
        Assert.All(en.Concat(nl), p => Assert.True(p.Value.Length <= MentorTiming.MaxLineLength, p.Key));
        Assert.All(en.Keys, k => Assert.Matches(@"^mentor\.[a-z_0-9]+(\.[a-z_0-9]+)*$", k));
    }

    [Fact]
    public void ProfileAndCoachKeysExistInBothLanguages()
    {
        string[] prefixes = { "profile.", "coach." };
        var all = (string lang) => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(LangDir(), lang + ".json")))!
            .Keys.Where(k => prefixes.Any(p => k.StartsWith(p, StringComparison.Ordinal))).Order().ToList();
        Assert.Equal(all("en"), all("nl"));
        Assert.Contains("profile.finish", all("en"));
    }

    [Fact]
    public void EveryTutorialScenarioHasItsFiveLines()
    {
        var en = Mentor("en");
        foreach (var s in new[] { "a", "b", "c" })
            foreach (var part in new[] { "start", "hint", "done", "late", "end" })
                Assert.True(en.ContainsKey($"mentor.scenario_{s}.{part}"), $"{s}.{part}");
    }

    [Fact]
    public void MouthFollowsTheTextAndClosesAtTheEnd()
    {
        const string text = "Aha, nice";
        Assert.Equal(Mouth.Open, MentorTiming.MouthAt(text, 0));
        Assert.Equal(Mouth.Closed, MentorTiming.MouthAt(text, MentorTiming.Duration(text) - 0.1));
        Assert.Equal(Mouth.Closed, MentorTiming.MouthAt(text, -1));
        Assert.Equal(Mouth.Closed, MentorTiming.MouthAt("", 0));
        Assert.True(MentorTiming.Duration(text) > MentorTiming.BaseSeconds);
    }
}
