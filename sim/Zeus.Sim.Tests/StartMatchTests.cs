namespace Zeus.Sim.Tests;

/// <summary>
/// Guards the first menu match (godot/GameSession.cs: DefaultHomeId/DefaultAwayId, Seed = 12, neutral level): Positiespel 4-2-3-1
/// v Vleugelspel 4-3-3 must stay a readable football match on the seeds a new player sees first (the menu adds 1 per match).
/// With the RC7 offside fix: 3, 4, 5, 3, 2 goals and at most 21 shots per team on seeds 12-16 (the old window 2-6 held a 4-3).
/// See docs/kalibratie.md, "Buitenspel aan voor RC7".
/// </summary>
public class StartMatchTests
{
    [Fact]
    public void DefaultMenuMatchIsNotLopsided()
    {
        var home = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("positiespel_4231"));
        var away = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("vleugelspel_433"));
        int total = 0;
        for (ulong seed = 12; seed <= 16; seed++)
        {
            var match = Match.Create(new MatchConfig(), home, away, seed);
            for (int i = 0; i < 150000 && match.Status != MatchStatus.Finished; i++) match.Step();
            Assert.Equal(MatchStatus.Finished, match.Status);
            var stats = MatchStatistics.FromMatch(match);
            int goals = stats.Home.Goals + stats.Away.Goals;
            Assert.True(goals <= 6, $"seed {seed}: {stats.Home.Goals}-{stats.Away.Goals}");
            Assert.All(new[] { stats.Home, stats.Away }, team => Assert.InRange(team.Shots, 5, 40));
            total += goals;
        }
        Assert.InRange(total / 5.0, 1.0, 5.0);
    }
}
