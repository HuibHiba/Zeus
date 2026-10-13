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
