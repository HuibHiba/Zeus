using System.Globalization;
using System.Text.Json;
using Zeus.Sim;

internal static class LevelCalibration
{
    // Sequential by design: calibration must also fit on a machine with limited memory.
    internal static int Run(string[] args)
    {
        string Value(string key, string fallback)
        {
            int i = Array.IndexOf(args, key);
            if (i < 0) return fallback;
            if (i + 1 == args.Length) throw new ArgumentException($"Missing value for {key}.");
            return args[i + 1];
        }
        string reuse = Value("--reuse-results", "");
        if (reuse.Length > 0)
        {
            var saved = new Dictionary<string, List<(int A, int B)>>();
            foreach (string line in File.ReadLines(reuse))
            {
                using var document = JsonDocument.Parse(line);
                var row = document.RootElement;
                string pair = row.GetProperty("home").GetString() + "/" + row.GetProperty("away").GetString();
                int a = row.GetProperty("goalsA").GetInt32(), b = row.GetProperty("goalsB").GetInt32();
                if (a < 0 || b < 0) throw new ArgumentException("Negative goal count.");
                if (!saved.ContainsKey(pair)) saved[pair] = new();
                saved[pair].Add((a, b));
            }
            if (saved.Count == 0) throw new ArgumentException("No saved results.");
            return Summarize(saved, args.Contains("--verify-levels"));
        }
        int seeds = int.Parse(Value("--seeds", "40"), CultureInfo.InvariantCulture);
        int first = int.Parse(Value("--first-seed", "1"), CultureInfo.InvariantCulture);
        if (seeds < 1 || first < 1) throw new ArgumentException("Seeds must be positive.");
        string path = Value("--results", "data/level-calibration.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var output = new StreamWriter(path);
        var totals = new Dictionary<string, List<(int A, int B)>>();
        // --config <json>: partial MatchConfig overrides for a measurement (same shape as the main runner's --config).
        string configPath = Value("--config", "");
        var config = configPath.Length == 0 ? new MatchConfig() : MatchConfigJson.Parse(File.ReadAllText(configPath));
        var shotTotals = new Dictionary<string, List<int>>();
        var requested = Value("--pairs", "amateur:amateur,halfprof:halfprof,prof:prof,prof:amateur").Split(',');
        var pairs = new[] { ("amateur", "amateur"), ("halfprof", "halfprof"), ("prof", "prof"), ("prof", "amateur") }
            .Where(p => requested.Contains(p.Item1 + ":" + p.Item2)).ToArray();
        if (pairs.Length != requested.Length) throw new ArgumentException("Unknown or duplicate level pair.");
        foreach (string name in Value("--tactics", "hoge_druk_433,positiespel_4231,counter_532").Split(','))
        {
            var tactic = TacticLoader.LoadFile($"tactics/{name}.json");
            foreach (var (home, away) in pairs)
            {
                var a = SquadFactory.ForLevel(home, tactic, "squads");
                var b = SquadFactory.ForLevel(away, tactic, "squads");
                var scores = new List<(int A, int B)>();
                string pair = home + "/" + away;
                if (!totals.ContainsKey(pair)) totals[pair] = new();
                for (int seed = first; seed < first + seeds; seed++)
                {
                    // Alternate the stronger team's venue without counting a seed twice.
                    bool swap = home != away && seed % 2 == 0;
                    var match = Match.Create(config, tactic, tactic, (ulong)seed,
                        homeSquad: swap ? b : a, awaySquad: swap ? a : b);
                    for (int tick = 0; tick < 150000 && match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned); tick++)
                        match.Step();
                    if (match.Status != MatchStatus.Finished) throw new InvalidOperationException($"{name}/{home}/{away}/{seed}: {match.Status}");
                    var stats = MatchStatistics.FromMatch(match);
                    int ga = swap ? stats.Away.Goals : stats.Home.Goals;
                    int gb = swap ? stats.Home.Goals : stats.Away.Goals;
                    scores.Add((ga, gb));
                    if (!shotTotals.ContainsKey(pair)) shotTotals[pair] = new();
                    shotTotals[pair].Add(stats.Home.Shots + stats.Away.Shots);
                    totals[pair].Add((ga, gb));
                    output.WriteLine(JsonSerializer.Serialize(new { tactic = name, home, away, seed, swap, goalsA = ga, goalsB = gb,
                        shots = stats.Home.Shots + stats.Away.Shots, eventHash = match.EventHash() }));
                    output.Flush();
                }
                Console.WriteLine(FormattableString.Invariant($"{name} {home}/{away}: n={seeds}, goals={scores.Average(s => s.A):F3}+{scores.Average(s => s.B):F3}={scores.Average(s => s.A + s.B):F3}, W/D/L={scores.Count(s => s.A > s.B)}/{scores.Count(s => s.A == s.B)}/{scores.Count(s => s.A < s.B)}, maxGap={scores.Max(s => Math.Abs(s.A - s.B))}"));
            }
        }
        foreach (var (pair, shots) in shotTotals)
            Console.WriteLine(FormattableString.Invariant($"SHOTS {pair}: {shots.Average():F1} per match"));
        return Summarize(totals, args.Contains("--verify-levels"));
    }

    private static int Summarize(Dictionary<string, List<(int A, int B)>> totals, bool verify)
    {
        bool passed = true;
        foreach (var (pair, scores) in totals)
        {
            double goals = scores.Average(s => s.A + s.B);
            double wins = scores.Count(s => s.A > s.B) / (double)scores.Count;
            int maxGap = scores.Max(s => Math.Abs(s.A - s.B));
            int largeGaps = scores.Count(s => Math.Abs(s.A - s.B) >= 8);
            bool within = goals is >= 2.2 and <= 3.2 && largeGaps / (double)scores.Count <= .01
                && (pair != "prof/amateur" || wins is >= .60 and <= .75);
            passed &= within;
            Console.WriteLine(FormattableString.Invariant($"ALL {pair}: n={scores.Count}, goals={goals:F3}, win={wins:P1}, maxGap={maxGap}, gap8Plus={largeGaps}/{scores.Count}, bounds={(within ? "PASS" : "FAIL")}"));
        }
        return verify && !passed ? 3 : 0;
    }
}
