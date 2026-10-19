using Xunit;

namespace Zeus.Sim.Tests;

public class SmartOpponentTests
{
    static Tactic Load(string name) => TacticLoader.LoadFile(TacticLoaderTests.TacticPath(name));

    static MatchStatistics Stats(int homeControls, int awayControls) =>
        new(5400, new TeamStatistics(0, homeControls, 0, 0, 0, 0), new TeamStatistics(0, awayControls, 0, 0, 0, 0));

    static SeasonState Career(int division, params StyleSample[] history) =>
        new() { Division = division, StyleHistory = history.ToList() };

    static readonly StyleSample High = new(.95, .85, .85), Low = new(.2, .25, .6), Ball = new(.5, .6, .45, .65);

    [Fact]
    public void ThreeStylesAreRecognisedFromSettingsAndPossession()
    {
        Assert.Equal(PlayStyle.HighPress, PlayStyleProfile.Classify(PlayStyleProfile.Sample(Load("hoge_druk_433"))));
        Assert.Equal(PlayStyle.LowBlock, PlayStyleProfile.Classify(PlayStyleProfile.Sample(Load("laag_blok_442"))));
        Assert.Equal(PlayStyle.Possession, PlayStyleProfile.Classify(PlayStyleProfile.Sample(Load("positiespel_4231"))));
        // possession share comes from the statistics, seen from the player's side
        var tactic = Load("vleugelspel_433");
        Assert.Equal(PlayStyle.None, PlayStyleProfile.Classify(PlayStyleProfile.Sample(tactic, Stats(40, 60))));
        Assert.Equal(PlayStyle.Possession, PlayStyleProfile.Classify(PlayStyleProfile.Sample(tactic, Stats(70, 30))));
        Assert.Equal(PlayStyle.Possession, PlayStyleProfile.Classify(PlayStyleProfile.Sample(tactic, Stats(30, 70), playerIsHome: false)));
        Assert.Null(PlayStyleProfile.Sample(tactic, Stats(0, 0)).Possession);
    }

    [Fact]
    public void CounterTableIsFixed()
    {
        Assert.Equal(("lange_bal_442.json", "opponent.adapt.high_press"), PlayStyleProfile.Counter(PlayStyle.HighPress));
        Assert.Equal(("laag_blok_442.json", "opponent.adapt.possession"), PlayStyleProfile.Counter(PlayStyle.Possession));
        Assert.Equal(("hoge_druk_433.json", "opponent.adapt.low_block"), PlayStyleProfile.Counter(PlayStyle.LowBlock));
        Assert.Null(PlayStyleProfile.Counter(PlayStyle.None));
        foreach (var style in new[] { PlayStyle.HighPress, PlayStyle.Possession, PlayStyle.LowBlock })
            Assert.True(File.Exists(TacticLoaderTests.TacticPath(Path.GetFileNameWithoutExtension(PlayStyleProfile.Counter(style)!.Value.TacticFile))));
    }

    [Fact]
    public void OpponentOnlyAdaptsAfterThreeMatchesInTheSameStyle()
    {
        Assert.Equal(PlayStyle.None, PlayStyleProfile.Detect(new[] { High, High }));
        Assert.Equal(PlayStyle.HighPress, PlayStyleProfile.Detect(new[] { High, High, High }));
        Assert.Equal(PlayStyle.None, PlayStyleProfile.Detect(new[] { High, High, Low }));
        Assert.Equal(PlayStyle.Possession, PlayStyleProfile.Detect(new[] { High, Ball, Ball, Ball }));
        Assert.Null(Competition.Adaptation(Career(2, High, High)));
        var adaptation = Competition.Adaptation(Career(2, High, High, High));
        Assert.Equal(new OpponentAdaptation(PlayStyle.HighPress, "lange_bal_442.json", "opponent.adapt.high_press"), adaptation);
        Assert.Null(Competition.Adaptation(Career(2, High, Low, High)));
    }

    [Fact]
    public void AdaptationDependsOnTheLevelAndIsDeterministic()
    {
        Assert.Null(Competition.Adaptation(Career(0, High, High, High)));         // amateur never
        Assert.NotNull(Competition.Adaptation(Career(2, High, High, High)));      // prof always
        var rounds = Enumerable.Range(0, Competition.Rounds).Select(r => Competition.AdaptsAtLevel("halfprof", 1, r)).ToList();
        Assert.Contains(true, rounds);
        Assert.Contains(false, rounds);
        Assert.Equal(rounds, Enumerable.Range(0, Competition.Rounds).Select(r => Competition.AdaptsAtLevel("halfprof", 1, r)));
        foreach (var season in new[] { 1, 2, 3 })
            Assert.Equal(Competition.Rounds / 2, Enumerable.Range(0, Competition.Rounds).Count(r => Competition.AdaptsAtLevel("halfprof", season, r)));
        for (int round = 0; round < Competition.Rounds; round++)
        {
            var state = Career(1, High, High, High); state.Round = round; state.Results = Competition.Schedule().Where(f => f.Round < round).Select(f => new CompetitionResult(f.Round, f.Home, f.Away, 0, 0)).ToList();
            Assert.Equal(rounds[round], Competition.Adaptation(state) is not null);
        }
    }

