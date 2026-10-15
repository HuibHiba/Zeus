// Choices made in the menu, read by Main (match) and Editor. In-memory only.
public static class GameSession
{
    public static string? HomePath;
    public static string? AwayPath;
    public static ulong Seed = 1;
    public static string? EditPath;
    public static string? LastError;
    public static string HomeLevel = "neutraal", AwayLevel = "neutraal";

    // Competition (v0, voorlopig): Season sets InCompetition before it starts a live match; Main then writes the final score to LastResult
    // (home, away goals) and returns to Season, which processes it when LastResultRound still equals the current round.
    public static bool InCompetition;
    public static (int Home, int Away)? LastResult;
    public static int LastResultRound = -1;
    /// <summary>Scene the editor returns to instead of the menu (consumed once by the editor).</summary>
    public static string? ReturnScene;

    public static string SquadsDir() =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(Godot.ProjectSettings.GlobalizePath("res://"), "..", "squads"));

    public static string ChallengesDir() =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(Godot.ProjectSettings.GlobalizePath("res://"), "..", "challenges"));

    public static string TacticsDir() =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(Godot.ProjectSettings.GlobalizePath("res://"), "..", "tactics"));
}
