using System.Globalization;
using System.Text.Json;

namespace Zeus.Sim;

/// <summary>One goal of a challenge, e.g. "average goals against &lt;= 1.0". Metrics are listed in <see cref="ChallengeEvaluator.Metrics"/>.</summary>
public sealed record ChallengeCriterion(string Metric, string Operator, double Value);

public sealed record ChallengeSpec(string Id, string Name, string Description, string Opponent, int Matches,
    IReadOnlyList<ChallengeCriterion> Criteria, string Hint, bool Adaptive = false, string OpponentLevel = "neutraal",
    string StartTactic = "", string StartNote = "", IReadOnlyList<IReadOnlyList<ChallengeCriterion>>? StarCriteria = null,
    string PlayerLevel = "", int StartFor = 0, int StartAgainst = 0, bool OpponentSentOff = false, string Theme = "", int StartMinute = 0)
{
    /// <summary>True when the match does not start as a normal 0-0 at 0' with eleven a side (see <see cref="Match.StartScenario"/>).</summary>
    public bool HasScenario => StartFor > 0 || StartAgainst > 0 || OpponentSentOff || StartMinute > 0;

    /// <summary>Applies the scenario to a freshly created match (the player is the home side); nothing for a normal challenge.</summary>
    public void ApplyScenario(Match match)
    {
        if (HasScenario) match.StartScenario(StartFor, StartAgainst, OpponentSentOff ? Side.Away : null, StartMinute);
    }

    /// <summary>Most stars this challenge can give: 1 for passing, plus one per stricter level ("ster_2", "ster_3" in the JSON).</summary>
    public int MaxStars => 1 + (StarCriteria?.Count ?? 0);
}

/// <summary>Built-in tactics that only become available after enough challenge stars; every other tactic is always available.</summary>
public static class TacticUnlocks
{
    /// <summary>Catalog id (file name) -> stars needed, in unlock order.</summary>
    public static readonly IReadOnlyList<(string Id, int Stars)> Required = new[]
    {
        ("lange_bal_442.json", 3), ("flankdruk_352.json", 7), ("vleugelspel_433.json", 9),
    };

    /// <summary>Total stars needed for this catalog id (0 = always available). Own tactics ("own@...") are never locked.</summary>
    public static int StarsNeeded(string id) => Required.FirstOrDefault(r => r.Id == id).Stars;

    public static bool IsUnlocked(string id, int stars) => stars >= StarsNeeded(id);
}

/// <summary>Outcome of one played match, seen from the player's side.</summary>
public sealed record ChallengeMatchResult(int GoalsFor, int GoalsAgainst, int ShotsFor, int ShotsAgainst,
    int ControlsFor, int ControlsAgainst, int Fouls, int YellowCards, int RedCards, bool Abandoned = false);

public sealed record CriterionResult(ChallengeCriterion Criterion, double Actual, bool Passed, string Text);

/// <summary>Interim state of one goal while a challenge is played live: what it is now, and whether the remaining matches can still make it.</summary>
public sealed record GoalStanding(CriterionResult Current, bool Possible)
{
    public bool OnTrack => Current.Passed;
}

/// <summary>The standing after the first <see cref="Played"/> of <see cref="ChallengeSpec.Matches"/> matches; <see cref="Result"/> is exactly what Evaluate gives for those matches.</summary>
public sealed record ChallengeStanding(ChallengeSpec Spec, int Played, IReadOnlyList<GoalStanding> Goals, ChallengeResult Result)
{
    public bool Complete => Played >= Spec.Matches;
    public bool AllPossible => Goals.All(g => g.Possible);
}

