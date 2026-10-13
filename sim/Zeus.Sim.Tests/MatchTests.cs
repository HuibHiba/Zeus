using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class MatchTests
{
    internal static Match MakeMatch(ulong seed) => Make(seed);
    // Preserve the movement baseline independently of later tactical/energy modules.
    private static Match Make(ulong seed) => Match.Create(new MatchConfig(EnableFatigue: false, EnableTeamPhases: false),
        TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442")),
        TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433")), seed, (_, _) => new FormationBrain());

    [Fact]
    public void KickoffHas22PlayersAtPhasePositions()
    {
        var s = Make(1).GetSnapshot();
        Assert.Equal(22, s.Players.Count);
        Assert.Equal(11, s.Players.Count(p => p.Team == Side.Home));
        var gk = s.Players.First(p => p.Team == Side.Home && p.Id == "GK");
        Assert.Equal(Pitch.ToMeters(0.04, 0.5, Side.Home).X, gk.Pos.X, 9);
    }

    [Fact]
    public void After600StepsClockIs60Seconds()
    {
        var m = Make(1);
        for (int i = 0; i < 600; i++) m.Step();
        var s = m.GetSnapshot();
        Assert.Equal(600, s.Tick);
        Assert.Equal(60.0, s.TimeSeconds, 9);
    }

    [Fact]
    public void AllPlayersInsidePitch()
    {
        var m = Make(1);
        for (int i = 0; i < 600; i++) m.Step();
        const double margin = 1.0;
        foreach (var p in m.GetSnapshot().Players)
        {
            Assert.True(Math.Abs(p.Pos.X) <= Pitch.HalfLength + margin);
            Assert.True(Math.Abs(p.Pos.Y) <= Pitch.HalfWidth + margin);
        }
    }

    [Fact]
    public void SameSeedGivesEqualSnapshots()
    {
        var a = Make(42); var b = Make(42);
        for (int i = 0; i < 600; i++) { a.Step(); b.Step(); }
        Assert.Equal(a.GetSnapshot(), b.GetSnapshot());
    }

    [Fact]
    public void SnapshotIsImmutableCopy()
    {
        var m = Make(1);
        var before = m.GetSnapshot();
        m.Step();
        Assert.Equal(0, before.Tick);
    }
}
