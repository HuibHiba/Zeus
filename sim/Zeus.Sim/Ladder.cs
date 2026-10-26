using System.Security.Cryptography;
using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Zeus.Sim;

/// <summary>One ladder match between friends: enough to replay it (build, seed, tactic hashes, levels) and what it gave.</summary>
public sealed record LadderResult(int Version, string Build, ulong Seed, string HomeTactic, string AwayTactic, string HomeName, string AwayName,
    string HomeLevel, string AwayLevel, int HomeGoals, int AwayGoals, string EventHash, int Round = 1);

public sealed record LadderRow(string Name, int Played, int Won, int Drawn, int Lost, int GoalsFor, int GoalsAgainst)
{
    public int Points => 3 * Won + Drawn;
}

/// <summary>
/// Ladder between friends without a server: a result is a small code; whoever receives it replays the match on the same seed,
/// tactics and levels, so a doctored score shows up. Needs the identical simulator build (the build hash is in the result).
/// </summary>
public static class Ladder
{
    const int StepLimit = 150000;

    /// <summary>Identity of a tactic for the ladder: SHA-256 of the compact JSON (field order kept), first 16 hex characters.</summary>
    public static string TacticHash(string tacticJson)
    {
        var compact = JsonNode.Parse(tacticJson)?.ToJsonString() ?? throw new FormatException("Empty tactic.");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(compact)), 0, 8).ToLowerInvariant();
    }

    public static ulong SeedFor(string homeTactic, string awayTactic, int round)
    {
        if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
        var bytes = Encoding.UTF8.GetBytes(homeTactic + ":" + awayTactic + ":" + round.ToString(CultureInfo.InvariantCulture));
        return BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(bytes));
    }

    public static bool SamePair(LadderResult result, string homeTactic, string awayTactic) =>
        (result.HomeTactic == homeTactic && result.AwayTactic == awayTactic)
        || (result.HomeTactic == awayTactic && result.AwayTactic == homeTactic);

    public static int NextRound(IEnumerable<LadderResult> results, string homeTactic, string awayTactic) =>
        checked(results.Where(r => SamePair(r, homeTactic, awayTactic)).Select(r => r.Round).DefaultIfEmpty(0).Max() + 1);

    public static bool ContainsRound(IEnumerable<LadderResult> results, LadderResult claimed) =>
        results.Any(r => r.Round == claimed.Round && SamePair(r, claimed.HomeTactic, claimed.AwayTactic));

    public static LadderResult Play(string homeJson, string awayJson, string homeLevel, string awayLevel, int round, string squadsDirectory)
    {
        string homeHash = TacticHash(homeJson), awayHash = TacticHash(awayJson);
        ulong seed = SeedFor(homeHash, awayHash, round);
        var home = TacticLoader.Parse(homeJson);
        var away = TacticLoader.Parse(awayJson);
        var match = Match.Create(new MatchConfig(), home, away, seed,
            homeSquad: SquadFactory.ForLevel(homeLevel, home, squadsDirectory), awaySquad: SquadFactory.ForLevel(awayLevel, away, squadsDirectory));
        for (int step = 0; step < StepLimit && match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned); step++) match.Step();
        if (match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned)) throw new InvalidOperationException($"Match was not completed ({match.Status}).");
        var (homeGoals, awayGoals) = MatchStatistics.FromMatch(match).AwardedGoals();
        return new LadderResult(1, ReplayFile.CurrentBuildHash, seed, homeHash, awayHash, Name(home), Name(away),
            homeLevel, awayLevel, homeGoals, awayGoals, match.EventHash(), round);
    }

    static string Name(Tactic tactic) => tactic.Name.Length <= TacticExchange.MaxNameLength ? tactic.Name : tactic.Name[..TacticExchange.MaxNameLength];

    public static string ToCode(LadderResult result) => ShareCode.Encode(ShareCode.LadderKind, JsonSerializer.Serialize(result));

    public static LadderResult FromCode(string code)
    {
        LadderResult? r;
        try { r = JsonSerializer.Deserialize<LadderResult>(ShareCode.Decode(ShareCode.LadderKind, code)); }
        catch (JsonException e) { throw new FormatException("Result code does not hold a result.", e); }
        if (r is null || r.Version != 1 || string.IsNullOrWhiteSpace(r.Build) || string.IsNullOrWhiteSpace(r.EventHash)
            || !IsHash(r.HomeTactic) || !IsHash(r.AwayTactic) || r.HomeName is null || r.AwayName is null
            || !SquadFactory.Levels.Contains(r.HomeLevel) || !SquadFactory.Levels.Contains(r.AwayLevel)
            || r.HomeGoals is < 0 or > 99 || r.AwayGoals is < 0 or > 99 || r.Round < 1)
            throw new FormatException("Result code holds an invalid result.");
        return r;
    }

    static bool IsHash(string? s) => s is { Length: 16 } && s.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    /// <summary>
    /// Replays the claimed match on its seed, tactics and levels. False with reason "version" (other simulator build: no verdict),
    /// "tactic" (the given tactics are not the ones in the result) or "result" (replay gives another score or event hash).
    /// </summary>
    public static bool Verify(LadderResult claimed, string homeJson, string awayJson, string squadsDirectory, out string reason)
    {
        reason = "";
        if (claimed.Build != ReplayFile.CurrentBuildHash) { reason = "version"; return false; }
        if (TacticHash(homeJson) != claimed.HomeTactic || TacticHash(awayJson) != claimed.AwayTactic) { reason = "tactic"; return false; }
        if (claimed.Round < 1 || claimed.Seed != SeedFor(claimed.HomeTactic, claimed.AwayTactic, claimed.Round)) { reason = "result"; return false; }
        var replay = Play(homeJson, awayJson, claimed.HomeLevel, claimed.AwayLevel, claimed.Round, squadsDirectory);
        if (replay.HomeGoals != claimed.HomeGoals || replay.AwayGoals != claimed.AwayGoals || replay.EventHash != claimed.EventHash) { reason = "result"; return false; }
        return true;
    }

    /// <summary>Table per tactic (3/1/0 points) over all results, best first: points, goal difference, goals for, name.</summary>
    public static IReadOnlyList<LadderRow> Standings(IEnumerable<LadderResult> results)
    {
        var rows = new Dictionary<string, LadderRow>(StringComparer.Ordinal);
        void Add(string hash, string name, int gf, int ga)
        {
            var row = rows.TryGetValue(hash, out var r) ? r : new LadderRow(name, 0, 0, 0, 0, 0, 0);
            rows[hash] = row with
            {
                Played = row.Played + 1, Won = row.Won + (gf > ga ? 1 : 0), Drawn = row.Drawn + (gf == ga ? 1 : 0), Lost = row.Lost + (gf < ga ? 1 : 0),
                GoalsFor = row.GoalsFor + gf, GoalsAgainst = row.GoalsAgainst + ga,
            };
        }
        var counted = new List<LadderResult>();
        foreach (var r in results)
        {
            if (ContainsRound(counted, r)) continue;
            counted.Add(r);
            Add(r.HomeTactic, r.HomeName, r.HomeGoals, r.AwayGoals);
            Add(r.AwayTactic, r.AwayName, r.AwayGoals, r.HomeGoals);
        }
        return rows.Values.OrderByDescending(r => r.Points).ThenByDescending(r => r.GoalsFor - r.GoalsAgainst)
            .ThenByDescending(r => r.GoalsFor).ThenBy(r => r.Name, StringComparer.Ordinal).ToList();
    }
}
