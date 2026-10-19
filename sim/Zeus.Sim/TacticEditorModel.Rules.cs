using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Zeus.Sim;

public sealed record TriggerInfo(string Name, string Phase, string Condition, string? Zone, IReadOnlyList<(string Player, string Instruction)> Actions);

public sealed record BlockSwitchInfo(string Name, string? Stand, int? FromMinute, double? FatigueAbove, string To);

/// <summary>Roles, team form, marking, rules (triggers), block switches and phase tools of the tactic editor. Every edit is
/// validated with the real loader and rolled back when it would produce an invalid tactic.</summary>
public sealed partial class TacticEditorModel
{
    public static IReadOnlyList<string> Roles { get; } = Array.AsReadOnly(new[]
    {
        "keeper", "centrale_verdediger", "back", "controlerende_middenvelder", "box_to_box",
        "aanvallende_middenvelder", "buitenspeler", "hangende_spits", "spits", "targetman"
    });

    public static IReadOnlyList<string> Conditions { get; } = Array.AsReadOnly(new[]
    {
        "bal_naar_keeper", "tegenstander_ontvangt_op_flank", "slechte_aanname",
        "bal_terug_naar_middenvelder", "balverlies_in_eigen_helft", "balwinst_in_hoge_zone"
    });

    public static IReadOnlyList<string> BasePhases { get; } = Array.AsReadOnly(new[]
        { "opbouw", "aanval", "balverlies", "verdedigen", "balwinst", "stilstaand" });

    public static IReadOnlyList<string> BlockHeights { get; } = Array.AsReadOnly(new[] { "hoog", "midden", "laag" });
    public static IReadOnlyList<string> PressingForms { get; } = Array.AsReadOnly(new[] { "zone", "man", "gemengd" });
    public static IReadOnlyList<string> Standings { get; } = Array.AsReadOnly(new[] { "voor", "gelijk", "achter" });

    /// <summary>Zone codes for triggers: third (V, M, A) and lane (BL, HL, C, HR, BR), e.g. "A-HL".</summary>
    public static IReadOnlyList<string> Zones { get; } = Array.AsReadOnly(
        new[] { "V", "M", "A" }.SelectMany(t => new[] { "BL", "HL", "C", "HR", "BR" }.Select(l => $"{t}-{l}")).ToArray());

    static readonly Regex OpponentId = new("^[A-Z0-9]{1,4}$");

    /// <summary>Base phases plus block variants ("verdedigen@hoog") that exist in this tactic.</summary>
    public IReadOnlyList<string> AllPhases =>
        Phases.Concat((_root["verdedigen_varianten"] as JsonObject)?.Select(kv => "verdedigen@" + kv.Key) ?? Enumerable.Empty<string>()).ToList();

    /// <summary>The phase this one is an alias of ("gelijk_aan"), or null when it has its own positions.</summary>
    public string? AliasOf(string phase) => _root["fases"]![phase]?["gelijk_aan"]?.GetValue<string>();

    // ---- transaction -------------------------------------------------------------------------------------------

    private void Mutate(Action edit)
    {
        var backup = (JsonObject)_root.DeepClone();
        try { edit(); Validate(); }
        catch
        {
            foreach (var key in _root.Select(kv => kv.Key).ToList()) _root.Remove(key);
            foreach (var kv in backup.ToList()) { backup.Remove(kv.Key); _root[kv.Key] = kv.Value; }
            throw;
        }
    }

    // ---- roles, team form, follow-the-ball -----------------------------------------------------------------------

    private JsonObject PlayerEntry(string playerId)
    {
        foreach (var p in _root["spelers"]!.AsArray())
            if (p!["id"]!.GetValue<string>() == playerId) return p.AsObject();
        throw new ArgumentException($"Player '{playerId}' not in tactic.");
    }

    public string GetRole(string playerId) => PlayerEntry(playerId)["rol"]!.GetValue<string>();

    public void SetRole(string playerId, string role)
    {
        if (!Roles.Contains(role)) throw new ArgumentException($"Unknown role '{role}'.", nameof(role));
        Mutate(() => PlayerEntry(playerId)["rol"] = role);
    }

    public string GetPressingForm() => _root["team"]!["pressing_vorm"]?.GetValue<string>() ?? "zone";

