// Choices made in the menu, read by Main (match) and Editor. In-memory only.
public static class GameSession
{
    public static string? HomePath;
    public static string? AwayPath;
    public static ulong Seed = 1;
    public static string? EditPath;

    public static string TacticsDir() =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(Godot.ProjectSettings.GlobalizePath("res://"), "..", "tactics"));
}
