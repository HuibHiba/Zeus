using System.Text.Json;

namespace Zeus.Sim;

/// <summary>Small, versioned record of actions actually completed by the player.</summary>
public sealed class OnboardingState
{
    public static IReadOnlyList<string> Steps { get; } = Array.AsReadOnly(new[] { "challenge", "substitution", "tactic", "meter", "season" });
    readonly HashSet<string> _completed = new(StringComparer.Ordinal);
    public bool WelcomeSeen { get; set; }
    public int CompletedCount => _completed.Count;
    public bool IsDone(string step) => _completed.Contains(step);
    public bool Mark(string step) => Steps.Contains(step) && _completed.Add(step);
    public string ToJson() => JsonSerializer.Serialize(new { version = 1, welcomeSeen = WelcomeSeen, completed = Steps.Where(IsDone).ToArray() });

    public static OnboardingState Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.GetProperty("version").GetInt32() != 1) throw new FormatException("Unsupported onboarding version.");
            var state = new OnboardingState { WelcomeSeen = root.GetProperty("welcomeSeen").GetBoolean() };
            foreach (var step in root.GetProperty("completed").EnumerateArray())
                state.Mark(step.GetString() ?? throw new FormatException("Invalid onboarding step."));
            return state;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or OverflowException)
        {
            throw new FormatException("Invalid onboarding data.", e);
        }
    }
}
