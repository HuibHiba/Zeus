using Xunit;

namespace Zeus.Sim.Tests;

public class ChallengeTests
{
    static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Zeus.sln"))) dir = Path.GetDirectoryName(dir);
        return dir!;
    }

    static ChallengeMatchResult R(int gf, int ga, int sf = 10, int sa = 10, int cf = 100, int ca = 100, int fouls = 8, int yellow = 1, int red = 0) =>
        new(gf, ga, sf, sa, cf, ca, fouls, yellow, red);

    [Fact]
    public void AllShippedChallengesParseAndTheirOpponentsExist()
    {
        var specs = ChallengeEvaluator.LoadAll(Path.Combine(Root(), "challenges"));
        Assert.True(specs.Count >= 5);
        Assert.Equal(specs.Count, specs.Select(s => s.Id).Distinct().Count());
        foreach (var s in specs)
        {
            Assert.True(File.Exists(Path.Combine(Root(), "tactics", s.Opponent)), $"{s.Id}: opponent {s.Opponent} missing");
            TacticLoader.LoadFile(Path.Combine(Root(), "tactics", s.Opponent));
            Assert.NotEmpty(s.Criteria);
        }
    }

    [Fact]
    public void ParseRejectsUnknownMetricsOperatorsAndMissingParts()
    {
        const string ok = """{"id":"x","naam":"X","tegenstander":"a.json","criteria":[{"maat":"winst","operator":">=","waarde":1}]}""";
        Assert.Equal(5, ChallengeEvaluator.Parse(ok).Matches);
        Assert.Throws<FormatException>(() => ChallengeEvaluator.Parse(ok.Replace("winst", "magie")));
        Assert.Throws<FormatException>(() => ChallengeEvaluator.Parse(ok.Replace(">=", "==")));
        Assert.Throws<FormatException>(() => ChallengeEvaluator.Parse("""{"id":"x","tegenstander":"a.json","criteria":[]}"""));
        Assert.Throws<FormatException>(() => ChallengeEvaluator.Parse("""{"id":"","tegenstander":"a.json","criteria":[{"maat":"winst","operator":">=","waarde":1}]}"""));
        Assert.Throws<FormatException>(() => ChallengeEvaluator.Parse(ok.Replace("\"tegenstander\":\"a.json\"", "\"tegenstander\":\"\"")));
        Assert.Throws<FormatException>(() => ChallengeEvaluator.Parse(ok.Replace("\"criteria\"", "\"wedstrijden\":99,\"criteria\"")));
    }

    [Fact]
    public void MetricsAreAveragesCountsAndPossessionShare()
    {
        var results = new[] { R(2, 0, cf: 300, ca: 100, red: 1), R(0, 1, cf: 100, ca: 100), R(1, 1, cf: 200, ca: 100) };
        Assert.Equal(1.0, ChallengeEvaluator.Value("doelpunten_voor", results), 6);
        Assert.Equal(2.0 / 3, ChallengeEvaluator.Value("doelpunten_tegen", results), 6);
        Assert.Equal(1.0 / 3, ChallengeEvaluator.Value("doelsaldo", results), 6);
        Assert.Equal(100.0 * 600 / 900, ChallengeEvaluator.Value("balbezit", results), 6);
        Assert.Equal(1, ChallengeEvaluator.Value("winst", results));
        Assert.Equal(2, ChallengeEvaluator.Value("ongeslagen", results));
        Assert.Equal(1, ChallengeEvaluator.Value("rode_kaarten", results));
        Assert.Equal(0, ChallengeEvaluator.Value("winst", Array.Empty<ChallengeMatchResult>()));
        Assert.Throws<ArgumentException>(() => ChallengeEvaluator.Value("magie", results));
    }

    [Fact]
    public void EvaluateMarksEveryCriterionAndTheOverallResult()
    {
        var spec = new ChallengeSpec("t", "T", "", "a.json", 3, new[]
        {
            new ChallengeCriterion("doelpunten_tegen", "<=", 1.0), new ChallengeCriterion("winst", ">=", 2),
        }, "");
        var good = ChallengeEvaluator.Evaluate(spec, new[] { R(2, 0), R(1, 0), R(0, 0) });
        Assert.True(good.Passed);
        Assert.Equal((2, 1, 0), (good.Wins, good.Draws, good.Losses));
        Assert.All(good.Criteria, c => Assert.True(c.Passed));

        var bad = ChallengeEvaluator.Evaluate(spec, new[] { R(2, 3), R(1, 0), R(0, 2) });
        Assert.False(bad.Passed);
        Assert.False(bad.Criteria[0].Passed);
        Assert.False(bad.Criteria[1].Passed);
        Assert.Contains("hoogstens", bad.Criteria[0].Text);
        Assert.Contains("minstens", bad.Criteria[1].Text);

        Assert.False(ChallengeEvaluator.Evaluate(new ChallengeSpec("e", "E", "", "a.json", 1, Array.Empty<ChallengeCriterion>(), ""), new[] { R(1, 0) }).Passed);
    }

    [Fact]
    public void PlayRunsTheRequestedNumberOfMatchesDeterministically()
    {
        var spec = new ChallengeSpec("p", "P", "", "man_om_man_433.json", 1, new[] { new ChallengeCriterion("winst", ">=", 0) }, "");
        var player = TacticLoader.LoadFile(Path.Combine(Root(), "tactics", "laag_blok_442.json"));
        var opponent = TacticLoader.LoadFile(Path.Combine(Root(), "tactics", spec.Opponent));
        var first = ChallengeEvaluator.Play(spec, player, opponent);
        var second = ChallengeEvaluator.Play(spec, player, opponent);
        Assert.Single(first);
        Assert.Equal(first, second);
        Assert.True(first[0].ControlsFor > 0);
    }

    [Fact]
    public void PlayCanBeCancelled()
    {
        var spec = new ChallengeSpec("p", "P", "", "man_om_man_433.json", 1, new[] { new ChallengeCriterion("winst", ">=", 0) }, "");
        var player = TacticLoader.LoadFile(Path.Combine(Root(), "tactics", "laag_blok_442.json"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => ChallengeEvaluator.Play(spec, player, player, cts.Token));
    }
}
