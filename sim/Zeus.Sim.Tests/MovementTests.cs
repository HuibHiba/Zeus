using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class MovementTests
{
    private const double Dt = 0.1;
    private static readonly PlayerProfile P = PlayerProfile.Default;

    [Fact]
    public void SprintOf20mFromRestTakesAbout3To3_8Seconds()
    {
        // Far target so the player does not brake: time until 20 m are covered.
        var s = new BodyState(Vec2.Zero, Vec2.Zero, 0);
        var target = new Vec2(60, 0);
        int steps = 0;
        while (s.Pos.X < 20 && steps < 1000) { s = Mover.Step(s, target, P, Dt); steps++; }
        double t = steps * Dt;
        Assert.InRange(t, 3.0, 3.8);
    }

    [Fact]
    public void SpeedNeverExceedsMaximum()
    {
        var s = new BodyState(Vec2.Zero, Vec2.Zero, 0);
        foreach (var target in new[] { new Vec2(60, 0), new Vec2(-30, 25), new Vec2(10, -40) })
            for (int i = 0; i < 300; i++)
            {
                s = Mover.Step(s, target, P, Dt);
                Assert.True(s.Vel.Length <= P.MaxSpeed + 1e-9);
            }
    }

    [Fact]
    public void StopsOnTargetWithoutOvershoot()
    {
        var start = new Vec2(-30, 10);
        var target = new Vec2(12, -7);
        var line = target - start;
        double total = line.Length;
        var s = new BodyState(start, Vec2.Zero, Math.Atan2(line.Y, line.X));
        double maxAlong = 0;
        for (int i = 0; i < 400; i++)
        {
            s = Mover.Step(s, target, P, Dt);
            double along = ((s.Pos - start).X * line.X + (s.Pos - start).Y * line.Y) / total;
            maxAlong = Math.Max(maxAlong, along);
        }
        Assert.True((s.Pos - target).Length < 0.05);
        Assert.True(s.Vel.Length < 1e-9);
        Assert.True(maxAlong <= total + 1e-6, $"overshoot {maxAlong - total}");
    }

    private static double TimeToArrive(BodyState s, Vec2 target)
    {
        int steps = 0;
        while ((s.Pos - target).Length > 0.05 || s.Vel.Length > 1e-9) { s = Mover.Step(s, target, P, Dt); steps++; Assert.True(steps < 2000); }
        return steps * Dt;
    }

    [Fact]
    public void TurningAround180CostsTime()
    {
        var target = new Vec2(20, 0);
        double straight = TimeToArrive(new BodyState(Vec2.Zero, Vec2.Zero, 0), target);
        double turned = TimeToArrive(new BodyState(Vec2.Zero, Vec2.Zero, Math.PI), target);
        Assert.True(turned >= straight + Dt - 1e-9, $"straight {straight}, turned {turned}");
    }

    [Fact]
    public void HeadingTurnsAtMostTurnRate()
    {
        var s = new BodyState(Vec2.Zero, Vec2.Zero, Math.PI);
        var n = Mover.Step(s, new Vec2(20, 0), P, Dt);
        Assert.True(Math.Abs(Mover.NormalizeAngle(n.Heading - s.Heading)) <= P.TurnRate * Dt + 1e-9);
    }

    [Fact]
    public void PlayersStayInsidePitchOverAMatch()
    {
        var m = MatchTests.MakeMatch(7);
        for (int i = 0; i < 3000; i++)
        {
            m.Step();
            foreach (var p in m.GetSnapshot().Players)
            {
                Assert.True(Math.Abs(p.Pos.X) <= Pitch.HalfLength);
                Assert.True(Math.Abs(p.Pos.Y) <= Pitch.HalfWidth);
                Assert.True(p.Vel.Length <= P.MaxSpeed + 1e-9);
            }
        }
    }

    [Fact]
    public void PlayersReachTheirTargetsInAMatch()
    {
        var m = MatchTests.MakeMatch(7);
        for (int i = 0; i < 1200; i++) m.Step();
        Assert.All(m.GetSnapshot().Players, p => Assert.True(p.Vel.Length < 1e-9));
    }

    [Fact]
    public void KeeperCanBackpedalWithoutTurningAwayFromBall()
    {
        var body = new BodyState(Vec2.Zero, Vec2.Zero, 0);
        for (int i = 0; i < 10; i++) body = Mover.Step(body, new Vec2(-10, 0), P, Dt, allowBackpedal: true);
        Assert.True(body.Pos.X < 0);
        Assert.Equal(0, body.Heading);
        Assert.True(body.Vel.Length <= P.MaxSpeed * 0.5 + 1e-9);
    }
}
