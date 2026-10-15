using System.Globalization;
using Zeus.Sim;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
// Gebruik (vanuit de repo-root): dotnet run -c Release --project sim/Zeus.SquadProbe -- [seeds=8] [alleen-complex-niveaus, bv. neutraal,prof]
int seeds = args.Length > 0 ? int.Parse(args[0]) : 8;
string[]? onlyComplex = args.Length > 1 ? args[1].Split(',') : null;
var simple = TacticLoader.LoadFile("tactics/laag_blok_442.json");
var conditions = new[] { TriggerRules.BallToKeeper, TriggerRules.BadTouch, TriggerRules.OpponentOnFlank,
    TriggerRules.BackPassToMidfielder, TriggerRules.LossInOwnHalf, TriggerRules.WinInHighZone };
var complex = simple with { Name = "Complexe werkhypothese 4-4-2", Phases = simple.Phases.ToDictionary(x => x.Key,
    x => x.Value with { Players = x.Value.Players.Select(p => p.Id == "GK" ? p : p with {
        Instructions = p.Instructions.Append("dek_man").Distinct().ToArray(), Mark = p.Id }).ToArray() }),
    Triggers = conditions.Select(c => new Trigger(c, c == TriggerRules.LossInOwnHalf ? GamePhase.Balverlies :
        c == TriggerRules.WinInHighZone ? GamePhase.Balwinst : GamePhase.Verdedigen, c, null,
        simple.Roster.Where(p => p.Id != "GK").Select(p => new TriggerAction(p.Id, "start_pressing")).ToArray())).ToArray() };
// "neutraal" = geen selectie meegegeven (oud gedrag); een probe-uitvoer per niveau tegen neutraal meet het niveaueffect.
var squads = new[] { "amateur", "halfprof", "prof" }.ToDictionary(x => x, x => (Squad?)SquadLoader.LoadFile("squads/" + x + ".json"));
squads["neutraal"] = null;
Console.WriteLine("| Proef (beide rollen) | Ploeg | Passes | Nauwkeurig % | Bezit % | Goals | Schoten | Spreiding m | Buiten bereik passes/schoten | Gestaakt | W-G-V |");
Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
foreach (var pair in onlyComplex is not null ? Array.Empty<(string, string)>() : new[] { ("prof", "amateur"), ("halfprof", "amateur"), ("prof", "halfprof"), ("prof", "neutraal"), ("halfprof", "neutraal"),
    ("amateur", "neutraal"), ("neutraal", "neutraal"), ("amateur", "amateur"), ("halfprof", "halfprof"), ("prof", "prof") })
    Run(pair.Item1 + " / " + pair.Item2, simple, simple, pair.Item1, pair.Item2);
foreach (string level in onlyComplex ?? new[] { "amateur", "halfprof", "prof" })
{
    Run("eenvoudig", simple, simple, level, level, firstOnly: true);
    Run("complex", complex, simple, level, level, firstOnly: true);
}

void Run(string label, Tactic a, Tactic b, string levelA, string levelB, bool firstOnly = false)
{
    var rows = new List<Row>[] { new(), new() };
    for (ulong seed = 1; seed <= (ulong)seeds; seed++)
    for (int swap = 0; swap < 2; swap++)
    {
        var match = Match.Create(new MatchConfig(), swap == 0 ? a : b, swap == 0 ? b : a, seed,
            homeSquad: squads[swap == 0 ? levelA : levelB], awaySquad: squads[swap == 0 ? levelB : levelA]);
        var ticks = new int[2]; var spread = new double[2]; var samples = new int[2];
        for (int i = 0; i < 150000 && match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned); i++)
        {
            match.Step();
            if (match.Status != MatchStatus.Running) continue;
            ticks[(int)match.Possession]++;
            if (match.Tick % 10 != 0) continue;
            var world = match.GetSnapshot();
            foreach (var side in new[] { Side.Home, Side.Away })
            {
                if (side == match.Possession) continue;
                var players = world.Players.Where(p => p.Team == side && !p.IsKeeper && !p.SentOff).ToArray();
                double x = players.Average(p => p.Pos.X), y = players.Average(p => p.Pos.Y);
                spread[(int)side] += Math.Sqrt(players.Average(p => Math.Pow(p.Pos.X - x, 2) + Math.Pow(p.Pos.Y - y, 2)));
                samples[(int)side]++;
            }
        }
        var stats = MatchStatistics.FromMatch(match);
        var completed = new int[2]; Event? pending = null;
        foreach (var e in match.Events.Events)
        {
            if (e.Type == EventType.Pass) { pending = e; continue; }
            if (pending is null) continue;
            if (e.Type is EventType.BallControl or EventType.Interception or EventType.PossessionChange)
            {
                if (e.Team == pending.Team && e.PlayerId != pending.PlayerId) completed[(int)e.Team]++;
                pending = null;
            }
            else if (e.Type is EventType.RestartAwarded or EventType.Shot or EventType.Goal or EventType.HalfTime or EventType.FullTime) pending = null;
        }
        for (int side = 0; side < 2; side++)
        {
            var t = side == 0 ? stats.Home : stats.Away;
            rows[side ^ swap].Add(new Row(t.Passes, t.Passes == 0 ? 0 : 100.0 * completed[side] / t.Passes,
                100.0 * ticks[side] / Math.Max(1, ticks.Sum()), t.Goals, t.Shots, spread[side] / Math.Max(1, samples[side]),
                t.Passes is < 250 or > 700 || t.Shots is < 5 or > 30, match.Status == MatchStatus.Abandoned,
                Math.Sign(t.Goals - (side == 0 ? stats.Away : stats.Home).Goals)));
        }
    }
    for (int i = 0; i < (firstOnly ? 1 : 2); i++)
    {
        var r = rows[i];
        Console.WriteLine($"| {label} | {(i == 0 ? levelA : levelB)} | {r.Average(x => x.Passes):F2} | {r.Average(x => x.Accuracy):F2} | {r.Average(x => x.Possession):F2} | {r.Average(x => x.Goals):F2} | {r.Average(x => x.Shots):F2} | {r.Average(x => x.Spread):F2} | {r.Count(x => x.OutOfRange)}/{r.Count} | {r.Count(x => x.Abandoned)} | {r.Count(x => x.Result > 0)}-{r.Count(x => x.Result == 0)}-{r.Count(x => x.Result < 0)} |");
    }
}
record Row(int Passes, double Accuracy, double Possession, int Goals, int Shots, double Spread, bool OutOfRange, bool Abandoned, int Result);
