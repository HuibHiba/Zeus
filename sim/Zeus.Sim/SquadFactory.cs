namespace Zeus.Sim;

/// <summary>
/// Builds a squad of a given level for ANY tactic. The shipped squads (squads/amateur|halfprof|prof.json) carry the slot ids of
/// one formation; here the template players are assigned to the slots of the chosen tactic by role, so every formation can be
/// played at every level. Names and attributes come from the template; slot ids, and the bench B1..B7, are kept.
/// </summary>
public static class SquadFactory
{
    public static readonly IReadOnlyList<string> Levels = new[] { "neutraal", "amateur", "halfprof", "prof" };

    /// <summary>Loads squads/&lt;level&gt;.json from <paramref name="squadsDirectory"/> and fits it to the tactic; null for "neutraal" (no squad = old behaviour).</summary>
    public static Squad? ForLevel(string level, Tactic tactic, string squadsDirectory)
    {
        if (string.Equals(level, "neutraal", StringComparison.OrdinalIgnoreCase)) return null;
        if (!Levels.Contains(level)) throw new ArgumentException($"Onbekend niveau '{level}'.", nameof(level));
        return Fit(SquadLoader.LoadFile(Path.Combine(squadsDirectory, level + ".json")), tactic);
    }

    /// <summary>Assigns the template's starters to the tactic's slots: keeper to keeper, then same role, then same role group, then any.</summary>
    public static Squad Fit(Squad template, Tactic tactic)
    {
        var unused = template.Starters.ToList();
        var starters = new List<SquadPlayer>();
        // Keeper slots first so a keeper template player is never taken by a field slot.
        foreach (var slot in tactic.Roster.OrderBy(r => r.Role == "keeper" ? 0 : 1))
        {
            SquadPlayer? pick = slot.Role == "keeper"
                ? unused.FirstOrDefault(p => p.Role == "keeper" || p.Keeper is not null)
                : unused.FirstOrDefault(p => p.Role == slot.Role && p.Keeper is null)
                    ?? unused.FirstOrDefault(p => p.Keeper is null && Group(p.Role) == Group(slot.Role))
                    ?? unused.FirstOrDefault(p => p.Keeper is null);
            if (pick is null) throw new InvalidOperationException($"Geen speler in het sjabloon '{template.Name}' voor slot {slot.Id} ({slot.Role}).");
            unused.Remove(pick);
            string role = SquadLoader.Roles.Contains(slot.Role) ? slot.Role : (slot.Role == "keeper" ? "keeper" : "spits");
            starters.Add(pick with { Id = slot.Id, Role = role, Keeper = slot.Role == "keeper" ? pick.Keeper : null });
        }
        // Keep the roster order so the squad lists its starters like the tactic does.
        var ordered = tactic.Roster.Select(r => starters.First(s => s.Id == r.Id)).ToArray();
        return new Squad(template.Name, ordered, template.Bench);
    }

    static TacticTemplates.Group Group(string role) => TacticTemplates.GroupOf(role);
}
