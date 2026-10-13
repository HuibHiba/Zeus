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
        new object[] { "laag_blok_442", "man_om_man_433", 42UL, "05f21fd4214042081fd11c774ebb1c19bba49cb2fb37f25ea635ec433deafad1", "8f1cb8638fb985dceae57a08c6a37b95605e4d6b824d278683e765dcf69fccb6" },
        new object[] { "laag_blok_442", "laag_blok_442", 7UL, "ae336072a6157b36b633bfe2d3b8523fb5166ef712dc0565b279b90d55150595", "02032896caeee4519ec8b31fbda4c537a2bcda0fdc04f5428bdf78e802eb90ab" },
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
