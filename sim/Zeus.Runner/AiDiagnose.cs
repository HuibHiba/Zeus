using System.Globalization;
using System.Text;
using Zeus.Sim;

/// <summary>
/// Diagnosis tool (read-only on the simulation): measures (a) open scoring chances and what the carrier's brain does with them,
/// (b) standing still per player. Sequential, one match at a time. Usage:
/// Zeus.Runner --ai-diagnose [--seeds 4] [--first-seed 1] [--tactics-dir tactics] [--out report.txt] [--max-ticks 150000]
/// Home plays tactic i, away plays tactic i+1 (rotation over the six example tactics). Both teams are measured.
/// </summary>
internal static class AiDiagnose
{
    private sealed class Tally : Dictionary<string, long>
    {
        public void Inc(string key, long n = 1) { TryGetValue(key, out var v); this[key] = v + n; }
    }

    private sealed class StillTally : Dictionary<string, (long Still, long Total)>
    {
        public void Rec(string key, bool still)
        {
            TryGetValue(key, out var v);
            this[key] = (v.Still + (still ? 1 : 0), v.Total + 1);
        }
    }

    private sealed class Episode
    {
        public string Team = "", CarrierId = "", Tactic = "";
        public Side Side;
        public long StartTick, Ticks;
        public double MinDist = 99, MaxXg;
        public long FirstXgTick = -1;
        public double StartDist, LastDist, StartHeld, LastHeld; public bool KeeperOff;
        public string Outcome = "";
        public bool Closed;
        public double DirSign;
        public Vec2 CarrierPos;
    }

