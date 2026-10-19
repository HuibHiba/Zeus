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

    [Fact]
    public void AClaimReplaysAndADoctoredOneIsRejected()
    {
        // One match keeps the test fast; the spec itself is what a shipped challenge looks like.
        var spec = new ChallengeSpec("proef", "Proef", "", "laag_blok_442.json", 1, new[] { new ChallengeCriterion("winst", ">=", 0) }, "", OpponentLevel: "halfprof");
        string tacticJson = File.ReadAllText(Path.Combine(Tactics, "positiespel_4231.json"));
        var player = TacticLoader.Parse(tacticJson);
        var opponent = TacticLoader.LoadFile(Path.Combine(Tactics, spec.Opponent));
        var results = ChallengeEvaluator.Play(spec, player, opponent, playerSquad: SquadFactory.ForLevel("halfprof", player, Squads), opponentSquad: SquadFactory.ForLevel("halfprof", opponent, Squads));
        var claim = ChallengeShare.Claim(spec, tacticJson, player.Name, "halfprof", results);
        Assert.Equal(1, claim.Stars);
        var code = ChallengeShare.ToCode(claim);
        Assert.StartsWith("ZC1.", code);
        var back = ChallengeShare.FromCode(code);
        Assert.Equal(claim with { Results = back.Results }, back);      // record equality compares the list by reference
        Assert.Equal(claim.Results, back.Results);

        Assert.True(ChallengeShare.Verify(claim, spec, tacticJson, Tactics, Squads, out var reason), reason);
        Assert.False(ChallengeShare.Verify(claim with { Results = new() { results[0] with { GoalsFor = results[0].GoalsFor + 2 } } }, spec, tacticJson, Tactics, Squads, out reason));
        Assert.Equal("result", reason);
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
}
