using System.Globalization;
using System.Text.Json;
using Zeus.Sim;

/// <summary>
/// B6 measurement: corners, free kicks and goals shortly after a set piece, per match (JSON line) and in total.
/// Usage (repo root): Zeus.Runner --set-piece-stats --pairs a:b,c:d [--level prof] [--seeds 20] [--first-seed 901]
///   [--set-pieces corner_voor=korte_paal,corner_tegen=zone,palen=2,vrije_trap_voor=voorzet,vrije_trap_tegen=zone] [--no-headers] [--out file.jsonl]
/// Without --set-pieces the tactics run as they are (baseline). With it the same block is applied to both teams of every pair.
/// A goal counts as "from a set piece" when it falls within 10 s of the kick of a corner, free kick or penalty (same period).
/// </summary>
internal static class SetPieceStats
{
    internal static int Run(string[] args)
    {
        string V(string k, string d) { int i = Array.IndexOf(args, k); return i >= 0 && i + 1 < args.Length ? args[i + 1] : d; }
        var inv = CultureInfo.InvariantCulture;
        int seeds = int.Parse(V("--seeds", "20"), inv), first = int.Parse(V("--first-seed", "901"), inv);
        string level = V("--level", "prof");
        var pairs = V("--pairs", "") is { Length: > 0 } p ? p.Split(',').Select(x => x.Split(':')).ToArray()
            : throw new ArgumentException("missing --pairs home:away[,home:away]");
        SetPieces? block = V("--set-pieces", "") is { Length: > 0 } sp ? Parse(sp) : null;
        var jopt = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        using var output = V("--out", "") is { Length: > 0 } o ? new StreamWriter(o, false, new System.Text.UTF8Encoding(false)) : null;
        int matches = 0, goals = 0, corners = 0, freeKicks = 0, penalties = 0, gCorner = 0, gFree = 0, gPenalty = 0, headers = 0, clears = 0;
        int armed = 0, entered = 0, keeperAt = 0, withCandidate = 0, rolled = 0, succeeded = 0, slotCount = 0, claims = 0, duels = 0, duelsDef = 0, flicks = 0;
        double slotSum = 0;
        // --no-headers: routines without the header model (the 2026-10-19 measurement).
        var config = args.Contains("--no-headers") ? new MatchConfig(HeaderFinishing: new HeaderConfig(Enabled: false)) : new MatchConfig();
        var tactics = new Dictionary<string, Tactic>();
        Tactic Load(string n) => tactics.TryGetValue(n, out var t) ? t : tactics[n] = Apply(TacticLoader.LoadFile($"tactics/{n}.json"), block);
        foreach (var pair in pairs)
            for (int s = 0; s < seeds; s++)
            {
                var home = Load(pair[0]); var away = Load(pair[1]);
                var m = Match.Create(config, home, away, (ulong)(first + s),
                    homeSquad: SquadFactory.ForLevel(level, home, "squads"), awaySquad: SquadFactory.ForLevel(level, away, "squads"));
                while (m.Status is not (MatchStatus.Finished or MatchStatus.Abandoned) && m.Tick < 150000) m.Step();
                var ev = m.Events.Events;
                int c = 0, f = 0, pe = 0, gc = 0, gf = 0, gp = 0; // counted when taken, with the reason of the last award
                string? reason = null; (string Reason, double Seconds, int Period)? last = null;
                foreach (var e in ev)
                {
                    if (e.Type == EventType.RestartAwarded) reason = e.Outcome;
                    else if (e.Type == EventType.RestartTaken)
                    {
                        if (reason == "Corner") c++; else if (reason == "free_kick") f++; else if (reason == "penalty") pe++;
                        last = reason is "Corner" or "free_kick" or "penalty" ? (reason, e.Seconds, e.Period) : null;
                    }
                    else if (e.Type == EventType.Goal && last is { } l && e.Period == l.Period && e.Seconds - l.Seconds <= 10)
                    {
                        if (l.Reason == "Corner") gc++; else if (l.Reason == "free_kick") gf++; else gp++;
                        last = null;
                    }
                }
                int g = m.HomeScore + m.AwayScore;
                headers += ev.Count(e => e.Type == EventType.Shot && e.Outcome == "header");
                clears += ev.Count(e => e.Type == EventType.Pass && e.Outcome == "header clear");
                var hs = m.HeaderStats;
                armed += hs.Armed; entered += hs.WindowEntered; keeperAt += hs.KeeperAtEntry; withCandidate += hs.WithCandidate;
                rolled += hs.AttemptsRolled; succeeded += hs.AttemptsSucceeded; slotSum += hs.EntrySlotDistanceSum; slotCount += hs.EntrySlotDistanceCount;
                claims += hs.KeeperClaims; duels += hs.Duels; duelsDef += hs.DuelsWonByDefender; flicks += hs.Flicks;
                matches++; goals += g; corners += c; freeKicks += f; penalties += pe; gCorner += gc; gFree += gf; gPenalty += gp;
                output?.WriteLine(JsonSerializer.Serialize(new { home = pair[0], away = pair[1], seed = first + s, status = m.Status.ToString(),
                    goals = g, corners = c, freeKicks = f, penalties = pe, goalsAfterCorner = gc, goalsAfterFreeKick = gf, goalsAfterPenalty = gp,
                    eventHash = m.EventHash() }, jopt));
            }
        string F(double x) => x.ToString("F2", inv);
        double n = Math.Max(1, matches);
        Console.WriteLine($"set-piece-stats: level={level} matches={matches} routine={(block is null ? "baseline" : V("--set-pieces", ""))}");
        Console.WriteLine($"  per match: goals {F(goals / n)}, corners {F(corners / n)}, free kicks {F(freeKicks / n)}, penalties {F(penalties / n)}");
        Console.WriteLine($"  goals within 10 s: corner {gCorner}, free kick {gFree}, penalty {gPenalty}; share of all goals {F(100.0 * (gCorner + gFree + gPenalty) / Math.Max(1, goals))}%");
        Console.WriteLine($"  goals per corner {F(100.0 * gCorner / Math.Max(1, corners))}%");
        Console.WriteLine($"  header shots {headers}, header clearances {clears}");
        Console.WriteLine($"  header window: armed {armed}, entered (descending 1.3-2.6 m in the box) {entered}, keeper within reach at entry {keeperAt}, "
            + $"candidate within R {withCandidate}, attempts rolled {rolled}, succeeded {succeeded}, "
            + $"mean entry distance to nearest slot {(slotCount > 0 ? F(slotSum / slotCount) : "-")} m");
        Console.WriteLine($"  keeper claims {claims}, aerial duels {duels} (won by defender {duelsDef}), flicks {flicks}");
        return 0;
    }

    static Tactic Apply(Tactic t, SetPieces? block) => block is null ? t : t with { SetPieces = block };

    static SetPieces Parse(string text)
    {
        var d = text.Split(',').Select(x => x.Split('=')).ToDictionary(x => x[0], x => x[1]);
        string? G(string k) => d.GetValueOrDefault(k);
        return new SetPieces(G("corner_voor"), G("corner_tegen"), int.Parse(G("palen") ?? "2", CultureInfo.InvariantCulture),
            G("vrije_trap_voor"), G("vrije_trap_tegen"));
    }
}
