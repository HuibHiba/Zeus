using Xunit;

namespace Zeus.Sim.Tests;

public class OpponentPlannerTests
{
    static Tactic Load(string name) => TacticLoader.LoadFile(TacticLoaderTests.TacticPath(name));

    static HistoryEntry Entry(Tactic t) => new(t.Name, StyleProfile.From(t));

    [Fact]
    public void StyleIsReadFromBlockPressingAndMarking()
    {
        var low = StyleProfile.From(Load("laag_blok_442"));
        Assert.Equal("laag", low.Block);
        Assert.Equal(0, low.Markers);
        Assert.True(low.Pressing < 0.35);
        var man = StyleProfile.From(Load("man_om_man_433"));
        Assert.True(man.Markers >= 6);
        Assert.Contains("man-dekking", man.Describe());
        Assert.Contains("laag blok", low.Describe());
    }

    [Fact]
    public void DistanceIsZeroForTheSameStyleAndLargeForOppositeStyles()
    {
        var low = StyleProfile.From(Load("laag_blok_442"));
        var high = StyleProfile.From(Load("hoge_druk_433"));
        Assert.Equal(0, low.DistanceTo(low), 9);
        Assert.True(low.DistanceTo(high) > 0.4, $"distance {low.DistanceTo(high)}");
        Assert.Equal(low.DistanceTo(high), high.DistanceTo(low), 9);
    }

    [Fact]
    public void OpponentPreparesOnlyWhenThreeOfTheLastFiveMatchesLookAlike()
    {
        var low = Load("laag_blok_442");
        var high = Load("hoge_druk_433");
        var style = StyleProfile.From(low);
        Assert.False(OpponentPlanner.ShouldPrepare(Array.Empty<HistoryEntry>(), style));
        Assert.False(OpponentPlanner.ShouldPrepare(new[] { Entry(low), Entry(low) }, style));
        Assert.True(OpponentPlanner.ShouldPrepare(new[] { Entry(low), Entry(low), Entry(low) }, style));
        Assert.True(OpponentPlanner.ShouldPrepare(new[] { Entry(high), Entry(low), Entry(high), Entry(low), Entry(low) }, style));
        Assert.False(OpponentPlanner.ShouldPrepare(new[] { Entry(low), Entry(low), Entry(high), Entry(high), Entry(high) }, style));
        // old matches fall out of the window of five
        Assert.False(OpponentPlanner.ShouldPrepare(new[] { Entry(low), Entry(low), Entry(low), Entry(high), Entry(high), Entry(high), Entry(high) }, style));
    }

    [Fact]
    public void NoPreparationKeepsTheDefaultOpponent()
    {
        var low = Load("laag_blok_442");
        var pool = new[] { ("man_om_man_433", Load("man_om_man_433")) };
        var plan = OpponentPlanner.Plan(low, pool, "hoge_druk_433.json", new[] { Entry(low) });
        Assert.False(plan.Prepared);
        Assert.Equal("hoge_druk_433.json", plan.OpponentName);
        Assert.Empty(plan.Scores);
        Assert.Contains("gewone tactiek", plan.Reason);
    }

    [Fact]
    public void PreparedOpponentPicksTheBestPracticeResultDeterministicallyWithAReason()
    {
        var low = Load("laag_blok_442");
        var pool = new[] { ("hoge_druk_433", Load("hoge_druk_433")), ("man_om_man_433", Load("man_om_man_433")), ("laag_blok_442", low) };
        var history = new[] { Entry(low), Entry(low), Entry(low) };
        var first = OpponentPlanner.Plan(low, pool, "x.json", history, matchesPerCandidate: 1);
        var second = OpponentPlanner.Plan(low, pool, "x.json", history, matchesPerCandidate: 1);
        Assert.True(first.Prepared);
        Assert.Equal(first.OpponentName, second.OpponentName);
        Assert.Equal(first.Scores, second.Scores);
        Assert.DoesNotContain(first.Scores, s => s.Name == "laag_blok_442");      // the player's own tactic is not a candidate
        Assert.Equal(2, first.Scores.Count);
        var best = first.Scores.OrderByDescending(s => s.Points).ThenByDescending(s => s.GoalDifference).ThenBy(s => s.Name, StringComparer.Ordinal).First();
        Assert.Equal(best.Name, first.OpponentName);
        Assert.Contains(first.OpponentName, first.Reason);
        Assert.Contains("laag blok", first.Reason);
    }

    [Fact]
    public void PlanCanBeCancelled()
    {
        var low = Load("laag_blok_442");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var pool = new[] { ("hoge_druk_433", Load("hoge_druk_433")) };
        Assert.ThrowsAny<OperationCanceledException>(() =>
            OpponentPlanner.Plan(low, pool, "x.json", new[] { Entry(low), Entry(low), Entry(low) }, cts.Token, 1));
    }
}
