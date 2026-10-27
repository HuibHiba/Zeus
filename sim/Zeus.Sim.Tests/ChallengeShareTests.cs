namespace Zeus.Sim.Tests;

/// <summary>Challenge a friend: a claim code is replayed on seeds 1..N with the same tactic; doctored stars are rejected.</summary>
public sealed class ChallengeShareTests
{
    static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Zeus.sln"))) dir = Path.GetDirectoryName(dir);
        return dir!;
    }

    static string Tactics => Path.Combine(Root(), "tactics");
    static string Squads => Path.Combine(Root(), "squads");

    [Theory]
    [InlineData("A\u202eB")]
    [InlineData("A\u2066B")]
    [InlineData("A\nB")]
    [InlineData("")]
    public void SharedNamesRejectControlsAndOverlongNames(string name)
    {
        foreach (string bad in new[] { name, new string('a', TacticExchange.MaxNameLength + 1) })
        {
            var result = new LadderResult(1, "build", 1, new string('a', 16), new string('b', 16), bad, "B", "prof", "prof", 0, 0, "hash");
            Assert.Throws<FormatException>(() => Ladder.FromCode(Ladder.ToCode(result)));
            Assert.Throws<FormatException>(() => Ladder.FromCode(Ladder.ToCode(result with { HomeName = "A", AwayName = bad })));
            var claim = new ChallengeClaim(1, "build", "test", new string('a', 16), bad, "prof", 1,
                new() { new ChallengeMatchResult(0, 0, 0, 0, 0, 0, 0, 0, 0) });
            Assert.Throws<FormatException>(() => ChallengeShare.FromCode(ChallengeShare.ToCode(claim)));
        }
        Assert.True(TacticExchange.IsValidName(new string('a', TacticExchange.MaxNameLength)));
        Assert.True(TacticExchange.IsValidName("Jos? ??"));
    }

    [Fact]
    public void DailyClaimCanBeResolvedByIdAndVerifiedInAnotherLanguage()
    {
        var spec = DailyChallenge.ForDate(new DateOnly(2026, 10, 26));
        string tacticJson = File.ReadAllText(Path.Combine(Tactics, "positiespel_4231.json"));
        var player = TacticLoader.Parse(tacticJson);
        var opponent = TacticLoader.LoadFile(Path.Combine(Tactics, spec.Opponent));
        var results = ChallengeEvaluator.Play(spec, player, opponent, maxParallel: 1,
            playerSquad: SquadFactory.ForLevel("halfprof", player, Squads), opponentSquad: SquadFactory.ForLevel(spec.OpponentLevel, opponent, Squads));
        var claim = ChallengeShare.FromCode(ChallengeShare.ToCode(ChallengeShare.Claim(spec, tacticJson, player.Name, "halfprof", results, ReplayFile.DataHash(Squads, Path.Combine(Root(), "challenges"), Path.Combine(Tactics, spec.Opponent)))));
        var resolved = ChallengeEvaluator.LoadAll(Path.Combine(Root(), "challenges"), out _, "en").FirstOrDefault(s => s.Id == claim.ChallengeId)
            ?? DailyChallenge.FromId(claim.ChallengeId, "en");
        Assert.NotNull(resolved);
        Assert.True(ChallengeShare.Verify(claim, resolved, tacticJson, Tactics, Squads, out var reason), reason);
    }

    [Fact]
    public void AClaimReplaysAndADoctoredOneIsRejected()
    {
        // One match keeps the test fast; the spec itself is what a shipped challenge looks like.
        var spec = new ChallengeSpec("proef", "Proef", "", "laag_blok_442.json", 1, new[] { new ChallengeCriterion("winst", ">=", 0) }, "", OpponentLevel: "halfprof");
        string tacticJson = File.ReadAllText(Path.Combine(Tactics, "positiespel_4231.json"));
        var player = TacticLoader.Parse(tacticJson);
        var opponent = TacticLoader.LoadFile(Path.Combine(Tactics, spec.Opponent));
        var results = ChallengeEvaluator.Play(spec, player, opponent, maxParallel: 1, playerSquad: SquadFactory.ForLevel("halfprof", player, Squads), opponentSquad: SquadFactory.ForLevel("halfprof", opponent, Squads));
        var claim = ChallengeShare.Claim(spec, tacticJson, player.Name, "halfprof", results, ReplayFile.DataHash(Squads, Path.Combine(Root(), "challenges"), Path.Combine(Tactics, spec.Opponent)));
        Assert.Equal(1, claim.Stars);
        var code = ChallengeShare.ToCode(claim);
        Assert.StartsWith("ZC1.", code);
        var back = ChallengeShare.FromCode(code);
        Assert.Equal(claim with { Results = back.Results }, back);      // record equality compares the list by reference
        Assert.Equal(claim.Results, back.Results);

        Assert.False(ChallengeShare.Verify(claim, spec with { PlayerLevel = "amateur" }, tacticJson, Tactics, Squads, out var reason));
        Assert.Equal("result", reason);
        Assert.True(ChallengeShare.Verify(claim, spec with { PlayerLevel = "halfprof" }, tacticJson, Tactics, Squads, out reason), reason);
        Assert.False(ChallengeShare.Verify(claim with { Results = new() { results[0] with { GoalsFor = results[0].GoalsFor + 2 } } }, spec, tacticJson, Tactics, Squads, out reason));
        Assert.Equal("result", reason);
        Assert.False(ChallengeShare.Verify(claim with { DataHash = "changed" }, spec, tacticJson, Tactics, Squads, out reason));
        Assert.Equal("data", reason);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => ChallengeShare.Verify(claim, spec, tacticJson, Tactics, Squads, out _, cancelled.Token));
        Assert.False(ChallengeShare.Verify(claim with { Build = "x" }, spec, tacticJson, Tactics, Squads, out reason));
        Assert.Equal("version", reason);
        Assert.False(ChallengeShare.Verify(claim, spec with { Adaptive = true }, tacticJson, Tactics, Squads, out reason));
        Assert.Equal("challenge", reason);
        Assert.False(ChallengeShare.Verify(claim, null, tacticJson, Tactics, Squads, out reason));
        Assert.Equal("challenge", reason);
        Assert.False(ChallengeShare.Verify(claim, spec, File.ReadAllText(Path.Combine(Tactics, "counter_532.json")), Tactics, Squads, out reason));
        Assert.Equal("tactic", reason);
        Assert.Throws<FormatException>(() => ChallengeShare.FromCode(ChallengeShare.ToCode(claim with { Stars = 7 })));
        Assert.Throws<FormatException>(() => ChallengeShare.FromCode(ShareCode.Encode(ShareCode.LadderKind, "{}")));
    }

    static (ChallengeClaim Claim, string TacticJson) PlayClaim(ChallengeSpec spec)
    {
        string tacticJson = File.ReadAllText(Path.Combine(Tactics, "positiespel_4231.json"));
        var player = TacticLoader.Parse(tacticJson);
        var opponent = TacticLoader.LoadFile(Path.Combine(Tactics, spec.Opponent));
        string level = spec.PlayerLevel.Length > 0 ? spec.PlayerLevel : "halfprof";
        var results = ChallengeEvaluator.Play(spec, player, opponent, maxParallel: 1,
            playerSquad: SquadFactory.ForLevel(level, player, Squads), opponentSquad: SquadFactory.ForLevel(spec.OpponentLevel, opponent, Squads));
        var claim = ChallengeShare.Claim(spec, tacticJson, player.Name, level, results, ReplayFile.DataHash(Squads, Path.Combine(Root(), "challenges"), Path.Combine(Tactics, spec.Opponent)));
        return (ChallengeShare.FromCode(ChallengeShare.ToCode(claim)), tacticJson);
    }

    [Fact]
    public void ScenarioClaimsVerifyAndAreBoundToTheScenario()
    {
        // 36 starts 0-1 down at minute 60; a dag_ challenge with a scenario is the daily variant of the same thing.
        var shipped = ChallengeEvaluator.LoadAll(Path.Combine(Root(), "challenges")).Single(s => s.Id.StartsWith("36_"));
        var daily = Enumerable.Range(0, 60).Select(d => DailyChallenge.ForDate(new DateOnly(2026, 10, 26).AddDays(d))).First(s => s.HasScenario);
        foreach (var spec in new[] { shipped, daily })
        {
            Assert.True(spec.HasScenario, spec.Id);
            var (claim, json) = PlayClaim(spec);
            Assert.True(ChallengeShare.Verify(claim, spec, json, Tactics, Squads, out var reason), spec.Id + " " + reason);
            // The same claim against a plain 0-0 version of the challenge must not replay to the same result.
            var plain = spec with { StartFor = 0, StartAgainst = 0, OpponentSentOff = false, StartMinute = 0 };
            Assert.False(ChallengeShare.Verify(claim, plain, json, Tactics, Squads, out reason), spec.Id);
            Assert.Equal("result", reason);
        }
    }

    [Fact]
    public void LiveAdaptiveChallengesCannotBeShared()
    {
        var adaptive = ChallengeEvaluator.LoadAll(Path.Combine(Root(), "challenges")).Where(s => s.Adaptive).ToList();
        Assert.NotEmpty(adaptive);
        foreach (var spec in adaptive)
        {
            var (claim, json) = PlayClaim(spec with { Adaptive = false });
            Assert.False(ChallengeShare.Verify(claim with { ChallengeId = spec.Id }, spec, json, Tactics, Squads, out var reason), spec.Id);
            Assert.Equal("challenge", reason);
        }
    }
}
