namespace Zeus.Sim;

/// <summary>Which home-screen parts the tutorial has opened, by step (see <see cref="OnboardingState.TutorialStep"/>: 2-4 = scenarios A-C, 5 = real match, 6 = closing).
/// shortcut: the league screen opens at step 5 (the real match is played from it) instead of after it as design §7 says; challenges follow after the real match.</summary>
public static class TutorialFlow
{
    public const int FirstScenarioStep = 2, LastScenarioStep = 4, RealMatchStep = 5, ClosingStep = 6;

    // feature -> (first step at which it is open, id of the step that opens it: the lock.after.<id> text)
    static readonly Dictionary<string, (int Step, string After)> Rules = new(StringComparer.Ordinal)
    {
        ["squad"] = (3, "a"), ["editor"] = (4, "b"), ["library"] = (4, "b"), ["meter"] = (5, "c"), ["league"] = (5, "c"),
        ["challenges"] = (6, "real"), ["friends"] = (7, "closing"),
    };

    /// <summary>Unknown features (settings, help, about, feedback) are always open; everything is open once the tutorial is done.</summary>
    public static bool Unlocked(string feature, int step, bool done) => done || !Rules.TryGetValue(feature, out var rule) || step >= rule.Step;

    public static string After(string feature) => Rules.TryGetValue(feature, out var rule) ? rule.After : "";

    /// <summary>Index into <see cref="TutorialScenarios.Ids"/> for the step, or -1 when the step is not a practice match.</summary>
    public static int ScenarioIndex(int step) => step is >= FirstScenarioStep and <= LastScenarioStep ? step - FirstScenarioStep : -1;
}
