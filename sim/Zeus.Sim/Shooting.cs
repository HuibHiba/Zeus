namespace Zeus.Sim;

/// <summary>
/// Shot decision and execution settings. Calibrated on counts (docs/kalibratie.md), not on a physical striking model.
/// Source of the xG coefficients: calibration/event_models.json schot_doelpunt (StatsBomb, logistic fit, 19081 shots).
/// </summary>
public sealed record ShotConfig(
    // A carrier shoots when the estimated goal probability of the shot is at least this (and he held the ball long enough).
    double MinGoalProbability = 0.125,
    // Execution: direction error (rad, triangular noise) = (AngleErrorBase + AngleErrorPerMeter * distance) * (1 - skill).
    double AngleErrorBase = 1.25,
    double AngleErrorPerMeter = 0.0,
    double SpeedMean = 26,
    double SpeedErrorFraction = 0.15,
    double RiseSpeed = 1.5,
    // Share of saves that the keeper pushes behind for a corner (rest: he holds the ball). 0 = always holds.
    double ParryCornerRate = 0.35)
{
    public void Validate()
    {
        if (new[] { MinGoalProbability, AngleErrorBase, AngleErrorPerMeter, SpeedMean, SpeedErrorFraction, RiseSpeed }
                .Any(v => !double.IsFinite(v) || v < 0) || MinGoalProbability > 1 || SpeedMean <= 0 || SpeedErrorFraction >= 1)
            throw new ArgumentException("Invalid shot settings.");
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
