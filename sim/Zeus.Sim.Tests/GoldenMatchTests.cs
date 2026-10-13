using Zeus.Sim;
namespace Zeus.Sim.Tests;

/// <summary>
/// Full-match golden hashes. Same seed + same build + same platform/runtime must give these exact hashes. A failure on a
/// machine where the code is unchanged means the simulation is not bit-identical across platforms (Math.Sin/Cos/Atan2/Sqrt),
/// see docs/hervatten.md "Determinisme". After an intended behaviour change, copy the 'actual' hashes into the table below and
/// mention it in the commit message.
/// </summary>
public class GoldenMatchTests
{
    public static IEnumerable<object[]> Cases => new[]
    {
        new object[] { "laag_blok_442", "man_om_man_433", 42UL, "0c7cfa8e2dde6d67d65f551016809cff501b648a15f2ed05f5b9e6b36df014f6", "83b6280daad92723cefe7ab62e671a86ebb4f70b55c54f9d754a47a2d89424b2" },
        new object[] { "laag_blok_442", "laag_blok_442", 7UL, "9f5fbb716b0d259a905bc00364ed5c20091c55ec517c2ce1a36aab877e764923", "79f0fe30463eb1e619800a729dc45a5ee02075672e7cd3640deefb4b41496073" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void FullMatchHashesAreStable(string home, string away, ulong seed, string eventHash, string snapshotHash)
    {
        var match = Match.Create(new MatchConfig(), TacticLoader.LoadFile(TacticLoaderTests.TacticPath(home)),
            TacticLoader.LoadFile(TacticLoaderTests.TacticPath(away)), seed);
        for (int i = 0; i < 150000 && match.Status != MatchStatus.Finished; i++) match.Step();
        Assert.Equal(MatchStatus.Finished, match.Status);
        Assert.Equal(eventHash + "|" + snapshotHash, match.EventHash() + "|" + match.SnapshotHash());
    }
}
