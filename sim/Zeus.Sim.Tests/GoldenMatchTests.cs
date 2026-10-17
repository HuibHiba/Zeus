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
        new object[] { "laag_blok_442", "man_om_man_433", 42UL, "9a59e28aff6dced136492de073aab9a413da2e5356412474e398c6dd278fdbbb", "106fcb088d6df09aa851b866144cfdbbd9ffbaae07f97e0d88020c564cf4c196" },
        new object[] { "laag_blok_442", "laag_blok_442", 7UL, "9eab77357bc90e76ed389862c1a51b5bae11854dfae84e84016eb9c9b1eed36a", "aadd0cbb5fe2987d47642413064a212d01675c26178831f95cd64c4c3efce763" },
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
