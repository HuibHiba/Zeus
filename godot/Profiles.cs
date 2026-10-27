using Zeus.Sim;

/// <summary>The active profile decides where the per-profile files live (see <see cref="ProfileLayout"/>). Settings, feedback and logs stay global.
/// ZEUS_PROFILE (debug builds) picks a profile for autotests and screenshots; otherwise the "active_profile" setting; empty = the main folder.</summary>
public static class Profiles
{
    public static string Active()
    {
        var env = GameSession.DebugEnvironment("ZEUS_PROFILE");
        return env.Length > 0 ? env : Loc.Setting("active_profile", "");
    }

    public static string Path(string fileName) => ProfileLayout.Path(fileName, Active());
}
