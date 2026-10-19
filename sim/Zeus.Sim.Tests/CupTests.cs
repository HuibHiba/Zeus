using Xunit;

namespace Zeus.Sim.Tests;

/// <summary>Career v1 cup: eight clubs, three simulated rounds due after league rounds 3, 6 and 8.</summary>
public class CupTests
{
    static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Zeus.sln"))) dir = Path.GetDirectoryName(dir);
        return dir!;
    }

    static string Tactics => Path.Combine(Root(), "tactics");
    static string Squads => Path.Combine(Root(), "squads");
    static readonly MatchConfig Short = new(HalfSeconds: 120);

    [Fact]
    public void DrawHasEightDistinctClubsWithThePlayerAndTwoGuestsAndIsSeededPerSeason()
    {
        for (int division = 0; division < Competition.Divisions.Count; division++)
        {
            var draw = Cup.Draw(1, division);
            Assert.Equal(Cup.Teams, draw.Count);
            Assert.Equal(Cup.Teams, draw.Select(c => c.Name).Distinct().Count());
            Assert.Single(draw, c => c.IsPlayer);
            Assert.Equal(2, draw.Count(c => !Competition.Clubs(division).Contains(c)));
            Assert.Equal(draw, Cup.Draw(1, division));
        }
        Assert.NotEqual(Cup.Draw(1, 1).Select(c => c.Name), Cup.Draw(2, 1).Select(c => c.Name));
    }

    [Fact]
    public void RoundsBecomeDueAfterLeagueRoundsThreeSixAndEight()
    {
        var state = new SeasonState();
        Assert.False(Cup.Due(state));
        state.Round = 3;
        Assert.True(Cup.Due(state));
        Assert.NotNull(Cup.PlayerMatch(state));
        Assert.Equal(4, Cup.Pairs(state, 0).Count);
        Cup.PlayRound(state, Tactics, Squads, config: Short);
        Assert.Equal(1, Cup.RoundsPlayed(state));
        Assert.False(Cup.Due(state));          // the semi-final waits for league round 6
        state.Round = 6;
        Assert.True(Cup.Due(state));
        Assert.Equal(2, Cup.Pairs(state, 1).Count);
        Assert.All(Cup.Pairs(state, 1), p => Assert.Contains(state.Cup!, r => r.Winner == p.Home.Name || r.Winner == p.Away.Name));
        Assert.Equal(Cup.PlayerOut(state), Cup.PlayerMatch(state) is null);
        Cup.PlayRound(state, Tactics, Squads, config: Short);
        state.Round = 8;
        Cup.PlayRound(state, Tactics, Squads, config: Short);
        Assert.Equal(Cup.Rounds, Cup.RoundsPlayed(state));
        Assert.False(Cup.Due(state));
        Assert.NotNull(Cup.Winner(state));
        Assert.True(Cup.IsValid(state));
        Assert.Equal(7, state.Cup!.Count);
        Assert.InRange(Cup.PlayerWins(state), 0, 3);
        Assert.Throws<InvalidOperationException>(() => Cup.PlayRound(state, Tactics, Squads, config: Short));
    }

    [Fact]
    public void ACupRoundIsReproducibleAndSurvivesTheSaveFile()
    {
        SeasonState AtRound3()
        {
            var s = new SeasonState { Round = 3 };
            for (int round = 0; round < 3; round++)
                foreach (var f in Competition.RoundFixtures(round)) s.Results.Add(new CompetitionResult(round, f.Home, f.Away, 1, 1));
            return s;
        }
        var a = AtRound3(); var b = AtRound3();
        Cup.PlayRound(a, Tactics, Squads, config: Short);
        Cup.PlayRound(b, Tactics, Squads, config: Short);
        Assert.Equal(a.Cup, b.Cup);
        var back = SeasonState.Parse(a.ToJson());
        Assert.Equal(a.Cup, back.Cup);
        Assert.DoesNotContain("\"Cup\"", new SeasonState().ToJson());
        var tampered = a.ToJson().Replace("\"Winner\": \"" + a.Cup![0].Winner + "\"", "\"Winner\": \"Niemand\"");
        Assert.NotEqual(a.ToJson(), tampered);
        Assert.Throws<FormatException>(() => SeasonState.Parse(tampered));
    }

    [Fact]
    public void TheNextSeasonRecordsTheCupRunAndStartsAFreshCup()
    {
        var state = new SeasonState { Round = Competition.Rounds, Cup = new() { new CupResult(0, Competition.PlayerClubName, "VV Kleiduinen", 2, 0, Competition.PlayerClubName) } };
        for (int round = 0; round < Competition.Rounds; round++)
            foreach (var f in Competition.RoundFixtures(round)) state.Results.Add(new CompetitionResult(round, f.Home, f.Away, 1, 1));
        Competition.StartNextSeason(state);
        Assert.Null(state.Cup);
        Assert.Equal(1, state.History.Single().CupWins);
        Assert.Equal(0, SeasonState.Parse("{\"Version\":2,\"History\":[{\"Season\":1,\"Division\":0,\"Position\":3,\"Outcome\":0}]}").History[0].CupWins);
    }
}
