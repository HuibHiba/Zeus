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
        new object[] { "laag_blok_442", "man_om_man_433", 42UL, "b2145825034393184f35a8c1e1f78700296170c110dd6b16c04c94bc6308fe5d", "82bad842ee976a8127c15a825b3151f3ee2060bfa2f605901019c981f9441ac5" },
        new object[] { "laag_blok_442", "laag_blok_442", 7UL, "0840c98c685b4f3d27790a1227015a8bff59727980d9cf0e263a26c8a68f3569", "52263ddaf26c8de6695c60d66baad6cfde2d5f774a08ffb5a42aeb2b746512cf" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void FullMatchHashesAreStable(string home, string away, ulong seed, string eventHash, string snapshotHash)
    {
        var match = Match.Create(new MatchConfig(), TacticLoader.LoadFile(TacticLoaderTests.TacticPath(home)),
            TacticLoader.LoadFile(TacticLoaderTests.TacticPath(away)), seed);
        var possession = new int[2];
        for (int i = 0; i < 150000 && match.Status != MatchStatus.Finished; i++)
        {
            match.Step();
            if (match.Status == MatchStatus.Running) possession[match.Possession == Side.Home ? 0 : 1]++;
        }
        Assert.Equal(MatchStatus.Finished, match.Status);
        var stats = MatchStatistics.FromMatch(match);
        foreach (var team in new[] { stats.Home, stats.Away })
        {
            // The symmetric reference has calibrated ranges; the asymmetric HEAD baseline is already skewed.
            Assert.InRange(team.Passes, home == away ? 250 : 100, home == away ? 700 : 900);
            Assert.InRange(team.Shots, 5, home == away ? 30 : 100);
        }
        Assert.All(possession, ticks => Assert.InRange((double)ticks / possession.Sum(), .10, .90));
        Assert.Equal(eventHash + "|" + snapshotHash, match.EventHash() + "|" + match.SnapshotHash());
    }
}
