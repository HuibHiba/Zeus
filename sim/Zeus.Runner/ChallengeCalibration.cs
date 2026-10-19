using System.Globalization;
using System.Text.Json;
using Zeus.Sim;

/// <summary>
/// --challenge-calibrate: plays player tactics against one opponent the way a challenge does (player at home, levels via SquadFactory)
/// and writes one JSON line per match with the <see cref="ChallengeMatchResult"/> fields, for threshold calibration on fresh seeds.
/// </summary>
internal static class ChallengeCalibration
{
    internal static int Run(string[] args)
    {
        string Value(string key, string fallback)
        {
            int i = Array.IndexOf(args, key);
            if (i < 0) return fallback;
            if (i + 1 == args.Length) throw new ArgumentException($"Missing value for {key}.");
            return args[i + 1];
        }
        string opponentName = Value("--opponent", "");
        if (opponentName.Length == 0) throw new ArgumentException("--opponent <tactic name> is required.");
        string opponentLevel = Value("--opponent-level", "halfprof"), playerLevel = Value("--level", "halfprof");
        int seeds = int.Parse(Value("--seeds", "40"), CultureInfo.InvariantCulture);
        int first = int.Parse(Value("--first-seed", "801"), CultureInfo.InvariantCulture);
        if (seeds < 1 || first < 1) throw new ArgumentException("Seeds must be positive.");
        int parallel = int.Parse(Value("--max-parallel", "1"), CultureInfo.InvariantCulture);
        if (parallel < 1) throw new ArgumentException("--max-parallel must be at least 1.");
        // Scenario challenges: --start-for N --start-against N (score at kick-off, player's view), --opponent-sent-off (ten men).
        int startFor = int.Parse(Value("--start-for", "0"), CultureInfo.InvariantCulture), startAgainst = int.Parse(Value("--start-against", "0"), CultureInfo.InvariantCulture);
        bool opponentSentOff = args.Contains("--opponent-sent-off");
        int startMinute = int.Parse(Value("--start-minute", "0"), CultureInfo.InvariantCulture);
        string path = Value("--results", "data/challenge-calibration.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var output = new StreamWriter(path, append: true);
        var opponent = TacticLoader.LoadFile($"tactics/{opponentName}.json");
        var opponentSquad = SquadFactory.ForLevel(opponentLevel, opponent, "squads");
        foreach (string name in Value("--tactics", "hoge_druk_433,positiespel_4231,counter_532,laag_blok_442").Split(','))
        {
            var player = TacticLoader.LoadFile($"tactics/{name}.json");
            var playerSquad = SquadFactory.ForLevel(playerLevel, player, "squads");
            // --max-parallel N plays N seeds at once (matches share no state); lines are still written in seed order.
            var lines = new string[seeds];
            Parallel.For(0, seeds, new ParallelOptions { MaxDegreeOfParallelism = parallel }, i =>
            {
                int seed = first + i;
                var match = Match.Create(new MatchConfig(), player, opponent, (ulong)seed, homeSquad: playerSquad, awaySquad: opponentSquad);
                if (startFor > 0 || startAgainst > 0 || opponentSentOff || startMinute > 0) match.StartScenario(startFor, startAgainst, opponentSentOff ? Side.Away : null, startMinute);
                for (int tick = 0; tick < 150000 && match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned); tick++)
                    match.Step();
                var r = ChallengeEvaluator.ToResult(MatchStatistics.FromMatch(match));
                lines[i] = JsonSerializer.Serialize(new { tactic = name, opponent = opponentName, playerLevel, opponentLevel, seed, startFor, startAgainst, opponentSentOff, startMinute,
                    gf = r.GoalsFor, ga = r.GoalsAgainst, sf = r.ShotsFor, sa = r.ShotsAgainst, cf = r.ControlsFor, ca = r.ControlsAgainst,
                    fouls = r.Fouls, yellow = r.YellowCards, red = r.RedCards, abandoned = r.Abandoned, finished = match.Status == MatchStatus.Finished });
            });
            foreach (var line in lines) output.WriteLine(line);
            output.Flush();
            Console.WriteLine($"{name} vs {opponentName} ({playerLevel}/{opponentLevel}): {seeds} matches");
        }
        return 0;
    }
}
