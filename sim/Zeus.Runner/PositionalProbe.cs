using System.Globalization;
using Zeus.Sim;

/// <summary>
/// Read-only frame dump for the positional-play gap analysis (scripts/ai_gap_sim.py computes the metrics).
/// Usage: Zeus.Runner --positional-probe [--level prof] [--first-seed 901] [--seeds 20] [--pairs a:b,c:d] --out-dir dir.
/// Writes per match frames_*.tsv (1 Hz, running play only; players = team|id|x|y|speed|keeper) and events_*.tsv. Sequential, does not change the sim.
/// </summary>
internal static class PositionalProbe
{
    internal static int Run(string[] args)
    {
        string Value(string key, string fallback)
        {
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
        int seeds = int.Parse(Value("--seeds", "20"), CultureInfo.InvariantCulture);
        int first = int.Parse(Value("--first-seed", "901"), CultureInfo.InvariantCulture);
        string level = Value("--level", "prof");
        string outDir = Value("--out-dir", "positional");
        Directory.CreateDirectory(outDir);
        var pairs = Value("--pairs", "positiespel_4231:counter_532,counter_532:laag_blok_442,laag_blok_442:positiespel_4231")
            .Split(',').Select(p => p.Split(':')).ToArray();
        var inv = CultureInfo.InvariantCulture;
        foreach (var pair in pairs)
            for (int seed = first; seed < first + seeds; seed++)
            {
                var home = TacticLoader.LoadFile($"tactics/{pair[0]}.json");
                var away = TacticLoader.LoadFile($"tactics/{pair[1]}.json");
                var m = Match.Create(new MatchConfig(), home, away, (ulong)seed,
                    homeSquad: SquadFactory.ForLevel(level, home, "squads"), awaySquad: SquadFactory.ForLevel(level, away, "squads"));
                string tag = $"{pair[0]}__{pair[1]}__{seed}";
                using var frames = new StreamWriter(Path.Combine(outDir, $"frames_{tag}.tsv"));
                frames.WriteLine("t\tperiod\tswapped\tposs\tcarrier\tbx\tby\tplayers");
                while (m.Status is not (MatchStatus.Finished or MatchStatus.Abandoned) && m.Tick < 150000)
                {
                    m.Step();
                    if (m.Status != MatchStatus.Running || m.Tick % 10 != 0) continue; // 1 Hz at the default 0.1 s step
                    var s = m.GetSnapshot();
                    var players = string.Join(';', s.Players.Where(p => !p.SentOff).Select(p => string.Format(inv,
                        "{0}|{1}|{2:F2}|{3:F2}|{4:F2}|{5}", p.Team == Side.Home ? "H" : "A", p.Id, p.Pos.X, p.Pos.Y, p.Vel.Length, p.IsKeeper ? 1 : 0)));
                    frames.WriteLine(string.Format(inv, "{0:F1}\t{1}\t{2}\t{3}\t{4}\t{5:F2}\t{6:F2}\t{7}", s.TimeSeconds, s.Period,
                        s.EndsSwapped ? 1 : 0, m.Possession == Side.Home ? "H" : "A", m.CarrierId ?? "", s.Ball!.Pos.X, s.Ball.Pos.Y, players));
                }
                using var events = new StreamWriter(Path.Combine(outDir, $"events_{tag}.tsv"));
                events.WriteLine("type\tteam\tplayer\tt\tperiod\tx\ty\toutcome");
                foreach (var e in m.Events.Events)
                    events.WriteLine(string.Format(inv, "{0}\t{1}\t{2}\t{3:F1}\t{4}\t{5:F2}\t{6:F2}\t{7}", e.Type,
                        e.Team == Side.Home ? "H" : "A", e.PlayerId, e.Seconds, e.Period, e.Location.X, e.Location.Y, e.Outcome));
                Console.Error.WriteLine($"{tag} {m.HomeScore}-{m.AwayScore}");
            }
        return 0;
    }
}
