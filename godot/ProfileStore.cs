using System;
using Godot;
using Zeus.Sim;

/// <summary>profiel.json of the active profile (null until the first start has written it). Newer or unreadable files are never overwritten.</summary>
public static class ProfileStore
{
    static ProfileData? _current;
    static bool _loaded, _blocked;
    static string _loadedFor = "";
    static string File => Profiles.Path("profiel.json");

    public static ProfileData? Current
    {
        get
        {
            if (_loaded && _loadedFor == File) return _current;
            _loaded = true; _loadedFor = File; _current = null; _blocked = false;
            try
            {
                var status = SafeFile.Read(File, out var json);
                _blocked = status is SafeFile.ReadStatus.Unreadable or SafeFile.ReadStatus.Newer;
                if (status == SafeFile.ReadStatus.Ok)
                {
                    try { _current = ProfileData.Parse(json); }
                    catch (FormatException) { _blocked = !SafeFile.SetAside(File); }
                }
            }
            catch (Exception e) { _blocked = true; GD.PushWarning(e.Message); }
            return _current;
        }
    }

    /// <summary>True when the profile was written; with ZEUS_NOSAVE it only lives in memory.</summary>
    public static bool Save(ProfileData data)
    {
        _ = Current;
        if (_blocked) return false;
        _current = data;
        if (GameSession.DebugEnvironment("ZEUS_NOSAVE").Length > 0) return true;
        try
        {
            if (SafeFile.Write(File, data.ToJson(), out var error)) return true;
            GD.PushWarning(error);
        }
        catch (Exception e) { GD.PushWarning(e.Message); }
        return false;
    }
}
