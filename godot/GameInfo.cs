using System;
using System.IO;
using Godot;

public static class GameInfo
{
    public const string Title = "Gaffer's Notebook";
    public const string ShortTitle = Title;
    public static string Version { get; } = ReadVersion();

    static string ReadVersion()
    {
        var besideExe = Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath())!, "VERSIE.txt");
        var repo = OS.HasFeature("editor") ? Path.Combine(GameSession.DataRoot(), "VERSIE.txt") : besideExe;
        foreach (var path in new[] { besideExe, repo })
        {
            try { if (File.Exists(path) && File.ReadAllText(path).Trim() is { Length: > 0 } version) return version; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return "dev";
    }
}
