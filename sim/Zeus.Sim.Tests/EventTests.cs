using System.Diagnostics;
using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class EventTests
{
    private static (string Events, string Snap) Run(ulong seed, int steps)
    {
        var m = MatchTests.MakeMatch(seed);
        for (int i = 0; i < steps; i++) m.Step();
        return (m.EventHash(), m.SnapshotHash());
    }

    [Fact]
    public void KickOffAndOneMoveStartPerPlayer()
    {
        var m = MatchTests.MakeMatch(1);
        for (int i = 0; i < 10; i++) m.Step();
        var ev = m.Events.Events;
        Assert.Equal(EventType.KickOff, ev[0].Type);
        Assert.Equal(0, ev[0].Tick);
        Assert.Equal(22, ev.Count(e => e.Type == EventType.MoveStart));
        Assert.All(ev.Where(e => e.Type == EventType.MoveStart), e => Assert.True(e.Seconds <= 0.3 + 1e-9));
    }

    [Fact]
    public void SameSeedSameHash() => Assert.Equal(Run(42, 600), Run(42, 600));

    [Fact]
    public void OtherSeedOtherHash()
    {
        Assert.NotEqual(Run(42, 600).Events, Run(43, 600).Events);
        Assert.NotEqual(Run(42, 5).Snap, Run(43, 5).Snap);
    }

    [Fact]
    public void CanonicalNumbersAreFixedAndInvariant()
    {
        Assert.Equal("0.000", Canonical.Num(-0.0001));
        Assert.Equal("1.235", Canonical.Num(1.2345));
        Assert.Equal("-12.500", Canonical.Num(-12.5));
    }

    // Golden hash for seed 42 (events after 600 steps, snapshot after 20 steps). Update on purpose when behaviour changes
    // 2026-10-13: ball snapshots, phase targets and a kick-off taker at the centre spot (tasks 11-13).
    // Full-match runs independently checked: equal seed = equal events; different seed = different events.
    private const string GoldenEventHash = "a1c48130396ac98af05c0b07ea1f34ab4df51078f265c51d8492a69acd79e9d5";
    private const string GoldenSnapshotHash = "634196e52aeb89cba0a4588e982bbcfffaf62c166226c6e69b28026cbbf2487f";

    [Fact]
    public void GoldenHashesForSeed42()
    {
        Assert.Equal(GoldenEventHash, Run(42, 600).Events);
        Assert.Equal(GoldenSnapshotHash, Run(42, 20).Snap); // mid-movement snapshot
    }

    [Fact]
    public void NinetyMinutesRunInUnderTenSeconds()
    {
        var m = MatchTests.MakeMatch(42);
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 54000; i++) m.Step();
        sw.Stop();
        Console.WriteLine($"90 min: {sw.Elapsed.TotalSeconds:F3} s");
        Assert.True(sw.Elapsed.TotalSeconds < 10, $"took {sw.Elapsed.TotalSeconds:F2} s");
    }
}
