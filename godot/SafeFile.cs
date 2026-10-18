// Careful reading and writing of the small user:// files (career, progress, history): a failed read never counts as "damaged",
// UserSaveFile preserves legacy saves, adds a version marker and keeps a backup before replacing a save.
using System;
using Godot;

public static class SafeFile
{
    /// <summary>Newer: written by a newer game version; treat like Unreadable (never write), but tell the player why.</summary>
    public enum ReadStatus { Missing, Ok, Unreadable, Newer }

    public static ReadStatus Read(string path, out string text)
    {
        var status = UserSaveFile.Read(ProjectSettings.GlobalizePath(path), out text) switch
        {
            UserSaveFile.ReadStatus.Missing => ReadStatus.Missing,
            UserSaveFile.ReadStatus.Ok => ReadStatus.Ok,
            UserSaveFile.ReadStatus.Newer => ReadStatus.Newer,
            _ => ReadStatus.Unreadable
        };
        if (status is ReadStatus.Unreadable or ReadStatus.Newer) GD.PushWarning($"{path}: {status}, file left untouched, saving to it is off");
        return status;
    }

    /// <summary>Writes through a unique temporary file, retaining the previous save as .bak. A failed backup blocks replacement.</summary>
    public static bool Write(string path, string text, out string error)
    {
        error = "";
        try
        {
            UserSaveFile.Write(ProjectSettings.GlobalizePath(path), text);
            return true;
        }
        catch (Exception e) { error = Loc.Error(e); return false; }
    }

    /// <summary>Keeps damaged saves as .bad/.bad.N; newer formats are never moved or overwritten.</summary>
    public static bool SetAside(string path)
    {
        try
        {
            return UserSaveFile.SetAside(ProjectSettings.GlobalizePath(path));
        }
        catch (Exception) { return false; }
    }
}