    [Fact]
    public void LiveMatchUsesTheCounterOnlyWhenAdaptiveIsOn()
    {
        var state = Career(2, High, High, High);
        var normal = Competition.NextLiveMatch(state)!;
        var adaptive = Competition.NextLiveMatch(state, adaptive: true)!;
        string Opponent(Competition.LiveMatch m) => m.PlayerIsHome ? m.AwayTactic : m.HomeTactic;
        Assert.NotEqual("lange_bal_442.json", Opponent(normal));
        Assert.Equal("lange_bal_442.json", Opponent(adaptive));
        Assert.Equal(state.PlayerTactic, adaptive.PlayerIsHome ? adaptive.HomeTactic : adaptive.AwayTactic);
        Assert.Equal(normal.Seed, adaptive.Seed);
    }

    [Fact]
    public void OldCareerWithoutStyleHistoryStillLoadsAndIsWrittenWithoutIt()
    {
        var old = SeasonState.Parse("""{"Version":2,"Season":3,"Division":1,"Round":0,"PlayerTactic":"laag_blok_442.json","Results":[],"History":[]}""");
        Assert.Null(old.StyleHistory);
        Assert.DoesNotContain("StyleHistory", old.ToJson());
        Assert.Null(Competition.Adaptation(old));
        var withHistory = Career(2, High, Ball);
        var back = SeasonState.Parse(withHistory.ToJson());
        Assert.Equal(withHistory.StyleHistory, back.StyleHistory);
    }

    [Theory]
    [InlineData("""{"StyleHistory":[null]}""")]
    [InlineData("""{"StyleHistory":[{"Pressing":2,"LineHeight":0.5,"Tempo":0.5,"Possession":null}]}""")]
    [InlineData("""{"StyleHistory":[{"Pressing":0.5,"LineHeight":0.5,"Tempo":0.5,"Possession":-1}]}""")]
    public void ParseRejectsBadStyleHistory(string json) => Assert.Throws<FormatException>(() => SeasonState.Parse(json));

    [Fact]
    public void PlayRoundRecordsTheStyleAndKeepsOnlyTheLastFive()
    {
        var root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "Zeus.sln"))) root = Path.GetDirectoryName(root);
        var state = new SeasonState { PlayerTactic = "hoge_druk_433.json" };
        var config = new MatchConfig(HalfSeconds: 120);
        for (int i = 0; i < 6; i++)
            Competition.PlayRound(state, Path.Combine(root!, "tactics"), Path.Combine(root!, "squads"), config: config, adaptiveOpponent: true);
        Assert.Equal(PlayStyleProfile.MaxKept, state.StyleHistory!.Count);
        Assert.Equal(PlayStyle.HighPress, PlayStyleProfile.Detect(state.StyleHistory));
    }

    [Fact]
    public void AdaptiveOffLeavesTheSaveWithoutStyleHistory()
    {
        var root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "Zeus.sln"))) root = Path.GetDirectoryName(root);
        var state = new SeasonState { PlayerTactic = "hoge_druk_433.json" };
        Competition.PlayRound(state, Path.Combine(root!, "tactics"), Path.Combine(root!, "squads"), config: new MatchConfig(HalfSeconds: 120));
        Assert.Null(state.StyleHistory);
        Assert.DoesNotContain("StyleHistory", state.ToJson());
    }

    [Fact]
    public void AdaptationNeverChangesMatchesBetweenTwoAiClubs()
    {
        var root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "Zeus.sln"))) root = Path.GetDirectoryName(root);
        SeasonState Fresh() => Career(2, High, High, High);
        var config = new MatchConfig(HalfSeconds: 120);
        var plain = Fresh(); var adaptive = Fresh();
        Competition.PlayRound(plain, Path.Combine(root!, "tactics"), Path.Combine(root!, "squads"), config: config);
        Competition.PlayRound(adaptive, Path.Combine(root!, "tactics"), Path.Combine(root!, "squads"), config: config, adaptiveOpponent: true);
        Assert.NotNull(Competition.Adaptation(Career(2, High, High, High)));
        var aiOnly = new Func<CompetitionResult, bool>(r => r.Home != 0 && r.Away != 0);
        Assert.Equal(plain.Results.Where(aiOnly), adaptive.Results.Where(aiOnly));
    }
}
