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
    static string DefaultFile => Profiles.Path("zeus_vertrouwdheid.json");

    readonly string _path;
    readonly Dictionary<string, string> _entries = new(StringComparer.Ordinal);
    bool _writable = true;
    public string Note { get; private set; } = "";

    FamiliarityStore(string path) { _path = path; }

    public static string FilePath()
    {
        var env = GameSession.DebugEnvironment("ZEUS_VERTROUWDHEID_FILE");      // visual checks must not touch the real file
        return env.Length > 0 ? env : DefaultFile;
    }

    public static string Key(string level, string club) => level + "|" + club;

    public static FamiliarityStore Load(string? path = null)
    {
        var store = new FamiliarityStore(path ?? FilePath());
        var status = SafeFile.Read(store._path, out var text);
        if (status == SafeFile.ReadStatus.Missing) return store;
        if (status is SafeFile.ReadStatus.Unreadable or SafeFile.ReadStatus.Newer)
        {
            store._writable = false; store.Note = Loc.T(status == SafeFile.ReadStatus.Newer ? "squad.store_newer" : "squad.store_unreadable");
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
            store._writable = SafeFile.SetAside(store._path);
            store.Note = store._writable
                ? Loc.T("squad.store_recovered")
                : Loc.T("squad.store_blocked");
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
            catch (Exception) { Note = Loc.T("squad.store_invalid"); }
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
        if (!_writable) return Loc.T("squad.not_saved") + (Note.Length > 0 ? Note : Loc.T("squad.not_writable"));
        return SafeFile.Write(_path, JsonSerializer.Serialize(_entries), out var error) ? "" : Loc.T("squad.save_failed") + error;
    }
}
