using System.Text.Json;
using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class StatisticsAndConfigTests
{
    private static Match Create(MatchConfig config, ulong seed = 3) => Match.Create(config,
        TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442")),
        TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433")), seed);

    [Fact]
    public void StatisticsStatusFollowsTheMatchStatus()
    {
        var running = Create(new MatchConfig());
        for (int i = 0; i < 5; i++) running.Step();
        Assert.Equal(MatchStatus.Running, MatchStatistics.FromMatch(running).Status);

        var finished = Create(new MatchConfig(HalfSeconds: 3));
        for (int i = 0; i < 1000 && finished.Status != MatchStatus.Finished; i++) finished.Step();
        Assert.Equal(MatchStatus.Finished, MatchStatistics.FromMatch(finished).Status);

        var abandoned = Create(new MatchConfig());
        var incident = new RefereeIncident(ContactAction.Strike, Conduct.ExcessiveForce, true, true,
            TacticalContext: TacticalContext.NoTacticalOffence, AgainstOpponent: true);
        foreach (var id in new[] { "RB", "RC", "LC", "LB", "RM" })
            abandoned.ReportIncident(Side.Home, id, Vec2.Zero, incident with { BallInPlay = abandoned.Status == MatchStatus.Running });
        Assert.Equal(MatchStatus.Abandoned, abandoned.Status);
        Assert.Equal(MatchStatus.Abandoned, MatchStatistics.FromMatch(abandoned).Status);
    }

    static Match Abandoned(Side offender)
    {
        var blok = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        var mm = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433"));
        var m = Match.Create(new MatchConfig(), offender == Side.Home ? blok : mm, offender == Side.Home ? mm : blok, 3);   // the offender always plays laag_blok_442
        var incident = new RefereeIncident(ContactAction.Strike, Conduct.ExcessiveForce, true, true,
            TacticalContext: TacticalContext.NoTacticalOffence, AgainstOpponent: true);
        foreach (var id in new[] { "RB", "RC", "LC", "LB", "RM" })
            m.ReportIncident(offender, id, Vec2.Zero, incident with { BallInPlay = m.Status == MatchStatus.Running });
        return m;
    }

    [Theory]
    [InlineData(Side.Home, 0, 3)]
    [InlineData(Side.Away, 3, 0)]
    public void AbandonedMatchHasADefinedForfeitResult(Side offender, int home, int away)
    {
        var stats = MatchStatistics.FromMatch(Abandoned(offender));
        Assert.True(stats.Abandoned);
        Assert.Equal(offender, stats.AbandonedBy);
        Assert.Equal((home, away), stats.AwardedGoals());
        var result = ChallengeEvaluator.ToResult(stats);   // no exception: the player (home) gets a result
        Assert.True(result.Abandoned);
        Assert.Equal((home, away), (result.GoalsFor, result.GoalsAgainst));
        Assert.Equal(1, ChallengeEvaluator.Evaluate(new ChallengeSpec("a", "A", "", "x.json", 1, new[] { new ChallengeCriterion("winst", ">=", 0) }, ""), new[] { result }).Abandoned);
    }

    [Fact]
    public void ForfeitOnlyWorsensTheOffendersResultToThreeGoals()
    {
        TeamStatistics T(int goals) => new(0, 0, 0, 0, 0, 0, 0, goals);
        MatchStatistics S(int h, int a, Side by) => new(100, T(h), T(a), MatchStatus.Abandoned, by);
        Assert.Equal((4, 1), S(2, 1, Side.Away).AwardedGoals());       // offender ahead 2-1 -> loses 1-4
        Assert.Equal((4, 1), S(4, 1, Side.Away).AwardedGoals());       // already 3 behind: score stands
        Assert.Equal((5, 0), S(5, 0, Side.Away).AwardedGoals());
        Assert.Equal((2, 5), S(2, 2, Side.Home).AwardedGoals());       // level -> opponent credited up to a 3-goal win
        Assert.Equal((1, 2), new MatchStatistics(100, T(1), T(2), MatchStatus.Finished, Side.Home).AwardedGoals());   // finished: untouched
    }

    [Fact]
    public void ConfigJsonAcceptsPartialOverridesAndKeepsOtherDefaults()
    {
        var config = MatchConfigJson.Parse("{\"Duels\":{\"AttemptRate\":0.2},\"HalfSeconds\":100}");
        Assert.Equal(0.2, config.DuelSettings.AttemptRate);
        Assert.Equal(100, config.HalfSeconds);
        Assert.Equal(new DuelConfig().Reach, config.DuelSettings.Reach);
    }

    [Fact]
    public void ConfigJsonRejectsUnknownKeysAtEveryLevel()
    {
        // A typo must be an error, not a silently ignored setting.
        Assert.Throws<ArgumentException>(() => MatchConfigJson.Parse("{\"HalfSecond\":100}"));
        Assert.Throws<ArgumentException>(() => MatchConfigJson.Parse("{\"Duels\":{\"AttemptRat\":0.2}}"));
        Assert.Throws<ArgumentException>(() => MatchConfigJson.Parse("{\"Sensing\":{}}")); // computed property, not a setting
        Assert.Throws<ArgumentException>(() => MatchConfigJson.Parse("{\"Duels\":{\"Reach\":1,\"Bogus\":2}}"));
        Assert.Throws<ArgumentException>(() => MatchConfigJson.Parse("not json"));
    }

    [Fact]
    public void SerializedConfigRoundTripsThroughTheStrictParser()
    {
        var config = new MatchConfig(Duels: new DuelConfig(AttemptRate: 0.3), Penalties: new PenaltyConfig(AimOffsetMeters: 3.0));
        var back = MatchConfigJson.Parse(JsonSerializer.Serialize(config));
        Assert.Equal(config.DuelSettings.AttemptRate, back.DuelSettings.AttemptRate);
        Assert.Equal(3.0, back.PenaltySettings.AimOffsetMeters);
    }
}
