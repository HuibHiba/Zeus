using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class RngTests
{
    [Fact]
    public void MatchesPcgCDemoReferenceValues()
    {
        var rng = new Rng(42UL, 54UL);
        uint[] expected = { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };
        foreach (var e in expected) Assert.Equal(e, rng.NextUInt());
    }

    [Fact]
    public void SameSeedGivesSame1000Numbers()
    {
        var a = new Rng(7); var b = new Rng(7);
        for (int i = 0; i < 1000; i++) Assert.Equal(a.NextUInt(), b.NextUInt());
    }

    [Fact]
    public void DifferentSeedGivesDifferentNumbers()
    {
        var a = new Rng(7); var b = new Rng(8);
        int same = 0;
        for (int i = 0; i < 1000; i++) if (a.NextUInt() == b.NextUInt()) same++;
        Assert.True(same < 5);
    }

    [Fact]
    public void BoundedAndDoubleStayInRange()
    {
        var r = new Rng(1);
        for (int i = 0; i < 1000; i++)
        {
            Assert.True(r.NextUInt(6) < 6);
            var d = r.NextDouble();
            Assert.True(d >= 0 && d < 1);
        }
    }
}
