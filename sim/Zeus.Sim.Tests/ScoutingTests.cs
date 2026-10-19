using Xunit;

namespace Zeus.Sim.Tests;

public class ScoutingTests
{
    static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Zeus.sln"))) dir = Path.GetDirectoryName(dir);
        return dir!;
    }

    [Fact]
    public void TheScoutSeesTheStyleThroughNoiseThatShrinksWithTheLevel()
    {
        var press = TacticLoader.LoadFile(Path.Combine(Root(), "tactics", "hoge_druk_433.json"));
        var block = TacticLoader.LoadFile(Path.Combine(Root(), "tactics", "laag_blok_442.json"));
        var truth = PlayStyleProfile.Sample(press);
        Assert.True(truth.Pressing > 0.5 + Scouting.Band, "the shipped high press really presses");
        int right = 0, sure = 0;
        for (int round = 0; round < 40; round++)
        {
            var report = Scouting.Report(press, 1, round, "prof");
            Assert.Equal(report, Scouting.Report(press, 1, round, "prof"));      // seeded
            Assert.DoesNotContain(report, n => n.Key == "press_low");
            if (report.Any(n => n.Key == "press_high")) right++;
            sure += report.Count(n => n.Sure);
        }
        Assert.Equal(40, right);
        Assert.True(sure > 40, "a professional scout is sure most of the time");
        int amateurSure = Enumerable.Range(0, 40).Sum(round => Scouting.Report(press, 1, round, "amateur").Count(n => n.Sure));
        Assert.True(amateurSure < sure, "an amateur scout is less often sure");
        Assert.Contains(Scouting.Report(block, 1, 0, "prof"), n => n.Key is "line_low" or "press_low");
        Assert.All(Scouting.Report(block, 2, 3, "amateur"), n => Assert.Contains(n.Key, new[] { "press_high", "press_low", "line_high", "line_low", "tempo_high", "tempo_low" }));
    }
}
