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
        new object[] { "laag_blok_442", "man_om_man_433", 42UL, "0ce1e97e0e09a70c19bf4f8ed0529f8a415cc638014eaab8e7bb8dfe4f84f610", "ce1713847e3bd0ed00ee38bb8bcb635a71d39ef1d2e7c5a0753f5fbf7f22abbc" },
        new object[] { "laag_blok_442", "laag_blok_442", 7UL, "828be0ca1cab823a05a8ba78ec5ce98fb9aeca5dbe9c1fdb9099cd87cce6dc7c", "3e1efdf5f9c79097ca626fceaf94b0b9601b2108707de498a957fa40421ef71e" },
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
