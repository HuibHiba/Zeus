using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class BallTests
{
    private const double Dt = 0.1;
    private static readonly BallConfig C = BallConfig.Default;

    [Fact]
    public void DropFrom2mBouncesToAboutRestitutionSquaredTimesHeight()
    {
        var s = new BallState(new Vec3(0, 0, 2), Vec3.Zero);
        bool bounced = false;
        double apex = 0;
        for (int i = 0; i < 100; i++)
        {
            double vzBefore = s.Vel.Z;
            s = BallPhysics.Step(s, C, 0.02);
            if (!bounced && vzBefore < 0 && s.Vel.Z > 0) bounced = true;
            if (bounced) { apex = Math.Max(apex, s.Pos.Z); if (s.Vel.Z < 0) break; }
        }
        Assert.True(bounced);
        double expected = C.Restitution * C.Restitution * 2.0;
        Assert.InRange(apex, expected * 0.95, expected * 1.05);
    }

    [Fact]
    public void DropAlsoWorksWithDecisionStepOf100ms()
    {
        var s = new BallState(new Vec3(0, 0, 2), Vec3.Zero);
        double apex = 0;
        bool bounced = false;
        for (int i = 0; i < 100; i++)
        {
            double vz = s.Vel.Z;
            s = BallPhysics.Step(s, C, Dt);
            if (vz < 0 && s.Vel.Z > 0) bounced = true;
            if (bounced && s.Vel.Z > 0) apex = Math.Max(apex, s.Pos.Z);
        }
        // Sampled at 0.1 s the apex is only approximated, but must never exceed the exact value by much.
        Assert.True(apex > 0.5 && apex < 0.8, $"apex {apex}");
    }

    [Fact]
    public void HorizontalShotLandsAndStaysCloseToDragFreePath()
    {
        var start = new BallState(new Vec3(0, 0, 1), new Vec3(25, 0, 0));
        var noDrag = C with { DragCoefficient = 0 };
        var a = start; var b = start;
        bool bounced = false;
        for (int i = 0; i < 10; i++) // 1.0 s, ball travels ca. 25 m
        {
            double previousVz = a.Vel.Z;
            a = BallPhysics.Step(a, C, Dt);
            b = BallPhysics.Step(b, noDrag, Dt);
            if (previousVz < 0 && a.Vel.Z > 0) bounced = true;
        }
        Assert.True(bounced); // Ground contact can occur between snapshots; detect the velocity reversal.
        Assert.True(a.Pos.X < b.Pos.X); // drag only slows
        Assert.True(Math.Abs(a.Pos.X - b.Pos.X) / b.Pos.X < 0.25);
        Assert.True(b.Pos.X < 30);
    }

    [Fact]
    public void ShotIsDeterministic()
    {
        BallState Run()
        {
            var s = new BallState(new Vec3(0, 0, 1), new Vec3(25, 3, 4));
            for (int i = 0; i < 300; i++) s = BallPhysics.Step(s, C, Dt);
            return s;
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void RollingStopTimeMatchesResistanceAndQuadraticAirDrag()
    {
        var s = new BallState(Vec3.Zero, new Vec3(10, 0, 0));
        int steps = 0;
        while (s.Vel.Length > 0 && steps < 2000) { s = BallPhysics.Step(s, C, Dt); steps++; }
        double t = steps * Dt;
        // Analytical solution of dv/dt = -(a + k*v^2), rather than ignoring air resistance.
        double k = 0.5 * C.AirDensity * C.DragCoefficient * Math.PI * C.Radius * C.Radius / C.Mass;
        double expected = Math.Atan(10 * Math.Sqrt(k / C.RollDeceleration)) / Math.Sqrt(C.RollDeceleration * k);
        Assert.InRange(t, expected - 0.1, expected + 0.1);
        Assert.Equal(0, s.Pos.Z);
    }

    [Fact]
    public void NoNaNOrInfinityUnderExtremeStarts()
    {
        foreach (var v in new[] { new Vec3(60, 20, 30), new Vec3(-80, 0, -50), new Vec3(0, 0, 0.01), new Vec3(0.001, -0.002, 0) })
            foreach (var z in new[] { 0.0, 0.5, 30.0 })
            {
                var s = new BallState(new Vec3(0, 0, z), v);
                for (int i = 0; i < 3000; i++)
                {
                    s = BallPhysics.Step(s, C, Dt);
                    Assert.True(double.IsFinite(s.Pos.X) && double.IsFinite(s.Pos.Y) && double.IsFinite(s.Pos.Z));
                    Assert.True(double.IsFinite(s.Vel.X) && double.IsFinite(s.Vel.Y) && double.IsFinite(s.Vel.Z));
                    Assert.True(s.Pos.Z >= 0);
                }
            }
    }

    [Fact]
    public void SnapshotContainsBallAndMatchBallIsDeterministic()
    {
        var a = MatchTests.MakeMatch(7); var b = MatchTests.MakeMatch(7);
        var v = new Vec3(18, 2, 6);
        a.SetBall(new Vec3(-20, 0, 0.5), v); b.SetBall(new Vec3(-20, 0, 0.5), v);
        for (int i = 0; i < 200; i++) { a.Step(); b.Step(); }
        Assert.NotNull(a.GetSnapshot().Ball);
        Assert.Equal(a.GetSnapshot().Ball, b.GetSnapshot().Ball);
        Assert.Equal(a.SnapshotHash(), b.SnapshotHash());
    }
}
