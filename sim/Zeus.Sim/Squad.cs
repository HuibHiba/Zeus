using System.Text.Json;

namespace Zeus.Sim;

/// <summary>Provisional v0 attributes, normalized from the external integer scale 1..20.</summary>
public sealed record PlayerAttributes(double Passing, double FirstTouch, double Finishing, double Dribbling,
    double Defending, double Heading, double Speed, double Acceleration, double Strength, double Endurance,
    double Vision, double Anticipation, double Positioning, double Decisions, double Discipline)
{
    public static readonly PlayerAttributes Neutral = new(.75, .75, .75, .75, .6, .5, .5, .5, .5, .5, .5, .5, .5, .5, .75);
}

public sealed record KeeperAttributes(double Reflexes, double Positioning, double RushingOut);
/// <summary>Age is optional (0 = unknown, treated as a player in his prime by <see cref="Development"/>).</summary>
public sealed record SquadPlayer(string Id, string Name, string Role, string PreferredPosition,
    PlayerAttributes Attributes, KeeperAttributes? Keeper = null, int Age = 0, int SignedSeason = 1);

/// <summary>Exactly eleven slot identities and seven reserves. No substitutions in v0.</summary>
public sealed record Squad(string Name, IReadOnlyList<SquadPlayer> Starters, IReadOnlyList<SquadPlayer> Bench)
{
    public static Squad NeutralFor(Tactic tactic) => new("Neutraal (voorlopig)",
        tactic.Roster.Select(p => new SquadPlayer(p.Id, "Fictieve speler " + p.Id, p.Role, p.Id, PlayerAttributes.Neutral,
            p.Id == "GK" ? new KeeperAttributes(.5, .5, .5) : null)).ToArray(),
        Enumerable.Range(1, 7).Select(i => new SquadPlayer("B" + i, "Fictieve reserve " + i, i == 1 ? "keeper" : "spits",
            i == 1 ? "GK" : "ST", PlayerAttributes.Neutral, i == 1 ? new KeeperAttributes(.5, .5, .5) : null)).ToArray());
}

public sealed class SquadLoadException(string message, Exception? inner = null) : Exception(message, inner);

public static class SquadLoader
{
    public static readonly string[] AttributeNames = ["passing", "firstTouch", "finishing", "dribbling", "defending", "heading",
        "speed", "acceleration", "strength", "endurance", "vision", "anticipation", "positioning", "decisions", "discipline"];
    public static readonly string[] Positions = ["GK", "RB", "RC", "LC", "LB", "RWB", "LWB", "CB", "DM", "RCM", "LCM", "CM", "AM", "RM", "LM", "RW", "LW", "RS", "LS", "ST"];
    public const int MinAge = 16, MaxAge = 40;
    public static readonly string[] Roles = ["keeper", "back", "centrale_verdediger", "wingback", "controlerende_middenvelder", "box_to_box", "aanvallende_middenvelder", "buitenspeler", "spits", "targetman"];

    public static Squad LoadFile(string path)
    {
        try { return Parse(File.ReadAllText(path)); }
        catch (IOException e) { throw new SquadLoadException(path + ": " + e.Message, e); }
    }

