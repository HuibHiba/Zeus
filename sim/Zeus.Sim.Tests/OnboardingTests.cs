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
