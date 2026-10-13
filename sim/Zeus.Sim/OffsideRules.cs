namespace Zeus.Sim;

public enum OffsideSource { OpenPlay, GoalKick, ThrowIn, Corner }

/// <summary>
/// IFAB 2026/27 Law 11 position subset, using player centroids as a body-part proxy.
/// Returned players are candidates, not established offences. Capture at the teammate's touch.
/// </summary>
public static class OffsideRules
{
    public static IReadOnlyList<string> PositionsAtTouch(Snapshot snapshot, Side attackingSide,
        bool attacksPositiveX, OffsideSource source = OffsideSource.OpenPlay)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!Enum.IsDefined(attackingSide) || !Enum.IsDefined(source))
            throw new ArgumentException("Unknown offside team or source.");
        // This exemption applies only to direct receipt; a teammate's subsequent touch is OpenPlay.
        if (source != OffsideSource.OpenPlay) return Array.Empty<string>();
        if (snapshot.Ball is null || !double.IsFinite(snapshot.Ball.Pos.X))
            throw new ArgumentException("Offside positions require a finite ball position at the touch.");
        var active = snapshot.Players.Where(p => !p.SentOff).ToArray();
        if (active.Any(p => !double.IsFinite(p.Pos.X)))
            throw new ArgumentException("Offside positions require finite active player positions.");
        double direction = attacksPositiveX ? 1 : -1;
        var opponents = active.Where(p => p.Team != attackingSide)
            .Select(p => direction * p.Pos.X).OrderDescending().ToArray();
        if (opponents.Length < 2)
            throw new ArgumentException("Offside positions require at least two active opponents.");
        double ballLine = direction * snapshot.Ball.Pos.X;
        double opponentLine = opponents[1];
        return Array.AsReadOnly(active.Where(p => p.Team == attackingSide
                && direction * p.Pos.X > 0
                && direction * p.Pos.X > ballLine
                && direction * p.Pos.X > opponentLine)
            .Select(p => p.Id).ToArray());
    }
}
