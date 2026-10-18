using System.Globalization;
using System.Text.Json;
using Zeus.Sim;

/// <summary>
/// Headless QA soak: every home x away tactic pair (incl. mirror) per level, N seeds, invariant checks per match, one JSON line each.
/// Usage (from repo root): Zeus.Runner --qa-soak [--levels neutraal,amateur,halfprof,prof] [--seeds 3] [--first-seed 1]
///   [--tactics a,b] [--out file.jsonl] [--max-matches N] [--config overrides.json]. Sequential by design (memory). Does not change the sim.
/// Determinism: the first seed of a pair is replayed once (level rotates per pair) and its EventHash compared.
/// </summary>
internal static class QaSoak
{
    internal static int Run(string[] args)
    {
        string V(string k, string d) { int i = Array.IndexOf(args, k); return i >= 0 && i + 1 < args.Length ? args[i + 1] : d; }
        var inv = CultureInfo.InvariantCulture;
        int seeds = int.Parse(V("--seeds", "3"), inv), first = int.Parse(V("--first-seed", "1"), inv);
        int maxMatches = int.Parse(V("--max-matches", "0"), inv);
        string outPath = V("--out", "qa_soak.jsonl");
        var levels = V("--levels", "neutraal,amateur,halfprof,prof").Split(',');
        string[] names = V("--tactics", "") is { Length: > 0 } t ? t.Split(',')
            : Directory.GetFiles("tactics", "*.json").Select(f => Path.GetFileNameWithoutExtension(f)!).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        // --config <json>: partial MatchConfig overrides (as in the single-match runner), e.g. to measure a pre-change baseline.
        var config = V("--config", "") is { Length: > 0 } cp ? MatchConfigJson.Parse(File.ReadAllText(cp)) : new MatchConfig();
        var tactics = names.ToDictionary(n => n, n => TacticLoader.LoadFile($"tactics/{n}.json"));
        var jopt = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        using var output = new StreamWriter(outPath, false, new System.Text.UTF8Encoding(false));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int total = 0, bad = 0, pairIndex = 0;
        var perCheck = new SortedDictionary<string, int>();
        foreach (string h in names)
            foreach (string a in names)
            {
                pairIndex++;
                foreach (string level in levels)
                    for (int s = 0; s < seeds; s++)
                    {
                        if (maxMatches > 0 && total >= maxMatches) goto done;
                        ulong seed = (ulong)(first + s);
                        bool det = s == 0 && levels[pairIndex % levels.Length] == level;
                        var row = Play(config, h, a, level, seed, tactics[h], tactics[a], det);
                        total++;
                        var fails = (List<string>)row["fails"]!;
                        if (fails.Count > 0)
                        {
                            bad++;
                            foreach (var f in fails) { string k = f.Split(':')[0]; perCheck[k] = perCheck.GetValueOrDefault(k) + 1; }
                        }
                        output.WriteLine(JsonSerializer.Serialize(row, jopt));
                        output.Flush();
                        if (total % 50 == 0) Console.Error.WriteLine($"{total} matches, {bad} flagged, {sw.Elapsed.TotalMinutes:F1} min");
                    }
            }
        done:
        Console.WriteLine($"qa-soak: {total} matches, {bad} flagged, {sw.Elapsed.TotalMinutes:F1} min, out={outPath}");
        foreach (var (k, v) in perCheck) Console.WriteLine($"  {k}: {v}");
        return bad == 0 ? 0 : 4;
    }

    static (Match? M, double MaxStill, long Ticks, string? Exc) Sim(MatchConfig config, Tactic home, Tactic away, string level, ulong seed)
    {
        Match? m = null; double still = 0, maxStill = 0; Vec2 anchor = default; bool have = false;
        try
        {
            m = Match.Create(config, home, away, seed,
                homeSquad: SquadFactory.ForLevel(level, home, "squads"), awaySquad: SquadFactory.ForLevel(level, away, "squads"));
            double dt = config.StepSeconds;
            while (m.Status is not (MatchStatus.Finished or MatchStatus.Abandoned) && m.Tick < 150000)
            {
                m.Step();
                if (m.Status != MatchStatus.Running) { still = 0; have = false; continue; }
                var b = m.GetSnapshot().Ball!.Pos.XY;
                if (!have || (b - anchor).Length > 0.5) { anchor = b; have = true; still = 0; }
                else { still += dt; if (still > maxStill) maxStill = still; }
            }
            return (m, maxStill, m.Tick, null);
        }
        catch (Exception e) { return (m, maxStill, m?.Tick ?? 0, e.GetType().Name + ": " + e.Message); }
    }

