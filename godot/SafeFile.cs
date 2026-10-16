// Careful reading and writing of the small user:// files (career, progress, history): a failed read never counts as "damaged",
// a damaged file is set aside as .bad before anything new is written, and writes go through a .tmp file that is renamed over the target.
using System;
using Godot;

public static class SafeFile
{
    public enum ReadStatus { Missing, Ok, Unreadable }

    public static ReadStatus Read(string path, out string text)
    {
        text = "";
        if (!Godot.FileAccess.FileExists(path)) return ReadStatus.Missing;
        using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (f is null) return ReadStatus.Unreadable;            // temporary failure (locked, permissions): the file may be fine, so never overwrite it
        text = f.GetAsText();
        return Godot.FileAccess.GetOpenError() == Error.Ok ? ReadStatus.Ok : ReadStatus.Unreadable;
    }

    /// <summary>Writes atomically: to path.tmp first, then renamed over the target. Returns false (with the reason) when that did not work.</summary>
    public static bool Write(string path, string text, out string error)
    {
        error = "";
        var tmp = path + ".tmp";
        try
        {
            using (var f = Godot.FileAccess.Open(tmp, Godot.FileAccess.ModeFlags.Write))
            {
                if (f is null) { GD.PushWarning(Godot.FileAccess.GetOpenError().ToString()); error = Loc.T("common.write_failed"); return false; }
                f.StoreString(text);
            }
            var rename = DirAccess.RenameAbsolute(tmp, path);
            if (rename != Error.Ok) { GD.PushWarning(rename.ToString()); error = Loc.T("common.rename_failed"); return false; }
            return true;
        }
        catch (Exception e) { error = Loc.Error(e); return false; }
    }

    /// <summary>Moves a damaged file to the same name with extension .bad (replacing an older .bad). Returns false when that failed.</summary>
    public static bool SetAside(string path)
    {
        try
        {
            var bad = System.IO.Path.ChangeExtension(path, ".bad");
            if (Godot.FileAccess.FileExists(bad)) DirAccess.RemoveAbsolute(bad);
            return DirAccess.RenameAbsolute(path, bad) == Error.Ok;
        }
        catch (Exception) { return false; }
    }
}
