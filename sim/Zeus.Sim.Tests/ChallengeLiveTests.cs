using Xunit;

namespace Zeus.Sim.Tests;

public class ChallengeLiveTests
{
    static ChallengeMatchResult R(int gf, int ga, int sf = 10, int sa = 10, int cf = 100, int ca = 100, int fouls = 8, int yellow = 1, int red = 0, bool abandoned = false) =>
        new(gf, ga, sf, sa, cf, ca, fouls, yellow, red, abandoned);

    static ChallengeCriterion C(string m, string o, double v) => new(m, o, v);

    static ChallengeSpec Spec(int matches, params ChallengeCriterion[] criteria) =>
        new("x", "X", "", "a.json", matches, criteria, "");

    [Fact]
    public void StandingAfterAllMatchesEqualsTheSimulationEvaluation()
    {
        var spec = Spec(5, C("doelpunten_tegen", "<=", 1.2), C("winst", ">=", 2), C("balbezit", ">=", 50), C("rode_kaarten", "<=", 1));
        var results = new List<ChallengeMatchResult> { R(2, 0), R(1, 1), R(0, 3, red: 1), R(3, 1), R(1, 0, abandoned: true) };
        var live = ChallengeEvaluator.Standing(spec, results);
        var sim = ChallengeEvaluator.Evaluate(spec, results);
        Assert.True(live.Complete);
        Assert.Equal(sim.Passed, live.Result.Passed);
        Assert.Equal((sim.Wins, sim.Draws, sim.Losses, sim.Abandoned), (live.Result.Wins, live.Result.Draws, live.Result.Losses, live.Result.Abandoned));
        Assert.Equal(sim.Criteria.Select(c => (c.Actual, c.Passed, c.Text)), live.Result.Criteria.Select(c => (c.Actual, c.Passed, c.Text)));
        // With nothing left to play, "possible" is exactly "passed".
        Assert.Equal(sim.Criteria.Select(c => c.Passed), live.Goals.Select(g => g.Possible));
    }

    [Fact]
    public void ResultsAccumulatedOneByOneGiveTheSameVerdictAsAllAtOnce()
    {
        var spec = Spec(3, C("doelsaldo", ">=", 1), C("ongeslagen", ">=", 2));
        var all = new[] { R(2, 0), R(1, 1), R(0, 2) };
        var played = new List<ChallengeMatchResult>();
        ChallengeStanding last = null!;
        foreach (var r in all) { played.Add(r); last = ChallengeEvaluator.Standing(spec, played); }
        Assert.Equal(all.Length, last.Played);
        Assert.Equal(ChallengeEvaluator.Evaluate(spec, all).Passed, last.Result.Passed);
    }

    [Fact]
    public void AverageGoalWithRemainingMatchesIsOnTrackOrStillPossible()
    {
        var spec = Spec(5, C("doelpunten_tegen", "<=", 1.2));
        var good = ChallengeEvaluator.Standing(spec, new[] { R(1, 0) }).Goals[0];
        Assert.True(good.OnTrack); Assert.True(good.Possible);
        // 6 against in the first match: even with four clean sheets the average is 1.2 -> still just possible.
        var edge = ChallengeEvaluator.Standing(spec, new[] { R(0, 6) }).Goals[0];
        Assert.False(edge.OnTrack); Assert.True(edge.Possible);
        // 7 against: 7/5 = 1.4 > 1.2 -> out of reach.
        var gone = ChallengeEvaluator.Standing(spec, new[] { R(0, 7) }).Goals[0];
        Assert.False(gone.OnTrack); Assert.False(gone.Possible);
    }

    [Fact]
    public void CountGoalsBecomeImpossibleWhenTheRemainingMatchesCannotReachThem()
    {
        var spec = Spec(5, C("winst", ">=", 4), C("rode_kaarten", "<=", 0), C("ongeslagen", ">=", 5));
        var s = ChallengeEvaluator.Standing(spec, new[] { R(0, 1, red: 1), R(2, 0) });
        Assert.True(s.Goals[0].Possible);           // one win in two matches, three to go: 1 + 3 = 4 is still reachable
        Assert.False(s.Goals[1].Possible);          // red card already given
        Assert.False(s.Goals[2].Possible);          // one defeat
        Assert.False(s.AllPossible);
    }

    [Fact]
    public void WinGoalStaysPossibleWhileEnoughMatchesRemain()
    {
        var spec = Spec(5, C("winst", ">=", 4));
        Assert.True(ChallengeEvaluator.Standing(spec, new[] { R(0, 1) }).Goals[0].Possible);        // 0 + 4 remaining = 4
        Assert.False(ChallengeEvaluator.Standing(spec, new[] { R(0, 1), R(1, 1) }).Goals[0].Possible);  // 0 + 3 remaining
    }

    [Fact]
    public void PossessionAndLowerBoundGoalsUseTheirOwnBestCase()
    {
        var spec = Spec(2, C("balbezit", ">=", 60), C("schoten_tegen", "<=", 5));
        var s = ChallengeEvaluator.Standing(spec, new[] { R(0, 0, sa: 20, cf: 10, ca: 90) });
        Assert.True(s.Goals[0].Possible);           // possession can still be pulled up with the second match
        Assert.False(s.Goals[1].Possible);          // 20 shots against in match 1 already means an average of at least 10
    }

    [Fact]
    public void MatchesPlayedOneByOneGiveTheSameResultAsThePlayBatch()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Zeus.sln"))) dir = Path.GetDirectoryName(dir);
        var player = TacticLoader.LoadFile(Path.Combine(dir!, "tactics", "laag_blok_442.json"));
        var opponent = TacticLoader.LoadFile(Path.Combine(dir!, "tactics", "testcase_a_433.json"));
        var spec = Spec(2, C("doelpunten_tegen", "<=", 3), C("balbezit", ">=", 10));
        var batch = ChallengeEvaluator.Play(spec, player, opponent);
        var live = new List<ChallengeMatchResult>();
        for (int seed = 1; seed <= spec.Matches; seed++)      // what the live flow does: the real match with seed 1..N, then ToResult on its statistics
        {
            var match = Match.Create(new MatchConfig(), player, opponent, (ulong)seed);
            for (int i = 0; i < 150000 && match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned); i++) match.Step();
            live.Add(ChallengeEvaluator.ToResult(MatchStatistics.FromMatch(match)));
        }
        Assert.Equal(batch, live);
        Assert.Equal(ChallengeEvaluator.Evaluate(spec, batch).Passed, ChallengeEvaluator.Standing(spec, live).Result.Passed);
    }
}
