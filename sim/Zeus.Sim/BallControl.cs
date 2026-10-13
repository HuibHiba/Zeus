namespace Zeus.Sim;

/// <summary>Provisional foot-control limits, not yet calibrated to player ability.</summary>
public sealed record BallControlConfig(
    double ControlRadius = 1.0,
    double MaxHeight = 0.3,
    double MaxRelativeSpeed = 18.0) // m/s: assumption allowing ordinary passes; no measured skill model yet.
{
    public static readonly BallControlConfig Default = new();

    public void Validate()
    {
        if (!double.IsFinite(ControlRadius) || ControlRadius <= 0
            || !double.IsFinite(MaxHeight) || MaxHeight < 0
            || !double.IsFinite(MaxRelativeSpeed) || MaxRelativeSpeed < 0)
            throw new ArgumentException("Invalid ball-control settings.");
    }
}

/// <summary>First reachable ball position; Fraction is between zero and one within the supplied substep.</summary>
public readonly record struct BallContact(int PlayerIndex, Vec3 Position, double Fraction);

/// <summary>Continuous foot-control detection over one linear ball/player substep. Pure, with no random outcome.</summary>
public static class BallControl
{
    /// <summary>
    /// Intersects horizontal reach, foot height and relative-speed intervals. Player arrays must have matching order.
    /// The earliest contact wins; identical times retain caller array order. Call separately for physics substeps,
    /// particularly around bounces, because this function treats positions and velocities as linear between endpoints.
    /// </summary>
    public static BallContact? FindContact(BallState ballBefore, BallState ballAfter,
        IReadOnlyList<PlayerSnapshot> playersBefore, IReadOnlyList<PlayerSnapshot> playersAfter,
        BallControlConfig config, int? excludedPlayerIndex = null,
        Func<PlayerSnapshot, BallControlConfig>? limitsForPlayer = null)
    {
        config.Validate();
        if (playersBefore.Count != playersAfter.Count)
            throw new ArgumentException("Player snapshots must have matching counts and order.");
        if (excludedPlayerIndex is { } excluded && (excluded < 0 || excluded >= playersBefore.Count))
            throw new ArgumentOutOfRangeException(nameof(excludedPlayerIndex));

        BallContact? best = null;
        var ballDelta = ballAfter.Pos - ballBefore.Pos;
        for (int i = 0; i < playersBefore.Count; i++)
        {
            var before = playersBefore[i];
            var after = playersAfter[i];
            if (before.Team != after.Team || before.Id != after.Id)
                throw new ArgumentException("Player snapshots must have matching counts and order.");
            if (i == excludedPlayerIndex || before.SentOff || after.SentOff) continue;
            var limits = limitsForPlayer?.Invoke(before) ?? config;
            limits.Validate();
            double lo = 0, hi = 1;
            var offset = ballBefore.Pos.XY - before.Pos;
            var offsetDelta = ballDelta.XY - (after.Pos - before.Pos);
            if (!WithinSphere(new Vec3(offset.X, offset.Y, 0), new Vec3(offsetDelta.X, offsetDelta.Y, 0),
                    limits.ControlRadius, ref lo, ref hi)
                || !WithinHeight(ballBefore.Pos.Z, ballDelta.Z, limits.MaxHeight, ref lo, ref hi)) continue;

            var relativeBefore = ballBefore.Vel - new Vec3(before.Vel.X, before.Vel.Y, 0);
            var relativeAfter = ballAfter.Vel - new Vec3(after.Vel.X, after.Vel.Y, 0);
            if (!WithinSphere(relativeBefore, relativeAfter - relativeBefore, limits.MaxRelativeSpeed, ref lo, ref hi))
                continue;
            if (best is null || lo < best.Value.Fraction)
                best = new BallContact(i, ballBefore.Pos + ballDelta * lo, lo);
        }
        return best;
    }

    private static bool WithinHeight(double height, double delta, double max, ref double lo, ref double hi)
    {
        if (delta == 0) return height >= 0 && height <= max;
        double a = -height / delta, b = (max - height) / delta;
        lo = Math.Max(lo, Math.Min(a, b));
        hi = Math.Min(hi, Math.Max(a, b));
        return lo <= hi;
    }

    private static bool WithinSphere(Vec3 start, Vec3 delta, double radius, ref double lo, ref double hi)
    {
        double a = Dot(delta, delta);
        double b = 2 * Dot(start, delta);
        double c = Dot(start, start) - radius * radius;
        if (a == 0) return c <= 0;
        double discriminant = b * b - 4 * a * c;
        if (discriminant < 0) return false;
        double root = Math.Sqrt(discriminant);
        // Stable quadratic roots also preserve small entry times when starting close to the boundary.
        double q = -0.5 * (b + Math.CopySign(root, b));
        double t1 = q == 0 ? -b / (2 * a) : q / a;
        double t2 = q == 0 ? t1 : c / q;
        lo = Math.Max(lo, Math.Min(t1, t2));
        hi = Math.Min(hi, Math.Max(t1, t2));
        return lo <= hi;
    }

    private static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
}
