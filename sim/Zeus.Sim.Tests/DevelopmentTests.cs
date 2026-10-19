using Xunit;

namespace Zeus.Sim.Tests;

/// <summary>Career v1: growth and ageing of the player's squad, one step per completed season.</summary>
public class DevelopmentTests
{
    static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Zeus.sln"))) dir = Path.GetDirectoryName(dir);
        return dir!;
    }

    static string Tactics => Path.Combine(Root(), "tactics");
    static string Squads => Path.Combine(Root(), "squads");

    [Fact]
    public void ShippedSquadsHaveAgesAndTheLoaderBoundsThem()
    {
        foreach (var level in new[] { "amateur", "halfprof", "prof" })
        {
            var squad = SquadLoader.LoadFile(Path.Combine(Squads, level + ".json"));
            Assert.All(squad.Starters.Concat(squad.Bench), p => Assert.InRange(p.Age, SquadLoader.MinAge, SquadLoader.MaxAge));
            Assert.Contains(squad.Starters.Concat(squad.Bench), p => p.Age < Development.PrimeFrom);
            Assert.Contains(squad.Starters.Concat(squad.Bench), p => p.Age >= Development.DeclineFrom);
        }
        var json = File.ReadAllText(Path.Combine(Squads, "halfprof.json"));
        int first = SquadLoader.LoadFile(Path.Combine(Squads, "halfprof.json")).Starters[0].Age;
        Assert.Throws<SquadLoadException>(() => SquadLoader.Parse(json.Replace("\"age\": " + first, "\"age\": 9")));
        Assert.Equal(0, SquadLoader.Parse(System.Text.RegularExpressions.Regex.Replace(json, ",\\s*\"age\": \\d+", "")).Starters[0].Age);      // age stays optional
    }

    [Fact]
    public void YoungPlayersGrowOldPlayersDeclineAndTheDeltaIsBounded()
    {
        Assert.Equal(Development.Grow, Development.StepFor(20));
        Assert.Equal(0, Development.StepFor(27));
        Assert.Equal(Development.Decline, Development.StepFor(31));
        Assert.Equal(Development.SteepDecline, Development.StepFor(35));
        var young = new SquadPlayer("RB", "Jong", "back", "RB", PlayerAttributes.Neutral, Age: 19);
        var old = new SquadPlayer("LB", "Oud", "back", "LB", PlayerAttributes.Neutral, Age: 33);
        var prime = new SquadPlayer("CB", "Prime", "centrale_verdediger", "CB", PlayerAttributes.Neutral, Age: 26);
        var deltas = new Dictionary<string, double>();
        for (int season = 0; season < 8; season++) deltas = Development.AfterSeason(deltas, new[] { young, old, prime }, season);
        Assert.Equal(Development.MaxUp, deltas["Jong"], 9);          // 5 growing seasons (19..23) = 0.10, then prime
        Assert.Equal(Development.MaxDown, deltas["Oud"], 9);         // 8 steep seasons, bounded
        Assert.Equal(3 * Development.Decline + Development.SteepDecline, deltas["Prime"], 9);      // 26..29 holds, 30..32 declines, 33 steep
        Assert.False(Development.AfterSeason(new Dictionary<string, double>(), new[] { prime }, 0).ContainsKey("Prime"));
        Assert.True(Development.IsValid(deltas));
    }

    [Fact]
    public void ApplyShiftsEveryAttributeAndKeeperValuesWithinBoundsAndShowsCurrentAges()
    {
        var tactic = TacticLoader.LoadFile(Path.Combine(Tactics, "positiespel_4231.json"));
        var squad = SquadFactory.ForLevel("prof", tactic, Squads)!;
        var keeper = squad.Starters.First(p => p.Keeper is not null);
        var deltas = new Dictionary<string, double> { [keeper.Name] = -0.2, [squad.Starters[1].Name] = 0.1 };
        var after = Development.Apply(squad, deltas, seasonsCompleted: 2);
        SquadLoader.Validate(after, tactic);
        var k = after.Starters.First(p => p.Id == keeper.Id);
        Assert.Equal(Math.Clamp(keeper.Attributes.Passing - 0.2, 0, 1), k.Attributes.Passing, 9);
        Assert.Equal(Math.Clamp(keeper.Keeper!.Reflexes - 0.2, 0, 1), k.Keeper!.Reflexes, 9);
        Assert.Equal(Math.Clamp(squad.Starters[1].Attributes.Speed + 0.1, 0, 1), after.Starters[1].Attributes.Speed, 9);
        Assert.Equal(keeper.Age + 2, k.Age);
        Assert.Equal(squad.Starters[2].Attributes, after.Starters[2].Attributes);
    }

    [Fact]
    public void TheNextSeasonAdvancesDevelopmentAndTheElevenUsesIt()
    {
        var state = new SeasonState { Division = 2, Round = Competition.Rounds };
        for (int round = 0; round < Competition.Rounds; round++)
            foreach (var f in Competition.RoundFixtures(round)) state.Results.Add(new CompetitionResult(round, f.Home, f.Away, 1, 1));
        var tactic = TacticLoader.LoadFile(Path.Combine(Tactics, state.PlayerTactic));
        var before = Competition.PlayerSquad(state, tactic, Squads)!;
        Competition.StartNextSeason(state, Squads);
        Assert.NotNull(state.Development);
        Assert.NotEmpty(state.Development!);
        var after = Competition.PlayerSquad(state, tactic, Squads)!;
        var grown = before.Starters.Zip(after.Starters).First(pair => pair.First.Age < Development.PrimeFrom);
        Assert.Equal(Math.Clamp(grown.First.Attributes.Passing + Development.Grow, 0, 1), grown.Second.Attributes.Passing, 9);
        Assert.Equal(grown.First.Age + 1, grown.Second.Age);
        var back = SeasonState.Parse(state.ToJson());
        Assert.Equal(state.Development, back.Development);
        // Retirement: a player who reaches 36 stops and a weaker youth player with the same role takes his slot.
        var oldest = before.Starters.Concat(before.Bench).OrderByDescending(p => p.Age).First();
        int seasonsNeeded = Development.RetireAt - oldest.Age;
        var late = new SeasonState { Division = 2, Round = Competition.Rounds, Season = seasonsNeeded };
        for (int round = 0; round < Competition.Rounds; round++)
            foreach (var f in Competition.RoundFixtures(round)) late.Results.Add(new CompetitionResult(round, f.Home, f.Away, 1, 1));
        Competition.StartNextSeason(late, Squads);
        Assert.Contains(oldest.Name, late.Retired!);
        var youth = Development.Youth(oldest);
        Assert.EndsWith("(jeugd)", youth.Name);
        Assert.Equal(oldest.Role, youth.Role);
        Assert.InRange(youth.Age, 18, 21);
        Assert.Equal(Math.Clamp(oldest.Attributes.Passing * Development.YouthFactor, 0, 1), youth.Attributes.Passing, 9);
        Assert.Equal(youth, Development.Youth(oldest));
        var next = Competition.PlayerSquad(late, tactic, Squads)!;
        Assert.DoesNotContain(next.Starters.Concat(next.Bench), p => p.Name == oldest.Name);
        Assert.Contains(next.Starters.Concat(next.Bench), p => p.Name == youth.Name);
        SquadLoader.Validate(next, tactic);
        Assert.Equal(late.Retired, SeasonState.Parse(late.ToJson()).Retired);
        Assert.DoesNotContain("Development", new SeasonState().ToJson());
        Assert.Throws<FormatException>(() => SeasonState.Parse(state.ToJson().Replace("\"Development\": {", "\"Development\": {\"X\": 5,")));
    }
}
