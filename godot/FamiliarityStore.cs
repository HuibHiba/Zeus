// Familiarity per level + club in user://zeus_vertrouwdheid.json (v0, VOORLOPIG). The file maps a key to the JSON of one FamiliarityState.
// A damaged file never breaks a screen: it is set aside as .bad; an unreadable (locked) file is never overwritten.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using Zeus.Sim;

public sealed class FamiliarityStore
{
    const string DefaultFile = "user://zeus_vertrouwdheid.json";

    readonly string _path;
    readonly Dictionary<string, string> _entries = new(StringComparer.Ordinal);
    bool _writable = true;
    public string Note { get; private set; } = "";

    FamiliarityStore(string path) { _path = path; }

    public static string FilePath()
    {
        var env = OS.GetEnvironment("ZEUS_VERTROUWDHEID_FILE");      // visual checks must not touch the real file
        return env.Length > 0 ? env : DefaultFile;
    }

    public static string Key(string level, string club) => level + "|" + club;

    public static FamiliarityStore Load(string? path = null)
    {
        var store = new FamiliarityStore(path ?? FilePath());
        var status = SafeFile.Read(store._path, out var text);
        if (status == SafeFile.ReadStatus.Missing) return store;
        if (status == SafeFile.ReadStatus.Unreadable)
        {
            store._writable = false; store.Note = "Vertrouwdheidsbestand is niet leesbaar; wijzigingen worden niet bewaard.";
            return store;
        }
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(text);
            if (map is null) throw new JsonException("leeg");
            foreach (var (k, v) in map) store._entries[k] = v;
        }
        catch (Exception)
        {
            store.Note = SafeFile.SetAside(store._path)
                ? "Vertrouwdheidsbestand was beschadigd (opzij gezet als .bad); opnieuw begonnen."
                : "Vertrouwdheidsbestand is beschadigd en kon niet opzij gezet worden; wijzigingen worden niet bewaard.";
            store._writable = store.Note.Contains("opzij gezet als");
        }
        return store;
    }

    /// <summary>The saved state for the key (a bad entry is ignored), completed with every id in <paramref name="ids"/>.</summary>
    public FamiliarityState Get(string key, string teamId, IEnumerable<string> ids)
    {
        FamiliarityState? state = null;
        if (_entries.TryGetValue(key, out var json))
        {
            try { state = FamiliarityState.FromJson(json); }
            catch (Exception) { Note = "Een bewaarde vertrouwdheid was ongeldig en is genegeerd."; }
        }
        state ??= FamiliarityState.ForTeam(teamId, ids);
        foreach (var id in ids)
            if (!state.Players.ContainsKey(id))
                state.Players[id] = new FamiliarityPlayer { Concepts = TacticalConcepts.All.ToDictionary(c => c, _ => 0.0, StringComparer.Ordinal) };
        foreach (var p in state.Players.Values)                       // a saved player may miss a concept; keep Validate happy
            foreach (var c in TacticalConcepts.All) p.Concepts.TryAdd(c, 0.0);
        return state;
    }

    /// <summary>Stores the state and writes the file atomically. Returns an error text, or "" when it worked.</summary>
    public string Put(string key, FamiliarityState state)
    {
        _entries[key] = state.ToJson();
        if (!_writable) return "Niet bewaard: " + (Note.Length > 0 ? Note : "bestand niet schrijfbaar.");
        return SafeFile.Write(_path, JsonSerializer.Serialize(_entries), out var error) ? "" : "Opslaan mislukt: " + error;
    }
}
