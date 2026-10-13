using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class BallControlTests
{
    private static PlayerSnapshot Player(string id, double x = 0, double y = 0, double vx = 0, double vy = 0) =>
        new(Side.Home, id, new Vec2(x, y), new Vec2(vx, vy), 0);

    private static BallState Ball(double x, double y = 0, double z = 0, double vx = 10, double vz = 0) =>
        new(new Vec3(x, y, z), new Vec3(vx, 0, vz));

    [Fact]
    public void DetectsPassCrossingPlayerBetweenFrames()
    {
        var players = new[] { Player("receiver") };
        var contact = BallControl.FindContact(Ball(-2), Ball(2), players, players, BallControlConfig.Default);
        Assert.NotNull(contact);
        Assert.Equal(0.25, contact.Value.Fraction, 12);
        Assert.Equal(new Vec3(-1, 0, 0), contact.Value.Position);
    }

    [Fact]
    public void MovingPlayerCanCrossStationaryBallBetweenFrames()
    {
        var before = new[] { Player("runner", -2, vx: 4) };
        var after = new[] { Player("runner", 2, vx: 4) };
        var ball = Ball(0, vx: 0);
        var contact = BallControl.FindContact(ball, ball, before, after, BallControlConfig.Default);
        Assert.NotNull(contact);
        Assert.Equal(0.25, contact.Value.Fraction, 12);
    }

    [Fact]
    public void EarliestContactWinsEvenWhenPlayerIsLaterInArray()
    {
        var players = new[] { Player("late", 2), Player("early", -1) };
        var contact = BallControl.FindContact(Ball(-4), Ball(4), players, players, BallControlConfig.Default);
        Assert.Equal(1, contact!.Value.PlayerIndex);
        Assert.Equal(0.25, contact.Value.Fraction, 12);
    }

    [Fact]
    public void EqualContactTimeUsesCallerOrderAndCanExcludePasser()
    {
        var players = new[] { Player("passer"), Player("opponent") };
        var a = BallControl.FindContact(Ball(-2), Ball(2), players, players, BallControlConfig.Default);
        var b = BallControl.FindContact(Ball(-2), Ball(2), players, players, BallControlConfig.Default, 0);
        Assert.Equal(0, a!.Value.PlayerIndex);
        Assert.Equal(1, b!.Value.PlayerIndex);
        Assert.Equal(a.Value.Fraction, b.Value.Fraction);
    }

    [Fact]
    public void HeightMustOverlapHorizontalReach()
    {
        var players = new[] { Player("receiver") };
        Assert.Null(BallControl.FindContact(Ball(-2, z: 1), Ball(2, z: 1), players, players, BallControlConfig.Default));
        // Ball drops into control height while still within the radius.
        var contact = BallControl.FindContact(Ball(-2, z: 0.6), Ball(2), players, players, BallControlConfig.Default);
        Assert.NotNull(contact);
        Assert.Equal(0.5, contact.Value.Fraction, 12);
        Assert.Equal(0.3, contact.Value.Position.Z, 12);
        // It drops only after passing the player: intersecting the two intervals must reject it.
        Assert.Null(BallControl.FindContact(Ball(-2, z: 2), Ball(2), players, players, BallControlConfig.Default));
    }

    [Fact]
    public void SpeedLimitIsRelativeToPlayerAndIncludesVerticalVelocity()
    {
        var still = new[] { Player("receiver") };
        var runner = new[] { Player("receiver", vx: 5) };
        Assert.Null(BallControl.FindContact(Ball(-2, vx: 20), Ball(2, vx: 20), still, still, BallControlConfig.Default));
        Assert.NotNull(BallControl.FindContact(Ball(-2, vx: 20), Ball(2, vx: 20), runner, runner, BallControlConfig.Default));
        Assert.Null(BallControl.FindContact(Ball(0, vx: 15, vz: 15), Ball(0, vx: 15, vz: 15), still, still, BallControlConfig.Default));
    }

    [Fact]
    public void DeceleratingBallCanBecomeControllableInsideRadius()
    {
        var players = new[] { Player("receiver") };
        var contact = BallControl.FindContact(Ball(0, vx: 20), Ball(0.5, vx: 10), players, players, BallControlConfig.Default);
        Assert.NotNull(contact);
        Assert.Equal(0.2, contact.Value.Fraction, 12);
    }

    [Fact]
    public void TangencyAndStartingAtRestInsideRadiusAreContacts()
    {
        var players = new[] { Player("receiver") };
        var tangent = BallControl.FindContact(Ball(-2, y: 1), Ball(2, y: 1), players, players, BallControlConfig.Default);
        Assert.Equal(0.5, tangent!.Value.Fraction, 12);
        var rest = Ball(0, vx: 0);
        Assert.Equal(0, BallControl.FindContact(rest, rest, players, players, BallControlConfig.Default)!.Value.Fraction);
        Assert.Null(BallControl.FindContact(Ball(-2, y: 1.01), Ball(2, y: 1.01), players, players, BallControlConfig.Default));
    }

    [Fact]
    public void ConfigurationAndSnapshotPairingAreValidated()
    {
        Assert.Throws<ArgumentException>(() => new BallControlConfig(MaxRelativeSpeed: double.NaN).Validate());
        Assert.Throws<ArgumentException>(() => new BallControlConfig(ControlRadius: 0).Validate());
        Assert.Throws<ArgumentException>(() => new BallControlConfig(MaxHeight: -1).Validate());
        var players = new[] { Player("receiver") };
        Assert.Throws<ArgumentException>(() => BallControl.FindContact(Ball(0), Ball(0), players,
            Array.Empty<PlayerSnapshot>(), BallControlConfig.Default));
        Assert.Throws<ArgumentException>(() => BallControl.FindContact(Ball(0), Ball(0), players,
            new[] { Player("different") }, BallControlConfig.Default));
        Assert.Throws<ArgumentOutOfRangeException>(() => BallControl.FindContact(Ball(0), Ball(0), players,
            players, BallControlConfig.Default, 1));
    }
}
