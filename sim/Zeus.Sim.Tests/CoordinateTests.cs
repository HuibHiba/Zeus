using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class CoordinateTests
{
    [Fact]
    public void HomeOwnGoalLineMiddleIsMinusHalfLength()
    {
        var p = Pitch.ToMeters(0, 0.5, Side.Home);
        Assert.Equal(-52.5, p.X, 9);
        Assert.Equal(0, p.Y, 9);
    }

    [Fact]
    public void AwayIsRotated180Degrees()
    {
        var home = Pitch.ToMeters(0.2, 0.1, Side.Home);
        var away = Pitch.ToMeters(0.2, 0.1, Side.Away);
        Assert.Equal(-home.X, away.X, 9);
        Assert.Equal(-home.Y, away.Y, 9);
        Assert.Equal(52.5, Pitch.ToMeters(0, 0.5, Side.Away).X, 9);
    }

    [Fact]
    public void CentreMapsToOrigin()
    {
        Assert.Equal(0, Pitch.ToMeters(0.5, 0.5, Side.Home).Length, 9);
        Assert.Equal(0, Pitch.ToMeters(0.5, 0.5, Side.Away).Length, 9);
    }

    [Fact]
    public void CornersReachPitchBounds()
    {
        var p = Pitch.ToMeters(1, 1, Side.Home);
        Assert.Equal(52.5, p.X, 9);
        Assert.Equal(34, p.Y, 9);
    }
}