    internal static int Run(string[] args)
    {
        string Value(string key, string fallback)
        {
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
        int seeds = int.Parse(Value("--seeds", "4"), CultureInfo.InvariantCulture);
        int first = int.Parse(Value("--first-seed", "1"), CultureInfo.InvariantCulture);
        int maxTicks = int.Parse(Value("--max-ticks", "150000"), CultureInfo.InvariantCulture);
        string dir = Value("--tactics-dir", "tactics");
        string outPath = Value("--out", "");
        string[] names = { "hoge_druk_433", "positiespel_4231", "counter_532", "laag_blok_442", "man_om_man_433", "testcase_a_433" };
        string only = Value("--only", "");
        string level = Value("--level", "neutraal");
        string squadsDir = Value("--squads-dir", "squads");
        var dumpTicks = new HashSet<long>(Value("--dump-ticks", "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(long.Parse));
        string onlyAway = Value("--only-away", "");
        var config = new MatchConfig();
        var passConfig = config.PassSettings;
        var shotConfig = config.ShotSettings;
        double holdSeconds = passConfig.HoldSeconds;

        // Aggregates over all matches.
        var openTicks = new Tally();            // cross-table of open-chance ticks: band|held|xg|intent
        var openByTactic = new Tally();
        var carrierTicks = new Tally();         // all carrier ticks by tactic (denominator)
        var outcomes = new Tally();             // episode outcomes
        var outcomesByBand = new Tally();
        var episodeLens = new List<double>();
        var backExamples = new List<string>();
        var shotWhileOpenAtMinXg = new Tally();
        var holdCarrier = new StillTally();     // carrier standing still during the hold window
        var still = new StillTally();
        var stillRuns = new Dictionary<string, List<int>>();
        var longStillExamples = new List<string>();
        var decisionIntervals = new Tally();
        var insideBoxIntents = new Tally();     // carrier < 11 m of goal: what is he doing in the hold window
        var openIntentTargetAway = new Tally();
        var freeze = new Tally();               // team-wide freezes and dead-ball stalls
        var freezeExamples = new List<string>();
        var longestRuns = new List<(int Len, string Who, long StartTick, string Match)>();
        var fresh = new Tally();                // decision-time table (fresh decisions only): heldView|xG(view)|intent
        var episodeXg = new Tally();            // open episodes that at some point had xG>=0.08: outcome
        var shotDelay = new List<double>();     // seconds from first (open and xG>=0.08) tick to the shot
        var evTotals = new Tally();
        int matches = 0;
        long totalTicksRunning = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _epXg = episodeXg; _shotDelay = shotDelay;

        for (int ti = 0; ti < names.Length; ti++)
        {
            if (only.Length > 0 && names[ti] != only) continue;
            if (onlyAway.Length > 0 && names[(ti + 1) % names.Length] != onlyAway) continue;
            string homeName = names[ti], awayName = names[(ti + 1) % names.Length];
            var home = TacticLoader.Parse(File.ReadAllText(Path.Combine(dir, homeName + ".json")));
            var away = TacticLoader.Parse(File.ReadAllText(Path.Combine(dir, awayName + ".json")));
            var roleOf = new Dictionary<(Side, string), string>();
            foreach (var r in home.Roster) roleOf[(Side.Home, r.Id)] = r.Role;
            foreach (var r in away.Roster) roleOf[(Side.Away, r.Id)] = r.Role;
            string TacticOf(Side s) => s == Side.Home ? homeName : awayName;
            for (int s = 0; s < seeds; s++)
            {
                ulong seed = (ulong)(first + s);
                var m = Match.Create(config, home, away, seed,
                    homeSquad: SquadFactory.ForLevel(level, home, squadsDir), awaySquad: SquadFactory.ForLevel(level, away, squadsDir));
                matches++;
                var runLen = new Dictionary<(Side, string), int>();
                var lastViewTick = new Dictionary<(Side, string), long>();
                var controlSince = new Dictionary<string, long>();
                string? prevCarrierKey = null;
                long controlStart = 0;
                Episode? ep = null, grace = null;
                long ticksSinceRestart = 9999;
                int deadBallRun = 0, freezeRun = 0;
                var runStart = new Dictionary<(Side, string), long>();
                string matchId = $"{homeName} v {awayName} seed {seed}";
                for (int step = 0; step < maxTicks; step++)
                {
                    if (m.Status is MatchStatus.Finished or MatchStatus.Abandoned) break;
                    bool running = m.Status == MatchStatus.Running;
                    var snap = m.GetSnapshot();
                    ticksSinceRestart = running ? ticksSinceRestart + 1 : 0;
                    string? cid = m.CarrierId;
                    string? carrierKey = running && cid is not null ? m.Possession + "/" + cid : null;
                    if (carrierKey != prevCarrierKey) { controlStart = m.Tick; prevCarrierKey = carrierKey; }
                    double held = (m.Tick - controlStart) * config.StepSeconds;
                    PlayerSnapshot? carrier = carrierKey is null ? null : snap.Players.First(p => p.Team == m.Possession && p.Id == cid);
                    double dirOf(Side side) => (side == Side.Home) != m.EndsSwapped ? 1 : -1;

                    if (dumpTicks.Contains(m.Tick))
                    {
                        Console.WriteLine($"==== DUMP {homeName} v {awayName} seed {seed}");
                        Console.WriteLine(TeamState(m, snap));
                        if (carrier is not null && m.PerceptionOf(carrier.Team, carrier.Id) is { } cv)
                        {
                            var ci = m.IntentOf(carrier.Team, carrier.Id);
                            var opt = PassChoice.Choose(cv, passConfig);
                            Console.WriteLine($"  carrier {carrier.Team}/{carrier.Id} intent {ci?.Kind} target {F(ci!.Target)} held {cv.HeldBallSeconds:F1}s viewTick {cv.Tick} visible players {cv.VisiblePlayers.Count} (mates {cv.VisiblePlayers.Count(q => q.Team == carrier.Team)}) option {opt?.ReceiverId} score {opt?.Score:F2} heading {carrier.Heading:F2} instr=[{string.Join(",", cv.Instructions ?? Array.Empty<string>())}]");
                            foreach (var q in cv.VisiblePlayers.Where(q => q.Team == carrier.Team))
                                Console.WriteLine($"    mate {q.Id} {F(q.Pos)} dist {(q.Pos - carrier.Pos).Length:F1}");
                        }
                    }
                    // ---------- (a) open chance ----------
                    bool open = false;
                    if (carrier is not null && !carrier.IsKeeper)
                    {
                        double dsign = dirOf(carrier.Team);
                        var goal = new Vec2(dsign * Pitch.HalfLength, 0);
                        var line = goal - carrier.Pos;
                        double len = line.Length;
                        carrierTicks.Inc(TacticOf(carrier.Team));
                        double dist = len;
                        bool laneFree = len > 0.5;
                        double ux = line.X / Math.Max(len, 1e-9), uy = line.Y / Math.Max(len, 1e-9);
                        PlayerSnapshot? keeper = null;
                        foreach (var o in snap.Players)
                        {
                            if (o.Team == carrier.Team || o.SentOff) continue;
                            if (o.IsKeeper) { keeper = o; continue; }
                            var rel = o.Pos - carrier.Pos;
                            double along = rel.X * ux + rel.Y * uy, lat = Math.Abs(rel.X * uy - rel.Y * ux);
                            if ((along > 0.5 && along < len && lat < 2.0) || rel.Length < 2.5) laneFree = false;
                        }
                        open = dist < 25 && laneFree && Math.Abs(carrier.Pos.X) < Pitch.HalfLength;
                        var intent = m.IntentOf(carrier.Team, carrier.Id);
                        double xgRaw = ShotModel.GoalProbability(dist, carrier.Pos.Y);
                        // Decision hold window bookkeeping for every carrier tick (also not open).
                        if (intent is not null && held < holdSeconds)
                        {
                            holdCarrier.Rec("hold window, all carriers", carrier.Vel.Length < 0.3);
                            if (dist < 11) insideBoxIntents.Inc($"<11 m, hold<2.4s: {intent.Kind}" + (intent.Kind == IntentKind.MoveToPosition
                                ? ((intent.Target - goal).Length > dist + 0.5 ? " AWAY from goal" : " toward goal") : ""));
                        }
                        if (open && intent is not null)
                        {
                            string band = dist < 12 ? "a <12 m" : dist < 18 ? "b 12-18 m" : "c 18-25 m";
                            string heldKey = held >= holdSeconds ? "held>=2.4s" : "held<2.4s";
                            string xgKey = xgRaw >= shotConfig.MinGoalProbability ? "xG>=0.08" : "xG<0.08";
                            string label = intent.Kind.ToString();
                            if (intent.Kind == IntentKind.Pass)
                            {
                                var recv = snap.Players.FirstOrDefault(p => p.Team == carrier.Team && p.Id == intent.ReceiverId);
                                bool back = recv is not null && (recv.Pos.X - carrier.Pos.X) * dsign < -1;
                                label += back ? " (back)" : " (fwd/side)";
                                if (intent.IsThroughBall) label += " [through]";
                            }
                            else if (intent.Kind == IntentKind.MoveToPosition)
                                label += (intent.Target - goal).Length > dist + 0.5 ? " (away from goal)" : " (toward goal)";
                            openTicks.Inc($"{band} | {heldKey} | {xgKey} | {label}");
                            openByTactic.Inc(TacticOf(carrier.Team));
                            if (m.PerceptionOf(carrier.Team, carrier.Id) is { } fv && fv.Tick == m.Tick - 1)
                            {
                                double dv = (goal - fv.Self.Pos).Length;
                                double xgv = ShotModel.GoalProbability(dv, fv.Self.Pos.Y);
                                string fb = dv < 12 ? "a <12 m" : dv < 18 ? "b 12-18 m" : dv < 25 ? "c 18-25 m" : "d >=25 m";
                                string fl = intent.Kind.ToString();
                                if (intent.Kind == IntentKind.Pass)
                                {
                                    var rv = snap.Players.FirstOrDefault(p => p.Team == carrier.Team && p.Id == intent.ReceiverId);
                                    fl += rv is not null && (rv.Pos.X - carrier.Pos.X) * dsign < -1 ? " (back)" : " (fwd/side)";
                                    if (intent.IsThroughBall) fl += " [through]";
                                }
                                else if (intent.Kind == IntentKind.MoveToPosition)
                                    fl += (intent.Target - goal).Length > dv + 0.5 ? " (away from goal)" : " (toward goal)";
                                fresh.Inc($"{fb} | held {(fv.HeldBallSeconds >= holdSeconds ? ">=2.4s" : "<2.4s")} | xG {(xgv >= shotConfig.MinGoalProbability ? ">=0.08" : "<0.08")} | {fl}");
                            }
                            if (ep is null || ep.CarrierId != carrier.Id || ep.Side != carrier.Team)
                            {
                                ep = new Episode { Team = carrier.Team.ToString(), Side = carrier.Team, CarrierId = carrier.Id,
                                    Tactic = TacticOf(carrier.Team), StartTick = m.Tick, DirSign = dsign, CarrierPos = carrier.Pos };
                            }
                            ep.Ticks++;
                            if (ep.Ticks == 1) { ep.StartDist = dist; ep.StartHeld = held; }
                            ep.LastDist = dist; ep.LastHeld = held;
                            if (keeper is not null) { var kr = keeper.Pos - carrier.Pos; double kalong = kr.X * ux + kr.Y * uy, klat = Math.Abs(kr.X * uy - kr.Y * ux); if (klat > 4 || (keeper.Pos - goal).Length > 8) ep.KeeperOff = true; }
                            ep.MinDist = Math.Min(ep.MinDist, dist);
                            ep.MaxXg = Math.Max(ep.MaxXg, xgRaw);
                            if (xgRaw >= shotConfig.MinGoalProbability && ep.FirstXgTick < 0) ep.FirstXgTick = m.Tick;
                        }
                    }
                    if (ep is not null && !open)
                    {
                        // Episode ends without a kick this tick (kick event may still arrive in this step, see grace).
                        ep.Closed = true;
                        ep.Outcome = carrier is null || carrier.Id != ep.CarrierId || carrier.Team != ep.Side ? "ball lost / lost control" : "no longer open (pressed / moved)";
                        grace = ep; ep = null;
                        FinishEpisode(grace, outcomes, outcomesByBand, episodeLens, false);
                    }
                    else grace = null;

                    // ---------- (b) standing still ----------
                    if (running)
                    {
                        totalTicksRunning++;
                        {
                            bool dead = carrier is null && snap.Ball!.Vel.XY.Length < 0.3;
                            deadBallRun = dead ? deadBallRun + 1 : 0;
                            int moving = snap.Players.Count(q => !q.SentOff && !q.IsKeeper && q.Vel.Length >= 0.3);
                            freezeRun = moving <= 3 ? freezeRun + 1 : 0;
                            if (dead) freeze.Inc("ticks ball at rest without carrier");
                            if (moving <= 3) freeze.Inc("ticks <=3 outfield players moving (of 20)");
                            if (deadBallRun == 300) { freeze.Inc("dead-ball runs >= 30 s"); if (freezeExamples.Count < 4) freezeExamples.Add("DEAD BALL 30 s in " + matchId + "\n" + TeamState(m, snap)); }
                            if (freezeRun == 300) freeze.Inc("freeze runs (<=3 moving) >= 30 s");
                        }
                        foreach (var p in snap.Players)
                        {
                            if (p.SentOff) continue;
                            var key = (p.Team, p.Id);
                            var view = m.PerceptionOf(p.Team, p.Id);
                            var intent = m.IntentOf(p.Team, p.Id);
                            var targetN = m.TargetOf(p.Team, p.Id);
                            if (view is null || intent is null || targetN is null) continue;
                            var target = targetN.Value;
                            if (lastViewTick.TryGetValue(key, out var lv) && lv != view.Tick)
                                decisionIntervals.Inc(((view.Tick - lv)).ToString());
                            lastViewTick[key] = view.Tick;
                            if (m.Tick < 100 || ticksSinceRestart < 50) { runLen[key] = 0; continue; }
                            bool isStill = p.Vel.Length < 0.3;
                            string role = roleOf.TryGetValue(key, out var rr) ? rr : "?";
                            string rel = carrier is null ? "loose ball"
                                : carrier.Team == p.Team ? (carrier.Id == p.Id ? "carrier" : "teammate of carrier") : "opponent of carrier";
                            string phase = m.PhaseOf(p.Team).ToString();
                            string distT = (target - p.Pos).Length < 0.5 ? "at target (<0.5 m)" : (target - p.Pos).Length < 3 ? "near target (0.5-3 m)" : "far from target (>3 m)";
                            string tac = TacticOf(p.Team);
                            still.Rec("ALL", isStill);
                            still.Rec("role: " + role, isStill);
                            still.Rec("rel: " + rel, isStill);
                            still.Rec("phase: " + phase, isStill);
                            still.Rec("intent: " + intent.Kind, isStill);
                            still.Rec("target: " + distT, isStill);
                            still.Rec("tactic: " + tac, isStill);
                            still.Rec("sees ball: " + (view.VisibleBall is not null), isStill);
                            still.Rec("knows ball (seen or memory): " + (view.KnownBall is not null), isStill);
                            if (rel != "carrier") still.Rec("non-carrier phase+sees: " + phase + " / sees=" + (view.VisibleBall is not null), isStill);
                            still.Rec("rel+phase: " + rel + " / " + phase, isStill);
                            still.Rec("rel+intent: " + rel + " / " + intent.Kind, isStill);
                            still.Rec("role+rel: " + role + " / " + rel, isStill);
                            double ballDist = (snap.Ball!.Pos.XY - p.Pos).Length;
                            still.Rec("ballDist: " + (ballDist < 15 ? "<15 m" : ballDist < 30 ? "15-30 m" : ballDist < 50 ? "30-50 m" : ">50 m"), isStill);
                            if (view.Instructions is { } ins) foreach (var i in ins) still.Rec("instr: " + i, isStill);
                            else still.Rec("instr: (none)", isStill);
                            if (isStill && !p.IsKeeper)
                            {
                                still.Rec("still-only intent+target: " + intent.Kind + " / " + distT, true);
                            }
                            if (carrier is not null && carrier.Id == p.Id && carrier.Team == p.Team && held < holdSeconds)
                                holdCarrier.Rec("hold window, carrier incl. intent " + intent.Kind, isStill);
                            // Runs of stillness (3 s = 30 ticks and up), outfield only.
                            runLen.TryGetValue(key, out int rl);
                            if (isStill && !p.IsKeeper)
                            {
                                rl++;
                                if (rl == 1) runStart[key] = m.Tick;
                                runLen[key] = rl;
                                if (rl == 100 && longStillExamples.Count < 8)
                                    longStillExamples.Add(Dump(m, snap, p, intent, target, role, rel, tac, view));
                            }
                            else
                            {
                                if (rl >= 600) longestRuns.Add((rl, $"{p.Team}/{p.Id} {role} [{rel}] last intent {intent.Kind}", runStart.GetValueOrDefault(key), matchId));
                                if (rl >= 30)
                                {
                                    if (!stillRuns.TryGetValue(role, out var list)) stillRuns[role] = list = new();
                                    list.Add(rl);
                                }
                                runLen[key] = 0;
                            }
                        }
                    }

                    int evBefore = m.Events.Events.Count;
                    m.Step();
                    // ---------- outcome of the open episode from this step's events ----------
                    var evs = m.Events.Events;
                    for (int e = evBefore; e < evs.Count; e++)
                    {
                        var ev = evs[e];
                        if (ev.Type is not (EventType.Pass or EventType.Shot)) continue;
                        var target = ep ?? grace;
                        if (target is null || ev.PlayerId != target.CarrierId || ev.Team != target.Side) continue;
                        if (target.Closed)
                        {
                            // retract the 'closed' outcome that was already counted
                            UndoEpisode(target, outcomes, outcomesByBand, episodeLens);
                            target.Closed = false;
                        }
                        string outcome;
                        if (ev.Type == EventType.Shot) outcome = "SHOT";
                        else if (ev.Outcome == "clear") outcome = "clearance";
                        else
                        {
                            string rid = ev.Outcome.StartsWith("to ") ? ev.Outcome.Split(' ')[1] : "";
                            var recv = snap.Players.FirstOrDefault(p => p.Team == ev.Team && p.Id == rid);
                            var car = snap.Players.FirstOrDefault(p => p.Team == ev.Team && p.Id == target.CarrierId);
                            bool back = recv is not null && car is not null && (recv.Pos.X - car.Pos.X) * target.DirSign < -1;
                            bool toKeeper = recv is not null && recv.IsKeeper;
                            outcome = toKeeper ? "PASS back to own keeper" : back ? "PASS backwards (outfield)" : "PASS forward/sideways";
                            if (back && backExamples.Count < 6)
                            {
                                var v = m.PerceptionOf(ev.Team, target.CarrierId);
                                backExamples.Add(BackDump(m, snap, car!, recv!, v, target, passConfig, shotConfig, held, TacticOf(ev.Team), ev.Outcome));
                            }
                        }
                        target.Outcome = outcome;
                        FinishEpisode(target, outcomes, outcomesByBand, episodeLens, true);
                        if (ReferenceEquals(target, ep)) ep = null;
                        grace = null;
                    }
                }
                // Per-match: flush open runs.
                foreach (var kv in runLen)
                {
                    if (kv.Value >= 30)
                    {
                        string role = roleOf.TryGetValue(kv.Key, out var rr) ? rr : "?";
                        if (!stillRuns.TryGetValue(role, out var list)) stillRuns[role] = list = new();
                        list.Add(kv.Value);
                    }
                }
                foreach (var ev in m.Events.Events) if (ev.Type is EventType.Shot or EventType.Pass or EventType.Goal or EventType.Interception or EventType.Offside or EventType.Foul) evTotals.Inc(ev.Type.ToString());
                Console.Error.WriteLine($"[{matches}] {homeName} v {awayName} seed {seed} done, {m.Tick} ticks, {m.HomeScore}-{m.AwayScore}, {sw.Elapsed.TotalSeconds:F0}s");
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# AI diagnose raw output (level {level}): {matches} matches, {totalTicksRunning} running ticks, seeds {first}..{first + seeds - 1}");
        sb.AppendLine();
        sb.AppendLine("## Event totals (both teams, all matches): " + string.Join(", ", evTotals.OrderBy(k => k.Key).Select(k => k.Key + " " + k.Value)));
        sb.AppendLine();
        sb.AppendLine("## Carrier ticks (outfield) per tactic and open-chance ticks (<25 m, no outfield opponent within 2 m of the shot lane or 2.5 m of the carrier)");
        foreach (var kv in carrierTicks.OrderBy(k => k.Key))
        {
            openByTactic.TryGetValue(kv.Key, out var o);
            sb.AppendLine($"{kv.Key}: carrier ticks {kv.Value}, open ticks {o} ({100.0 * o / Math.Max(1, kv.Value):F1}%)");
        }
        sb.AppendLine();
        sb.AppendLine("## Open-chance ticks: band | held | xG | carrier intent (ticks of 0.1 s)");
        long openTotal = openTicks.Values.Sum();
        foreach (var kv in openTicks.OrderBy(k => k.Key)) sb.AppendLine($"{kv.Key} : {kv.Value} ({100.0 * kv.Value / Math.Max(1, openTotal):F1}%)");
        sb.AppendLine($"TOTAL open ticks {openTotal}");
        sb.AppendLine();
        sb.AppendLine("## Open episodes (consecutive open ticks of one carrier): outcome");
        long epTotal = outcomes.Values.Sum();
        foreach (var kv in outcomes.OrderByDescending(k => k.Value)) sb.AppendLine($"{kv.Key} : {kv.Value} ({100.0 * kv.Value / Math.Max(1, epTotal):F1}%)");
        sb.AppendLine($"TOTAL episodes {epTotal}");
        sb.AppendLine("by band of minimal distance during the episode:");
        foreach (var kv in outcomesByBand.OrderBy(k => k.Key)) sb.AppendLine($"  {kv.Key} : {kv.Value}");
        if (episodeLens.Count > 0)
        {
            episodeLens.Sort();
            sb.AppendLine($"episode length (s): median {episodeLens[episodeLens.Count / 2]:F1}, mean {episodeLens.Average():F2}, p90 {episodeLens[(int)(episodeLens.Count * 0.9)]:F1}");
        }
        sb.AppendLine();
        sb.AppendLine("## Episode details per outcome (distances in 0.1 m sums; divide by n)");
        foreach (var kv in Detail!.OrderBy(k => k.Key)) sb.AppendLine($"{kv.Key} : {kv.Value}");
        sb.AppendLine();
        sb.AppendLine("## Fresh-decision table (open ticks where the carrier just decided; xG and held from the decision view)");
        long freshTotal = fresh.Values.Sum();
        foreach (var kv in fresh.OrderBy(k => k.Key)) sb.AppendLine($"{kv.Key} : {kv.Value} ({100.0 * kv.Value / Math.Max(1, freshTotal):F1}%)");
        sb.AppendLine($"TOTAL fresh {freshTotal}");
        sb.AppendLine();
        sb.AppendLine("## Open episodes that reached xG>=0.08: outcome");
        foreach (var kv in episodeXg.OrderByDescending(k => k.Value)) sb.AppendLine($"{kv.Key} : {kv.Value}");
        if (shotDelay.Count > 0) { shotDelay.Sort(); sb.AppendLine($"seconds from first shootable open tick to the shot: median {shotDelay[shotDelay.Count / 2]:F1}, mean {shotDelay.Average():F2}, p90 {shotDelay[(int)(shotDelay.Count * 0.9)]:F1}, n={shotDelay.Count}"); }
        sb.AppendLine();
        sb.AppendLine("## Freezes / dead balls");
        foreach (var kv in freeze.OrderBy(k => k.Key)) sb.AppendLine($"{kv.Key} : {kv.Value}");
        foreach (var e in freezeExamples) sb.AppendLine(e);
        sb.AppendLine("## Still runs >= 60 s (outfield)");
        foreach (var r in longestRuns.OrderByDescending(x => x.Len).Take(25)) sb.AppendLine($"{r.Len / 10.0:F0} s from tick {r.StartTick}: {r.Who} in {r.Match}");
        sb.AppendLine($"count of still runs >= 60 s: {longestRuns.Count}");
        sb.AppendLine();
        sb.AppendLine("## Carrier within 11 m of goal during hold window: what does he do?");
        foreach (var kv in insideBoxIntents.OrderByDescending(k => k.Value)) sb.AppendLine($"{kv.Key} : {kv.Value}");
        sb.AppendLine();
        sb.AppendLine("## Pass-back examples while open");
        foreach (var e in backExamples) { sb.AppendLine(e); sb.AppendLine(); }
        sb.AppendLine("## Standing still (speed < 0.3 m/s), running play, outfield+GK, >=5 s after restart");
        foreach (var kv in still.OrderBy(k => k.Key))
            sb.AppendLine($"{kv.Key} : still {kv.Value.Still} / {kv.Value.Total} = {100.0 * kv.Value.Still / Math.Max(1, kv.Value.Total):F1}%");
        sb.AppendLine();
        sb.AppendLine("## Carrier hold window (first 2.4 s of control)");
        foreach (var kv in holdCarrier.OrderBy(k => k.Key))
            sb.AppendLine($"{kv.Key} : still {kv.Value.Still} / {kv.Value.Total} = {100.0 * kv.Value.Still / Math.Max(1, kv.Value.Total):F1}%");
        sb.AppendLine();
        sb.AppendLine("## Still runs >= 3 s per role (outfield): count, mean s, max s, share of runs >= 10 s");
        foreach (var kv in stillRuns.OrderBy(k => k.Key))
        {
            var l = kv.Value;
            sb.AppendLine($"{kv.Key} : n={l.Count} mean={l.Average() / 10.0:F1} max={l.Max() / 10.0:F1} >=10s={100.0 * l.Count(x => x >= 100) / l.Count:F0}% total still seconds={l.Sum() / 10.0:F0}");
        }
        sb.AppendLine();
        sb.AppendLine("## Decision interval in ticks (View.Tick differences) ");
        foreach (var kv in decisionIntervals.OrderBy(k => int.Parse(k.Key))) sb.AppendLine($"{kv.Key} : {kv.Value}");
        sb.AppendLine();
        sb.AppendLine("## Long still examples (100 ticks = 10 s still)");
        foreach (var e in longStillExamples) { sb.AppendLine(e); sb.AppendLine(); }
        string text = sb.ToString();
        Console.WriteLine(text);
        if (outPath.Length > 0) File.WriteAllText(outPath, text);
        return 0;
    }

    private static readonly Dictionary<Episode, bool> Counted = new();
    private static Tally? Detail = new();
    private static Tally? _epXg; private static List<double>? _shotDelay;

    private static void FinishEpisode(Episode e, Tally outcomes, Tally byBand, List<double> lens, bool kicked)
    {
        outcomes.Inc(e.Outcome);
        Detail!.Inc($"{e.Outcome} | n"); Detail.Inc($"{e.Outcome} | sum startDist", (long)(e.StartDist * 10)); Detail.Inc($"{e.Outcome} | sum lastDist", (long)(e.LastDist * 10)); Detail.Inc($"{e.Outcome} | sum startHeld(0.1s)", (long)(e.StartHeld * 10)); Detail.Inc($"{e.Outcome} | sum lastHeld(0.1s)", (long)(e.LastHeld * 10));
        if (e.KeeperOff) Detail.Inc($"{e.Outcome} | keeper off lane/goal");
        if (e.LastDist > e.StartDist + 2) Detail.Inc($"{e.Outcome} | carrier moved >2 m away from goal during episode");
        if (e.MaxXg >= 0.08) _epXg!.Inc(e.Outcome);
        if (e.Outcome == "SHOT" && e.FirstXgTick >= 0) _shotDelay!.Add((e.StartTick + e.Ticks - e.FirstXgTick) * 0.1);
        string band = e.MinDist < 12 ? "min dist <12 m" : e.MinDist < 18 ? "min dist 12-18 m" : "min dist 18-25 m";
        byBand.Inc(band + " -> " + e.Outcome);
        lens.Add(e.Ticks * 0.1);
        Counted[e] = true;
    }

    private static void UndoEpisode(Episode e, Tally outcomes, Tally byBand, List<double> lens)
    {
        if (!Counted.Remove(e)) return;
        Detail!.Inc($"{e.Outcome} | n", -1); Detail.Inc($"{e.Outcome} | sum startDist", -(long)(e.StartDist * 10)); Detail.Inc($"{e.Outcome} | sum lastDist", -(long)(e.LastDist * 10)); Detail.Inc($"{e.Outcome} | sum startHeld(0.1s)", -(long)(e.StartHeld * 10)); Detail.Inc($"{e.Outcome} | sum lastHeld(0.1s)", -(long)(e.LastHeld * 10));
        if (e.KeeperOff) Detail.Inc($"{e.Outcome} | keeper off lane/goal", -1);
        if (e.LastDist > e.StartDist + 2) Detail.Inc($"{e.Outcome} | carrier moved >2 m away from goal during episode", -1);
        outcomes.Inc(e.Outcome, -1);
        if (e.MaxXg >= 0.08) _epXg!.Inc(e.Outcome, -1);
        string band = e.MinDist < 12 ? "min dist <12 m" : e.MinDist < 18 ? "min dist 12-18 m" : "min dist 18-25 m";
        byBand.Inc(band + " -> " + e.Outcome, -1);
        lens.RemoveAt(lens.Count - 1);
    }

    private static string TeamState(Match m, Snapshot snap)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"  tick {m.Tick} ({m.TimeSeconds:F0}s) status {m.Status} possession {m.Possession} carrier {m.CarrierId} ball {F(snap.Ball!.Pos.XY)} v {snap.Ball.Vel.XY.Length:F2} z {snap.Ball.Pos.Z:F2} phase H {m.PhaseOf(Side.Home)} A {m.PhaseOf(Side.Away)}");
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            sb.Append($"  {side}: ");
            foreach (var p in snap.Players.Where(p => p.Team == side))
            {
                var i = m.IntentOf(p.Team, p.Id);
                sb.Append($"{p.Id}{F(p.Pos)} v{p.Vel.Length:F1} {i?.Kind}{(m.PerceptionOf(p.Team, p.Id)?.VisibleBall is null ? "(blind)" : "")}; ");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string F(Vec2 v) => $"({v.X:F1},{v.Y:F1})";

    private static string Dump(Match m, Snapshot snap, PlayerSnapshot who, Intent intent, Vec2 target, string role, string rel, string tactic, Perception view)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"STILL 10 s: tick {m.Tick} ({m.TimeSeconds:F0}s) {who.Team}/{who.Id} role={role} tactic={tactic} rel={rel} phase={m.PhaseOf(who.Team)} score {m.HomeScore}-{m.AwayScore}");
        sb.AppendLine($"  pos {F(who.Pos)} target {F(target)} intent {intent.Kind} target-of-intent {F(intent.Target)} formationTarget {F(view.FormationTarget)} instr=[{string.Join(",", view.Instructions ?? Array.Empty<string>())}] mark={view.MarkId} presser={view.IsPresser} sawBall={view.VisibleBall is not null}");
        sb.AppendLine($"  ball {F(snap.Ball!.Pos.XY)} carrier {m.Possession}/{m.CarrierId}");
        sb.Append("  team: ");
        foreach (var p in snap.Players.Where(p => p.Team == who.Team))
            sb.Append($"{p.Id}{F(p.Pos)}[{m.IntentOf(p.Team, p.Id)?.Kind.ToString()[0]}] ");
        return sb.ToString();
    }

    private static string BackDump(Match m, Snapshot snap, PlayerSnapshot carrier, PlayerSnapshot receiver, Perception? view, Episode ep,
        PassConfig pc, ShotConfig sc, double held, string tactic, string evOutcome)
    {
        double dsign = ep.DirSign;
        var goal = new Vec2(dsign * Pitch.HalfLength, 0);
        double dist = (goal - carrier.Pos).Length;
        double xg = ShotModel.GoalProbability(dist, carrier.Pos.Y);
        var sb = new StringBuilder();
        sb.AppendLine($"BACKPASS while open: tick {m.Tick} {carrier.Team}/{carrier.Id} tactic={tactic} dist-to-goal {dist:F1} lateral {carrier.Pos.Y:F1} xG {xg:F3} (threshold {sc.MinGoalProbability}) held {held:F1}s -> {evOutcome} (receiver {receiver.Id} at {F(receiver.Pos)}; carrier {F(carrier.Pos)})");
        sb.AppendLine($"  instructions=[{string.Join(",", view?.Instructions ?? Array.Empty<string>())}] episode ticks {ep.Ticks}");
        if (view is not null)
        {
            var option = PassChoice.Choose(view, pc);
            sb.AppendLine($"  PassChoice score {option?.Score:F2} to {option?.ReceiverId} through={option?.IsThroughBall}; opponents visible {view.VisiblePlayers.Count(p => p.Team != carrier.Team)}");
        }
        sb.Append("  opponents: ");
        foreach (var o in snap.Players.Where(p => p.Team != carrier.Team && !p.SentOff && (p.Pos - carrier.Pos).Length < 25))
            sb.Append($"{o.Id}{F(o.Pos)}{(o.IsKeeper ? "GK" : "")} ");
        return sb.ToString();
    }
}