    public void SetPressingForm(string form)
    {
        if (!PressingForms.Contains(form)) throw new ArgumentException($"Unknown pressing form '{form}'.", nameof(form));
        Mutate(() => _root["team"]!["pressing_vorm"] = form);
    }

    /// <summary>How much the position follows the ball in x and y (0 = fixed, 1 = fully); null when unset.</summary>
    public (double X, double Y)? GetFollow(string phase, string playerId)
    {
        var arr = FindPlayer(PhaseObject(phase), playerId)["volg_bal"] as JsonArray;
        return arr is null ? null : (arr[0]!.GetValue<double>(), arr[1]!.GetValue<double>());
    }

    public void SetFollow(string phase, string playerId, double x, double y) => Mutate(() =>
        EditablePlayer(phase, playerId)["volg_bal"] = new JsonArray(Math.Round(Math.Clamp(x, 0, 1), 2), Math.Round(Math.Clamp(y, 0, 1), 2)));

    // ---- marking (dek_man) ---------------------------------------------------------------------------------------

    public string? GetMarking(string phase, string playerId) =>
        FindPlayer(PhaseObject(phase), playerId)["dek"]?.GetValue<string>();

    /// <summary>Marks an opponent position id (same ids as the own team: GK, LB, RS ...); null removes the marking.</summary>
    public void SetMarking(string phase, string playerId, string? opponentId)
    {
        if (opponentId is not null && !OpponentId.IsMatch(opponentId))
            throw new ArgumentException($"Opponent id '{opponentId}' must be 1-4 capital letters or digits.", nameof(opponentId));
        Mutate(() =>
        {
            var player = EditablePlayer(phase, playerId);
            var instr = player["instructies"] as JsonArray ?? new JsonArray();
            foreach (var i in instr.Where(i => i!.GetValue<string>() == "dek_man").ToList()) instr.Remove(i);
            if (opponentId is null) player.Remove("dek");
            else { player["dek"] = opponentId; instr.Add(JsonValue.Create("dek_man")); }
            if (instr.Count == 0) player.Remove("instructies");
            else player["instructies"] = instr;
        });
    }

    // ---- triggers ------------------------------------------------------------------------------------------------

    public IReadOnlyList<TriggerInfo> Triggers =>
        (_root["triggers"] as JsonArray)?.Select(t => new TriggerInfo(
            t!["naam"]?.GetValue<string>() ?? "",
            t["wanneer"]!["fase"]!.GetValue<string>(),
            t["wanneer"]!["voorwaarde"]!.GetValue<string>(),
            t["wanneer"]!["zone"]?.GetValue<string>(),
            t["dan"]!.AsArray().Select(a => (a!["speler"]!.GetValue<string>(), a["instructie"]!.GetValue<string>())).ToList())).ToList()
        ?? new List<TriggerInfo>();

    public void AddTrigger(string name, string phase, string condition, string? zone, IEnumerable<(string Player, string Instruction)> actions)
    {
        var acts = actions.ToList();
        if (acts.Count == 0) throw new ArgumentException("A trigger needs at least one action.", nameof(actions));
        if (!BasePhases.Contains(phase)) throw new ArgumentException($"Unknown phase '{phase}'.", nameof(phase));
        if (!Conditions.Contains(condition)) throw new ArgumentException($"Unknown condition '{condition}'.", nameof(condition));
        if (zone is not null && !Zones.Contains(zone)) throw new ArgumentException($"Unknown zone '{zone}'.", nameof(zone));
        foreach (var a in acts)
            if (!AllowedInstructions.Contains(a.Instruction)) throw new ArgumentException($"Unknown instruction '{a.Instruction}'.", nameof(actions));
        Mutate(() =>
        {
            var when = new JsonObject { ["fase"] = phase, ["voorwaarde"] = condition };
            if (zone is not null) when["zone"] = zone;
            var trigger = new JsonObject
            {
                ["naam"] = name, ["wanneer"] = when,
                ["dan"] = new JsonArray(acts.Select(a => (JsonNode)new JsonObject { ["speler"] = a.Player, ["instructie"] = a.Instruction }).ToArray()),
            };
            if (_root["triggers"] is not JsonArray list) _root["triggers"] = list = new JsonArray();
            list.Add(trigger);
        });
    }

