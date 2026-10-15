using System.Text.Json;
using System.Text.Json.Nodes;

namespace Zeus.Sim;

/// <summary>
/// UI-independent editing layer over a tactic file. Works on the raw JSON so every field the editor
/// does not know about (instructions, triggers, blocks) survives a save. Positions are relative (0..1).
/// </summary>
public sealed class TacticEditorModel
{
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
        // Phases that alias this one keep their current positions: detach them before the source changes.
        foreach (var other in Phases.Where(p => p != phase && _root["fases"]![p]?["gelijk_aan"]?.GetValue<string>() == phase).ToList())
            Detach(other);
        Detach(phase);
        var player = FindPlayer(PhaseObject(phase), playerId);
        player["pos"] = new JsonArray(Math.Round(Math.Clamp(x, 0, 1), 3), Math.Round(Math.Clamp(y, 0, 1), 3));
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
