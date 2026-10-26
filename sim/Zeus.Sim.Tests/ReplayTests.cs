using System.Text.Json;
using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class ReplayTests
{
    [Fact]
    public void FullTimeReplayUsesCompletedTickCountIncludingFinalWhistle()
    {
        var home = File.ReadAllText(TacticLoaderTests.TacticPath("laag_blok_442"));
        var away = File.ReadAllText(TacticLoaderTests.TacticPath("man_om_man_433"));
        var config = new MatchConfig(HalfSeconds: 1, RestartPauseSeconds: 0.3);
        var match = Match.Create(config, TacticLoader.Parse(home), TacticLoader.Parse(away), 42);
        for (int i = 0; i < 200 && match.Status != MatchStatus.Finished; i++) match.Step();
        Assert.Equal(MatchStatus.Finished, match.Status);
        var replay = ReplayFile.Capture(config, home, away, 42, (int)match.Tick, match.EventHash());
        Assert.Equal(MatchStatus.Finished, replay.Run().Status);
        Assert.Equal(match.SnapshotHash(), replay.Run().SnapshotHash());
    }
    private static ReplayFile Capture()
    {
        var home = File.ReadAllText(TacticLoaderTests.TacticPath("laag_blok_442"));
        var away = File.ReadAllText(TacticLoaderTests.TacticPath("man_om_man_433"));
        var config = new MatchConfig();
        var match = Match.Create(config, TacticLoader.Parse(home), TacticLoader.Parse(away), 42);
        for (int i = 0; i < 300; i++) match.Step();
        return ReplayFile.Capture(config, home, away, 42, 300, match.EventHash());
    }

    [Fact]
    public void ReplayRoundTripsAllInputsAndReproducesEvents()
    {
        var replay = Capture();
        var restored = JsonSerializer.Deserialize<ReplayFile>(JsonSerializer.Serialize(replay))!;
        Assert.Equal(replay.ExpectedEventHash, restored.Run().EventHash());
        Assert.Equal(300, restored.Run().Tick);
    }

    [Fact]
    public void ChangedBuildOrResultIsRejected()
    {
        var replay = Capture();
        Assert.Throws<ArgumentException>(() => (replay with { BuildHash = "other" }).Run());
        Assert.Throws<InvalidOperationException>(() => (replay with { ExpectedEventHash = "other" }).Run());
        Assert.Throws<ArgumentException>(() => (replay with { FormatVersion = 2 }).Run());
    }

    [Fact]
    public void DataHashTracksContentsAndNamesButNotInstallLocationOrFileOrder()
    {
        string root = Path.Combine(Path.GetTempPath(), "zeus-data-" + Guid.NewGuid());
        string a = Path.Combine(root, "a"), b = Path.Combine(root, "b");
        Directory.CreateDirectory(a); Directory.CreateDirectory(b);
        try
        {
            File.WriteAllText(Path.Combine(a, "1.json"), "{}");
            File.WriteAllText(Path.Combine(a, "2.json"), "[]");
            File.WriteAllText(Path.Combine(b, "2.json"), "[]");
            File.WriteAllText(Path.Combine(b, "1.json"), "{}");
            string hash = ReplayFile.DataHash(a);
            Assert.Equal(64, hash.Length);
            Assert.Equal(hash, ReplayFile.DataHash(b));
            File.WriteAllText(Path.Combine(b, "1.json"), "{ }");
            Assert.NotEqual(hash, ReplayFile.DataHash(b));
            File.WriteAllText(Path.Combine(b, "1.json"), "{}");
            File.Move(Path.Combine(b, "1.json"), Path.Combine(b, "3.json"));
            Assert.NotEqual(hash, ReplayFile.DataHash(b));
        }
        finally { Directory.Delete(root, true); }
    }
}