public sealed record ChallengeResult(ChallengeSpec Spec, IReadOnlyList<CriterionResult> Criteria, int Wins, int Draws, int Losses, int Abandoned = 0)
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

    public static ChallengeSpec Parse(string json, string language = "nl")
    {
        try { return ParseCore(json, language); }
        catch (FormatException) { throw; }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or OverflowException or NullReferenceException)
        {
            throw new FormatException("Ongeldige uitdaging: " + e.Message, e);
        }
    }

    static ChallengeSpec ParseCore(string json, string language)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        if (r.ValueKind != JsonValueKind.Object) throw new FormatException("A challenge must be a JSON object.");
        string Str(string name) => r.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString()! : "";
        string Text(string name) => Str(name + "_" + language) is { Length: > 0 } localized ? localized : Str(name);
        var criteria = Criteria("criteria");
        // Optional stricter levels: "ster_2" (and then "ster_3") are extra criteria on top of the normal ones.
        var stars = new List<IReadOnlyList<ChallengeCriterion>>();
        foreach (var key in new[] { "ster_2", "ster_3" })
        {
            if (!r.TryGetProperty(key, out _)) break;
            var extra = Criteria(key);
            if (extra.Count == 0) throw new FormatException($"'{key}' needs at least one criterion.");
            stars.Add(extra);
        }
        if (stars.Count < 2 && r.TryGetProperty("ster_3", out _)) throw new FormatException("'ster_3' needs 'ster_2'.");

        List<ChallengeCriterion> Criteria(string name)
        {
            var list = new List<ChallengeCriterion>();
            if (!r.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return list;
            foreach (var c in arr.EnumerateArray())
            {
                if (c.ValueKind != JsonValueKind.Object) throw new FormatException("A criterion must be an object.");
                var metric = c.TryGetProperty("maat", out var mp) && mp.ValueKind == JsonValueKind.String ? mp.GetString() ?? "" : "";
                var op = c.TryGetProperty("operator", out var op2) && op2.ValueKind == JsonValueKind.String ? op2.GetString() ?? "" : "";
                if (!Metrics.ContainsKey(metric)) throw new FormatException($"Unknown metric '{metric}'.");
                if (!Operators.Contains(op)) throw new FormatException($"Unknown operator '{op}'.");
                if (!c.TryGetProperty("waarde", out var wp) || wp.ValueKind != JsonValueKind.Number || !wp.TryGetDouble(out var waarde) || double.IsNaN(waarde) || double.IsInfinity(waarde))
                    throw new FormatException($"Criterion '{metric}' needs a numeric 'waarde'.");
                list.Add(new ChallengeCriterion(metric, op, waarde));
            }
            return list;
        }

        if (Str("id").Length == 0) throw new FormatException("A challenge needs an id.");
        if (criteria.Count == 0) throw new FormatException($"Challenge '{Str("id")}' has no criteria.");
        int matches = 5;
        if (r.TryGetProperty("wedstrijden", out var m) && (m.ValueKind != JsonValueKind.Number || !m.TryGetInt32(out matches)))
            throw new FormatException("'wedstrijden' must be a whole number.");
        if (matches is < 1 or > 50) throw new FormatException("'wedstrijden' must be between 1 and 50.");
        if (Str("tegenstander").Length == 0) throw new FormatException("A challenge needs an opponent tactic file.");
        if (!TacticFileNames.IsBare(Str("tegenstander"))) throw new FormatException("'tegenstander' must be a plain file name without a path.");
        bool adaptive = r.TryGetProperty("past_zich_aan", out var ad) && ad.ValueKind == JsonValueKind.True;
        var level = Str("niveau_tegenstander").Length > 0 ? Str("niveau_tegenstander") : "neutraal";
        if (!SquadFactory.Levels.Contains(level)) throw new FormatException($"Unknown opponent level '{level}'.");
        var playerLevel = Str("niveau_speler");     // optional: fixes the player's squad level (e.g. "press with amateurs"); empty = free choice
        if (playerLevel.Length > 0 && !SquadFactory.Levels.Contains(playerLevel)) throw new FormatException($"Unknown player level '{playerLevel}'.");
        // Optional scenario: score at kick-off (player's view) and the opponent a man short from the start.
        int Goals(string name)
        {
            if (!r.TryGetProperty(name, out var g)) return 0;
            if (g.ValueKind != JsonValueKind.Number || !g.TryGetInt32(out var value) || value < 0 || value > Match.MaxScenarioGoals)
                throw new FormatException($"'{name}' must be a whole number between 0 and {Match.MaxScenarioGoals}.");
            return value;
        }
        bool sentOff = r.TryGetProperty("tegenstander_rood", out var red) && red.ValueKind == JsonValueKind.True;
        int minute = 0;
        if (r.TryGetProperty("start_minuut", out var sm) && (sm.ValueKind != JsonValueKind.Number || !sm.TryGetInt32(out minute)
            || (minute != 0 && (minute < Match.MinScenarioMinute || minute > Match.MaxScenarioMinute))))
            throw new FormatException($"'start_minuut' must be 0 or a whole number between {Match.MinScenarioMinute} and {Match.MaxScenarioMinute}.");
        return new ChallengeSpec(Str("id"), Text("naam"), Text("uitleg"), Str("tegenstander"), matches, criteria, Text("tip"), adaptive, level,
            TacticFileNames.IsBare(Str("start_tactiek")) ? Str("start_tactiek") : "", Text("start"), stars.Count > 0 ? stars : null, playerLevel,
            Goals("start_voor"), Goals("start_tegen"), sentOff, Text("thema"), minute);
    }

    /// <summary>Stars earned with these results: 0 when not passed, 1 for passing, one more per stricter level that is also met (in order).</summary>
    public static int Stars(ChallengeSpec spec, IReadOnlyList<ChallengeMatchResult> results)
    {
        if (!Evaluate(spec, results).Passed) return 0;
        int stars = 1;
        foreach (var level in spec.StarCriteria ?? Array.Empty<IReadOnlyList<ChallengeCriterion>>())
        {
            if (!level.All(c => Meets(c, Value(c.Metric, results)))) break;
            stars++;
        }
        return stars;
    }

    /// <summary>Loads every valid challenge in a directory; a bad file is skipped and reported in <paramref name="errors"/> (duplicate ids count as bad).</summary>
    public static IReadOnlyList<ChallengeSpec> LoadAll(string directory, out IReadOnlyList<string> errors, string language = "nl")
    {
        var specs = new List<ChallengeSpec>();
        var problems = new List<string>();
        errors = problems;
        if (!Directory.Exists(directory)) return specs;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(directory, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            try
            {
                var spec = Parse(File.ReadAllText(file), language);
                if (!seen.Add(spec.Id)) throw new FormatException($"Duplicate challenge id '{spec.Id}'.");
                specs.Add(spec);
            }
            catch (Exception e) when (e is not OutOfMemoryException) { problems.Add($"{Path.GetFileName(file)}: {e.Message}"); }
        }
        return specs;
    }

    public static IReadOnlyList<ChallengeSpec> LoadAll(string directory) => LoadAll(directory, out _);

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

    static bool Meets(ChallengeCriterion c, double actual) => c.Operator == ">=" ? actual >= c.Value - 1e-9 : actual <= c.Value + 1e-9;

    /// <summary>
    /// Tussenstand after the matches played so far: the normal evaluation of those matches, plus per goal whether it can still be met
    /// if every remaining match went as well as possible for that goal (a bound, not a prediction).
    /// </summary>
    public static ChallengeStanding Standing(ChallengeSpec spec, IReadOnlyList<ChallengeMatchResult> played)
    {
        var current = Evaluate(spec, played);
        int remaining = Math.Max(0, spec.Matches - played.Count);
        var goals = new List<GoalStanding>();
        foreach (var row in current.Criteria)
        {
            bool possible = row.Passed;
            if (remaining > 0)
            {
                var all = played.Concat(Enumerable.Repeat(BestCase(row.Criterion), remaining)).ToList();
                possible = Meets(row.Criterion, Value(row.Criterion.Metric, all));
            }
            goals.Add(new GoalStanding(row, possible));
        }
        return new ChallengeStanding(spec, played.Count, goals, current);
    }

    /// <summary>The most favourable possible match for one criterion (extreme but finite numbers).</summary>
    static ChallengeMatchResult BestCase(ChallengeCriterion c)
    {
        const int Big = 1000, BigControls = 1_000_000;
        bool atLeast = c.Operator == ">=";
        int Hi(bool want) => want ? Big : 0;
        return c.Metric switch
        {
            "doelpunten_voor" => new(Hi(atLeast), 0, 0, 0, 0, 0, 0, 0, 0),
            "doelpunten_tegen" => new(0, Hi(atLeast), 0, 0, 0, 0, 0, 0, 0),
            "doelsaldo" => atLeast ? new(Big, 0, 0, 0, 0, 0, 0, 0, 0) : new(0, Big, 0, 0, 0, 0, 0, 0, 0),
            "schoten_voor" => new(0, 0, Hi(atLeast), 0, 0, 0, 0, 0, 0),
            "schoten_tegen" => new(0, 0, 0, Hi(atLeast), 0, 0, 0, 0, 0),
            "balbezit" => atLeast ? new(0, 0, 0, 0, BigControls, 0, 0, 0, 0) : new(0, 0, 0, 0, 0, BigControls, 0, 0, 0),
            "overtredingen" => new(0, 0, 0, 0, 0, 0, Hi(atLeast), 0, 0),
            "gele_kaarten" => new(0, 0, 0, 0, 0, 0, 0, Hi(atLeast), 0),
            "rode_kaarten" => new(0, 0, 0, 0, 0, 0, 0, 0, Hi(atLeast)),
            "winst" or "ongeslagen" => atLeast ? new(1, 0, 0, 0, 0, 0, 0, 0, 0)
                : (c.Metric == "winst" ? new(0, 0, 0, 0, 0, 0, 0, 0, 0) : new(0, 1, 0, 0, 0, 0, 0, 0, 0)),
            _ => new(0, 0, 0, 0, 0, 0, 0, 0, 0),
        };
    }

    public static ChallengeResult Evaluate(ChallengeSpec spec, IReadOnlyList<ChallengeMatchResult> results)
    {
        var rows = new List<CriterionResult>();
        foreach (var c in spec.Criteria)
        {
            var actual = Value(c.Metric, results);
            bool ok = Meets(c, actual);
            var (label, unit, _) = Metrics[c.Metric];
            var want = $"{label} {(c.Operator == ">=" ? "minstens" : "hoogstens")} {Num(c.Value)}{(unit.Length > 0 ? " " + unit : "")}";
            rows.Add(new CriterionResult(c, actual, ok, $"{want}  (jij: {Num(Math.Round(actual, 1))})"));
        }
        return new ChallengeResult(spec, rows, results.Count(x => x.GoalsFor > x.GoalsAgainst),
            results.Count(x => x.GoalsFor == x.GoalsAgainst), results.Count(x => x.GoalsFor < x.GoalsAgainst), results.Count(x => x.Abandoned));
    }

    /// <summary>Uses <see cref="MatchStatistics.AwardedGoals"/>, so an abandoned match has a defined result instead of an error.</summary>
    public static ChallengeMatchResult ToResult(MatchStatistics stats)
    {
        var (home, away) = stats.AwardedGoals();
        return new(home, away, stats.Home.Shots, stats.Away.Shots, stats.Home.BallControls, stats.Away.BallControls,
            stats.Home.Fouls, stats.Home.YellowCards, stats.Home.RedCards, stats.Abandoned);
    }

    /// <summary>
    /// Plays the challenge's matches (seeds 1..N, the player is the home side) in parallel and returns the results in seed order,
    /// so the outcome is identical to playing them one after another. Matches share no mutable state.
    /// <paramref name="progress"/> receives the total progress in finished-match equivalents (0..N) from worker threads.
    /// </summary>
    public static List<ChallengeMatchResult> Play(ChallengeSpec spec, Tactic player, Tactic opponent,
        CancellationToken token = default, Action<double>? progress = null, int stepLimit = 150000, int maxParallel = 0,
        Squad? playerSquad = null, Squad? opponentSquad = null, bool opponentSubstitutions = false)
    {
        int n = spec.Matches;
        var results = new ChallengeMatchResult[n];
        var fractions = new double[n];
        double reported = 0;
        void Report() { if (progress is null) return; lock (fractions) { reported = Math.Max(reported, fractions.Sum()); progress(reported); } } // non-decreasing, called under the lock: the callback must still be thread-safe (any worker thread)
        int degree = SimulationBatch.WorkerCount(n, Environment.ProcessorCount, maxParallel);
        ParallelErrors.For(n, new ParallelOptions { MaxDegreeOfParallelism = degree, CancellationToken = token }, i =>
        {
            var match = Match.Create(new MatchConfig(), player, opponent, (ulong)(i + 1), homeSquad: playerSquad, awaySquad: opponentSquad);
            spec.ApplyScenario(match);
            var substitutions = opponentSubstitutions ? new OpponentSubstitutionPolicy() : null;
            int steps = 0;
            while (match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned) && steps < stepLimit)
            {
                if (match.IsRestMoment) substitutions?.TrySubstitute(match, Side.Away);
                match.Step();
                if (++steps % 64 != 0) continue;
                token.ThrowIfCancellationRequested();
                lock (fractions) fractions[i] = Math.Min(0.99, match.TimeSeconds / 5400);
                Report();
            }
            if (match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned))
                throw new InvalidOperationException($"Wedstrijd {i + 1} werd niet afgerond ({match.Status}).");
            results[i] = ToResult(MatchStatistics.FromMatch(match));
            lock (fractions) fractions[i] = 1;
            Report();
        });
        token.ThrowIfCancellationRequested();
        return results.ToList();
    }
}
