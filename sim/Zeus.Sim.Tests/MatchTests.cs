using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class MatchTests
{
    internal static Match MakeMatch(ulong seed) => Make(seed);
    // Preserve the movement baseline independently of later tactical/energy modules.
    private static Match Make(ulong seed, bool instructions = false) => Match.Create(new MatchConfig(Instructions: instructions ? new() : InstructionConfig.Disabled, EnableFatigue: false, EnableTeamPhases: false),
        HistoricalTactics.Load("laag_blok_442"),
        HistoricalTactics.Load("man_om_man_433"), seed, (_, _) => new FormationBrain());

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KickoffHas22PlayersAtPhasePositions(bool instructions)
    {
        var s = Make(1, instructions).GetSnapshot();
        Assert.Equal(22, s.Players.Count);
        Assert.Equal(11, s.Players.Count(p => p.Team == Side.Home));
        var gk = s.Players.First(p => p.Team == Side.Home && p.Id == "GK");
        Assert.Equal(Pitch.ToMeters(0.04, 0.5, Side.Home).X, gk.Pos.X, 9);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void After600StepsClockIs60Seconds(bool instructions)
    {
        var m = Make(1, instructions);
        for (int i = 0; i < 600; i++) m.Step();
        var s = m.GetSnapshot();
        Assert.Equal(600, s.Tick);
        Assert.Equal(60.0, s.TimeSeconds, 9);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AllPlayersInsidePitch(bool instructions)
    {
        var m = Make(1, instructions);
        for (int i = 0; i < 600; i++) m.Step();
        const double margin = 1.0;
        foreach (var p in m.GetSnapshot().Players)
        {
            Assert.True(Math.Abs(p.Pos.X) <= Pitch.HalfLength + margin);
            Assert.True(Math.Abs(p.Pos.Y) <= Pitch.HalfWidth + margin);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SameSeedGivesEqualSnapshots(bool instructions)
    {
        var a = Make(42, instructions); var b = Make(42, instructions);
        for (int i = 0; i < 600; i++) { a.Step(); b.Step(); }
        Assert.Equal(a.GetSnapshot(), b.GetSnapshot());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SnapshotIsImmutableCopy(bool instructions)
    {
        var m = Make(1, instructions);
        var before = m.GetSnapshot();
        m.Step();
        Assert.Equal(0, before.Tick);
    }
}
