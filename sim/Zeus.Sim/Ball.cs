namespace Zeus.Sim;

/// <summary>Ball physics constants. Values and sources: see Natuurkundewaarden in the vault.</summary>
public sealed record BallConfig(
    double Gravity = 9.81,          // m/s^2
    double DragCoefficient = 0.22,  // constant Cd, drag crisis ignored (Natuurkundewaarden: Trionda 0.22)
    double Mass = 0.43,             // kg (assumption within IFAB 0.41-0.45)
    double Radius = 0.11,           // m (circumference 0.69 m)
    double AirDensity = 1.225,      // kg/m^3
    double Restitution = 0.6,       // bounce on grass: assumption, not measured (steel 0.82-0.88)
    double RollDeceleration = 0.98, // m/s^2 = 0.1 g: assumption, not measured
    double MinBounceSpeed = 0.5,    // m/s vertical; below this the ball stops bouncing and rolls
    int SubSteps = 5)               // physics substeps per decision step (50 Hz at 0.1 s)
{
    public static readonly BallConfig Default = new();
}

/// <summary>Ball state: position (x, y, z) and velocity in metres and m/s.</summary>
public readonly record struct BallState(Vec3 Pos, Vec3 Vel)
{
    public static readonly BallState AtRest = new(Vec3.Zero, Vec3.Zero);
}

/// <summary>Ball integration (semi-implicit Euler with interpolated ground-impact time). Pure function, no state.</summary>
public static class BallPhysics
{
    private const double Eps = 1e-9;

    /// <summary>Advances the ball by <paramref name="dt"/> seconds in <paramref name="subSteps"/> equal substeps.</summary>
    public static BallState Step(BallState s, BallConfig c, double dt)
    {
        int n = Math.Max(1, c.SubSteps);
        double h = dt / n;
        for (int i = 0; i < n; i++) s = SubStep(s, c, h);
        return s;
    }

    private static bool OnGroundRolling(BallState s) => s.Pos.Z <= Eps && Math.Abs(s.Vel.Z) <= Eps;

    private static BallState SubStep(BallState s, BallConfig c, double h)
    {
        if (OnGroundRolling(s)) return Roll(s, c, h);

        double remaining = h;
        // At most a few impacts per substep; bounces shrink geometrically.
        for (int guard = 0; guard < 8 && remaining > Eps; guard++)
        {
            var next = Integrate(s, c, remaining);
            if (next.Pos.Z >= 0)
            {
                s = next;
                remaining = 0;
                break;
            }

            // Ground impact within this step: find the impact time (linear in z over the step), bounce, continue.
            double z0 = s.Pos.Z;
            double frac = z0 <= 0 ? 0 : z0 / (z0 - next.Pos.Z);
            double tHit = remaining * frac;
            var hit = Integrate(s, c, tHit);
            double vz = -hit.Vel.Z * c.Restitution;
            hit = new BallState(new Vec3(hit.Pos.X, hit.Pos.Y, 0), new Vec3(hit.Vel.X, hit.Vel.Y, vz));
            if (vz < c.MinBounceSpeed) hit = new BallState(hit.Pos, new Vec3(hit.Vel.X, hit.Vel.Y, 0));
            s = hit;
            remaining -= tHit;
            if (OnGroundRolling(s)) return Roll(s, c, remaining);
        }
        return s;
    }

    private static BallState Integrate(BallState s, BallConfig c, double h)
    {
        double speed = s.Vel.Length;
        double area = Math.PI * c.Radius * c.Radius;
        double k = 0.5 * c.AirDensity * c.DragCoefficient * area / c.Mass; // a_drag = -k * |v| * v
        var acc = new Vec3(-k * speed * s.Vel.X, -k * speed * s.Vel.Y, -c.Gravity - k * speed * s.Vel.Z);
        var vel = s.Vel + acc * h;
        var pos = s.Pos + vel * h;
        return new BallState(pos, vel);
    }

    private static BallState Roll(BallState s, BallConfig c, double h)
    {
        var v = new Vec2(s.Vel.X, s.Vel.Y);
        double speed = v.Length;
        if (speed <= Eps) return new BallState(new Vec3(s.Pos.X, s.Pos.Y, 0), Vec3.Zero);

        double area = Math.PI * c.Radius * c.Radius;
        double k = 0.5 * c.AirDensity * c.DragCoefficient * area / c.Mass;
        double decel = c.RollDeceleration + k * speed * speed; // rolling resistance plus air drag
        double newSpeed = speed - decel * h;
        if (newSpeed <= 0)
        {
            // Stopped part-way: move the distance until standstill (average speed over the step).
            var stop = v * (0.5 * speed / decel);
            return new BallState(new Vec3(s.Pos.X + stop.X, s.Pos.Y + stop.Y, 0), Vec3.Zero);
        }
        var dir = v * (1.0 / speed);
        var nv = dir * newSpeed;
        var np = new Vec2(s.Pos.X, s.Pos.Y) + nv * h;
        return new BallState(new Vec3(np.X, np.Y, 0), new Vec3(nv.X, nv.Y, 0));
    }
}
