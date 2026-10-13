using System.Text.Json;
using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class ReplayTests
{
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
}
