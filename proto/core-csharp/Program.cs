// WP8 proef A: wegwerpprototype van de simulatiekern in C# (headless).
// 22 stippen nemen posities uit het tactiekbestand in, spelen elkaar aan, 90 minuten, tijdmeting.
// Kansen zijn abstract en NIET gekalibreerd; het doel is snelheid, determinisme en de dataroute.
using System.Diagnostics;
using System.Text.Json;

var root = args.Length > 0 ? args[0] : @"C:\dev\zeus";
var tactics = new[]
{
    Tactic.Load(Path.Combine(root, "tactics", "laag_blok_442.json")),
    Tactic.Load(Path.Combine(root, "tactics", "man_om_man_433.json")),
};

// Determinisme: zelfde seed twee keer
var h1 = new Match(tactics, 42).Run();
var h2 = new Match(tactics, 42).Run();
Console.WriteLine($"Determinisme (seed 42): {(h1.Hash == h2.Hash ? "OK" : "FOUT")}  hash={h1.Hash:X16}");
Console.WriteLine($"Voorbeeldwedstrijd: stand {h1.Goals[0]}-{h1.Goals[1]}, passes A/B {h1.Passes[0]}/{h1.Passes[1]}, " +
                  $"passnauwkeurigheid A/B {h1.Accuracy(0):P0}/{h1.Accuracy(1):P0}, schoten A/B {h1.Shots[0]}/{h1.Shots[1]}");

// Snelheid: 1 wedstrijd en 200 wedstrijden
var sw = Stopwatch.StartNew();
new Match(tactics, 1).Run();
Console.WriteLine($"1 wedstrijd: {sw.ElapsedMilliseconds} ms");

const int n = 200;
sw.Restart();
var allPasses = 0; var allComplete = 0; var allGoals = 0; var allShots = 0;
for (var s = 0; s < n; s++)
{
    var r = new Match(tactics, 1000 + s).Run();
    allPasses += r.Passes[0] + r.Passes[1];
    allComplete += r.Complete[0] + r.Complete[1];
    allGoals += r.Goals[0] + r.Goals[1];
    allShots += r.Shots[0] + r.Shots[1];
}
var ms = sw.ElapsedMilliseconds;
Console.WriteLine($"{n} wedstrijden: {ms} ms totaal, {ms / (double)n:F1} ms per wedstrijd, 1000 wedstrijden ca. {ms * 1000.0 / n / 1000:F1} s");
Console.WriteLine($"Gemiddeld per wedstrijd: passes {allPasses / (double)n:F0}, passnauwkeurigheid {allComplete / (double)allPasses:P0}, " +
                  $"schoten {allShots / (double)n:F1}, doelpunten {allGoals / (double)n:F2}");

record Slot(double X, double Y, double Fx, double Fy);

class Tactic
{
    public Dictionary<string, Slot[]> Phases = new();

    public static Tactic Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var fases = doc.RootElement.GetProperty("fases");
        var ids = doc.RootElement.GetProperty("spelers").EnumerateArray()
            .Select(s => s.GetProperty("id").GetString()!).ToList();
        var t = new Tactic();
        foreach (var f in fases.EnumerateObject()) t.Phases[f.Name] = Resolve(fases, f.Name, ids);
        return t;
    }

    static Slot[] Resolve(JsonElement fases, string name, List<string> ids)
    {
        var f = fases.GetProperty(name);
        if (f.TryGetProperty("gelijk_aan", out var g)) return Resolve(fases, g.GetString()!, ids);
        var map = f.GetProperty("spelers").EnumerateArray().ToDictionary(s => s.GetProperty("id").GetString()!, s => s);
        return ids.Select(id =>
        {
            var s = map[id];
            var p = s.GetProperty("pos");
            double fx = 0, fy = 0;
            if (s.TryGetProperty("volg_bal", out var v)) { fx = v[0].GetDouble(); fy = v[1].GetDouble(); }
            return new Slot(p[0].GetDouble(), p[1].GetDouble(), fx, fy);
        }).ToArray();
    }
}

class Result
{
    public int[] Goals = new int[2], Passes = new int[2], Complete = new int[2], Shots = new int[2];
    public ulong Hash = 14695981039346656037UL;
    public double Accuracy(int t) => Passes[t] == 0 ? 0 : Complete[t] / (double)Passes[t];
    public void Add(int tick, int type, int team, int who)
    {
        foreach (var v in new[] { tick, type, team, who })
        {
            Hash ^= (ulong)v;
            Hash *= 1099511628211UL;
        }
    }
}

