using System.Globalization;
using Zeus.Sim;

// B4 measurement: a fixed player tactic is played 3 times in the same style, then the opponent counters (PlayStyleProfile.Counter).
// Win% of the player against the club's normal opponent tactic ("before") and against the counter ("after"), per level pair.
internal static class AdaptiveCheck
{
    // Sequential by design: must also fit on a machine with limited memory.
    internal static int Run(string[] args)
    {
        string Value(string key, string fallback)
        {
            int i = Array.IndexOf(args, key);
            if (i < 0) return fallback;
            if (i + 1 == args.Length) throw new ArgumentException($"Missing value for {key}.");
            return args[i + 1];
        }
        int seeds = int.Parse(Value("--seeds", "40"), CultureInfo.InvariantCulture);
        int first = int.Parse(Value("--first-seed", "441"), CultureInfo.InvariantCulture);
        if (seeds < 1 || first < 1) throw new ArgumentException("Seeds must be positive.");
        var player = TacticLoader.LoadFile($"tactics/{Value("--player", "hoge_druk_433")}.json");
        var normal = TacticLoader.LoadFile($"tactics/{Value("--opponent", "positiespel_4231")}.json");
        // Same samples as three matches with this tactic: the opponent only sees its settings.
        var sample = PlayStyleProfile.Sample(player);
        var style = PlayStyleProfile.Detect(new[] { sample, sample, sample });
        if (PlayStyleProfile.Counter(style) is not { } counterInfo)
        {
            Console.WriteLine($"Player tactic '{player.Name}' has no recognised style ({style}): nothing to adapt to.");
            return 2;
        }
        var counter = TacticLoader.LoadFile($"tactics/{counterInfo.TacticFile}");
        Console.WriteLine($"player={player.Name} style={style} normal={normal.Name} counter={counter.Name} seeds={first}..{first + seeds - 1}");
        foreach (var pair in Value("--pairs", "amateur:amateur,halfprof:halfprof,prof:prof,prof:amateur").Split(','))
        {
            var parts = pair.Split(':');
            if (parts.Length != 2 || !SquadFactory.Levels.Contains(parts[0]) || !SquadFactory.Levels.Contains(parts[1]))
                throw new ArgumentException($"Unknown level pair '{pair}'.");
            var before = Play(player, normal, parts[0], parts[1], first, seeds);
            var after = Play(player, counter, parts[0], parts[1], first, seeds);
            double Win(List<(int A, int B)> r) => r.Count(s => s.A > s.B) / (double)r.Count;
            double Diff(List<(int A, int B)> r) => r.Average(s => s.A - s.B);
            Console.WriteLine(FormattableString.Invariant(
                $"{pair}: win before={Win(before):P1} after={Win(after):P1} delta={(Win(after) - Win(before)) * 100:F1}pt, goal diff before={Diff(before):F2} after={Diff(after):F2}"
                + (parts[1] == "halfprof" ? $", expected at 50% adaptation={(Win(before) + Win(after)) / 2:P1}" : "")));
        }
        return 0;
    }

    static List<(int A, int B)> Play(Tactic player, Tactic opponent, string playerLevel, string opponentLevel, int first, int seeds)
    {
        var a = SquadFactory.ForLevel(playerLevel, player, "squads");
        var b = SquadFactory.ForLevel(opponentLevel, opponent, "squads");
        var scores = new List<(int A, int B)>();
        for (int seed = first; seed < first + seeds; seed++)
        {
            var match = Match.Create(new MatchConfig(), player, opponent, (ulong)seed, homeSquad: a, awaySquad: b);
            for (int tick = 0; tick < 150000 && match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned); tick++) match.Step();
            if (match.Status != MatchStatus.Finished) throw new InvalidOperationException($"{opponent.Name}/{seed}: {match.Status}");
            var stats = MatchStatistics.FromMatch(match);
            scores.Add((stats.Home.Goals, stats.Away.Goals));
        }
        return scores;
    }
}
