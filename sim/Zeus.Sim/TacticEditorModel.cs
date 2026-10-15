using System.Text.Json;
using System.Text.Json.Nodes;

namespace Zeus.Sim;

/// <summary>
/// UI-independent editing layer over a tactic file. Works on the raw JSON so every field the editor
/// does not know about (instructions, triggers, blocks) survives a save. Positions are relative (0..1).
/// </summary>
public sealed partial class TacticEditorModel
{
    public static IReadOnlyList<string> AllowedInstructions { get; } = Array.AsReadOnly(new[]
    {
        "dribbel_in", "blijf_achter", "loop_diep", "laat_zakken", "blijf_breed",
        "kom_op", "sluit_naar_binnen", "start_pressing", "dek_man", "loop_zestien_in",
        "dek_ruimte_achter_lijn", "vrije_man_zoeken"
    });

    private readonly JsonObject _root;

    private TacticEditorModel(JsonObject root) => _root = root;

    public static TacticEditorModel Parse(string json)
    {
        var node = JsonNode.Parse(json) as JsonObject ?? throw new TacticLoadException("Tactic file must be a JSON object.");
        var model = new TacticEditorModel(node);
        model.Validate();
        return model;
    }

    public static TacticEditorModel LoadFile(string path) => Parse(File.ReadAllText(path));

    public string Name
    {
        get => _root["naam"]?.GetValue<string>() ?? "";
        set => _root["naam"] = value;
    }

    public IReadOnlyList<string> PlayerIds =>
        _root["spelers"]!.AsArray().Select(p => p!["id"]!.GetValue<string>()).ToList();

    public IReadOnlyList<string> Phases =>
        _root["fases"]!.AsObject().Select(kv => kv.Key).ToList();

    private JsonObject PhaseObject(string phase)
    {
        if (phase.StartsWith("verdedigen@", StringComparison.Ordinal))      // a block variant: verdedigen@hoog|midden|laag
            return _root["verdedigen_varianten"]?[phase["verdedigen@".Length..]] as JsonObject
                ?? throw new ArgumentException($"Unknown phase '{phase}'.");
        var fases = _root["fases"]!.AsObject();
        var node = fases[phase] as JsonObject ?? throw new ArgumentException($"Unknown phase '{phase}'.");
        if (node["gelijk_aan"] is JsonNode alias)
            return PhaseObject(alias.GetValue<string>());
        return node;
    }

    /// <summary>True when the phase is an alias ("gelijk_aan") of another phase.</summary>
    public bool IsAlias(string phase) => _root["fases"]![phase]?["gelijk_aan"] != null;

    /// <summary>Turns an alias phase into an independent copy, so it can be edited on its own.</summary>
    public void Detach(string phase)
    {
        if (!IsAlias(phase)) return;
        var copy = PhaseObject(phase).DeepClone();
        _root["fases"]![phase] = copy;
    }

    public (double X, double Y) GetPosition(string phase, string playerId)
    {
        var pos = FindPlayer(PhaseObject(phase), playerId)["pos"]!.AsArray();
        return (pos[0]!.GetValue<double>(), pos[1]!.GetValue<double>());
    }

    public void SetPosition(string phase, string playerId, double x, double y)
    {
        var player = EditablePlayer(phase, playerId);
        player["pos"] = new JsonArray(Math.Round(Math.Clamp(x, 0, 1), 3), Math.Round(Math.Clamp(y, 0, 1), 3));
    }

    private JsonObject EditablePlayer(string phase, string playerId)
    {
        FindPlayer(PhaseObject(phase), playerId);
        // Phases that alias this one keep their current positions: detach them before the source changes.
        foreach (var other in Phases.Where(p => p != phase && _root["fases"]![p]?["gelijk_aan"]?.GetValue<string>() == phase).ToList())
            Detach(other);
        Detach(phase);
        return FindPlayer(PhaseObject(phase), playerId);
    }

    public IReadOnlyList<string> GetInstructions(string phase, string playerId) =>
        FindPlayer(PhaseObject(phase), playerId)["instructies"]?.AsArray()
            .Select(i => i!.GetValue<string>()).ToList() ?? new List<string>();

    public void ToggleInstruction(string phase, string playerId, string instruction)
    {
        if (instruction == "dek_man")
        {
            if (!GetInstructions(phase, playerId).Contains(instruction))
                throw new ArgumentException("dek_man requires a target; use SetMarking.", nameof(instruction));
            SetMarking(phase, playerId, null);
            return;
        }
        if (!AllowedInstructions.Contains(instruction))
            throw new ArgumentException($"Unknown instruction '{instruction}'.", nameof(instruction));
        var player = EditablePlayer(phase, playerId);
        var instructions = player["instructies"] as JsonArray ?? new JsonArray();
        var index = instructions.Select(i => i!.GetValue<string>()).ToList().IndexOf(instruction);
        if (index >= 0) instructions.RemoveAt(index);
        else instructions.Add(JsonValue.Create(instruction));
        if (instructions.Count == 0) player.Remove("instructies");
        else if (instructions.Parent is null) player["instructies"] = instructions;
    }

    public double? GetTeamSetting(string setting)
    {
        CheckTeamSetting(setting);
        return _root["team"]![setting]?.GetValue<double>();
    }

    public void SetTeamSetting(string setting, double value)
    {
        CheckTeamSetting(setting);
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        _root["team"]![setting] = Math.Round(Math.Clamp(value, 0, 1), 2);
    }

    private static void CheckTeamSetting(string setting)
    {
        if (setting is not ("tempo" or "pressing_intensiteit" or "lijnhoogte" or "compactheid"))
            throw new ArgumentException($"Unknown team setting '{setting}'.", nameof(setting));
    }

    private static JsonObject FindPlayer(JsonObject phase, string playerId)
    {
        foreach (var p in phase["spelers"]!.AsArray())
            if (p!["id"]!.GetValue<string>() == playerId) return p.AsObject();
        throw new ArgumentException($"Player '{playerId}' not in phase.");
    }

    public string ToJson() => _root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    /// <summary>Runs the real loader over the current state; throws <see cref="TacticLoadException"/> when invalid.</summary>
    public void Validate() => TacticLoader.Parse(ToJson());

    public void Save(string path)
    {
        Validate();
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, ToJson());
        File.Move(tmp, path, overwrite: true);
    }
}
