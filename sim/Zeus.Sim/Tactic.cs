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

/// <summary>Parsed tactic (schema v0.1) with all 'gelijk_aan' references resolved.</summary>
public sealed record Tactic(
    string Name,
    TeamSettings Team,
    IReadOnlyList<RosterPlayer> Roster,
    IReadOnlyDictionary<GamePhase, PhaseLayout> Phases,
    IReadOnlyDictionary<BlockHeight, PhaseLayout> DefendVariants,
    IReadOnlyList<BlockSwitch> BlockSwitches,
    IReadOnlyList<Trigger> Triggers)
{
    public PhaseLayout Phase(GamePhase phase) => Phases[phase];
}
