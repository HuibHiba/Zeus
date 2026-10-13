namespace Zeus.Sim;

/// <summary>Provisional runs behind the line and through-ball choices; see docs/buitenspel.md.</summary>
public sealed record AttackingRunConfig(bool EnableRuns = true, bool EnableThroughBalls = true,
    double RunDepthMeters = 1.5, double LineMisjudgementMeters = 1.0,
    double ThroughBallBonus = 0.8, double MinRunSpeed = 0.3,
    double RunSeconds = 4.0, double RunCooldownSeconds = 60.0, double RunStartDistanceMeters = 14.0)
{
    public void Validate()
    {
        if (!double.IsFinite(RunDepthMeters) || RunDepthMeters < 0
            || !double.IsFinite(LineMisjudgementMeters) || LineMisjudgementMeters < 0
            || !double.IsFinite(ThroughBallBonus) || ThroughBallBonus < 0
            || !double.IsFinite(MinRunSpeed) || MinRunSpeed <= 0
            || !double.IsFinite(RunSeconds) || RunSeconds <= 0
            || !double.IsFinite(RunCooldownSeconds) || RunCooldownSeconds < RunSeconds
            || !double.IsFinite(RunStartDistanceMeters) || RunStartDistanceMeters <= 0)
            throw new ArgumentException("Invalid attacking run settings.");
    }
}

public static class AttackingRuns
{
    public static bool IsAttackingRole(string role) => role is "spits" or "targetman" or "buitenspeler";

    // Missing opponents make this estimate conservative: the second last visible opponent cannot be deeper
    // than the real second last opponent. With fewer than two, there is no usable line estimate.
    public static double? EstimatedLine(Perception view)
    {
        double dir = view.AttacksPositiveX ? 1 : -1;
        var opponents = view.VisiblePlayers.Where(p => p.Team != view.Self.Team && !p.SentOff)
            .Select(p => p.Pos.X * dir).OrderDescending().ToArray();
        return opponents.Length < 2 ? null : opponents[1] + view.LineMisjudgement;
    }

    public static Vec2? Target(Perception view, AttackingRunConfig config)
    {
        if (!config.EnableRuns || !view.CanRunInBehind || view.HasBall || view.Self.IsKeeper
            || view.BallIsLoose || view.TeamPassInFlight || view.TeamBallCarrierId is null) return null;
        var carrier = view.VisiblePlayers.FirstOrDefault(p => p.Team == view.Self.Team && p.Id == view.TeamBallCarrierId);
        double dir = view.AttacksPositiveX ? 1 : -1;
        if (carrier is null || carrier.Pos.X * dir < 0 || EstimatedLine(view) is not { } line) return null;
        if (line - view.Self.Pos.X * dir > config.RunStartDistanceMeters) return null;
        return new Vec2(Math.Clamp((line + config.RunDepthMeters) * dir, -Pitch.HalfLength, Pitch.HalfLength),
            view.FormationTarget.Y);
    }
}