class Match
{
    const double L = 105, W = 68, Dt = 0.1, Speed = 6.0;
    const int Ticks = 54000; // 90 minuten bij 10 Hz
    readonly Tactic[] tac;
    readonly Random rng;
    readonly double[,] x = new double[2, 11], y = new double[2, 11];
    int possTeam, carrier = 9, countdown;
    readonly Result res = new();

    public Match(Tactic[] tactics, int seed) { tac = tactics; rng = new Random(seed); }

    static double WorldX(int team, double sx) => team == 0 ? (sx - 0.5) * L : (0.5 - sx) * L;
    static double WorldY(int team, double sy) => team == 0 ? (sy - 0.5) * W : (0.5 - sy) * W;
    // x in aanvalsrichting van het team (0 = eigen doellijn, 1 = tegenstanders doellijn)
    static double Rel(int team, double wx) => team == 0 ? wx / L + 0.5 : 0.5 - wx / L;

    public Result Run()
    {
        for (var t = 0; t < 2; t++)
        {
            var slots = tac[t].Phases["opbouw"];
            for (var i = 0; i < 11; i++) { x[t, i] = WorldX(t, slots[i].X); y[t, i] = WorldY(t, slots[i].Y); }
        }
        countdown = 40;
        for (var tick = 0; tick < Ticks; tick++)
        {
            var bx = x[possTeam, carrier];
            for (var t = 0; t < 2; t++)
            {
                var slots = tac[t].Phases[t == possTeam ? "opbouw" : "verdedigen"];
                var ballRel = Rel(t, bx);
                for (var i = 0; i < 11; i++)
                {
                    var s = slots[i];
                    var tx = WorldX(t, s.X + s.Fx * (ballRel - 0.5) * 0.5);
                    var ty = WorldY(t, s.Y);
                    var dx = tx - x[t, i]; var dy = ty - y[t, i];
                    var d = Math.Sqrt(dx * dx + dy * dy);
                    var step = Speed * Dt;
                    if (d <= step) { x[t, i] = tx; y[t, i] = ty; }
                    else { x[t, i] += dx / d * step; y[t, i] += dy / d * step; }
                }
            }
            if (--countdown <= 0) { Decide(tick); countdown = 30 + rng.Next(40); }
        }
        return res;
    }

    void Decide(int tick)
    {
        var t = possTeam; var o = 1 - t;
        var cx = x[t, carrier]; var cy = y[t, carrier];
        // druk op de baldrager: tegenstander binnen 2 m kan de bal veroveren
        for (var j = 0; j < 11; j++)
            if (Dist(cx, cy, x[o, j], y[o, j]) < 2.0 && rng.NextDouble() < 0.15)
            {
                res.Add(tick, 1, o, j); possTeam = o; carrier = j; return;
            }
        var goalX = t == 0 ? L / 2 : -L / 2;
        var dGoal = Dist(cx, cy, goalX, 0);
        if (dGoal < 25 && rng.NextDouble() < 0.35)
        {
            res.Shots[t]++;
            var p = 0.02 + 0.35 * Math.Pow(1 - dGoal / 25, 2);
            if (rng.NextDouble() < p) { res.Goals[t]++; res.Add(tick, 2, t, carrier); possTeam = o; carrier = 9; }
            else { res.Add(tick, 3, t, carrier); possTeam = o; carrier = 0; }
            return;
        }
        // pass: kies een vrije teamgenoot die niet ver achter de baldrager staat
        var ownRel = Rel(t, cx);
        var options = new List<int>();
        for (var j = 0; j < 11; j++)
        {
            if (j == carrier) continue;
            var d = Dist(cx, cy, x[t, j], y[t, j]);
            if (d is >= 8 and <= 35 && Rel(t, x[t, j]) > ownRel - 0.05) options.Add(j);
        }
        if (options.Count == 0) { carrier = rng.Next(11); return; }
        var r = options[rng.Next(options.Count)];
        var dist = Dist(cx, cy, x[t, r], y[t, r]);
        var pressure = 0;
        for (var j = 0; j < 11; j++) if (Dist(x[t, r], y[t, r], x[o, j], y[o, j]) < 4.0) pressure++;
        var pp = Math.Clamp(0.92 - 0.004 * dist - 0.12 * pressure, 0.3, 0.95);
        res.Passes[t]++;
        if (rng.NextDouble() < pp) { res.Complete[t]++; res.Add(tick, 4, t, r); carrier = r; }
        else
        {
            var near = 0; var best = double.MaxValue;
            for (var j = 0; j < 11; j++)
            {
                var d = Dist(x[t, r], y[t, r], x[o, j], y[o, j]);
                if (d < best) { best = d; near = j; }
            }
            res.Add(tick, 5, o, near); possTeam = o; carrier = near;
        }
    }

    static double Dist(double ax, double ay, double bx, double by)
    {
        var dx = ax - bx; var dy = ay - by;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
