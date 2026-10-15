using System.Globalization;
using System.Text.Json;

namespace Zeus.Sim;

/// <summary>One goal of a challenge, e.g. "average goals against &lt;= 1.0". Metrics are listed in <see cref="ChallengeEvaluator.Metrics"/>.</summary>
public sealed record ChallengeCriterion(string Metric, string Operator, double Value);

public sealed record ChallengeSpec(string Id, string Name, string Description, string Opponent, int Matches,
    IReadOnlyList<ChallengeCriterion> Criteria, string Hint, bool Adaptive = false, string OpponentLevel = "neutraal");

/// <summary>Outcome of one played match, seen from the player's side.</summary>
public sealed record ChallengeMatchResult(int GoalsFor, int GoalsAgainst, int ShotsFor, int ShotsAgainst,
    int ControlsFor, int ControlsAgainst, int Fouls, int YellowCards, int RedCards);

public sealed record CriterionResult(ChallengeCriterion Criterion, double Actual, bool Passed, string Text);

public sealed record ChallengeResult(ChallengeSpec Spec, IReadOnlyList<CriterionResult> Criteria, int Wins, int Draws, int Losses)
{
    public bool Passed => Criteria.Count > 0 && Criteria.All(c => c.Passed);
}

public static class ChallengeEvaluator
{
    /// <summary>Metric id -> (Dutch label, unit text, true when the value is an average per match, false when a total/count).</summary>
    public static readonly IReadOnlyDictionary<string, (string Label, string Unit, bool Average)> Metrics =
        new Dictionary<string, (string, string, bool)>
        {
            ["doelpunten_voor"] = ("doelpunten gemaakt", "per wedstrijd", true),
            ["doelpunten_tegen"] = ("doelpunten tegen", "per wedstrijd", true),
            ["doelsaldo"] = ("doelsaldo", "per wedstrijd", true),
            ["schoten_voor"] = ("schoten", "per wedstrijd", true),
            ["schoten_tegen"] = ("schoten tegen", "per wedstrijd", true),
            ["balbezit"] = ("balbezit", "%", true),
            ["overtredingen"] = ("overtredingen", "per wedstrijd", true),
            ["gele_kaarten"] = ("gele kaarten", "per wedstrijd", true),
            ["rode_kaarten"] = ("rode kaarten", "in totaal", false),
            ["winst"] = ("gewonnen wedstrijden", "", false),
            ["ongeslagen"] = ("wedstrijden niet verloren", "", false),
        };

    public static IReadOnlyList<string> Operators { get; } = new[] { ">=", "<=" };

    public static ChallengeSpec Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        string Str(string name) => r.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString()! : "";
        var criteria = new List<ChallengeCriterion>();
        if (r.TryGetProperty("criteria", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var c in arr.EnumerateArray())
            {
                var metric = c.GetProperty("maat").GetString() ?? "";
                var op = c.GetProperty("operator").GetString() ?? "";
                if (!Metrics.ContainsKey(metric)) throw new FormatException($"Unknown metric '{metric}'.");
                if (!Operators.Contains(op)) throw new FormatException($"Unknown operator '{op}'.");
                criteria.Add(new ChallengeCriterion(metric, op, c.GetProperty("waarde").GetDouble()));
            }
        if (Str("id").Length == 0) throw new FormatException("A challenge needs an id.");
        if (criteria.Count == 0) throw new FormatException($"Challenge '{Str("id")}' has no criteria.");
        int matches = r.TryGetProperty("wedstrijden", out var m) ? m.GetInt32() : 5;
        if (matches is < 1 or > 50) throw new FormatException("'wedstrijden' must be between 1 and 50.");
        if (Str("tegenstander").Length == 0) throw new FormatException("A challenge needs an opponent tactic file.");
        bool adaptive = r.TryGetProperty("past_zich_aan", out var ad) && ad.ValueKind == JsonValueKind.True;
        var level = Str("niveau_tegenstander").Length > 0 ? Str("niveau_tegenstander") : "neutraal";
        if (!SquadFactory.Levels.Contains(level)) throw new FormatException($"Unknown opponent level '{level}'.");
        return new ChallengeSpec(Str("id"), Str("naam"), Str("uitleg"), Str("tegenstander"), matches, criteria, Str("tip"), adaptive, level);
    }

    public static IReadOnlyList<ChallengeSpec> LoadAll(string directory) =>
        !Directory.Exists(directory) ? Array.Empty<ChallengeSpec>()
            : Directory.GetFiles(directory, "*.json").OrderBy(f => f, StringComparer.Ordinal).Select(f => Parse(File.ReadAllText(f))).ToList();

