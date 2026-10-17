using System.Globalization;
using Zeus.Sim;

/// <summary>
/// Read-only movement measurement for the "players run the wrong way and hang" complaint. Sequential, one match at a
/// time. Usage: Zeus.Runner --movement-probe [--seeds 2] [--first-seed 1] [--level neutraal] [--pairs a:b,c:d]
/// [--out file.tsv]. Off-ball outfield players in running play (at least 5 s after a restart) are measured.
/// </summary>
internal static class MovementProbe
{
    internal static int Run(string[] args)
    {
        string Value(string key, string fallback)
        {
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
        int seeds = int.Parse(Value("--seeds", "2"), CultureInfo.InvariantCulture);
        int first = int.Parse(Value("--first-seed", "1"), CultureInfo.InvariantCulture);
        string level = Value("--level", "neutraal");
        string outPath = Value("--out", "");
        bool dump = args.Contains("--dump");
        var pairs = Value("--pairs", "laag_blok_442:laag_blok_442,hoge_druk_433:hoge_druk_433,positiespel_4231:counter_532,man_om_man_433:laag_blok_442")
            .Split(',').Select(p => p.Split(':')).ToArray();
        var lines = new List<string> { string.Join('\t', "home", "away", "seed", "score", "shots", "passes", "still%", "stuck%",
            "jumps/min", "defLength", "deepToBall", "groupStill_s", "maxGroupStill_s", "nearStillRuns15", "unknownBall%", "realSeconds", "shotDist<12/12-18/18-25/25+", "defJumps/min", "attJumps/min", "stretched45%") };
        foreach (var pair in pairs)
            for (int seed = first; seed < first + seeds; seed++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var home = TacticLoader.LoadFile($"tactics/{pair[0]}.json");
                var away = TacticLoader.LoadFile($"tactics/{pair[1]}.json");
                var m = Match.Create(new MatchConfig(), home, away, (ulong)seed,
                    homeSquad: SquadFactory.ForLevel(level, home, "squads"), awaySquad: SquadFactory.ForLevel(level, away, "squads"));
                long defMeasured = 0, defJumps = 0, attJumps = 0, stretched = 0; long measured = 0, still = 0, stuck = 0, unknown = 0, jumps = 0, decisions = 0;
                double lengthSum = 0, deepSum = 0; long lengthN = 0;
                long groupStill = 0, groupRun = 0, maxGroupRun = 0, nearRuns = 0, lastRestartTick = 0;
                var lastTarget = new Dictionary<string, (long Tick, Vec2 Target)>();
                var stillRun = new Dictionary<string, int>();
                while (m.Status is not (MatchStatus.Finished or MatchStatus.Abandoned) && m.Tick < 150000)
                {
                    m.Step();
                    if (m.Status != MatchStatus.Running) { lastRestartTick = m.Tick; stillRun.Clear(); continue; }
                    if (m.Tick - lastRestartTick < 50) continue;
                    var snap = m.GetSnapshot();
                    var ball = snap.Ball!.Pos.XY;
                    int moving = 0;
                    foreach (var p in snap.Players)
                    {
                        if (p.SentOff || p.IsKeeper) continue;
                        bool isStill = p.Vel.Length < 0.3;
                        if (!isStill) moving++;
                        if (m.CarrierId == p.Id && m.Possession == p.Team) continue;
                        string key = p.Team + "/" + p.Id;
                        measured++;
                        bool defendingPlayer = p.Team != m.Possession;
                        if (defendingPlayer) defMeasured++;
                        var view = m.PerceptionOf(p.Team, p.Id);
                        if (isStill) still++;
                        if (view is not null)
                        {
                            if (view.KnownBall is null) unknown++;
                            if (isStill && (view.FormationTarget - p.Pos).Length > 3
                                && m.IntentOf(p.Team, p.Id)?.Kind is IntentKind.Scan or IntentKind.HoldPosition) stuck++;
                            if (!lastTarget.TryGetValue(key, out var last) || last.Tick != view.Tick)
                            {
                                if (last.Tick != 0 && view.Tick - last.Tick <= 5)
                                {
                                    decisions++;
                                    if ((view.FormationTarget - last.Target).Length > 8)
                                    {
                                        jumps++;
                                        if (defendingPlayer) defJumps++; else attJumps++;
                                    }
                                }
                                lastTarget[key] = (view.Tick, view.FormationTarget);
                            }
                        }
                        int run = isStill ? stillRun.GetValueOrDefault(key) + 1 : 0;
                        stillRun[key] = run;
                        if (run == 150 && (p.Pos - ball).Length < 40) nearRuns++;
                    }
                    if (moving <= 3) { groupStill++; groupRun++; maxGroupRun = Math.Max(maxGroupRun, groupRun); }
                    else groupRun = 0;
                    if (dump && groupRun == 15)
                    {
                        var kinds = snap.Players.Where(p => !p.IsKeeper).GroupBy(p => m.IntentOf(p.Team, p.Id)?.Kind.ToString() ?? "-")
                            .Select(g => g.Key + "=" + g.Count());
                        Console.Error.WriteLine(FormattableString.Invariant(
                            $"GROUP {pair[0]}-{pair[1]} {seed} t={m.TimeSeconds:F1} ball=({snap.Ball!.Pos.X:F1},{snap.Ball.Pos.Y:F1},{snap.Ball.Pos.Z:F1}) v={snap.Ball.Vel.XY.Length:F1} carrier={m.Possession}/{m.CarrierId} phases={m.PhaseOf(Side.Home)}/{m.PhaseOf(Side.Away)} {string.Join(' ', kinds)}"));
                    }
                    var defending = m.Possession == Side.Home ? Side.Away : Side.Home;
                    var defenders = snap.Players.Where(p => p.Team == defending && !p.SentOff && !p.IsKeeper).ToArray();
                    if (defenders.Length > 1)
                    {
                        double sign = defenders.Average(p => p.Pos.X) > ball.X ? 1 : -1; // own goal side of the ball
                        lengthSum += defenders.Max(p => p.Pos.X) - defenders.Min(p => p.Pos.X);
                        double deep = defenders.Max(p => p.Pos.X * sign) - ball.X * sign;
                        deepSum += deep;
                        if (deep > 45) stretched++;
                        lengthN++;
                    }
                }
                var stats = MatchStatistics.FromMatch(m);
                double step = new MatchConfig().StepSeconds, n = Math.Max(1, measured), rows = Math.Max(1, lengthN);
                double minutes = decisions == 0 ? 1 : n * step / 60;
                lines.Add(string.Join('\t', pair[0], pair[1], seed.ToString(CultureInfo.InvariantCulture),
                    $"{m.HomeScore}-{m.AwayScore}", $"{stats.Home.Shots}/{stats.Away.Shots}", $"{stats.Home.Passes}/{stats.Away.Passes}",
                    F(100.0 * still / n), F(100.0 * stuck / n), F(jumps / minutes), F(lengthSum / rows), F(deepSum / rows),
                    F(groupStill * step), F(maxGroupRun * step), nearRuns.ToString(CultureInfo.InvariantCulture), F(100.0 * unknown / n),
                    F(sw.Elapsed.TotalSeconds), ShotBands(m), F(defJumps / Math.Max(1e-9, defMeasured * step / 60)),
                    F(attJumps / Math.Max(1e-9, (measured - defMeasured) * step / 60)), F(100.0 * stretched / rows)));
                Console.Error.WriteLine(lines[^1]);
            }
        string text = string.Join(Environment.NewLine, lines) + Environment.NewLine;
        if (outPath.Length > 0) File.WriteAllText(outPath, text); else Console.Write(text);
        return 0;
    }

    private static string ShotBands(Match m)
    {
        var bands = new int[4];
        foreach (var e in m.Events.Events.Where(e => e.Type == EventType.Shot && !e.Outcome.Contains("penalty")))
        {
            double d = Math.Min((e.Location - new Vec2(Pitch.HalfLength, 0)).Length, (e.Location - new Vec2(-Pitch.HalfLength, 0)).Length);
            bands[d < 12 ? 0 : d < 18 ? 1 : d < 25 ? 2 : 3]++;
        }
        return string.Join('/', bands);
    }

    private static string F(double v) => v.ToString("F2", CultureInfo.InvariantCulture);
}
