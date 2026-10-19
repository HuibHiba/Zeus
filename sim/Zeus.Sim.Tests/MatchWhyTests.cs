using Xunit;

namespace Zeus.Sim.Tests;

public class MatchWhyTests
{
    static TeamStatistics Team(int shots, int goals, int controls = 300, int fouls = 8, int red = 0, int interceptions = 15) =>
        new(Passes: 300, BallControls: controls, Interceptions: interceptions, Fouls: fouls, YellowCards: 1, RedCards: red, Shots: shots, Goals: goals);

    [Fact]
    public void ARedCardAndNoChancesAreTheStrongestReasonsForALoss()
    {
        var stats = new MatchStatistics(5400, Team(4, 0, controls: 200, red: 1), Team(20, 3, controls: 400));
        var why = MatchWhy.Causes(stats, Side.Home);
        Assert.Equal(MatchWhy.MaxCauses, why.Count);
        Assert.Equal("red_card", why[0].Key);
        Assert.Contains(why, c => c.Key == "few_shots" && c.Value == 4 && !c.Good);
        Assert.All(why, c => Assert.False(c.Good));
        // The same match seen from the other side reads as strengths.
        var theirs = MatchWhy.Causes(stats, Side.Away);
        Assert.Contains(theirs, c => c.Key == "good_shots" && c.Good);
        Assert.Equal("good_defence", theirs[0].Key);      // only 4 shots against outweighs the rest
        Assert.All(theirs, c => Assert.True(c.Good));
    }

    [Fact]
    public void AnEvenMatchGivesNoCausesAndWastedChancesAreNamed()
    {
        Assert.Empty(MatchWhy.Causes(new MatchStatistics(5400, Team(12, 1), Team(12, 1)), Side.Home));
        var wasted = MatchWhy.Causes(new MatchStatistics(5400, Team(14, 0), Team(10, 1)), Side.Home);
        Assert.Equal("no_finish", Assert.Single(wasted).Key);
        Assert.Equal(14, wasted[0].Value);
    }
}