    public void RemoveTrigger(int index) => Mutate(() =>
    {
        var list = _root["triggers"] as JsonArray ?? throw new ArgumentOutOfRangeException(nameof(index));
        if (index < 0 || index >= list.Count) throw new ArgumentOutOfRangeException(nameof(index));
        list.RemoveAt(index);
        if (list.Count == 0) _root.Remove("triggers");
    });

    // ---- block switches ------------------------------------------------------------------------------------------

    public IReadOnlyList<BlockSwitchInfo> BlockSwitches =>
        (_root["blokwissels"] as JsonArray)?.Select(b => new BlockSwitchInfo(
            b!["naam"]?.GetValue<string>() ?? "",
            b["wanneer"]!["stand"]?.GetValue<string>(),
            b["wanneer"]!["vanaf_minuut"]?.GetValue<int>(),
            b["wanneer"]!["vermoeidheid_boven"]?.GetValue<double>(),
            b["naar"]!.GetValue<string>())).ToList()
        ?? new List<BlockSwitchInfo>();

    public string DefendingBlock => PhaseObject("verdedigen")["blokhoogte"]?.GetValue<string>() ?? "midden";

    /// <summary>Creates the block variant for <paramref name="block"/> (copy of verdedigen, line heights from the template).</summary>
    public void EnsureBlockVariant(string block)
    {
        if (!BlockHeights.Contains(block)) throw new ArgumentException($"Unknown block '{block}'.", nameof(block));
        if (_root["verdedigen_varianten"]?[block] is not null) return;
        Mutate(() =>
        {
            var variants = _root["verdedigen_varianten"] as JsonObject;
            if (variants is null) _root["verdedigen_varianten"] = variants = new JsonObject();
            var copy = (JsonObject)PhaseObject("verdedigen").DeepClone();
            copy["blokhoogte"] = block;
            variants[block] = copy;
            ApplyLineTemplateCore(copy, block, RoleMap());
        });
    }

    public void AddBlockSwitch(string name, string? stand, int? fromMinute, double? fatigueAbove, string to)
    {
        if (stand is null && fromMinute is null && fatigueAbove is null) throw new ArgumentException("A block switch needs at least one condition.");
        if (stand is not null && !Standings.Contains(stand)) throw new ArgumentException($"Unknown standing '{stand}'.", nameof(stand));
        if (!BlockHeights.Contains(to)) throw new ArgumentException($"Unknown block '{to}'.", nameof(to));
        EnsureBlockVariant(DefendingBlock);
        EnsureBlockVariant(to);
        Mutate(() =>
        {
            var when = new JsonObject();
            if (stand is not null) when["stand"] = stand;
            if (fromMinute is not null) when["vanaf_minuut"] = Math.Clamp(fromMinute.Value, 0, 120);
            if (fatigueAbove is not null) when["vermoeidheid_boven"] = Math.Round(Math.Clamp(fatigueAbove.Value, 0, 1), 2);
            if (_root["blokwissels"] is not JsonArray list) _root["blokwissels"] = list = new JsonArray();
            list.Add(new JsonObject { ["naam"] = name, ["wanneer"] = when, ["naar"] = to });
        });
    }

    public void RemoveBlockSwitch(int index) => Mutate(() =>
    {
        var list = _root["blokwissels"] as JsonArray ?? throw new ArgumentOutOfRangeException(nameof(index));
        if (index < 0 || index >= list.Count) throw new ArgumentOutOfRangeException(nameof(index));
        list.RemoveAt(index);
        if (list.Count == 0) _root.Remove("blokwissels");
    });

    // ---- phase tools ---------------------------------------------------------------------------------------------

    private static void ApplyLineTemplateCore(JsonObject phase, string line, IReadOnlyDictionary<string, string> roles)
    {
        var lines = TacticTemplates.Lines[line];
        foreach (var p in phase["spelers"]!.AsArray())
        {
            var id = p!["id"]!.GetValue<string>();
            var role = roles.TryGetValue(id, out var r) ? r : "spits";
            var pos = p["pos"]!.AsArray();
            p["pos"] = new JsonArray(lines[(int)TacticTemplates.GroupOf(role)], pos[1]!.GetValue<double>());
        }
    }

