// Choices made in the menu, read by Main (match) and Editor. In-memory only.
public static class GameSession
{
    public static string? HomePath;
    public static string? AwayPath;
    public static ulong Seed = 1;
    public static string? EditPath;
    public static string? LastError;
    public static string HomeLevel = "neutraal", AwayLevel = "neutraal";
    /// <summary>Selection screen checkbox (v0, voorlopig): the home team plays a menu match with its saved familiarity (EnableFamiliarity).</summary>
    public static bool UseFamiliarity;
    /// <summary>Provisional live substitutions for the AI side; opt-in, default off.</summary>
    public static bool OpponentSubstitutions;

    // Competition (v0, voorlopig): Season sets InCompetition before it starts a live match; Main then writes the final score to LastResult
    // (home, away goals) and returns to Season, which processes it when LastResultRound still equals the current round.
    public static bool InCompetition;
    public static Zeus.Sim.FamiliarityState? CompetitionFamiliarity;
    public static Zeus.Sim.TacticDemand? CompetitionDemand;
    public static (int Home, int Away)? LastResult;
    public static int LastResultRound = -1;
    /// <summary>Scene the editor returns to instead of the menu (consumed once by the editor).</summary>
    public static string? ReturnScene;

    /// <summary>Which side the player controls (pause menu, labels): always Home outside the competition; Season sets it from the fixture.</summary>
    public static Zeus.Sim.Side PlayerSide = Zeus.Sim.Side.Home;
    /// <summary>Portable catalog reference of the tactic the editor saved last; Season and Challenges consume it after returning from the editor.</summary>
    public static string? LastSavedTactic;
    /// <summary>The challenge that is being played live (see ChallengeRun): Main records each finished match in it and returns to Challenges.</summary>
    public static ChallengeRun? Challenge;

    // Season and Challenges borrow the match fields above for their own matches. They stash the menu's choices first and the menu
    // restores them when it opens, so the menu never shows competition values.
    static (string? Home, string? Away, ulong Seed, string HomeLevel, string AwayLevel)? _stash;

    public static void StashMenuChoices() => _stash ??= (HomePath, AwayPath, Seed, HomeLevel, AwayLevel);

    public static void RestoreMenuChoices()
    {
        Challenge = null;           // the menu never continues a challenge
        if (_stash is not { } s) return;
        (HomePath, AwayPath, Seed, HomeLevel, AwayLevel) = s;
        _stash = null;
    }

    public static bool DebugEnabled => Godot.OS.IsDebugBuild() || Godot.OS.HasFeature("editor");
    public static string DebugEnvironment(string name) => DebugEnabled ? Godot.OS.GetEnvironment(name) : "";

    public static string DataRoot() => Zeus.Sim.DemoPaths.DataRoot(Godot.OS.HasFeature("editor"),
        Godot.ProjectSettings.GlobalizePath("res://"), Godot.OS.GetExecutablePath());
    public static string SquadsDir() => System.IO.Path.Combine(DataRoot(), "squads");
    public static string ChallengesDir() => System.IO.Path.Combine(DataRoot(), "challenges");
    public static string CompetitionsDir() => System.IO.Path.Combine(DataRoot(), "competitions");
    public static string TacticsDir() => System.IO.Path.Combine(DataRoot(), "tactics");
    public static string OwnTacticsDir() => Godot.ProjectSettings.GlobalizePath("user://tactieken");
    public static string WritableTacticsDir() => Godot.OS.HasFeature("editor") ? TacticsDir() : OwnTacticsDir();
    public sealed record TacticEntry(string Id, string Path, string Label, bool IsOwn);
    public static TacticEntry[] Tactics() => System.Array.ConvertAll(
        Zeus.Sim.TacticCatalog.List(TacticsDir(), OwnTacticsDir()), entry => new TacticEntry(entry.Id, entry.Path,
            entry.IsOwn ? Loc.T("common.own", System.IO.Path.GetFileNameWithoutExtension(entry.Path).Replace('_', ' '))
                : Loc.Name(System.IO.Path.GetFileNameWithoutExtension(entry.Path)), entry.IsOwn));
    public static string TacticPath(string id) => Zeus.Sim.TacticCatalog.Resolve(id, TacticsDir(), OwnTacticsDir());
    public static string TacticLabel(string path) => System.Array.Find(Tactics(), t => t.Path == path)?.Label
        ?? Loc.Name(System.IO.Path.GetFileNameWithoutExtension(path));
}
