using System.Text.Json;

namespace Zeus.Sim;

public sealed class TacticLoadException : Exception
{
    public TacticLoadException(string message) : base(message) { }
    public TacticLoadException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Structural loader for tactic schema v0.1. The Python validator remains the leading full-schema validator.</summary>
public static class TacticLoader
{
    private static readonly string[] PhaseNames = { "opbouw", "aanval", "balverlies", "verdedigen", "balwinst", "stilstaand" };

    public static Tactic LoadFile(string path)
    {
        string json;
        try { json = File.ReadAllText(path); }
        catch (Exception e) { throw new TacticLoadException($"Cannot read tactic file '{path}': {e.Message}", e); }
        try { return Parse(json); }
        catch (TacticLoadException e) { throw new TacticLoadException($"Tactic '{path}': {e.Message}", e); }
    }

    public static Tactic Parse(string json)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException e) { throw new TacticLoadException($"invalid JSON: {e.Message}", e); }
        using (doc)
        {
            try { return ParseRoot(doc.RootElement); }
            catch (Exception e) when (e is InvalidOperationException or FormatException or KeyNotFoundException)
            {
                throw new TacticLoadException($"unexpected structure: {e.Message}", e);
            }
        }
    }

    private static Tactic ParseRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new TacticLoadException("root must be an object");

        string version = Str(Req(root, "schemaVersie", "$"), "$.schemaVersie");
        if (version != "0.1") throw new TacticLoadException($"unsupported schemaVersie '{version}' (expected 0.1)");

        string name = Str(Req(root, "naam", "$"), "$.naam");
        if (name.Length == 0) throw new TacticLoadException("$.naam must not be empty");

        var team = ParseTeam(Req(root, "team", "$"));
        var roster = ParseRoster(Req(root, "spelers", "$"));
        var ids = roster.Select(r => r.Id).ToList();

        var fases = Req(root, "fases", "$");
        if (fases.ValueKind != JsonValueKind.Object) throw new TacticLoadException("$.fases must be an object");
        var phases = ResolvePhases(fases, ids);

        var variants = new SortedDictionary<BlockHeight, PhaseLayout>();
        if (root.TryGetProperty("verdedigen_varianten", out var vv))
        {
            foreach (var (key, bh) in new[] { ("hoog", BlockHeight.Hoog), ("midden", BlockHeight.Midden), ("laag", BlockHeight.Laag) })
                if (vv.TryGetProperty(key, out var v))
                    variants[bh] = ParseFullPhase(v, $"$.verdedigen_varianten.{key}", ids);
        }

        var switches = new List<BlockSwitch>();
        if (root.TryGetProperty("blokwissels", out var bw))
        {
            if (bw.ValueKind != JsonValueKind.Array || bw.GetArrayLength() > 8)
                throw new TacticLoadException("$.blokwissels must contain at most 8 switches");
            if (variants.Count == 0)
                throw new TacticLoadException("$.blokwissels requires verdedigen_varianten");
            int i = 0;
            foreach (var s in bw.EnumerateArray())
            {
                string at = $"$.blokwissels[{i++}]";
                var to = ParseBlock(Str(Req(s, "naar", at), at + ".naar"), at + ".naar");
                if (!variants.ContainsKey(to))
                    throw new TacticLoadException($"{at}.naar '{to}' has no matching verdedigen_varianten entry");
                var w = Req(s, "wanneer", at);
                ScoreState? score = null;
                if (w.TryGetProperty("stand", out var st))
                    score = Str(st, at + ".wanneer.stand") switch
                    {
                        "voor" => ScoreState.Voor,
                        "gelijk" => ScoreState.Gelijk,
                        "achter" => ScoreState.Achter,
                        var x => throw new TacticLoadException($"{at}.wanneer.stand invalid: '{x}'")
                    };
                int? minute = w.TryGetProperty("vanaf_minuut", out var m) ? m.GetInt32() : null;
                double? fatigue = w.TryGetProperty("vermoeidheid_boven", out var f) ? f.GetDouble() : null;
                if (score is null && minute is null && fatigue is null)
                    throw new TacticLoadException($"{at}.wanneer needs at least one condition");
                string? sname = s.TryGetProperty("naam", out var sn) ? sn.GetString() : null;
                switches.Add(new BlockSwitch(sname, new BlockSwitchCondition(score, minute, fatigue), to));
            }
        }

        var triggers = new List<Trigger>();
        if (root.TryGetProperty("triggers", out var tr))
        {
            if (tr.ValueKind != JsonValueKind.Array || tr.GetArrayLength() > 32)
                throw new TacticLoadException("$.triggers must contain at most 32 triggers");
            int i = 0;
            foreach (var t in tr.EnumerateArray())
            {
                string at = $"$.triggers[{i++}]";
                var w = Req(t, "wanneer", at);
                var phase = ParsePhaseName(Str(Req(w, "fase", at + ".wanneer"), at + ".wanneer.fase"), at + ".wanneer.fase");
                string cond = Str(Req(w, "voorwaarde", at + ".wanneer"), at + ".wanneer.voorwaarde");
                string? zone = w.TryGetProperty("zone", out var z) ? z.GetString() : null;
                var actions = new List<TriggerAction>();
                var then = Req(t, "dan", at);
                if (then.ValueKind != JsonValueKind.Array || then.GetArrayLength() > 11)
                    throw new TacticLoadException($"{at}.dan must contain at most 11 actions");
                foreach (var a in then.EnumerateArray())
                {
                    string who = Str(Req(a, "speler", at + ".dan"), at + ".dan.speler");
                    if (!ids.Contains(who)) throw new TacticLoadException($"{at}.dan references unknown player '{who}'");
                    string? instruction = a.TryGetProperty("instructie", out var instructionValue)
                        ? Str(instructionValue, at + ".dan.instructie") : null;
                    bool path = a.TryGetProperty("pad", out var pathValue);
                    if (path && pathValue.ValueKind != JsonValueKind.True)
                        throw new TacticLoadException($"{at}.dan.pad must be true");
                    if (instruction is null && !path)
                        throw new TacticLoadException($"{at}.dan needs instructie or pad: true");
                    actions.Add(new TriggerAction(who, instruction, path));
                }
                if (actions.Count == 0) throw new TacticLoadException($"{at}.dan must not be empty");
                string? tname = t.TryGetProperty("naam", out var tn) ? tn.GetString() : null;
                triggers.Add(new Trigger(tname, phase, cond, zone, actions));
            }
        }

        SetPieces? setPieces = root.TryGetProperty("standaardsituaties", out var ss) ? ParseSetPieces(ss) : null;
        return new Tactic(name, team, roster, phases, variants, switches, triggers, setPieces);
    }

    private static readonly string[] SetPieceKeys = { "corner_voor", "corner_tegen", "palen", "vrije_trap_voor", "vrije_trap_tegen" };

    private static SetPieces ParseSetPieces(JsonElement e)
    {
        const string at = "$.standaardsituaties";
        if (e.ValueKind != JsonValueKind.Object) throw new TacticLoadException(at + " must be an object");
        string? Pick(string key, IReadOnlyList<string> allowed)
        {
            if (!e.TryGetProperty(key, out var v)) return null;
            string s = Str(v, $"{at}.{key}");
            return allowed.Contains(s) ? s : throw new TacticLoadException($"{at}.{key} invalid: '{s}'");
        }
        foreach (var property in e.EnumerateObject())
            if (!SetPieceKeys.Contains(property.Name)) throw new TacticLoadException($"{at}.{property.Name} is not a known field");
        int posts = 2;
        if (e.TryGetProperty("palen", out var pv))
        {
            if (pv.ValueKind != JsonValueKind.Number || !pv.TryGetInt32(out posts) || posts < 0 || posts > 2)
                throw new TacticLoadException($"{at}.palen must be an integer within 0..2");
        }
        return new SetPieces(Pick("corner_voor", SetPieces.CornerRoutines), Pick("corner_tegen", SetPieces.Defences), posts,
            Pick("vrije_trap_voor", SetPieces.FreeKickRoutines), Pick("vrije_trap_tegen", SetPieces.Defences));
    }

    private static TeamSettings ParseTeam(JsonElement e)
    {
        double Num(string key, bool required = true)
        {
            if (!e.TryGetProperty(key, out var v))
            {
                if (required) throw new TacticLoadException($"$.team.{key} is missing");
                return double.NaN;
            }
            double d = Number(v, $"$.team.{key}");
            if (d < 0 || d > 1) throw new TacticLoadException($"$.team.{key} must be within 0..1 (was {d})");
            return d;
        }
        string form = Str(Req(e, "pressing_vorm", "$.team"), "$.team.pressing_vorm");
        if (form is not ("zone" or "man" or "gemengd"))
            throw new TacticLoadException($"$.team.pressing_vorm invalid: '{form}'");
        double compact = Num("compactheid", false);
        return new TeamSettings(Num("tempo"), Num("pressing_intensiteit"), form, Num("lijnhoogte"),
            double.IsNaN(compact) ? null : compact);
    }

    /// <summary>The team-setting rules of the loader, reusable for live adjustments (Match.Adjust).</summary>
    public static void ValidateTeamSettings(TeamSettings team)
    {
        foreach (var (key, value) in new[] { ("tempo", (double?)team.Tempo), ("pressing_intensiteit", team.PressingIntensity),
            ("lijnhoogte", team.LineHeight), ("compactheid", team.Compactness) })
            if (value is { } d && (!double.IsFinite(d) || d < 0 || d > 1))
                throw new TacticLoadException($"$.team.{key} must be within 0..1 (was {d})");
        if (team.PressingForm is not ("zone" or "man" or "gemengd"))
            throw new TacticLoadException($"$.team.pressing_vorm invalid: '{team.PressingForm}'");
    }

    /// <summary>The dek_man/dek rules of the loader for one player of one layout; <paramref name="ids"/> are the roster ids.</summary>
    public static void ValidatePhasePlayer(PhasePlayer player, IReadOnlyCollection<string> ids, string at)
    {
        if (player.Instructions.Contains("dek_man") != (player.Mark is not null))
            throw new TacticLoadException($"{at}: dek_man requires dek and dek requires dek_man");
        if (player.Mark is not null && !ids.Contains(player.Mark))
            throw new TacticLoadException($"{at}.dek references unknown player '{player.Mark}'");
    }

    private static List<RosterPlayer> ParseRoster(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Array) throw new TacticLoadException("$.spelers must be an array");
        if (e.GetArrayLength() != 11) throw new TacticLoadException($"$.spelers must have 11 players (found {e.GetArrayLength()})");
        var list = new List<RosterPlayer>();
        int i = 0;
        foreach (var p in e.EnumerateArray())
        {
            string at = $"$.spelers[{i++}]";
            string id = Str(Req(p, "id", at), at + ".id");
            if (list.Any(r => r.Id == id)) throw new TacticLoadException($"{at}.id '{id}' is duplicated");
            string role = Str(Req(p, "rol", at), at + ".rol");
            list.Add(new RosterPlayer(id, role, p.TryGetProperty("opmerking", out var n) ? n.GetString() : null));
        }
        return list;
    }

    private static Dictionary<GamePhase, PhaseLayout> ResolvePhases(JsonElement fases, List<string> ids)
    {
        var raw = new Dictionary<string, JsonElement>();
        foreach (var n in PhaseNames)
        {
            if (!fases.TryGetProperty(n, out var ph)) throw new TacticLoadException($"phase '{n}' is missing in $.fases");
            raw[n] = ph;
        }
        var result = new Dictionary<GamePhase, PhaseLayout>();
        var parsed = new Dictionary<string, PhaseLayout>(); // lookup only
        PhaseLayout Full(string key) =>
            parsed.TryGetValue(key, out var l) ? l : parsed[key] = ParseFullPhase(raw[key], $"$.fases.{key}", ids);
        foreach (var n in PhaseNames) // fixed order, not dictionary order
        {
            var ph = raw[n];
            if (ph.TryGetProperty("gelijk_aan", out var g))
            {
                string target = Str(g, $"$.fases.{n}.gelijk_aan");
                if (!raw.TryGetValue(target, out var tgt))
                    throw new TacticLoadException($"$.fases.{n}.gelijk_aan refers to unknown phase '{target}'");
                if (tgt.TryGetProperty("gelijk_aan", out _))
                    throw new TacticLoadException($"$.fases.{n}.gelijk_aan must refer to a fully specified phase ('{target}' is also gelijk_aan)");
                result[ParsePhaseName(n, "$.fases")] = Full(target);
            }
            else
                result[ParsePhaseName(n, "$.fases")] = Full(n);
        }
        return result;
    }

    private static PhaseLayout ParseFullPhase(JsonElement e, string at, List<string> ids)
    {
        if (e.ValueKind != JsonValueKind.Object) throw new TacticLoadException($"{at} must be an object");
        BlockHeight? block = e.TryGetProperty("blokhoogte", out var b) ? ParseBlock(Str(b, at + ".blokhoogte"), at + ".blokhoogte") : null;
        var arr = Req(e, "spelers", at);
        if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() != 11)
            throw new TacticLoadException($"{at}.spelers must have 11 players");
        var players = new List<PhasePlayer>();
        int i = 0;
        foreach (var p in arr.EnumerateArray())
        {
            string pat = $"{at}.spelers[{i++}]";
            string id = Str(Req(p, "id", pat), pat + ".id");
            if (!ids.Contains(id)) throw new TacticLoadException($"{pat}.id '{id}' is not in the roster");
            if (players.Any(x => x.Id == id)) throw new TacticLoadException($"{pat}.id '{id}' is duplicated");
            var (x, y) = Pair(Req(p, "pos", pat), pat + ".pos");
            (double, double)? follow = null;
            if (p.TryGetProperty("volg_bal", out var vb)) follow = Pair(vb, pat + ".volg_bal");
            var instr = new List<string>();
            if (p.TryGetProperty("instructies", out var ins))
                foreach (var s in ins.EnumerateArray()) instr.Add(Str(s, pat + ".instructies"));
            string? mark = p.TryGetProperty("dek", out var d) ? Str(d, pat + ".dek") : null;
            if (instr.Contains("dek_man") != (mark is not null))
                throw new TacticLoadException($"{pat}: dek_man requires dek and dek requires dek_man");
            if (mark is not null && !ids.Contains(mark))
                throw new TacticLoadException($"{pat}.dek references unknown player '{mark}'");
            IReadOnlyList<(double X, double Y)>? path = null;
            if (p.TryGetProperty("pad", out var pathValue))
            {
                if (pathValue.ValueKind != JsonValueKind.Array || pathValue.GetArrayLength() is < 1 or > 8)
                    throw new TacticLoadException($"{pat}.pad must contain 1..8 points");
                path = pathValue.EnumerateArray().Select((point, index) => Pair(point, $"{pat}.pad[{index}]")).ToArray();
            }
            string speed = p.TryGetProperty("pad_snelheid", out var ps) ? Str(ps, pat + ".pad_snelheid") : "joggen";
            if (speed is not ("wandelen" or "joggen" or "sprint"))
                throw new TacticLoadException($"{pat}.pad_snelheid unknown speed '{speed}' (wandelen, joggen, sprint)");
            double wait = p.TryGetProperty("pad_wacht", out var pw) ? Number(pw, pat + ".pad_wacht") : 0;
            if (!double.IsFinite(wait) || wait < 0 || wait > 5)
                throw new TacticLoadException($"{pat}.pad_wacht must be within 0..5 seconds");
            players.Add(new PhasePlayer(id, x, y, follow, instr, mark, path, speed, wait));
        }
        return new PhaseLayout(block, players);
    }

    private static (double, double) Pair(JsonElement e, string at)
    {
        if (e.ValueKind != JsonValueKind.Array || e.GetArrayLength() != 2)
            throw new TacticLoadException($"{at} must be an array of two numbers");
        double a = Number(e[0], at + "[0]"), b = Number(e[1], at + "[1]");
        if (!double.IsFinite(a) || !double.IsFinite(b) || a < 0 || a > 1 || b < 0 || b > 1)
            throw new TacticLoadException($"{at} is outside 0..1: [{a}, {b}]");
        return (a, b);
    }

    private static BlockHeight ParseBlock(string s, string at) => s switch
    {
        "hoog" => BlockHeight.Hoog,
        "midden" => BlockHeight.Midden,
        "laag" => BlockHeight.Laag,
        _ => throw new TacticLoadException($"{at} invalid block height '{s}'")
    };

    private static GamePhase ParsePhaseName(string s, string at) => s switch
    {
        "opbouw" => GamePhase.Opbouw,
        "aanval" => GamePhase.Aanval,
        "balverlies" => GamePhase.Balverlies,
        "verdedigen" => GamePhase.Verdedigen,
        "balwinst" => GamePhase.Balwinst,
        "stilstaand" => GamePhase.Stilstaand,
        _ => throw new TacticLoadException($"{at} unknown phase '{s}'")
    };

    private static JsonElement Req(JsonElement e, string key, string at)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(key, out var v))
            throw new TacticLoadException($"{at}.{key} is missing");
        return v;
    }

    private static string Str(JsonElement e, string at) =>
        e.ValueKind == JsonValueKind.String ? e.GetString()! : throw new TacticLoadException($"{at} must be a string");

    private static double Number(JsonElement e, string at) =>
        e.ValueKind == JsonValueKind.Number ? e.GetDouble() : throw new TacticLoadException($"{at} must be a number");
}
