using Xunit;

namespace Zeus.Sim.Tests;

public class ShootoutTests
{
    static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Zeus.sln"))) dir = Path.GetDirectoryName(dir);
        return dir!;
    }

    static Squad Fitted(string level) => SquadFactory.ForLevel(level, TacticLoader.LoadFile(Path.Combine(Root(), "tactics", "laag_blok_442.json")), Path.Combine(Root(), "squads"))!;

    [Fact]
    public void AShootoutIsSeededEndsWithAWinnerAndFollowsTheRules()
    {
        var home = Fitted("halfprof"); var away = Fitted("amateur");
        int homeWins = 0;
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var r = Shootout.Play(home, away, seed);
            var again = Shootout.Play(home, away, seed);
            Assert.Equal(r.Home, again.Home); Assert.Equal(r.Away, again.Away); Assert.Equal(r.Winner, again.Winner);      // seeded: same kicks, same winner
            Assert.InRange(r.Home.Count, 3, Shootout.MaxKicks);
            Assert.True(r.Home.Count == r.Away.Count || r.Home.Count == r.Away.Count + 1);
            if (r.Home.Count == r.Away.Count) Assert.True(r.Winner == Side.Home ? r.HomeScored > r.AwayScored : r.AwayScored > r.HomeScored, $"seed {seed}");
            else Assert.True(r.Winner == Side.Home       // decided before the away kick: the leader cannot be caught any more
                ? r.HomeScored > r.AwayScored + Math.Max(0, Shootout.Regular - r.Away.Count)
                : r.AwayScored > r.HomeScored + Math.Max(0, Shootout.Regular - r.Home.Count), $"seed {seed}");
            if (r.Winner == Side.Home) homeWins++;
        }
        Assert.InRange(homeWins, 100, 190);      // the better takers and keeper win more often, not always
        var neutral = Shootout.Play(Squad.NeutralFor(TacticLoader.LoadFile(Path.Combine(Root(), "tactics", "counter_532.json"))), home, 3);
        Assert.True(neutral.HomeScored != neutral.AwayScored || neutral.Home.Count != neutral.Away.Count);
    }
}
