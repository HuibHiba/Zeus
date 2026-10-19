using System.Text.Json;

namespace Zeus.Sim;

/// <summary>A friend's claim: "I got this many stars on this challenge with this tactic"; replayable because challenges play seeds 1..N.</summary>
public sealed record ChallengeClaim(int Version, string Build, string ChallengeId, string Tactic, string TacticName, string PlayerLevel,
    int Stars, List<ChallengeMatchResult> Results);

/// <summary>
/// Challenge a friend without a server: a claim is a small code (ZC1); whoever receives it replays the challenge with the same tactic,
/// level and seeds, so stars and scores must come out the same. Adaptive challenges (the scout) cannot be replayed and are refused.
/// </summary>
public static class ChallengeShare
{
    public const string Kind = "ZC";
    public const int MaxMatches = 50;

    public static ChallengeClaim Claim(ChallengeSpec spec, string tacticJson, string tacticName, string playerLevel, IReadOnlyList<ChallengeMatchResult> results) =>
        new(1, ReplayFile.CurrentBuildHash, spec.Id, Ladder.TacticHash(tacticJson), tacticName.Length <= TacticExchange.MaxNameLength ? tacticName : tacticName[..TacticExchange.MaxNameLength],
            playerLevel, ChallengeEvaluator.Stars(spec, results), results.ToList());

    public static string ToCode(ChallengeClaim claim) => ShareCode.Encode(Kind, JsonSerializer.Serialize(claim));

    public static ChallengeClaim FromCode(string code)
    {
        ChallengeClaim? c;
        try { c = JsonSerializer.Deserialize<ChallengeClaim>(ShareCode.Decode(Kind, code)); }
        catch (JsonException e) { throw new FormatException("Challenge code does not hold a claim.", e); }
        if (c is null || c.Version != 1 || string.IsNullOrWhiteSpace(c.Build) || string.IsNullOrWhiteSpace(c.ChallengeId) || c.TacticName is null
            || c.Tactic is not { Length: 16 } || !SquadFactory.Levels.Contains(c.PlayerLevel) || c.Stars is < 0 or > 3
            || c.Results is not { Count: > 0 and <= MaxMatches } || c.Results.Any(r => r is null || r.GoalsFor < 0 || r.GoalsAgainst < 0))
            throw new FormatException("Challenge code holds an invalid claim.");
        return c;
    }

    /// <summary>
    /// Replays the claim. False with reason "version" (other build), "challenge" (unknown id or adaptive opponent), "tactic" (hash mismatch)
    /// or "result" (replay gives other scores or stars).
    /// </summary>
    public static bool Verify(ChallengeClaim claim, ChallengeSpec? spec, string tacticJson, string tacticsDirectory, string squadsDirectory, out string reason)
    {
        reason = "";
        if (claim.Build != ReplayFile.CurrentBuildHash) { reason = "version"; return false; }
        if (spec is null || spec.Id != claim.ChallengeId || spec.Adaptive) { reason = "challenge"; return false; }
        if (Ladder.TacticHash(tacticJson) != claim.Tactic) { reason = "tactic"; return false; }
        var player = TacticLoader.Parse(tacticJson);
        var opponent = TacticLoader.LoadFile(Path.Combine(tacticsDirectory, spec.Opponent));
        var replay = ChallengeEvaluator.Play(spec, player, opponent,
            playerSquad: SquadFactory.ForLevel(claim.PlayerLevel, player, squadsDirectory), opponentSquad: SquadFactory.ForLevel(spec.OpponentLevel, opponent, squadsDirectory));
        if (!replay.SequenceEqual(claim.Results) || ChallengeEvaluator.Stars(spec, replay) != claim.Stars) { reason = "result"; return false; }
        return true;
    }
}
