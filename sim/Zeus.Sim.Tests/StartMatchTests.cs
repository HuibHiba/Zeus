namespace Zeus.Sim.Tests;

/// <summary>
/// Guards the first menu match (godot/GameSession.cs: DefaultHomeId/DefaultAwayId, Seed = 2, neutral level): Positiespel 4-2-3-1
/// v Vleugelspel 4-3-3 must stay a readable football match on the seeds a new player sees first (the menu adds 1 per match).
/// Measured at RC7: 4, 5, 3, 0, 2 goals and at most 20 shots per team. See docs/kalibratie.md, "RC7: scheve tactieken en startwedstrijd".
/// </summary>
public class StartMatchTests
{
    [Fact]
    public void DefaultMenuMatchIsNotLopsided()
    {
        var home = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("positiespel_4231"));
        var away = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("vleugelspel_433"));
        int total = 0;
        for (ulong seed = 2; seed <= 6; seed++)
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
