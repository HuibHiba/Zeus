using System.Globalization;
using Zeus.Sim;

/// <summary>
/// Read-only fatigue measurement (B5): distance, sprints and high-speed running per outfield player per 90 min, split by half,
/// plus the mean energy reservoirs. Sequential, one match at a time. Usage: Zeus.Runner --fatigue-probe [--seeds 4]
/// [--first-seed 1] [--level neutraal] [--pairs a:b,c:d] [--config file.json] [--out file.tsv].
/// Sprint = episode above 7.0 m/s of at least 1.0 s; high speed = distance above 5.5 m/s (same definitions as scripts/b5_vermoeidheid_data.py).
/// </summary>
internal static class FatigueProbe
{
    private const double SprintSpeed = 7.0, SprintMinSeconds = 1.0, HighSpeed = 5.5;

    internal static int Run(string[] args)
    {
        string Value(string key, string fallback)
        {
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
        int seeds = int.Parse(Value("--seeds", "4"), CultureInfo.InvariantCulture);
        int first = int.Parse(Value("--first-seed", "1"), CultureInfo.InvariantCulture);
        string level = Value("--level", "neutraal");
        string outPath = Value("--out", "");
        string configPath = Value("--config", "");
        var config = configPath == "" ? new MatchConfig() : MatchConfigJson.Parse(File.ReadAllText(configPath));
        var pairs = Value("--pairs", "hoge_druk_433:hoge_druk_433,laag_blok_442:laag_blok_442,positiespel_4231:counter_532")
            .Split(',').Select(p => p.Split(':')).ToArray();
        var rows = new List<double[]>();
        var lines = new List<string> { string.Join('\t', "home", "away", "seed", "km_h1", "km_h2", "km_per90", "sprints_h1", "sprints_h2",
            "sprints_per90", "hsr_m_per90", "shortE_ht", "longE_ht", "shortE_end", "longE_end") };
        foreach (var pair in pairs)
            for (int seed = first; seed < first + seeds; seed++)
            {
                var home = TacticLoader.LoadFile($"tactics/{pair[0]}.json");
                var away = TacticLoader.LoadFile($"tactics/{pair[1]}.json");
                var m = Match.Create(config, home, away, (ulong)seed,
                    homeSquad: SquadFactory.ForLevel(level, home, "squads"), awaySquad: SquadFactory.ForLevel(level, away, "squads"));
                var dist = new double[2]; var sprints = new int[2]; double hsr = 0, endSeconds = 0;
                var run = new Dictionary<string, int>(); // consecutive ticks above SprintSpeed per player
                double sumShortHt = 0, sumLongHt = 0; int nHt = 0;
                double dt = config.StepSeconds; int minTicks = (int)Math.Ceiling(SprintMinSeconds / dt);
                bool htTaken = false;
                while (m.Status is not (MatchStatus.Finished or MatchStatus.Abandoned) && m.Tick < 150000)
                {
                    m.Step();
                    var snap = m.GetSnapshot();
                    int half = snap.Period >= 2 ? 1 : 0;
                    if (half == 1 && !htTaken)
                    {
                        htTaken = true;
                        foreach (var p in snap.Players) if (!p.IsKeeper && !p.SentOff) { sumShortHt += p.ShortEnergy; sumLongHt += p.LongEnergy; nHt++; }
                    }
                    endSeconds = snap.TimeSeconds;
                    foreach (var p in snap.Players)
                    {
                        string id = p.Team + "/" + p.Id;
                        if (p.IsKeeper || p.SentOff) { run.Remove(id); continue; }
                        double v = p.Vel.Length;
                        dist[half] += v * dt;
                        if (v > HighSpeed) hsr += v * dt;
                        run.TryGetValue(id, out int n);
                        n = v > SprintSpeed ? n + 1 : 0;
                        run[id] = n;
                        if (n == minTicks) sprints[half]++; // counted once, when the episode reaches the minimum length
                    }
                }
                double sumShortEnd = 0, sumLongEnd = 0; int nEnd = 0;
                foreach (var p in m.GetSnapshot().Players) if (!p.IsKeeper && !p.SentOff) { sumShortEnd += p.ShortEnergy; sumLongEnd += p.LongEnergy; nEnd++; }
                int field = Math.Max(1, nEnd); // 20 outfield players unless someone was sent off
                double scale = 5400.0 / Math.Max(1.0, endSeconds);
                double km1 = dist[0] / 1000 / field, km2 = dist[1] / 1000 / field;
                double spr1 = (double)sprints[0] / field, spr2 = (double)sprints[1] / field;
                var row = new[] { km1, km2, (km1 + km2) * scale, spr1, spr2, (spr1 + spr2) * scale, hsr / field * scale,
                    nHt == 0 ? 1 : sumShortHt / nHt, nHt == 0 ? 1 : sumLongHt / nHt, sumShortEnd / field, sumLongEnd / field };
                rows.Add(row);
                lines.Add(string.Join('\t', new[] { pair[0], pair[1], seed.ToString(CultureInfo.InvariantCulture) }
                    .Concat(row.Select(x => x.ToString("0.###", CultureInfo.InvariantCulture)))));
                Console.WriteLine(lines[^1]);
            }
        if (rows.Count > 0)
        {
            var mean = Enumerable.Range(0, rows[0].Length).Select(i => rows.Average(r => r[i]));
            lines.Add(string.Join('\t', new[] { "MEAN", "", rows.Count.ToString(CultureInfo.InvariantCulture) }
                .Concat(mean.Select(x => x.ToString("0.###", CultureInfo.InvariantCulture)))));
            Console.WriteLine(lines[^1]);
        }
        if (outPath != "") File.WriteAllLines(outPath, lines);
        return 0;
    }
}
