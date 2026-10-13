namespace Zeus.Sim;

/// <summary>Kinematic state of one player. Heading is the body direction in radians (0 = +x).</summary>
public readonly record struct BodyState(Vec2 Pos, Vec2 Vel, double Heading);

/// <summary>Moves a body towards a target within the limits of a profile. Pure function, no state.</summary>
public static class Mover
{
    private const double ArriveRadius = 0.02;

    public static double NormalizeAngle(double a)
    {
        a %= 2 * Math.PI;
        if (a > Math.PI) a -= 2 * Math.PI;
        else if (a <= -Math.PI) a += 2 * Math.PI;
        return a;
    }

    public static BodyState Step(BodyState s, Vec2 target, PlayerProfile p, double dt)
    {
        var d = target - s.Pos;
        double dist = d.Length;
        double speed = s.Vel.Length;

        // Arrived: snap when close and slow enough to stop within this step.
        if (dist <= ArriveRadius && speed <= p.Deceleration * dt)
            return new BodyState(target, Vec2.Zero, s.Heading);

        double heading = s.Heading;
        double cap = p.MaxSpeed;
        Vec2 dir = Vec2.Zero;
        if (dist > 1e-9)
        {
            dir = d * (1.0 / dist);
            double want = Math.Atan2(dir.Y, dir.X);
            double diff = NormalizeAngle(want - heading);
            double maxTurn = p.TurnRate * dt;
            heading = NormalizeAngle(heading + Math.Clamp(diff, -maxTurn, maxTurn));
            // Speed along the look direction is limited while not yet facing the target.
            double remaining = NormalizeAngle(want - heading);
            cap *= Math.Max(0.0, Math.Cos(remaining));
        }

        // Braking law: stop exactly at the target; never plan more than the remaining distance per step.
        double desiredSpeed = Math.Min(cap, Math.Sqrt(2 * p.Deceleration * dist));
        desiredSpeed = Math.Min(desiredSpeed, dist / dt);
        var desired = dir * desiredSpeed;

        var dv = desired - s.Vel;
        double dvLen = dv.Length;
        double limit = (desiredSpeed >= speed ? p.Acceleration : p.Deceleration) * dt;
        var vel = dvLen <= limit ? desired : s.Vel + dv * (limit / dvLen);

        // Hard cap (rounding safety).
        double vLen = vel.Length;
        if (vLen > p.MaxSpeed) vel = vel * (p.MaxSpeed / vLen);

        var pos = s.Pos + vel * dt;
        // Do not overshoot: if this step would reach or pass the target, stop on it.
        if (vel.X * d.X + vel.Y * d.Y > 0 && (vel * dt).Length >= dist)
            return new BodyState(target, Vec2.Zero, heading);

        return new BodyState(pos, vel, heading);
    }
}