    static Dictionary<string, object?> Play(MatchConfig config, string h, string a, string level, ulong seed, Tactic ht, Tactic at, bool det)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var (m0, maxStill, ticks, exc) = Sim(config, ht, at, level, seed);
        var fails = new List<string>();
        var row = new Dictionary<string, object?> { ["home"] = h, ["away"] = a, ["level"] = level, ["seed"] = seed, ["fails"] = fails };
        if (exc is not null || m0 is null) { fails.Add("exception:" + exc); row["secs"] = sw.Elapsed.TotalSeconds; return row; }
        var m = m0;
        var st = MatchStatistics.FromMatch(m);
        var ev = m.Events.Events;
        int Off(Side s) => ev.Count(e => e.Type == EventType.Offside && e.Team == s);
        row["status"] = m.Status.ToString(); row["ticks"] = ticks; row["timeSec"] = m.TimeSeconds;
        row["goalsH"] = st.Home.Goals; row["goalsA"] = st.Away.Goals; row["shotsH"] = st.Home.Shots; row["shotsA"] = st.Away.Shots;
        row["passH"] = st.Home.Passes; row["passA"] = st.Away.Passes;
        row["foulsH"] = st.Home.Fouls; row["foulsA"] = st.Away.Fouls; row["offH"] = Off(Side.Home); row["offA"] = Off(Side.Away);
        row["yH"] = st.Home.YellowCards; row["yA"] = st.Away.YellowCards; row["rH"] = st.Home.RedCards; row["rA"] = st.Away.RedCards;
        row["maxBallStillSec"] = Math.Round(maxStill, 1);
        string hash = m.EventHash(); row["eventHash"] = hash;
        if (m.Status != MatchStatus.Finished) fails.Add("status:" + m.Status);
        if (m.Status == MatchStatus.Finished && (m.TimeSeconds < 5400 || m.TimeSeconds > 6000)) fails.Add($"time:{m.TimeSeconds}");
        if (st.Home.Goals > 15) fails.Add($"goals:H={st.Home.Goals}");
        if (st.Away.Goals > 15) fails.Add($"goals:A={st.Away.Goals}");
        foreach (var (t, s) in new[] { (st.Home, "H"), (st.Away, "A") })
        {
            if (t.RedCards > 4) fails.Add($"red:{s}={t.RedCards}");
            if (t.YellowCards > 12) fails.Add($"yellow:{s}={t.YellowCards}");
            if (t.Shots > 70) fails.Add($"shots:{s}={t.Shots}");
        }
        if (Off(Side.Home) > 15 || Off(Side.Away) > 15) fails.Add($"offside:{Off(Side.Home)}/{Off(Side.Away)}");
        if (maxStill > 60) fails.Add($"ballstuck:{maxStill:F0}s");
        var snap = m.GetSnapshot();
        static bool Bad(double v) => !double.IsFinite(v);
        foreach (var p in snap.Players)
        {
            if (Bad(p.Pos.X) || Bad(p.Pos.Y) || Bad(p.Vel.X) || Bad(p.Vel.Y)) { fails.Add($"nan:{p.Id}"); continue; }
            if (!p.SentOff && (Math.Abs(p.Pos.X) > Pitch.HalfLength + 0.5 || Math.Abs(p.Pos.Y) > Pitch.HalfWidth + 0.5))
                fails.Add($"outside:{p.Team}{p.Id}@{p.Pos.X:F1},{p.Pos.Y:F1}");
        }
        var b = snap.Ball!.Pos;
        if (Bad(b.X) || Bad(b.Y) || Bad(b.Z)) fails.Add("nan:ball");
        else if (Math.Abs(b.X) > Pitch.HalfLength + 5 || Math.Abs(b.Y) > Pitch.HalfWidth + 5 || b.Z < -0.1 || b.Z > 60)
            fails.Add($"ballpos:{b.X:F1},{b.Y:F1},{b.Z:F1}");
        if (det)
        {
            var (m2, _, _, e2) = Sim(config, ht, at, level, seed);
            string h2 = e2 is null && m2 is not null ? m2.EventHash() : "exc";
            row["determinism"] = h2 == hash ? "ok" : "MISMATCH";
            if (h2 != hash) fails.Add("determinism:hash");
        }
        row["secs"] = Math.Round(sw.Elapsed.TotalSeconds, 2);
        return row;
    }
}
