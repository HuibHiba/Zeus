using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class PressingPerceptionTests
{
    private static PlayerSnapshot P(Side team, string id, double x, double y, bool keeper = false) =>
        new(team, id, new Vec2(x, y), Vec2.Zero, 0, IsGoalkeeper: keeper);

    private static Perception View(PlayerSnapshot self, BallSnapshot? ball, params PlayerSnapshot[] seen) =>
        new(0, self, GamePhase.Verdedigen, false, self.Pos, seen, ball, null);

    private static readonly BallSnapshot Ball = new(new Vec3(10, 0, 0), Vec3.Zero);

    [Fact]
    public void NearestVisibleDefenderPressesAndTheOthersDoNot()
    {
        var near = P(Side.Away, "near", 12, 0);
        var far = P(Side.Away, "far", 20, 0);
        Assert.True(PressingAssignment.IsPresser(View(near, Ball, far)));
        Assert.False(PressingAssignment.IsPresser(View(far, Ball, near)));
    }

    [Fact]
    public void ADefenderWhoDoesNotSeeTheBallOrIsTheKeeperNeverPresses()
    {
        Assert.False(PressingAssignment.IsPresser(View(P(Side.Away, "a", 12, 0), null)));
        Assert.False(PressingAssignment.IsPresser(View(P(Side.Away, "GK", 12, 0, keeper: true), Ball)));
        // A closer teammate who is a keeper does not count.
        Assert.True(PressingAssignment.IsPresser(View(P(Side.Away, "a", 20, 0), Ball, P(Side.Away, "GK", 11, 0, keeper: true))));
    }

    [Fact]
    public void DefendersWhoDoNotSeeEachOtherMayBothPressAndTiesGoToTheLowerId()
    {
        var a = P(Side.Away, "a", 12, 0);
        var b = P(Side.Away, "b", 12, 0);
        Assert.True(PressingAssignment.IsPresser(View(a, Ball, b)));
        Assert.False(PressingAssignment.IsPresser(View(b, Ball, a)));
        Assert.True(PressingAssignment.IsPresser(View(b, Ball)));   // does not see a: also presses (own perception only)
        Assert.True(PressingAssignment.IsPresser(View(a, Ball, P(Side.Home, "opp", 11, 0)))); // opponents do not count
    }
}
