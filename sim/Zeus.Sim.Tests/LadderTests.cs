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
        Assert.Equal(7, result.Round);
        Assert.Equal(Ladder.SeedFor(result.HomeTactic, result.AwayTactic, 7), result.Seed);
        Assert.NotEmpty(result.EventHash);

        Assert.True(Ladder.Verify(result with { HomeName = "Verzonnen", AwayName = "Ook verzonnen" }, home, away, Squads, out var verified, out var reason), reason);
        Assert.Equal(result, verified);        // replays once: same score and event hash
        Assert.Equal("", reason);
        Assert.False(Ladder.Verify(result with { HomeGoals = result.HomeGoals + 1 }, home, away, Squads, out _, out reason));
        Assert.Equal("result", reason);
        Assert.False(Ladder.Verify(result with { Build = "deadbeef" }, home, away, Squads, out _, out reason));
        Assert.Equal("version", reason);
        Assert.False(Ladder.Verify(result, away, home, Squads, out _, out reason));
        Assert.Equal("tactic", reason);
        Assert.False(Ladder.Verify(result with { Seed = result.Seed ^ 1 }, home, away, Squads, out _, out reason));
        Assert.Equal("result", reason);
        Assert.False(Ladder.Verify(result with { Round = 8 }, home, away, Squads, out _, out reason));
        Assert.Equal("result", reason);
        Assert.False(Ladder.Verify(result with { AwayLevel = "amateur" }, home, away, Squads, out _, out reason));
        Assert.Equal("result", reason);
        Assert.False(Ladder.Verify(result with { Round = 0 }, home, away, Squads, out _, out reason));
        Assert.Equal("result", reason);

        Assert.False(Ladder.Verify(result with { DataHash = "changed" }, home, away, Squads, out _, out reason));
        Assert.Equal("data", reason);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => Ladder.Play(home, away, "halfprof", "halfprof", 7, Squads, cancelled.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => Ladder.Verify(result, home, away, Squads, out _, out _, cancelled.Token));
        var code = Ladder.ToCode(result);
        Assert.StartsWith("ZL1.", code);
        Assert.Equal(result, Ladder.FromCode(code));
        Assert.True(code.Length < 600, $"result code stays small ({code.Length} chars)");
    }

    [Fact]
    public void CodesWithImpossibleFieldsAreRejected()
    {
        var ok = new LadderResult(1, "build", 7, new string('a', 16), new string('b', 16), "A", "B", "halfprof", "halfprof", 2, 1, "hash");
        Assert.Equal(ok, Ladder.FromCode(Ladder.ToCode(ok)));
        var legacy = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(ok))!.AsObject();
        legacy.Remove("Round");
        Assert.Equal(ok, Ladder.FromCode(ShareCode.Encode(ShareCode.LadderKind, legacy.ToJsonString())));
        Assert.Throws<FormatException>(() => Ladder.FromCode(Ladder.ToCode(ok with { HomeGoals = 150 })));
        Assert.Throws<FormatException>(() => Ladder.FromCode(Ladder.ToCode(ok with { AwayLevel = "goden" })));
        Assert.Throws<FormatException>(() => Ladder.FromCode(Ladder.ToCode(ok with { AwayLevel = "prof" })));
        Assert.Throws<ArgumentException>(() => Ladder.Play("", "", "prof", "amateur", 1, Squads));
        Assert.Throws<FormatException>(() => Ladder.FromCode(Ladder.ToCode(ok with { HomeTactic = "kort" })));
        Assert.Throws<FormatException>(() => Ladder.FromCode(Ladder.ToCode(ok with { Version = 2 })));
        Assert.Throws<FormatException>(() => Ladder.FromCode(Ladder.ToCode(ok with { Round = 0 })));
        Assert.Throws<FormatException>(() => Ladder.FromCode(ShareCode.Encode(ShareCode.LadderKind, "[1,2]")));
        Assert.Throws<FormatException>(() => Ladder.FromCode(ShareCode.Encode(ShareCode.TacticKind, "{}")));
    }

    [Fact]
    public void SeedIsFixedByBothTacticsAndRound()
    {
        string a = new('a', 16), b = new('b', 16), c = new('c', 16);
        var seed = Ladder.SeedFor(a, b, 1);
        Assert.Equal(seed, Ladder.SeedFor(a, b, 1));
        Assert.NotEqual(seed, Ladder.SeedFor(a, b, 2));
        Assert.NotEqual(seed, Ladder.SeedFor(a, c, 1));
        Assert.NotEqual(seed, Ladder.SeedFor(c, b, 1));
        Assert.NotEqual(seed, Ladder.SeedFor(b, a, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ladder.SeedFor(a, b, 0));
    }

    [Fact]
    public void OnlyOneResultPerPairAndRoundCountsEvenWithDifferentSeedScoreOrLevels()
    {
        string a = new('a', 16), b = new('b', 16), c = new('c', 16);
        var result = new LadderResult(1, "x", 1, a, b, "A", "B", "prof", "prof", 2, 0, "h1");
        var duplicate = result with { Seed = 99, HomeGoals = 0, AwayGoals = 5, EventHash = "other", HomeLevel = "amateur" };
        var reversed = result with { HomeTactic = b, AwayTactic = a };
        var next = result with { Round = 2 };
        Assert.Equal(1, Ladder.NextRound(Array.Empty<LadderResult>(), a, b));
        Assert.Equal(2, Ladder.NextRound(new[] { result }, b, a));
        Assert.Equal(1, Ladder.NextRound(new[] { result }, a, c));
        Assert.True(Ladder.ContainsRound(new[] { result }, duplicate));
        Assert.True(Ladder.ContainsRound(new[] { result }, reversed));
        Assert.False(Ladder.ContainsRound(new[] { result }, next));
        Assert.Equal(Ladder.Standings(new[] { result }), Ladder.Standings(new[] { result, duplicate, reversed }));
        Assert.All(Ladder.Standings(new[] { result, next }), row => Assert.Equal(2, row.Played));
    }

    [Fact]
    public void StandingsKeepLevelsSeparateAndIgnoreUnequalLevels()
    {
        var first = new LadderResult(1, "x", 1, new string('a', 16), new string('b', 16), "A", "B", "prof", "prof", 2, 0, "h1");
        var second = first with { HomeLevel = "amateur", AwayLevel = "amateur", Round = 2 };
        var rows = Ladder.Standings(new[] { first, second, first with { AwayLevel = "halfprof", Round = 3 } });
        Assert.Equal(4, rows.Count);
        Assert.All(rows, row => Assert.Equal(1, row.Played));
        Assert.Equal(new[] { "amateur", "prof" }, rows.Where(row => row.Name == "A").Select(row => row.Level).Order());
    }

    [Fact]
    public void StandingsGiveThreeOneZeroPointsPerTactic()
    {
        string a = new('a', 16), b = new('b', 16), c = new('c', 16);
        var results = new[]
        {
            new LadderResult(1, "x", 1, a, b, "A", "B", "prof", "prof", 2, 0, "h1"),
            new LadderResult(1, "x", 2, b, a, "B", "A", "prof", "prof", 1, 1, "h2", Round: 2),
            new LadderResult(1, "x", 3, c, a, "C", "A", "prof", "prof", 0, 3, "h3"),
        };
        var rows = Ladder.Standings(results);
        Assert.Equal(new[] { "A", "B", "C" }, rows.Select(r => r.Name));
        Assert.Equal(new LadderRow("A", 3, 2, 1, 0, 6, 1, "prof"), rows[0]);
        Assert.Equal(7, rows[0].Points);
        Assert.Equal(1, rows[1].Points);
        Assert.Equal(0, rows[2].Points);
    }

    [Fact]
    public void MaximalSharedTacticCanBeCancelledDuringSimulation()
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(Tactic("man_om_man_433"))!;
        var trigger = root["triggers"]![0]!.DeepClone();
        var action = trigger["dan"]![0]!.DeepClone();
        trigger["dan"] = new System.Text.Json.Nodes.JsonArray(Enumerable.Range(0, 11).Select(_ => action.DeepClone()).ToArray());
        root["triggers"] = new System.Text.Json.Nodes.JsonArray(Enumerable.Range(0, 32).Select(_ => trigger.DeepClone()).ToArray());
        var block = root["blokwissels"]![0]!.DeepClone();
        root["blokwissels"] = new System.Text.Json.Nodes.JsonArray(Enumerable.Range(0, 8).Select(_ => block.DeepClone()).ToArray());
        string json = root.ToJsonString();
        TacticLoader.Parse(json);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        Assert.ThrowsAny<OperationCanceledException>(() => Ladder.Play(json, json, "halfprof", "halfprof", 1, Squads, cts.Token));
    }
}
