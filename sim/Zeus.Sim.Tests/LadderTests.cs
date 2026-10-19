namespace Zeus.Sim.Tests;

/// <summary>Ladder between friends: a result code is replayed on seed and tactics; a doctored one is rejected. Plays few matches (each takes seconds).</summary>
public sealed class LadderTests
{
    static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Zeus.sln"))) dir = Path.GetDirectoryName(dir);
        return dir!;
    }

    static string Tactic(string name) => File.ReadAllText(Path.Combine(Root(), "tactics", name + ".json"));
    static string Squads => Path.Combine(Root(), "squads");

    [Fact]
    public void ReplayVerifiesAnHonestResultAndRejectsADoctoredOne()
    {
        string home = Tactic("positiespel_4231"), away = Tactic("counter_532");
        var result = Ladder.Play(home, away, "halfprof", "halfprof", 7, Squads);
        Assert.Equal(1, result.Version);
        Assert.Equal(ReplayFile.CurrentBuildHash, result.Build);
        Assert.Equal(Ladder.TacticHash(home), result.HomeTactic);
        Assert.Equal(16, result.AwayTactic.Length);
        Assert.NotEmpty(result.EventHash);

        Assert.True(Ladder.Verify(result, home, away, Squads, out var reason), reason);        // replays once: same score and event hash
        Assert.Equal("", reason);
        Assert.False(Ladder.Verify(result with { HomeGoals = result.HomeGoals + 1 }, home, away, Squads, out reason));
        Assert.Equal("result", reason);
        Assert.False(Ladder.Verify(result with { Build = "deadbeef" }, home, away, Squads, out reason));
        Assert.Equal("version", reason);
        Assert.False(Ladder.Verify(result, away, home, Squads, out reason));
        Assert.Equal("tactic", reason);

        var code = Ladder.ToCode(result);
        Assert.StartsWith("ZL1.", code);
        Assert.Equal(result, Ladder.FromCode(code));
        Assert.True(code.Length < 600, $"result code stays small ({code.Length} chars)");
    }

    [Fact]
    public void CodesWithImpossibleFieldsAreRejected()
    {
        var ok = new LadderResult(1, "build", 7, new string('a', 16), new string('b', 16), "A", "B", "halfprof", "prof", 2, 1, "hash");
        Assert.Equal(ok, Ladder.FromCode(Ladder.ToCode(ok)));
        Assert.Throws<FormatException>(() => Ladder.FromCode(Ladder.ToCode(ok with { HomeGoals = 150 })));
        Assert.Throws<FormatException>(() => Ladder.FromCode(Ladder.ToCode(ok with { AwayLevel = "goden" })));
        Assert.Throws<FormatException>(() => Ladder.FromCode(Ladder.ToCode(ok with { HomeTactic = "kort" })));
        Assert.Throws<FormatException>(() => Ladder.FromCode(Ladder.ToCode(ok with { Version = 2 })));
        Assert.Throws<FormatException>(() => Ladder.FromCode(ShareCode.Encode(ShareCode.LadderKind, "[1,2]")));
        Assert.Throws<FormatException>(() => Ladder.FromCode(ShareCode.Encode(ShareCode.TacticKind, "{}")));
    }

    [Fact]
    public void StandingsGiveThreeOneZeroPointsPerTactic()
    {
        string a = new('a', 16), b = new('b', 16), c = new('c', 16);
        var results = new[]
        {
            new LadderResult(1, "x", 1, a, b, "A", "B", "prof", "prof", 2, 0, "h1"),
            new LadderResult(1, "x", 2, b, a, "B", "A", "prof", "prof", 1, 1, "h2"),
            new LadderResult(1, "x", 3, c, a, "C", "A", "prof", "prof", 0, 3, "h3"),
        };
        var rows = Ladder.Standings(results);
        Assert.Equal(new[] { "A", "B", "C" }, rows.Select(r => r.Name));
        Assert.Equal(new LadderRow("A", 3, 2, 1, 0, 6, 1), rows[0]);
        Assert.Equal(7, rows[0].Points);
        Assert.Equal(1, rows[1].Points);
        Assert.Equal(0, rows[2].Points);
    }
}
