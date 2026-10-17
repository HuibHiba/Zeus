using Zeus.Sim;

namespace Zeus.Sim.Tests;

public class LevelCalibrationTests
{
    [Theory]
    [InlineData("amateur", "amateur")]
    [InlineData("halfprof", "halfprof")]
    [InlineData("prof", "prof")]
    [InlineData("prof", "amateur")]
    public void ExampleLevelsKeepScoresAndAdvantageWithinCalibrationBounds(string levelA, string levelB)
    {
        int total = 0, wins = 0, games = 0;
        foreach (string name in new[] { "hoge_druk_433", "positiespel_4231", "counter_532" })
        {
            var tactic = TacticLoader.LoadFile(TacticLoaderTests.TacticPath(name));
            string directory = Path.GetDirectoryName(SquadTests.SquadPath("amateur"))!;
            var a = SquadFactory.ForLevel(levelA, tactic, directory);
            var b = SquadFactory.ForLevel(levelB, tactic, directory);
            Assert.NotNull(a);
            Assert.NotNull(b);
            // Venue-balanced regression guard: the first six seeds of the independent 401..420 validation block
            // (docs/kalibratie.md, "Balbeeld van verdedigers"). The former two seeds (6 matches per pair) no longer
            // represented the behaviour after the movement changes; bounds are unchanged. Statistical evidence comes
            // from the full Runner batch.
            for (ulong seed = 401; seed <= 406; seed++)
            {
                bool swap = levelA != levelB && seed % 2 == 0;
                var match = Match.Create(new(), tactic, tactic, seed,
                    homeSquad: swap ? b : a, awaySquad: swap ? a : b);
                for (int tick = 0; tick < 150000 && match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned); tick++)
                    match.Step();
                Assert.Equal(MatchStatus.Finished, match.Status);
                int ga = swap ? match.AwayScore : match.HomeScore;
                int gb = swap ? match.HomeScore : match.AwayScore;
                Assert.True(Math.Abs(ga - gb) < 8, $"{name}, seed {seed}: {ga}-{gb}");
                total += ga + gb;
                if (ga > gb) wins++;
                games++;
            }
        }
        Assert.InRange(total / (double)games, 2.2, 3.2);
        if (levelA != levelB) Assert.InRange(wins / (double)games, .55, .75);
    }

    [Fact]
    public void SubTickAnticipationDeficitDoesNotCostAWholeDecisionTick()
    {
        var tactic = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442")) with { Triggers = [] };
        var squad = Squad.NeutralFor(tactic);
        squad = squad with { Starters = squad.Starters.Select(p => p with {
            Attributes = p.Attributes with { Anticipation = .56 } }).ToArray() };
        var config = new MatchConfig(HalfSeconds: 60,
            Attributes: AttributeConfig.Disabled with { Anticipation = true });
        var baseline = Match.Create(config, tactic, tactic, 7);
        var supplied = Match.Create(config, tactic, tactic, 7, homeSquad: squad, awaySquad: squad);
        for (int tick = 0; tick < 3000 && baseline.Status != MatchStatus.Finished; tick++)
        {
            baseline.Step(); supplied.Step();
            Assert.Equal(baseline.GetSnapshot(), supplied.GetSnapshot());
        }
        Assert.Equal(MatchStatus.Finished, supplied.Status);
        Assert.Equal(baseline.EventHash(), supplied.EventHash());
    }
}
