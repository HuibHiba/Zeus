namespace Zeus.Sim;

/// <summary>A rejected live command (substitution or adjustment). The match state is unchanged when this is thrown.</summary>
public sealed class MatchCommandException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Change to one player's instructions. Applied to every phase layout (and block variant) of that player.</summary>
public sealed record InstructionChange(string PlayerId, IReadOnlyList<string>? Add = null,
    IReadOnlyList<string>? Remove = null, string? Mark = null);

/// <summary>
/// Live tactical adjustment; null fields are left unchanged. Compactness cannot be cleared, only set (0 = no effect).
/// Block selects an existing defending variant and then replaces the automatic block switches of that team.
/// </summary>
public sealed record TacticAdjustment(double? Tempo = null, double? PressingIntensity = null, double? LineHeight = null,
    double? Compactness = null, string? PressingForm = null, BlockHeight? Block = null,
    IReadOnlyList<InstructionChange>? Instructions = null)
{
    public bool IsEmpty => Tempo is null && PressingIntensity is null && LineHeight is null && Compactness is null
        && PressingForm is null && Block is null && (Instructions is null || Instructions.Count == 0);
}

/// <summary>An accepted live command with the tick (number of completed steps) it was applied at; this is the replay input timeline.</summary>
public sealed record MatchCommand(long Tick, Side Team, string Kind, string? OutPlayerId = null, string? InPlayerId = null,
    TacticAdjustment? Adjustment = null)
{
    public const string SubstituteKind = "substitute";
    public const string AdjustKind = "adjust";
}
