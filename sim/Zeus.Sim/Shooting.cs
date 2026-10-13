namespace Zeus.Sim;

/// <summary>Uncalibrated execution-only shot model. Ball flight/contact decides the outcome.</summary>
public static class ShotExecution
{
    public static Vec3 Velocity(Vec2 from, Vec2 aim, double skill, Rng rng)
    {
        double error = Math.Clamp(1 - skill, 0, 1);
        var offset = aim - from;
        double angle = Math.Atan2(offset.Y, offset.X) + (rng.NextDouble() + rng.NextDouble() - 1) * 0.12 * error;
        double speed = 26 * (1 + (rng.NextDouble() + rng.NextDouble() - 1) * 0.15 * error);
        return new Vec3(Math.Cos(angle) * speed, Math.Sin(angle) * speed, 1.5);
    }
}
