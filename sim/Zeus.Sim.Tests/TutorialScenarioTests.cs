namespace Zeus.Sim.Tests;

public sealed class TutorialScenarioTests
{
    static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "tactics"))) dir = dir.Parent;
        return dir!.FullName;
    }

    [Theory]
    [InlineData("en")]
    [InlineData("nl")]
    public void ThreeScenariosLoadInOrderWithTextsInBothLanguages(string language)
    {
        var specs = TutorialScenarios.Load(Path.Combine(Root(), "data", "tutorial"), language);
        Assert.Equal(TutorialScenarios.Ids, specs.Select(s => s.Id));
        Assert.All(specs, s => { Assert.True(s.HasScenario); Assert.NotEmpty(s.Name); Assert.NotEmpty(s.Hint); Assert.Equal(1, s.Matches); });
        Assert.NotEqual(TutorialScenarios.Load(Path.Combine(Root(), "data", "tutorial"), "en")[0].Name, TutorialScenarios.Load(Path.Combine(Root(), "data", "tutorial"), "nl")[0].Name);
    }

    [Fact]
    public void EachScenarioStartsAsDescribedAndPlaysToTheEnd()
    {
        var specs = TutorialScenarios.Load(Path.Combine(Root(), "data", "tutorial"));
        var expected = new[] { (1, 0, 75), (0, 1, 65), (0, 0, 80) };
        for (int i = 0; i < specs.Count; i++)
        {
            var spec = specs[i];
            var player = TacticLoader.LoadFile(Path.Combine(Root(), "tactics", spec.StartTactic));
            var opponent = TacticLoader.LoadFile(Path.Combine(Root(), "tactics", spec.Opponent));
            var match = Match.Create(new MatchConfig(), player, opponent, 1,
                homeSquad: SquadFactory.ForLevel(spec.PlayerLevel, player, Path.Combine(Root(), "squads")),
                awaySquad: SquadFactory.ForLevel(spec.OpponentLevel, opponent, Path.Combine(Root(), "squads")));
            spec.ApplyScenario(match);
            Assert.Equal((expected[i].Item1, expected[i].Item2), (match.HomeScore, match.AwayScore));
            Assert.True(match.TimeSeconds >= expected[i].Item3 * 60 - 1);
            for (int tick = 0; tick < 150000 && match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned); tick++) match.Step();
            Assert.Equal(MatchStatus.Finished, match.Status);
        }
    }
}