    public static double Value(string metric, IReadOnlyList<ChallengeMatchResult> results)
    {
        if (results.Count == 0) return 0;
        double Avg(Func<ChallengeMatchResult, double> f) => results.Average(f);
        return metric switch
        {
            "doelpunten_voor" => Avg(x => x.GoalsFor),
            "doelpunten_tegen" => Avg(x => x.GoalsAgainst),
            "doelsaldo" => Avg(x => x.GoalsFor - x.GoalsAgainst),
            "schoten_voor" => Avg(x => x.ShotsFor),
            "schoten_tegen" => Avg(x => x.ShotsAgainst),
            "balbezit" => 100.0 * results.Sum(x => x.ControlsFor) / Math.Max(1, results.Sum(x => x.ControlsFor + x.ControlsAgainst)),
            "overtredingen" => Avg(x => x.Fouls),
            "gele_kaarten" => Avg(x => x.YellowCards),
            "rode_kaarten" => results.Sum(x => x.RedCards),
            "winst" => results.Count(x => x.GoalsFor > x.GoalsAgainst),
            "ongeslagen" => results.Count(x => x.GoalsFor >= x.GoalsAgainst),
            _ => throw new ArgumentException($"Unknown metric '{metric}'.", nameof(metric)),
        };
    }

    static string Num(double v) => v.ToString(v % 1 == 0 ? "0" : "0.0", CultureInfo.GetCultureInfo("nl-NL"));

    public static ChallengeResult Evaluate(ChallengeSpec spec, IReadOnlyList<ChallengeMatchResult> results)
    {
        var rows = new List<CriterionResult>();
        foreach (var c in spec.Criteria)
        {
            var actual = Value(c.Metric, results);
            bool ok = c.Operator == ">=" ? actual >= c.Value - 1e-9 : actual <= c.Value + 1e-9;
            var (label, unit, _) = Metrics[c.Metric];
            var want = $"{label} {(c.Operator == ">=" ? "minstens" : "hoogstens")} {Num(c.Value)}{(unit.Length > 0 ? " " + unit : "")}";
            rows.Add(new CriterionResult(c, actual, ok, $"{want}  (jij: {Num(Math.Round(actual, 1))})"));
        }
        return new ChallengeResult(spec, rows, results.Count(x => x.GoalsFor > x.GoalsAgainst),
            results.Count(x => x.GoalsFor == x.GoalsAgainst), results.Count(x => x.GoalsFor < x.GoalsAgainst));
    }

    public static ChallengeMatchResult ToResult(MatchStatistics stats) => new(
        stats.Home.Goals, stats.Away.Goals, stats.Home.Shots, stats.Away.Shots, stats.Home.BallControls, stats.Away.BallControls,
        stats.Home.Fouls, stats.Home.YellowCards, stats.Home.RedCards);

    /// <summary>
    /// Plays the challenge's matches (seeds 1..N, the player is the home side) in parallel and returns the results in seed order,
    /// so the outcome is identical to playing them one after another. Matches share no mutable state.
    /// <paramref name="progress"/> receives the total progress in finished-match equivalents (0..N) from worker threads.
    /// </summary>
    public static List<ChallengeMatchResult> Play(ChallengeSpec spec, Tactic player, Tactic opponent,
        CancellationToken token = default, Action<double>? progress = null, int stepLimit = 150000, int maxParallel = 0,
        Squad? playerSquad = null, Squad? opponentSquad = null)
    {
        int n = spec.Matches;
        var results = new ChallengeMatchResult[n];
        var fractions = new double[n];
        double reported = 0;
        void Report() { if (progress is null) return; lock (fractions) { reported = Math.Max(reported, fractions.Sum()); progress(reported); } } // non-decreasing, called under the lock: the callback must still be thread-safe (any worker thread)
        int degree = maxParallel > 0 ? maxParallel : Math.Max(1, Math.Min(n, Environment.ProcessorCount - 1));
        ParallelErrors.For(n, new ParallelOptions { MaxDegreeOfParallelism = degree, CancellationToken = token }, i =>
        {
            var match = Match.Create(new MatchConfig(), player, opponent, (ulong)(i + 1), homeSquad: playerSquad, awaySquad: opponentSquad);
            int steps = 0;
            while (match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned) && steps < stepLimit)
            {
                match.Step();
                if (++steps % 256 != 0) continue;
                token.ThrowIfCancellationRequested();
                lock (fractions) fractions[i] = Math.Min(0.99, match.TimeSeconds / 5400);
                Report();
            }
            if (match.Status != MatchStatus.Finished)
                throw new InvalidOperationException($"Wedstrijd {i + 1} werd niet afgerond ({match.Status}).");
            results[i] = ToResult(MatchStatistics.FromMatch(match));
            lock (fractions) fractions[i] = 1;
            Report();
        });
        return results.ToList();
    }
}
