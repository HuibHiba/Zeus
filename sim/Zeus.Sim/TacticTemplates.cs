using System.Text.Json.Nodes;

namespace Zeus.Sim;

/// <summary>
/// Starting formations for a new tactic. Line heights per role group come from the measured templates in the vault
/// (SkillCorner A-League / DFL Bundesliga, indicative, semi-pro level); the editor is meant to move them.
/// </summary>
public static class TacticTemplates
{
    public enum Group { GK, CB, FB, DM, CM, AM, W, ST }

    public sealed record Slot(string Id, string Role, Group Group, double Y);

    /// <summary>x (0 = own goal line, 1 = opponent goal line) per group, in <see cref="Group"/> order.</summary>
    public static readonly IReadOnlyDictionary<string, double[]> Lines = new Dictionary<string, double[]>
    {
        ["laag"] = new[] { 0.03, 0.15, 0.17, 0.22, 0.25, 0.27, 0.26, 0.32 },
        ["midden"] = new[] { 0.08, 0.34, 0.38, 0.46, 0.50, 0.53, 0.51, 0.56 },
        ["hoog"] = new[] { 0.15, 0.55, 0.64, 0.62, 0.67, 0.72, 0.71, 0.77 },
        ["opbouw"] = new[] { 0.08, 0.18, 0.27, 0.28, 0.31, 0.34, 0.35, 0.39 },
        ["aanval"] = new[] { 0.12, 0.61, 0.72, 0.67, 0.73, 0.78, 0.79, 0.82 },
    };

    static Slot S(string id, string role, Group g, double y) => new(id, role, g, y);

    static readonly Slot GK = S("GK", "keeper", Group.GK, .5);

    public static readonly IReadOnlyDictionary<string, (string Label, Slot[] Slots)> Formations =
        new Dictionary<string, (string, Slot[])>
        {
            ["433"] = ("4-3-3", new[] { GK, S("RB", "back", Group.FB, .85), S("RC", "centrale_verdediger", Group.CB, .62), S("LC", "centrale_verdediger", Group.CB, .38),
                S("LB", "back", Group.FB, .15), S("RCM", "controlerende_middenvelder", Group.DM, .65), S("CM", "box_to_box", Group.CM, .5),
                S("LCM", "controlerende_middenvelder", Group.DM, .35), S("RW", "buitenspeler", Group.W, .88), S("ST", "spits", Group.ST, .5), S("LW", "buitenspeler", Group.W, .12) }),
            ["442"] = ("4-4-2", new[] { GK, S("RB", "back", Group.FB, .85), S("RC", "centrale_verdediger", Group.CB, .62), S("LC", "centrale_verdediger", Group.CB, .38),
                S("LB", "back", Group.FB, .15), S("RM", "buitenspeler", Group.W, .85), S("RCM", "controlerende_middenvelder", Group.DM, .62),
                S("LCM", "box_to_box", Group.CM, .38), S("LM", "buitenspeler", Group.W, .15), S("RS", "spits", Group.ST, .6), S("LS", "targetman", Group.ST, .4) }),
            ["4231"] = ("4-2-3-1", new[] { GK, S("RB", "back", Group.FB, .85), S("RC", "centrale_verdediger", Group.CB, .62), S("LC", "centrale_verdediger", Group.CB, .38),
                S("LB", "back", Group.FB, .15), S("RDM", "controlerende_middenvelder", Group.DM, .6), S("LDM", "controlerende_middenvelder", Group.DM, .4),
                S("AM", "aanvallende_middenvelder", Group.AM, .5), S("RW", "buitenspeler", Group.W, .88), S("ST", "spits", Group.ST, .5), S("LW", "buitenspeler", Group.W, .12) }),
            ["4141"] = ("4-1-4-1", new[] { GK, S("RB", "back", Group.FB, .85), S("RC", "centrale_verdediger", Group.CB, .62), S("LC", "centrale_verdediger", Group.CB, .38),
                S("LB", "back", Group.FB, .15), S("DM", "controlerende_middenvelder", Group.DM, .5), S("RM", "buitenspeler", Group.W, .86),
                S("RCM", "box_to_box", Group.CM, .62), S("LCM", "box_to_box", Group.CM, .38), S("LM", "buitenspeler", Group.W, .14), S("ST", "spits", Group.ST, .5) }),
            ["532"] = ("5-3-2", new[] { GK, S("RWB", "back", Group.FB, .93), S("RC", "centrale_verdediger", Group.CB, .72), S("CC", "centrale_verdediger", Group.CB, .5),
                S("LC", "centrale_verdediger", Group.CB, .28), S("LWB", "back", Group.FB, .07), S("RCM", "box_to_box", Group.CM, .66), S("LCM", "box_to_box", Group.CM, .34),
                S("CMD", "controlerende_middenvelder", Group.DM, .5), S("RS", "spits", Group.ST, .6), S("LS", "targetman", Group.ST, .4) }),
            ["343"] = ("3-4-3", new[] { GK, S("RC", "centrale_verdediger", Group.CB, .72), S("CC", "centrale_verdediger", Group.CB, .5), S("LC", "centrale_verdediger", Group.CB, .28),
                S("RWB", "back", Group.FB, .92), S("RCM", "controlerende_middenvelder", Group.DM, .62), S("LCM", "box_to_box", Group.CM, .38),
                S("LWB", "back", Group.FB, .08), S("RW", "buitenspeler", Group.W, .82), S("ST", "spits", Group.ST, .5), S("LW", "buitenspeler", Group.W, .18) }),
        };

