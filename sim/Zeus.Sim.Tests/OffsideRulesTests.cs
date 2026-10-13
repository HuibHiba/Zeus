using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class OffsideRulesTests
{
    private static PlayerSnapshot Player(string id, double x, Side team = Side.Home, bool sentOff = false) =>
        new(team, id, new Vec2(x, 0), Vec2.Zero, 0, SentOff: sentOff);

    private static Snapshot At(double ballX, params PlayerSnapshot[] players) =>
        new(0, 0, players, new BallSnapshot(new Vec3(ballX, 0, 0), Vec3.Zero));

    [Fact]
    public void BothBallAndSecondLastOpponentMustBeBehindPlayer()
    {
        var world = At(20, Player("ahead", 31), Player("equal_defender", 30),
            Player("equal_ball", 20), Player("behind_ball", 19),
            Player("D1", 45, Side.Away), Player("D2", 30, Side.Away));
        Assert.Equal(new[] { "ahead" }, OffsideRules.PositionsAtTouch(world, Side.Home, true));
        Assert.Empty(OffsideRules.PositionsAtTouch(world with {
            Ball = new BallSnapshot(new Vec3(31, 0, 0), Vec3.Zero)
        }, Side.Home, true));
    }

    [Fact]
    public void OwnHalfAndHalfwayLineAreOnsideEvenWithAllOpponentsBehind()
    {
        var world = At(-20, Player("own_half", -1), Player("halfway", 0), Player("forward", 1),
            Player("D1", -5, Side.Away), Player("D2", -10, Side.Away));
        Assert.Equal(new[] { "forward" }, OffsideRules.PositionsAtTouch(world, Side.Home, true));
    }

    [Fact]
    public void KeeperHasNoSpecialStatusAndCanBeFartherUpfieldThanLastTwoDefenders()
    {
        var world = At(5, Player("attacker", 20), Player("GK", 5, Side.Away),
            Player("D1", 45, Side.Away), Player("D2", 30, Side.Away));
        Assert.Empty(OffsideRules.PositionsAtTouch(world, Side.Home, true));
    }

    [Fact]
    public void LevelWithLastTwoOpponentsIsOnside()
    {
        var world = At(5, Player("equal", 30), Player("ahead", 30.001),
            Player("D1", 30, Side.Away), Player("D2", 30, Side.Away));
        Assert.Equal(new[] { "ahead" }, OffsideRules.PositionsAtTouch(world, Side.Home, true));
    }

    [Fact]
    public void MirroringCoordinatesAndTeamsPreservesCandidates()
    {
        var world = At(10, Player("A", 30), Player("B", 5),
            Player("D1", 40, Side.Away), Player("D2", 20, Side.Away));
        var mirrored = world with {
            Players = world.Players.Select(p => p with {
                Team = p.Team == Side.Home ? Side.Away : Side.Home,
                Pos = new Vec2(-p.Pos.X, p.Pos.Y)
            }).ToArray(),
            Ball = new BallSnapshot(new Vec3(-10, 0, 0), Vec3.Zero)
        };
        Assert.Equal(OffsideRules.PositionsAtTouch(world, Side.Home, true),
            OffsideRules.PositionsAtTouch(mirrored, Side.Away, false));
        // Attack direction is caller supplied, independently of team identity.
        Assert.Empty(OffsideRules.PositionsAtTouch(world, Side.Home, false));
    }

    [Theory]
    [InlineData(OffsideSource.GoalKick)]
    [InlineData(OffsideSource.ThrowIn)]
    [InlineData(OffsideSource.Corner)]
    public void DirectRestartReceiptIsExemptButNextTeammateTouchStartsNewAssessment(OffsideSource source)
    {
        var world = At(5, Player("A", 30), Player("D1", 40, Side.Away), Player("D2", 20, Side.Away));
        Assert.Empty(OffsideRules.PositionsAtTouch(world, Side.Home, true, source));
        Assert.Equal(new[] { "A" }, OffsideRules.PositionsAtTouch(world, Side.Home, true));
    }

    [Fact]
    public void SentOffPlayersDoNotSetLineOrBecomeCandidatesAndOutputKeepsInputOrder()
    {
        var world = At(5, Player("Z", 31), Player("dismissed_attacker", 50, sentOff: true),
            Player("A", 35), Player("dismissed_defender", 50, Side.Away, true),
            Player("D1", 40, Side.Away), Player("D2", 20, Side.Away));
        var result = OffsideRules.PositionsAtTouch(world, Side.Home, true);
        Assert.Equal(new[] { "Z", "A" }, result);
        Assert.True(((IList<string>)result).IsReadOnly);
    }

    [Fact]
    public void MissingBallAndInsufficientOpponentsAreNotSilentlyOnside()
    {
        var world = At(5, Player("A", 30), Player("D1", 40, Side.Away));
        Assert.Throws<ArgumentException>(() => OffsideRules.PositionsAtTouch(world, Side.Home, true));
        Assert.Throws<ArgumentException>(() => OffsideRules.PositionsAtTouch(world with { Ball = null }, Side.Home, true));
        Assert.Throws<ArgumentException>(() => OffsideRules.PositionsAtTouch(world, Side.Home, true, (OffsideSource)999));
    }

    [Fact]
    public void InvalidActiveCoordinatesAreRejected()
    {
        var world = At(5, Player("A", double.NaN), Player("D1", 40, Side.Away), Player("D2", 20, Side.Away));
        Assert.Throws<ArgumentException>(() => OffsideRules.PositionsAtTouch(world, Side.Home, true));
        Assert.Throws<ArgumentException>(() => OffsideRules.PositionsAtTouch(world with {
            Ball = new BallSnapshot(new Vec3(double.PositiveInfinity, 0, 0), Vec3.Zero)
        }, Side.Home, true));
    }
}