    private Dictionary<string, string> RoleMap() =>
        _root["spelers"]!.AsArray().ToDictionary(p => p!["id"]!.GetValue<string>(), p => p!["rol"]!.GetValue<string>());

    /// <summary>Sets every player's line height (x) in the phase from a template line (laag, midden, hoog, opbouw, aanval); y stays.</summary>
    public void ApplyLineTemplate(string phase, string line)
    {
        if (!TacticTemplates.Lines.ContainsKey(line)) throw new ArgumentException($"Unknown line '{line}'.", nameof(line));
        Mutate(() =>
        {
            EditablePlayer(phase, PlayerIds[0]);             // detaches aliases first
            ApplyLineTemplateCore(PhaseObject(phase), line, RoleMap());
            if (phase == "verdedigen" && BlockHeights.Contains(line)) PhaseObject(phase)["blokhoogte"] = line;
        });
    }

    /// <summary>Replaces <paramref name="to"/> with an independent copy of <paramref name="from"/>.</summary>
    public void CopyPhase(string from, string to)
    {
        if (from == to) return;
        Mutate(() =>
        {
            var copy = (JsonObject)PhaseObject(from).DeepClone();
            if (to.StartsWith("verdedigen@", StringComparison.Ordinal))
            {
                copy["blokhoogte"] = to["verdedigen@".Length..];
                _root["verdedigen_varianten"]![to["verdedigen@".Length..]] = copy;
            }
            else
            {
                if (!BasePhases.Contains(to)) throw new ArgumentException($"Unknown phase '{to}'.", nameof(to));
                if (to != "verdedigen") copy.Remove("blokhoogte");
                else copy["blokhoogte"] ??= "midden";
                _root["fases"]![to] = copy;
            }
        });
    }

    /// <summary>Makes <paramref name="phase"/> an alias of another base phase ("gelijk_aan").</summary>
    public void SetAlias(string phase, string of)
    {
        if (!BasePhases.Contains(phase) || !BasePhases.Contains(of) || phase == of)
            throw new ArgumentException("Alias needs two different base phases.");
        Mutate(() =>
        {
            // The loader allows no alias chains: phases that point at this one keep their current positions as their own copy.
            foreach (var dependent in Phases.Where(p => p != phase && AliasOf(p) == phase).ToList()) Detach(dependent);
            // The target must be fully specified: if it is itself an alias, make it independent first.
            Detach(of);
            _root["fases"]![phase] = new JsonObject { ["gelijk_aan"] = of };
        });
    }

    /// <summary>Mirrors left and right (y -> 1 - y) for every player in the phase.</summary>
    public void Mirror(string phase) => Mutate(() =>
    {
        foreach (var id in PlayerIds)
        {
            var (x, y) = GetPosition(phase, id);
            EditablePlayer(phase, id)["pos"] = new JsonArray(x, Math.Round(1 - y, 3));
        }
    });

    // ---- set pieces ("standaardsituaties") -----------------------------------------------------------------------

    public static IReadOnlyList<string> SetPieceKeys { get; } = Array.AsReadOnly(
        new[] { "corner_voor", "corner_tegen", "palen", "vrije_trap_voor", "vrije_trap_tegen" });

    /// <summary>The chosen value of one set-piece field; "" means unset (historical behaviour; palen then counts as 2).</summary>
    public string GetSetPiece(string key)
    {
        var v = _root["standaardsituaties"]?[key];
        if (v is null) return "";
        return v is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : v.ToString();
    }

    /// <summary>Sets one set-piece field; "" removes it, and the whole block disappears when it is empty.</summary>
    public void SetSetPiece(string key, string value)
    {
        if (!SetPieceKeys.Contains(key)) throw new ArgumentException($"Unknown set-piece field '{key}'.", nameof(key));
        Mutate(() =>
        {
            if (_root["standaardsituaties"] is not JsonObject block) { block = new JsonObject(); _root["standaardsituaties"] = block; }
            if (value.Length == 0) block.Remove(key);
            else block[key] = key == "palen" ? JsonValue.Create(int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)) : JsonValue.Create(value);
            if (block.Count == 0) _root.Remove("standaardsituaties");
        });
    }
}
