namespace Zeus.Sim;

/// <summary>
/// Shot decision and execution settings. Calibrated on counts (docs/kalibratie.md), not on a physical striking model.
/// Source of the xG coefficients: calibration/event_models.json schot_doelpunt (StatsBomb, logistic fit, 19081 shots).
/// </summary>
public sealed record ShotConfig(
    // A carrier shoots when the estimated goal probability of the shot is at least this (and he held the ball long enough).
    double MinGoalProbability = 0.08,
    // Execution: direction error (rad, triangular noise) = (AngleErrorBase + AngleErrorPerMeter * distance) * (1 - skill).
    double AngleErrorBase = 1.25,
    double AngleErrorPerMeter = 0.0,
    double SpeedMean = 26,
    double SpeedErrorFraction = 0.15,
    double RiseSpeed = 1.5,
    // Share of saves that the keeper pushes behind for a corner (rest: he holds the ball). 0 = always holds.
    double ParryCornerRate = 0.35,
    // Decision-level realism (calibration round 6): defenders in the shot line and close to the shooter lower the estimated
    // goal probability. Each defender within BlockCorridorMeters of the line shooter-goal multiplies it by BlockFactor, each
    // other defender within PressureRadiusMeters by PressureFactor. 1 = no effect (the old 'shot without pressure').
    double BlockFactor = 1.0,
    double BlockCorridorMeters = 1.2,
    double PressureFactor = 1.0,
    double PressureRadiusMeters = 2.5)
{
    public void Validate()
    {
        if (new[] { MinGoalProbability, AngleErrorBase, AngleErrorPerMeter, SpeedMean, SpeedErrorFraction, RiseSpeed }
                .Any(v => !double.IsFinite(v) || v < 0) || MinGoalProbability > 1 || SpeedMean <= 0 || SpeedErrorFraction >= 1)
            throw new ArgumentException("Invalid shot settings.");
        if (new[] { BlockFactor, PressureFactor }.Any(v => !double.IsFinite(v) || v < 0 || v > 1)
            || !double.IsFinite(BlockCorridorMeters) || BlockCorridorMeters < 0
            || !double.IsFinite(PressureRadiusMeters) || PressureRadiusMeters < 0)
            throw new ArgumentException("Invalid shot pressure settings.");
    }
}

/// <summary>Probability that a shot becomes a goal, from distance and visible goal angle (logistic fit on StatsBomb shots).</summary>
public static class ShotModel
{
    public const double GoalHalfWidth = 3.66;

    /// <summary>Open-play foot shot without pressure. Distance in metres to the goal centre; lateral offset in metres.</summary>
    public static double GoalProbability(double distance, double lateral)
    {
        double along = Math.Sqrt(Math.Max(distance * distance - lateral * lateral, 0.01));
        double angle = Math.Abs(Math.Atan2(GoalHalfWidth - lateral, along) - Math.Atan2(-GoalHalfWidth - lateral, along));
        double logit = -0.64518 - 0.1465 * distance + 0.09862 * distance * distance / 100.0 + 1.30056 * angle;
        return 1 / (1 + Math.Exp(-logit));
    }
}

public static class ShotPressure
{
    /// <summary>Lowers a shot's goal probability for defenders in the shot line and close to the shooter (see <see cref="ShotConfig"/>).</summary>
    public static double Apply(double probability, Vec2 shooter, Vec2 goal, IEnumerable<Vec2> defenders, ShotConfig config)
    {
        if (config.BlockFactor >= 1 && config.PressureFactor >= 1) return probability;
        var line = goal - shooter;
        double length = line.Length;
        if (length < 0.5) return probability;
        double dx = line.X / length, dy = line.Y / length;
        double result = probability;
        foreach (var defender in defenders)
        {
            var rel = defender - shooter;
            double along = rel.X * dx + rel.Y * dy, lateral = Math.Abs(rel.X * dy - rel.Y * dx);
            if (along > 0.5 && along < length && lateral < config.BlockCorridorMeters) result *= config.BlockFactor;
            else if (rel.Length < config.PressureRadiusMeters) result *= config.PressureFactor;
        }
        return result;
    }
}

/// <summary>Execution-only shot model. Ball flight/contact (including the keeper) decides the outcome.</summary>
public static class ShotExecution
{
    public static Vec3 Velocity(Vec2 from, Vec2 aim, double skill, Rng rng, ShotConfig? config = null)
    {
        config ??= new ShotConfig();
        double error = Math.Clamp(1 - skill, 0, 1);
        var offset = aim - from;
        double spread = (config.AngleErrorBase + config.AngleErrorPerMeter * offset.Length) * error;
        double angle = Math.Atan2(offset.Y, offset.X) + (rng.NextDouble() + rng.NextDouble() - 1) * spread;
        double speed = config.SpeedMean * (1 + (rng.NextDouble() + rng.NextDouble() - 1) * config.SpeedErrorFraction * error);
        return new Vec3(Math.Cos(angle) * speed, Math.Sin(angle) * speed, config.RiseSpeed);
    }
}
