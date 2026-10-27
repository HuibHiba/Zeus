using System.Text.Json;

namespace Zeus.Sim;

/// <summary>Small, versioned record of actions actually completed by the player, the tutorial step and the mentor lines already heard.
/// Version 1 files (no tutorial, no seen ids) stay readable; the file is always written as version 2.</summary>
public sealed class OnboardingState
{
    public static IReadOnlyList<string> Steps { get; } = Array.AsReadOnly(new[] { "challenge", "substitution", "tactic", "meter", "season" });
    /// <summary>Tutorial steps: 0 = first start, 1 = intro (T0), 2-4 = scenarios A-C, 5 = real match (T4), 6 = closing (T5); <see cref="TutorialDone"/> ends it.</summary>
    public const int LastTutorialStep = 6;
    const int MaxSeen = 512, MaxIdLength = 80;
    readonly HashSet<string> _completed = new(StringComparer.Ordinal);
    readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    int _tutorialStep;
    public bool WelcomeSeen { get; set; }
    public bool TutorialDone { get; set; }
    public int TutorialStep { get => _tutorialStep; set => _tutorialStep = Math.Clamp(value, 0, LastTutorialStep); }
    public int CompletedCount => _completed.Count;
    public IReadOnlyCollection<string> Seen => _seen;
    public bool IsDone(string step) => _completed.Contains(step);
    public bool Mark(string step) => Steps.Contains(step) && _completed.Add(step);
    public bool HasSeen(string id) => _seen.Contains(id);
    /// <summary>Remembers a mentor line id; false when already seen or not a usable id.</summary>
    public bool MarkSeen(string id) => id.Length is > 0 and <= MaxIdLength && _seen.Count < MaxSeen && _seen.Add(id);
    /// <summary>"Show the explanations again": forgets the heard lines, keeps tutorial and checklist.</summary>
    public void ClearSeen() => _seen.Clear();

    public string ToJson() => JsonSerializer.Serialize(new
    {
        version = 2, welcomeSeen = WelcomeSeen, completed = Steps.Where(IsDone).ToArray(),
        tutorial = new { step = TutorialStep, done = TutorialDone }, seen = _seen.Order(StringComparer.Ordinal).ToArray(),
    });

    public static OnboardingState Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            int version = root.GetProperty("version").GetInt32();
            if (version is not (1 or 2)) throw new FormatException("Unsupported onboarding version.");
            var state = new OnboardingState { WelcomeSeen = root.GetProperty("welcomeSeen").GetBoolean() };
            foreach (var step in root.GetProperty("completed").EnumerateArray())
                state.Mark(step.GetString() ?? throw new FormatException("Invalid onboarding step."));
            if (version == 2)
            {
                var tutorial = root.GetProperty("tutorial");
                int step = tutorial.GetProperty("step").GetInt32();
                if (step is < 0 or > LastTutorialStep) throw new FormatException("Invalid tutorial step.");
                state.TutorialStep = step;
                state.TutorialDone = tutorial.GetProperty("done").GetBoolean();
                foreach (var id in root.GetProperty("seen").EnumerateArray())
                    if (!state.MarkSeen(id.GetString() ?? throw new FormatException("Invalid seen id."))) throw new FormatException("Invalid seen id.");
            }
            return state;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or OverflowException)
        {
            throw new FormatException("Invalid onboarding data.", e);
        }
    }
}
