using Xunit;

namespace Zeus.Sim.Tests;

public class CompetitionTests
{
    static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Zeus.sln"))) dir = Path.GetDirectoryName(dir);
        return dir!;
    }

    static string Tactics => Path.Combine(Root(), "tactics");
    static string Squads => Path.Combine(Root(), "squads");
    static readonly MatchConfig Short = new(HalfSeconds: 120);        // short matches keep the season tests fast

    [Fact]
    public void ScheduleIsADoubleRoundRobinWithEveryTeamPlayingOncePerRound()
    {
        var all = Competition.Schedule();
        Assert.Equal(Competition.Rounds * Competition.MatchesPerRound, all.Count);
        Assert.Equal(10, Competition.Rounds);
        foreach (var round in all.GroupBy(f => f.Round))
        {
            var teams = round.SelectMany(f => new[] { f.Home, f.Away }).OrderBy(x => x).ToArray();
            Assert.Equal(Enumerable.Range(0, 6), teams);
        }
        // Every ordered pair (home, away) occurs exactly once: each pair meets twice, once at each ground.
        var pairs = all.Select(f => (f.Home, f.Away)).ToList();
        Assert.Equal(30, pairs.Distinct().Count());
        Assert.All(pairs, p => Assert.NotEqual(p.Home, p.Away));
        Assert.Equal(all.Select(f => (f.Round, f.Home, f.Away)), Competition.Schedule().Select(f => (f.Round, f.Home, f.Away)));
    }

    [Fact]
    public void EveryTeamPlaysFiveHomeAndFiveAwayMatches()
    {
        var all = Competition.Schedule();
        for (int t = 0; t < 6; t++)
        {
            Assert.Equal(5, all.Count(f => f.Home == t));
            Assert.Equal(5, all.Count(f => f.Away == t));
        }
    }

    [Fact]
    public void SeedsAreReproducibleDistinctAndNonZero()
    {
        Assert.Equal(Competition.Seed(1, 3, 0, 4), Competition.Seed(1, 3, 0, 4));
        var seeds = new HashSet<ulong>();
        foreach (var f in Competition.Schedule())
            foreach (var season in new[] { 1, 2 })
                seeds.Add(Competition.Seed(season, f.Round, f.Home, f.Away));
        Assert.Equal(60, seeds.Count);
        Assert.DoesNotContain(0UL, seeds);
    }

    static CompetitionResult R(int round, int home, int away, int hg, int ag) => new(round, home, away, hg, ag);

    [Fact]
    public void StandingsUsePointsThenGoalDifferenceThenGoalsForThenName()
    {
        // Division 0 clubs: 0 FC Zeus, 1 VV Kleiduinen, 2 SV Rietveld, 3 DVC Zandbergen, 4 FC Mulderhoek, 5 SC Hoogeweide.
        var s = new SeasonState { Round = 2 };
        s.Results.AddRange(new[]
        {
            R(0, 0, 1, 2, 0), R(0, 2, 3, 1, 1), R(0, 4, 5, 1, 1),
            R(1, 1, 2, 0, 0), R(1, 3, 4, 3, 3), R(1, 5, 0, 0, 0),
        });
        var table = Competition.Standings(s);
        Assert.Equal(6, table.Count);
        Assert.Equal("FC Zeus", table[0].Name);                  // 4 points, +2
        Assert.Equal(4, table[0].Points);
        Assert.Equal(2, table[0].GoalDifference);
        // Four clubs on 2 points and 0 difference: more goals scored first (Mulderhoek, Zandbergen 4 each), then name.
        Assert.Equal(new[] { "DVC Zandbergen", "FC Mulderhoek" }, table.Skip(1).Take(2).Select(r => r.Name));
        Assert.Equal(new[] { "SC Hoogeweide", "SV Rietveld" }, table.Skip(3).Take(2).Select(r => r.Name).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal("VV Kleiduinen", table[5].Name);            // 1 point, -2
        Assert.All(table, r => Assert.Equal(2, r.Played));
    }

    static SeasonState Finished(int division, int playerPosition)
    {
        // Constructs a finished season where the player ends exactly at the wanted position: it beats the clubs ranked below it, and loses to those above.
        var s = new SeasonState { Division = division, Round = Competition.Rounds };
        var order = Enumerable.Range(1, 5).ToList();            // opponents, strongest first
        order.Insert(playerPosition - 1, 0);
        int Rank(int club) => order.IndexOf(club);
        foreach (var f in Competition.Schedule())
        {
            bool homeBetter = Rank(f.Home) < Rank(f.Away);
            s.Results.Add(R(f.Round, f.Home, f.Away, homeBetter ? 1 : 0, homeBetter ? 0 : 1));
        }
        return s;
    }

    [Theory]
    [InlineData(0, 1, SeasonOutcome.Promoted, 1)]
    [InlineData(0, 2, SeasonOutcome.Promoted, 1)]
    [InlineData(0, 3, SeasonOutcome.Stayed, 0)]
    [InlineData(0, 6, SeasonOutcome.Stayed, 0)]          // bottom division: no relegation
    [InlineData(1, 2, SeasonOutcome.Promoted, 2)]
    [InlineData(1, 5, SeasonOutcome.Stayed, 1)]
    [InlineData(1, 6, SeasonOutcome.Relegated, 0)]
    [InlineData(2, 1, SeasonOutcome.Stayed, 2)]          // top division: no promotion
    [InlineData(2, 6, SeasonOutcome.Relegated, 1)]
    public void PromotionAndRelegationFollowThePositionExceptAtTheEdges(int division, int position, SeasonOutcome outcome, int nextDivision)
    {
        var s = Finished(division, position);
        Assert.Equal(position, Competition.Summarize(s).Position);
        var summary = Competition.StartNextSeason(s);
        Assert.Equal(outcome, summary.Outcome);
        Assert.Equal(nextDivision, s.Division);
        Assert.Equal(2, s.Season);
        Assert.Equal(0, s.Round);
        Assert.Empty(s.Results);
        Assert.Single(s.History);
        Assert.Equal(position, s.History[0].Position);
    }

    [Fact]
    public void AllClubsAreFictionalUniqueAndTheirTacticsExist()
    {
        var names = new List<string> { Competition.PlayerClubName };
        foreach (var d in Competition.Divisions)
        {
            Assert.Equal(Competition.TeamsPerDivision - 1, d.Opponents.Count);
            Assert.Contains(d.Level, SquadFactory.Levels);
            foreach (var c in d.Opponents)
            {
                names.Add(c.Name);
                TacticLoader.LoadFile(Path.Combine(Tactics, c.TacticFile!));
            }
        }
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void StateRoundTripsThroughJson()
    {
        var s = Finished(1, 3);
        s.Round = 4; s.Results = s.Results.Take(4 * 3).ToList(); s.PlayerTactic = "counter_532.json";
        s.History.Add(new SeasonRecord(1, 0, 2, SeasonOutcome.Promoted));
        var back = SeasonState.Parse(s.ToJson());
        Assert.Equal(s.ToJson(), back.ToJson());
        Assert.Equal(1, back.Division);
        Assert.Equal(SeasonOutcome.Promoted, back.History[0].Outcome);
    }

    [Theory]
    [InlineData("")]
    [InlineData("niet json")]
    [InlineData("null")]
    [InlineData("""{"Division":9}""")]
    [InlineData("""{"Round":3}""")]                     // 3 rounds but no results
    [InlineData("""{"Round":1,"Results":[{"Round":0,"Home":0,"Away":0,"HomeGoals":1,"AwayGoals":0},{"Round":0,"Home":2,"Away":3,"HomeGoals":1,"AwayGoals":0},{"Round":0,"Home":4,"Away":5,"HomeGoals":1,"AwayGoals":0}]}""")]
    public void DamagedStateIsRejectedWithAFormatException(string json) =>
        Assert.Throws<FormatException>(() => SeasonState.Parse(json));

    [Fact]
    public void NextLiveMatchUsesThePlayersTacticAndTheDivisionLevel()
    {
        var s = new SeasonState { PlayerTactic = "counter_532.json" };
        var live = Competition.NextLiveMatch(s)!;
        Assert.Equal("amateur", live.Level);
        Assert.Equal("counter_532.json", live.PlayerIsHome ? live.HomeTactic : live.AwayTactic);
        Assert.Equal(Competition.Seed(1, 0, live.Fixture.Home, live.Fixture.Away), live.Seed);
        s.Round = Competition.Rounds;
        Assert.Null(Competition.NextLiveMatch(s));
    }

    [Fact]
    public void PlayRoundIsReproducibleAndUsesAGivenPlayerResult()
    {
        var a = new SeasonState();
        var b = new SeasonState();
        Competition.PlayRound(a, Tactics, Squads, config: Short);
        Competition.PlayRound(b, Tactics, Squads, config: Short);
        Assert.Equal(1, a.Round);
        Assert.Equal(3, a.Results.Count);
        Assert.Equal(a.ToJson(), b.ToJson());

        var c = new SeasonState();
        var player = Competition.PlayerFixture(0);
        Competition.PlayRound(c, Tactics, Squads, (7, 5), config: Short);
        var mine = c.Results.Single(r => r.Home == player.Home && r.Away == player.Away);
        Assert.Equal((7, 5), (mine.HomeGoals, mine.AwayGoals));
        // The other two matches are the same as when everything is simulated.
        Assert.Equal(a.Results.Where(r => r.Home != 0 && r.Away != 0), c.Results.Where(r => r.Home != 0 && r.Away != 0));
    }

    [Fact]
    public void AWholeSeasonCanBeSimulatedAndThenContinuedInTheNextOne()
    {
        var s = new SeasonState();
        var rounds = new List<int>();
        Competition.PlayRest(s, Tactics, Squads, roundDone: rounds.Add, config: Short);
        Assert.True(Competition.IsFinished(s));
        Assert.Equal(Enumerable.Range(1, 10), rounds);
        Assert.Equal(30, s.Results.Count);
        var table = Competition.Standings(s);
        Assert.All(table, r => Assert.Equal(10, r.Played));
        Assert.Equal(s.Results.Sum(r => r.HomeGoals + r.AwayGoals), table.Sum(r => r.GoalsFor));
        Assert.Equal(table.Sum(r => r.GoalsFor), table.Sum(r => r.GoalsAgainst));
        var summary = Competition.StartNextSeason(s);
        Assert.Equal(2, s.Season);
        Assert.Equal(summary.Outcome == SeasonOutcome.Promoted ? 1 : 0, s.Division);
        Assert.Throws<InvalidOperationException>(() => Competition.Summarize(s));
    }

    [Fact]
    public void CancellationStopsASeasonSimulation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var s = new SeasonState();
        Assert.ThrowsAny<OperationCanceledException>(() => Competition.PlayRest(s, Tactics, Squads, cts.Token, config: Short));
        Assert.Equal(0, s.Round);
    }

    static SeasonState OneRoundPlayed()
    {
        var s = new SeasonState();
        Competition.PlayRound(s, Tactics, Squads, config: Short);
        return s;
    }

    [Fact]
    public void ParseRoundTripsAPlayedState()
    {
        var s = OneRoundPlayed();
        var back = SeasonState.Parse(s.ToJson());
        Assert.Equal(1, back.Round);
        Assert.Equal(3, back.Results.Count);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"Version\":3}")]
    [InlineData("{\"PlayerTactic\":\"..\\x.json\"}")]
    [InlineData("{\"PlayerTactic\":\"a/b.json\"}")]
    [InlineData("{\"History\":[null]}")]
    [InlineData("{\"History\":[{\"Season\":1,\"Division\":0,\"Position\":9,\"Outcome\":0}]}")]
    [InlineData("{\"Pending\":{\"Round\":0,\"HomeGoals\":-1,\"AwayGoals\":0}}")]
    public void ParseRejectsBadStatesWithAFormatException(string json) =>
        Assert.Throws<FormatException>(() => SeasonState.Parse(json));

    [Fact]
    public void ParseRejectsNullAndWrongResults()
    {
        var s = OneRoundPlayed();
        s.Results[1] = null!;
        Assert.Throws<FormatException>(() => SeasonState.Parse(s.ToJson()));

        var dup = OneRoundPlayed();
        dup.Results[2] = dup.Results[1];                               // a match twice, another one missing
        Assert.Throws<FormatException>(() => SeasonState.Parse(dup.ToJson()));

        var wrongRound = OneRoundPlayed();
        var other = Competition.RoundFixtures(3)[0];                   // a pair that is not scheduled in round 0
        wrongRound.Results[0] = new CompetitionResult(0, other.Home, other.Away, 1, 0);
        Assert.Throws<FormatException>(() => SeasonState.Parse(wrongRound.ToJson()));
    }

    [Fact]
    public void PendingLiveResultSurvivesSaveAndIsUsedAndClearedByPlayRound()
    {
        var s = new SeasonState { Pending = new PendingResult(0, 6, 4) };
        var loaded = SeasonState.Parse(s.ToJson());                    // as after a restart
        Assert.Equal(new PendingResult(0, 6, 4), loaded.Pending);
        Competition.PlayRound(loaded, Tactics, Squads, config: Short);
        var mine = loaded.Results.Single(r => r.Home == 0 || r.Away == 0);
        Assert.Equal((6, 4), (mine.HomeGoals, mine.AwayGoals));
        Assert.Null(loaded.Pending);
        Assert.Equal(1, loaded.Round);
    }

    [Fact]
    public void AStalePendingResultIsDroppedWhenParsing()
    {
        var s = OneRoundPlayed();
        s.Pending = new PendingResult(0, 3, 3);            // round 0 is already part of Results
        Assert.Null(SeasonState.Parse(s.ToJson()).Pending);
    }

    [Fact]
    public void AnExplicitResultWinsOverAPendingOneAndClearsIt()
    {
        var s = new SeasonState { Pending = new PendingResult(0, 9, 9) };
        Competition.PlayRound(s, Tactics, Squads, (1, 0), config: Short);
        var mine = s.Results.Single(r => r.Home == 0 || r.Away == 0);
        Assert.Equal((1, 0), (mine.HomeGoals, mine.AwayGoals));
        Assert.Null(s.Pending);
    }
    [Fact]
    public void GoalFollowsTheSquadRankInThirdsAndTiesSitInTheMiddle()
    {
        Assert.Equal(SeasonGoal.Promotion, Competition.GoalFor([.9, .5, .5, .5, .5, .8]));
        Assert.Equal(SeasonGoal.Survival, Competition.GoalFor([.4, .5, .5, .5, .5, .3]));
        Assert.Equal(SeasonGoal.TopHalf, Competition.GoalFor([.6, .5, .5, .7, .7, .3]));
        Assert.Equal(SeasonGoal.TopHalf, Competition.GoalFor([.5, .5, .5, .5, .5, .5]));      // all equal: middle
        Assert.True(Competition.GoalMet(SeasonGoal.Promotion, 2));
        Assert.False(Competition.GoalMet(SeasonGoal.Promotion, 3));
        Assert.True(Competition.GoalMet(SeasonGoal.TopHalf, 3));
        Assert.False(Competition.GoalMet(SeasonGoal.TopHalf, 4));
        Assert.True(Competition.GoalMet(SeasonGoal.Survival, 5));
        Assert.False(Competition.GoalMet(SeasonGoal.Survival, 6));
    }

    [Fact]
    public void ExpectedGoalWorksForEveryDivisionWithTheShippedData()
    {
        for (int d = 0; d < Competition.Divisions.Count; d++)
            Assert.True(Enum.IsDefined(Competition.ExpectedGoal(new SeasonState { Division = d }, Tactics, Squads)));
    }

    [Fact]
    public void GoalIsOptionalInTheSaveAndClearedForTheNextSeason()
    {
        var old = new SeasonState();
        Assert.DoesNotContain("Goal", old.ToJson());                  // older saves stay as they were
        Assert.Null(SeasonState.Parse(old.ToJson()).Goal);
        var s = new SeasonState { Goal = SeasonGoal.Survival };
        Assert.Equal(SeasonGoal.Survival, SeasonState.Parse(s.ToJson()).Goal);
        Assert.Throws<FormatException>(() => SeasonState.Parse("{\"Goal\":7}"));
        s.Round = Competition.Rounds;
        for (int r = 0; r < Competition.Rounds; r++)
            foreach (var f in Competition.RoundFixtures(r)) s.Results.Add(new CompetitionResult(r, f.Home, f.Away, 1, 0));
        Competition.StartNextSeason(s);
        Assert.Null(s.Goal);
    }

    [Theory]
    [InlineData("  Rode Leeuwen ", "Rode Leeuwen")]
    [InlineData("A\u0007B\n", "AB")]
    [InlineData("Ünïcødé ☆ FC", "Ünïcødé ☆ FC")]
    [InlineData("123456789012345678901234567890", "123456789012345678901234")]
    [InlineData("   ", null)]
    [InlineData("\t\r", null)]
    public void ClubNameDropsControlCharactersAndIsCapped(string raw, string? expected) =>
        Assert.Equal(expected, Competition.CleanClubName(raw));
}
