namespace Zeus.Sim.Tests;

public class DailyChallengeTests
{
    static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Zeus.sln"))) dir = Path.GetDirectoryName(dir);
        return dir!;
    }

    [Fact]
    public void OnlyTodaysDailyStarsCountTowardUnlocks()
    {
        var today = new DateOnly(2026, 10, 26);
        var progress = new[] { ("01_houd_de_nul", 3), (DailyChallenge.IdFor(today.AddDays(-1)), 3),
            (DailyChallenge.IdFor(today), 2), (DailyChallenge.IdFor(today.AddDays(1)), 1), ("dag_kapot", 3) };
        Assert.Equal(5, TacticUnlocks.TotalStars(progress, today));
        Assert.Equal(4, TacticUnlocks.TotalStars(progress, today.AddDays(1)));
        Assert.Equal(3, TacticUnlocks.TotalStars(progress, today.AddDays(2)));
        Assert.Equal(0, TacticUnlocks.TotalStars(Array.Empty<(string, int)>(), today));
    }

    [Fact]
    public void TheDayDecidesEverythingAndEveryDayIsAValidChallenge()
    {
        var a = DailyChallenge.ForDate(new DateOnly(2026, 10, 19));
        Assert.Equal("dag_20261019", a.Id);
        Assert.Equal(a.Id + a.Opponent + a.OpponentLevel + a.StartMinute, DailyChallenge.FromId(a.Id)!.Id + DailyChallenge.FromId(a.Id)!.Opponent + DailyChallenge.FromId(a.Id)!.OpponentLevel + DailyChallenge.FromId(a.Id)!.StartMinute);
        Assert.Null(DailyChallenge.FromId("01_houd_de_nul"));
        Assert.Null(DailyChallenge.FromId("dag_abc"));
        Assert.Equal(2, a.Criteria.Count);
        Assert.Equal(2, a.MaxStars);
        Assert.Equal("Vandaag", a.Theme);
        Assert.Equal("Today", DailyChallenge.ForDate(new DateOnly(2026, 10, 19), "en").Theme);
        int scenarios = 0;
        var ids = new HashSet<string>();
        var combos = new HashSet<string>();
        for (int day = 0; day < 120; day++)
        {
            var spec = DailyChallenge.ForDate(new DateOnly(2026, 1, 1).AddDays(day));
            Assert.True(ids.Add(spec.Id));
            combos.Add(spec.Opponent + spec.OpponentLevel + spec.Criteria[0].Metric);
            Assert.True(File.Exists(Path.Combine(Root(), "tactics", spec.Opponent)), spec.Opponent);
            Assert.Contains(spec.OpponentLevel, SquadFactory.Levels);
            Assert.NotEqual(spec.Criteria[0].Metric, spec.Criteria[1].Metric);
            Assert.All(spec.Criteria, c => Assert.Contains(c.Metric, ChallengeEvaluator.Metrics.Keys));
            if (spec.HasScenario) scenarios++;
            if (spec.StartMinute > 0) Assert.InRange(spec.StartMinute, Match.MinScenarioMinute, Match.MaxScenarioMinute);
        }
        Assert.InRange(scenarios, 20, 75);      // about two in five days
        Assert.True(combos.Count > 40, "the days vary");
    }
}
