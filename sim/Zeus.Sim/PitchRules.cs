namespace Zeus.Sim;

public enum PitchExitKind { Goal, ThrowIn, GoalKick, Corner }

/// <summary>Position is the ball centre at the exit threshold, not a chosen restart location.</summary>
public readonly record struct PitchExit(PitchExitKind Kind, Side RestartTeam, Side? ScoringTeam,
    Vec2 Position, double Fraction);

/// <summary>Pure swept whole-ball boundary decisions. Home attacks +x; no half-time rotation here.</summary>
public static class PitchRules
{
    // IFAB Law 1: clear inside distance between posts and underside height of the crossbar.
    public const double GoalWidth = 7.32;
    public const double GoalHeight = 2.44;

    /// <summary>
    /// Returns the first outward exit over a linear physics substep, or null. The centre must pass
    /// the line plus the radius; touching that threshold alone is not out. Endline takes priority
    /// at an exactly simultaneous corner crossing. Call per physics substep, especially around bounces.
    /// Positions use the simulator's height convention (rolling ball z=0).
    /// </summary>
    public static PitchExit? Evaluate(BallState before, BallState after, Side lastTouch, double ballRadius = 0.11,
        bool homeAttacksPositiveX = true)
    {
        if (!double.IsFinite(ballRadius) || ballRadius < 0)
            throw new ArgumentOutOfRangeException(nameof(ballRadius));
        if (!Finite(before.Pos) || !Finite(after.Pos))
            throw new ArgumentException("Ball positions must be finite.");
        if (lastTouch is not (Side.Home or Side.Away))
            throw new ArgumentOutOfRangeException(nameof(lastTouch));

        double? fraction = null;
        bool endline = false;
        int sign = 0;
        // Order deliberately fixes the otherwise ambiguous exact corner tie.
        Consider(before.Pos.X, after.Pos.X, Pitch.HalfLength + ballRadius, true, 1);
        Consider(-before.Pos.X, -after.Pos.X, Pitch.HalfLength + ballRadius, true, -1);
        Consider(before.Pos.Y, after.Pos.Y, Pitch.HalfWidth + ballRadius, false, 1);
        Consider(-before.Pos.Y, -after.Pos.Y, Pitch.HalfWidth + ballRadius, false, -1);
        if (fraction is null) return null;

        var point = before.Pos + (after.Pos - before.Pos) * fraction.Value;
        if (!endline)
            return new PitchExit(PitchExitKind.ThrowIn, Opponent(lastTouch), null, point.XY, fraction.Value);

        Side attacking = (sign > 0) == homeAttacksPositiveX ? Side.Home : Side.Away;
        Side defending = Opponent(attacking);
        // No posts/net collision model yet; the whole ball must fit through the clear goal opening.
        if (Math.Abs(point.Y) + ballRadius <= GoalWidth / 2 && point.Z >= 0
            && point.Z + ballRadius <= GoalHeight)
            return new PitchExit(PitchExitKind.Goal, defending, attacking, point.XY, fraction.Value);
        bool goalKick = lastTouch == attacking;
        return new PitchExit(goalKick ? PitchExitKind.GoalKick : PitchExitKind.Corner,
            goalKick ? defending : attacking, null, point.XY, fraction.Value);

        void Consider(double start, double finish, double threshold, bool isEndline, int sideSign)
        {
            if (start > threshold || finish <= threshold) return;
            double t = (threshold - start) / (finish - start);
            if (fraction is not null && t >= fraction.Value) return;
            fraction = t;
            endline = isEndline;
            sign = sideSign;
        }
    }

    private static Side Opponent(Side side) => side == Side.Home ? Side.Away : Side.Home;
    private static bool Finite(Vec3 value) => double.IsFinite(value.X) && double.IsFinite(value.Y) && double.IsFinite(value.Z);
}
