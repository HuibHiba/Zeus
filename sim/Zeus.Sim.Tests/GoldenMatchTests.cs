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
        new object[] { "laag_blok_442", "man_om_man_433", 42UL, "418675db369e55829f558d90dd7a0cacbfcccea001bf8869f2eeb353d8e3b11b", "776adb56ff3851f4293150f94bce0a640baac987cdfdbfbf21d0f926829e8d9b" },
        new object[] { "laag_blok_442", "laag_blok_442", 7UL, "77c96fae16aecca19dba51720f692687a9741b7559c7f484f81c7012a0056a66", "94923bb6b9812a0ac946b0542a48306987fff42ef0ea94be1124a687911da615" },
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