    public static Squad Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            Keys(root, "$", "schemaVersion", "status", "name", "starters", "bench");
            if (Text(root, "schemaVersion", "$") != "0.1") Fail("$.schemaVersion", "verwacht 0.1");
            if (Text(root, "status", "$") != "voorlopig") Fail("$.status", "verwacht voorlopig");
            var squad = new Squad(Text(root, "name", "$"), ReadPlayers(root, "starters", 11), ReadPlayers(root, "bench", 7));
            Validate(squad);
            return squad;
        }
        catch (JsonException e) { throw new SquadLoadException("$: ongeldige JSON: " + e.Message, e); }
    }

    public static void Validate(Squad squad, Tactic? tactic = null)
    {
        if (squad.Starters.Count != 11 || squad.Bench.Count != 7) Fail("$", "verwacht 11 basis en 7 bankspelers");
        var all = squad.Starters.Concat(squad.Bench).ToArray();
        if (all.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != 18) Fail("$.players.id", "dubbel speler-id");
        foreach (var p in all)
        {
            if (string.IsNullOrWhiteSpace(p.Name) || !Roles.Contains(p.Role) || !Positions.Contains(p.PreferredPosition))
                Fail("$.players." + p.Id, "ongeldige naam, rol of voorkeurspositie");
            if (p.Role == "keeper" && p.Keeper is null) Fail("$.players." + p.Id + ".keeper", "keeperblok ontbreekt");
            if (p.Age != 0 && p.Age is < MinAge or > MaxAge) Fail("$.players." + p.Id + ".age", $"verwacht {MinAge}..{MaxAge}");
            var a = p.Attributes;
            if (new[] { a.Passing, a.FirstTouch, a.Finishing, a.Dribbling, a.Defending, a.Heading, a.Speed, a.Acceleration,
                a.Strength, a.Endurance, a.Vision, a.Anticipation, a.Positioning, a.Decisions, a.Discipline }.Any(v => !double.IsFinite(v) || v < 0 || v > 1)
                || p.Keeper is { } k && new[] { k.Reflexes, k.Positioning, k.RushingOut }.Any(v => !double.IsFinite(v) || v < 0 || v > 1))
                Fail("$.players." + p.Id, "interne attributen moeten in 0..1 liggen");
        }
        if (squad.Starters.Count(p => p.Id == "GK" && p.Role == "keeper") != 1) Fail("$.starters", "GK met rol keeper ontbreekt");
        // With a tactic the starter ids are checked against its roster below (any slot id the schema allows); without one, against the known list.
        if (tactic is null && squad.Starters.Any(p => !Positions.Contains(p.Id))) Fail("$.starters.id", "onbekend positie-id");
        if (squad.Bench.Any(p => !System.Text.RegularExpressions.Regex.IsMatch(p.Id, "^B[1-7]$"))) Fail("$.bench.id", "verwacht B1..B7");
        if (tactic is not null && !squad.Starters.Select(p => p.Id).ToHashSet().SetEquals(tactic.Roster.Select(p => p.Id)))
            Fail("$.starters.id", "basis-id's passen niet op tactiekslots: " + string.Join(", ", tactic.Roster.Select(p => p.Id)));
    }

    private static SquadPlayer[] ReadPlayers(JsonElement root, string key, int count)
    {
        var array = Get(root, key, "$");
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() != count) Fail("$." + key, "verwacht " + count + " spelers");
        return array.EnumerateArray().Select((p, i) =>
        {
            string path = $"$.{key}[{i}]";
            Keys(p, path, "id", "name", "role", "preferredPosition", "attributes", "keeper", "age");
            int age = 0;
            if (p.TryGetProperty("age", out var ageValue) && (ageValue.ValueKind != JsonValueKind.Number || !ageValue.TryGetInt32(out age) || age < MinAge || age > MaxAge))
                throw new SquadLoadException(path + ".age: verwacht geheel getal " + MinAge + ".." + MaxAge);
            var a = Get(p, "attributes", path);
            Keys(a, path + ".attributes", AttributeNames);
            var v = AttributeNames.Select(n => Rating(a, n, path + ".attributes")).ToArray();
            KeeperAttributes? keeper = null;
            if (p.TryGetProperty("keeper", out var k))
            {
                Keys(k, path + ".keeper", "reflexes", "positioning", "rushingOut");
                keeper = new(Rating(k, "reflexes", path + ".keeper"), Rating(k, "positioning", path + ".keeper"), Rating(k, "rushingOut", path + ".keeper"));
            }
            return new SquadPlayer(Text(p, "id", path), Text(p, "name", path), Text(p, "role", path), Text(p, "preferredPosition", path),
                new(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9], v[10], v[11], v[12], v[13], v[14]), keeper, age);
        }).ToArray();
    }
    private static JsonElement Get(JsonElement obj, string key, string path) => obj.TryGetProperty(key, out var value) ? value : throw new SquadLoadException(path + "." + key + ": ontbreekt");
    private static string Text(JsonElement obj, string key, string path)
    {
        var value = Get(obj, key, path);
        return value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new SquadLoadException(path + "." + key + ": verwacht niet-lege tekst");
    }
    private static double Rating(JsonElement obj, string key, string path)
    {
        var value = Get(obj, key, path);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int n) || n < 1 || n > 20)
            throw new SquadLoadException(path + "." + key + ": verwacht geheel getal 1..20");
        return (n - 1) / 19.0;
    }
    private static void Keys(JsonElement obj, string path, params string[] allowed)
    {
        if (obj.ValueKind != JsonValueKind.Object) Fail(path, "verwacht object");
        var seen = new HashSet<string>();
        foreach (var p in obj.EnumerateObject())
            if (!allowed.Contains(p.Name) || !seen.Add(p.Name)) Fail(path + "." + p.Name, "onbekend of dubbel veld");
    }
    private static void Fail(string path, string message) => throw new SquadLoadException(path + ": " + message);
}
