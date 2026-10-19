namespace Zeus.Sim;

public enum GamePhase { Opbouw, Aanval, Balverlies, Verdedigen, Balwinst, Stilstaand }

public enum BlockHeight { Hoog, Midden, Laag }

public enum ScoreState { Voor, Gelijk, Achter }

public sealed record TeamSettings(
    double Tempo, double PressingIntensity, string PressingForm, double LineHeight, double? Compactness);

public sealed record RosterPlayer(string Id, string Role, string? Note);

/// <summary>Relative position (0..1) of one player in one phase.</summary>
public sealed record PhasePlayer(
    string Id,
    double X,
    double Y,
    (double X, double Y)? FollowBall,
    IReadOnlyList<string> Instructions,
    string? Mark,
    IReadOnlyList<(double X, double Y)>? Path = null,
    string PathSpeed = "joggen",
    double PathWait = 0);

public sealed record PhaseLayout(BlockHeight? Block, IReadOnlyList<PhasePlayer> Players);

public sealed record BlockSwitchCondition(ScoreState? Score, int? FromMinute, double? FatigueAbove);

public sealed record BlockSwitch(string? Name, BlockSwitchCondition When, BlockHeight To);

public sealed record TriggerAction(string Player, string? Instruction = null, bool Path = false);

public sealed record Trigger(string? Name, GamePhase Phase, string Condition, string? Zone, IReadOnlyList<TriggerAction> Then);

/// <summary>
/// Optional set-piece routines (JSON block "standaardsituaties"). A null field keeps the historical behaviour for that situation.
/// CornerFor: korte_paal | verre_paal | kort | penaltystip. FreeKickFor (attacking half): voorzet | schot (under 25 m, else voorzet) | kort.
/// CornerAgainst / FreeKickAgainst: zone | man. PostDefenders: 0..2 players on the posts when defending.
/// </summary>
public sealed record SetPieces(string? CornerFor = null, string? CornerAgainst = null, int PostDefenders = 2,
    string? FreeKickFor = null, string? FreeKickAgainst = null)
{
    public static readonly IReadOnlyList<string> CornerRoutines = new[] { "korte_paal", "verre_paal", "kort", "penaltystip" };
    public static readonly IReadOnlyList<string> FreeKickRoutines = new[] { "voorzet", "schot", "kort" };
    public static readonly IReadOnlyList<string> Defences = new[] { "zone", "man" };
}

/// <summary>Parsed tactic (schema v0.1) with all 'gelijk_aan' references resolved.</summary>
public sealed record Tactic(
    string Name,
    TeamSettings Team,
    IReadOnlyList<RosterPlayer> Roster,
    IReadOnlyDictionary<GamePhase, PhaseLayout> Phases,
    IReadOnlyDictionary<BlockHeight, PhaseLayout> DefendVariants,
    IReadOnlyList<BlockSwitch> BlockSwitches,
    IReadOnlyList<Trigger> Triggers,
    SetPieces? SetPieces = null)
{
    public PhaseLayout Phase(GamePhase phase) => Phases[phase];

    /// <summary>The same tactic without rules: the meter's "with/without rules" baseline.</summary>
    public Tactic WithoutTriggers() => this with { Triggers = Array.Empty<Trigger>() };
}
