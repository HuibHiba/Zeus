namespace Zeus.Sim;

/// <summary>Uncalibrated v0 passing choices and execution parameters.</summary>
public sealed record PassConfig(double MinDistance = 3, double MaxDistance = 35,
    double HoldSeconds = 0.8, double LeadSeconds = 0.35, double Skill = 0.75,
    double AngleErrorRadians = 0.06, double SpeedErrorFraction = 0.12)
{
    public void Validate()
    {
        if (!double.IsFinite(MinDistance) || MinDistance <= 0 || !double.IsFinite(MaxDistance) || MaxDistance < MinDistance
            || !double.IsFinite(HoldSeconds) || HoldSeconds < 0 || !double.IsFinite(LeadSeconds) || LeadSeconds < 0
            || !double.IsFinite(Skill) || Skill < 0 || Skill > 1
            || !double.IsFinite(AngleErrorRadians) || AngleErrorRadians < 0
            || !double.IsFinite(SpeedErrorFraction) || SpeedErrorFraction < 0 || SpeedErrorFraction >= 1)
            throw new ArgumentException("Invalid pass settings.");
    }
}

public sealed record PassOption(string ReceiverId, Vec2 Target, double Score);

public static class PassChoice
{
    public static PassOption? Choose(Perception view, PassConfig config)
    {
        if (!view.HasBall) return null;
        PassOption? best = null;
        foreach (var mate in view.VisiblePlayers)
        {
            if (mate.Team != view.Self.Team) continue;
            var target = mate.Pos + mate.Vel * config.LeadSeconds;
            target = new Vec2(Math.Clamp(target.X, -Pitch.HalfLength, Pitch.HalfLength),
                Math.Clamp(target.Y, -Pitch.HalfWidth, Pitch.HalfWidth));
            double distance = (target - view.Self.Pos).Length;
            if (distance < config.MinDistance || distance > config.MaxDistance) continue;
            double risk = 0;
            foreach (var opponent in view.VisiblePlayers.Where(p => p.Team != view.Self.Team))
            {
                double lane = DistanceToSegment(opponent.Pos, view.Self.Pos, target);
                risk += Math.Max(0, 1 - lane / 3);
                risk += Math.Max(0, 1 - (opponent.Pos - target).Length / 4);
            }
            double forward = (target.X - view.Self.Pos.X) * (view.Self.Team == Side.Home ? 1 : -1);
            double score = 1 + 0.025 * forward - 0.015 * distance - 1.2 * risk;
            // Stable identity tie-break: list order is not tactical information.
            if (score > 0 && (best is null || score > best.Score + 1e-9
                || (Math.Abs(score - best.Score) <= 1e-9 && string.CompareOrdinal(mate.Id, best.ReceiverId) < 0)))
                best = new PassOption(mate.Id, target, score);
        }
        return best;
    }

    internal static double DistanceToSegment(Vec2 point, Vec2 start, Vec2 end)
    {
        var line = end - start;
        double lengthSquared = line.X * line.X + line.Y * line.Y;
        double t = lengthSquared < 1e-9 ? 0 : Math.Clamp(((point - start).X * line.X + (point - start).Y * line.Y) / lengthSquared, 0, 1);
        return (point - (start + line * t)).Length;
    }
}

public static class PassExecution
{
    /// <summary>Execution error changes velocity only. Physics/contact determines success, never a second success roll.</summary>
    public static Vec3 Velocity(Vec2 from, Vec2 target, double visiblePressure, PassConfig config, Rng rng)
    {
        var difference = target - from;
        double distance = difference.Length;
        double difficulty = (1 - config.Skill) * (1 + distance / 35 + Math.Clamp(visiblePressure, 0, 3));
        double angleNoise = (rng.NextDouble() + rng.NextDouble() - 1) * config.AngleErrorRadians * difficulty;
        double speedNoise = (rng.NextDouble() + rng.NextDouble() - 1) * config.SpeedErrorFraction * difficulty;
        double angle = Math.Atan2(difference.Y, difference.X) + angleNoise;
        double speed = Math.Clamp(7 + 0.25 * distance, 8, 16) * Math.Max(0.2, 1 + speedNoise);
        return new Vec3(Math.Cos(angle) * speed, Math.Sin(angle) * speed, 0);
    }
}

public sealed class PassingBrain : IPlayerBrain
{
    private readonly PassConfig _config;
    private readonly FormationBrain _formation = new();
    public PassingBrain(PassConfig config) => _config = config;

    public Intent Decide(Perception view)
    {
        if (!view.HasBall)
        {
            if (view.IsPresser && view.VisibleBall is { } carriedBall)
                return new Intent(IntentKind.Press, carriedBall.Pos.XY);
            if (view.BallIsLoose && view.VisibleBall is { } ball && ball.Pos.Z < 0.5)
            {
                double ownDistance = (ball.Pos.XY - view.Self.Pos).Length;
                bool nearerMate = view.VisiblePlayers.Any(p => p.Team == view.Self.Team
                    && ((p.Pos - ball.Pos.XY).Length < ownDistance - 1e-9
                        || (Math.Abs((p.Pos - ball.Pos.XY).Length - ownDistance) <= 1e-9
                            && string.CompareOrdinal(p.Id, view.Self.Id) < 0)));
                if (!nearerMate)
                {
                    var target = ball.Pos.XY + ball.Vel.XY * 0.25;
                    return new Intent(IntentKind.MoveToPosition, target);
                }
            }
            // Once in position, keep scanning instead of freezing forever with a loose ball behind the body.
            if (view.VisibleBall is null && (view.FormationTarget - view.Self.Pos).Length <= 0.02)
                return new Intent(IntentKind.Scan, view.Self.Pos);
            return _formation.Decide(view);
        }
        if (view.HeldBallSeconds < _config.HoldSeconds) return _formation.Decide(view);
        var option = PassChoice.Choose(view, _config);
        return option is not null ? new Intent(IntentKind.Pass, option.Target, option.ReceiverId)
            : new Intent(IntentKind.Scan, view.Self.Pos);
    }
}