    public static IReadOnlyList<(string Key, string Label)> List =>
        Formations.Select(f => (f.Key, f.Value.Label)).ToList();

    /// <summary>Role group used to pick a line height; also used to reset a phase to a block height.</summary>
    public static Group GroupOf(string role) => role switch
    {
        "keeper" => Group.GK,
        "centrale_verdediger" => Group.CB,
        "back" => Group.FB,
        "controlerende_middenvelder" => Group.DM,
        "box_to_box" => Group.CM,
        "aanvallende_middenvelder" => Group.AM,
        "buitenspeler" => Group.W,
        "hangende_spits" => Group.AM,
        _ => Group.ST,
    };

    static JsonArray Players(Slot[] slots, string line, bool follow)
    {
        var arr = new JsonArray();
        foreach (var s in slots)
        {
            var p = new JsonObject
            {
                ["id"] = s.Id,
                ["pos"] = new JsonArray(Lines[line][(int)s.Group], s.Y),
            };
            if (follow) p["volg_bal"] = new JsonArray(0.3, 0.4);
            arr.Add(p);
        }
        return arr;
    }

    /// <summary>Creates a valid new tactic from a formation key (see <see cref="Formations"/>).</summary>
    public static TacticEditorModel Create(string formationKey, string name)
    {
        if (!Formations.TryGetValue(formationKey, out var f))
            throw new ArgumentException($"Unknown formation '{formationKey}'.", nameof(formationKey));
        var root = new JsonObject
        {
            ["schemaVersie"] = "0.1",
            ["naam"] = name,
            ["opmerking"] = $"Gemaakt in de editor vanuit sjabloon {f.Label}.",
            ["team"] = new JsonObject
            {
                ["tempo"] = 0.6, ["pressing_intensiteit"] = 0.5, ["pressing_vorm"] = "zone", ["lijnhoogte"] = 0.5, ["compactheid"] = 0.7,
            },
            ["spelers"] = new JsonArray(f.Slots.Select(s => (JsonNode)new JsonObject { ["id"] = s.Id, ["rol"] = s.Role }).ToArray()),
            ["fases"] = new JsonObject
            {
                ["verdedigen"] = new JsonObject { ["blokhoogte"] = "midden", ["spelers"] = Players(f.Slots, "midden", follow: true) },
                ["opbouw"] = new JsonObject { ["spelers"] = Players(f.Slots, "opbouw", follow: false) },
                ["aanval"] = new JsonObject { ["spelers"] = Players(f.Slots, "aanval", follow: false) },
                ["balverlies"] = new JsonObject { ["gelijk_aan"] = "verdedigen" },
                ["balwinst"] = new JsonObject { ["gelijk_aan"] = "opbouw" },
                ["stilstaand"] = new JsonObject { ["gelijk_aan"] = "verdedigen" },
            },
        };
        return TacticEditorModel.Parse(root.ToJsonString());
    }
}
