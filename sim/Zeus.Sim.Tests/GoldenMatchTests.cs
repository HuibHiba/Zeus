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
        new object[] { "laag_blok_442", "man_om_man_433", 42UL, "5fee50d5d622b3c04e470a4afaf1d34838d7ffed5f9bbdde75efc516d7dcb639", "85a79009442526e04eaf2bb63d0a53238e99c350caaccc2fcaee056b7b7fc887" },
        new object[] { "laag_blok_442", "laag_blok_442", 7UL, "b22e81f3212895cc8fad919e27c152ada9bad17caef6547ce1644f0a95592606", "cb793ca15fd88a0e3797773a0ec2047dea8dcde007e151487f2e7ee0acaef095" },
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
