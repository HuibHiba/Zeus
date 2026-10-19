// Choices made in the menu, read by Main (match) and Editor. In-memory only.
public static class GameSession
{
    public static string? HomePath;
    public static string? AwayPath;
    public static ulong Seed = 12;         // first menu match: see DefaultHomeId; Main adds 1 after every match
    public static string? EditPath;
    public static string? LastError;
    public static string HomeLevel = "neutraal", AwayLevel = "neutraal";
    /// <summary>Selection screen checkbox (v0, voorlopig): the home team plays a menu match with its saved familiarity (EnableFamiliarity).</summary>
    public static bool UseFamiliarity;
    /// <summary>Provisional live substitutions for the AI side; opt-in, default off.</summary>
    public static bool OpponentSubstitutions;
    /// <summary>Smart opponent v1 in the competition (docs/slimme-tegenstander.md); setting "adaptive_opponent", default on.</summary>
    public static bool AdaptiveOpponent = true;

    // Competition (v0, voorlopig): Season sets InCompetition before it starts a live match; Main then writes the final score to LastResult
    // (home, away goals) and returns to Season, which processes it when LastResultRound still equals the current round.
    public static bool InCompetition;
    public static Zeus.Sim.FamiliarityState? CompetitionFamiliarity;
    public static Zeus.Sim.TacticDemand? CompetitionDemand;
    public static (int Home, int Away)? LastResult;
    public static int LastResultRound = -1;
    /// <summary>Career v1: the player's eleven for the live match (signings in, suspended/injured men replaced from the bench), and who was sent off in it.</summary>
    public static Zeus.Sim.Squad? CompetitionSquad;
    public static System.Collections.Generic.List<string> LastRedCards = new();
    /// <summary>Display name of the competition opponent, for the scoreboard and end card.</summary>
    public static string CompetitionOpponent = "";
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
    /// <summary>Debug hints in footers and panels; ZEUS_RELEASEUI=1 hides them for store screenshots from a debug build.</summary>
    public static bool DebugHints => DebugEnabled && DebugEnvironment("ZEUS_RELEASEUI").Length == 0;

    public static string DataRoot() => Zeus.Sim.DemoPaths.DataRoot(Godot.OS.HasFeature("editor"),
        Godot.ProjectSettings.GlobalizePath("res://"), Godot.OS.GetExecutablePath());
    public static string SquadsDir() => System.IO.Path.Combine(DataRoot(), "squads");
    public static string ChallengesDir() => System.IO.Path.Combine(DataRoot(), "challenges");
    public static string CompetitionsDir() => System.IO.Path.Combine(DataRoot(), "competitions");
    public static string TacticsDir() => System.IO.Path.Combine(DataRoot(), "tactics");
    public static string OwnTacticsDir() => Godot.ProjectSettings.GlobalizePath("user://tactieken");
    public static string WritableTacticsDir() => Godot.OS.HasFeature("editor") ? TacticsDir() : OwnTacticsDir();
    public sealed record TacticEntry(string Id, string Path, string Label, bool IsOwn);
    /// <summary>Test fixture: left out of every player-facing list, still resolvable by id (TacticPath) for competition clubs and the sim.</summary>
    public const string HiddenTacticId = "testcase_a_433.json";
    /// <summary>Often gives lopsided matches (QA soak RC7, docs/kalibratie.md): stays selectable, labelled experimental.</summary>
    public const string ExperimentalTacticId = "man_om_man_433.json";
    public static TacticEntry[] Tactics() => System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(
        System.Linq.Enumerable.Where(Zeus.Sim.TacticCatalog.List(TacticsDir(), OwnTacticsDir()), entry => entry.IsOwn || entry.Id != HiddenTacticId),
        entry => new TacticEntry(entry.Id, entry.Path,
            entry.IsOwn ? Loc.T("common.own", System.IO.Path.GetFileNameWithoutExtension(entry.Path).Replace('_', ' '))
                : entry.Id == ExperimentalTacticId ? Loc.T("tactic.experimental", Loc.Name(System.IO.Path.GetFileNameWithoutExtension(entry.Path)))
                : Loc.Name(System.IO.Path.GetFileNameWithoutExtension(entry.Path)), entry.IsOwn)));
    public static string TacticPath(string id) => Zeus.Sim.TacticCatalog.Resolve(id, TacticsDir(), OwnTacticsDir());
    /// <summary>Plain-language "how it works" of a starter tactic (key tactic.explain.&lt;file name&gt;); empty for tactics without one.</summary>
    public static string TacticExplanation(string path)
    {
        var key = "tactic.explain." + System.IO.Path.GetFileNameWithoutExtension(path);
        var text = Loc.T(key);
        return text == key ? "" : text;
    }
    /// <summary>First-match defaults in the menu, together with Seed = 12: Positional play 4-2-3-1 v Wing play 4-3-3, neutral level,
    /// seeds 12-16 (offside fix on): 2-1, 3-1, 3-2, 1-2, 1-1, 10-21 shots per team (seeds 4 and 9 gave 7 goals). Guarded by
    /// StartMatchTests; see docs/kalibratie.md, 'Buitenspel aan voor RC7'.</summary>
    public const string DefaultHomeId = "positiespel_4231.json", DefaultAwayId = "vleugelspel_433.json";
    public static string? DefaultPath(string id) => System.Array.Find(Tactics(), t => t.Id == id)?.Path;
    public static string TacticLabel(string path) => System.Array.Find(Tactics(), t => t.Path == path)?.Label
        ?? Loc.Name(System.IO.Path.GetFileNameWithoutExtension(path));
}
