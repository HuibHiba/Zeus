namespace Zeus.Sim;

/// <summary>One unavailable player of the player's club, by squad name (slot ids are reassigned when a template is fitted).</summary>
public sealed record Absence(string Player, string Reason, int RoundsLeft);

/// <summary>
/// Career v1: suspensions (a red card costs the next round) and injuries (a seeded roll per starter per round, one to three rounds).
/// An absent starter is replaced from the bench by role, so the player's eleven stays complete; the AI clubs are not affected.
/// </summary>
public static class Absences
{
    public const string Red = "rood", Injury = "blessure";
    public const int RedRounds = 1, MaxInjuryRounds = 3, MaxRounds = 10;
    /// <summary>Per starter per round (v1 hypothesis: about one injury per three rounds in an eleven).</summary>
    public const double InjuryChance = 0.03;

    public static bool IsValid(Absence a) => a is not null && !string.IsNullOrWhiteSpace(a.Player) && a.Reason is Red or Injury && a.RoundsLeft is >= 1 and <= MaxRounds;

    /// <summary>The fitted squad with every absent starter swapped for a bench player: keeper for keeper, else same role, same group, any outfield.</summary>
    public static Squad WithoutAbsent(Squad squad, IEnumerable<string> absent)
    {
        var names = absent.ToHashSet(StringComparer.Ordinal);
        if (names.Count == 0) return squad;
        var starters = squad.Starters.ToList();
        var bench = squad.Bench.ToList();
        for (int i = 0; i < starters.Count; i++)
        {
            var slot = starters[i];
            if (!names.Contains(slot.Name)) continue;
            bool keeper = slot.Keeper is not null || slot.Role == "keeper";
            var pick = keeper
                ? bench.FirstOrDefault(b => !names.Contains(b.Name) && (b.Keeper is not null || b.Role == "keeper"))
                : bench.FirstOrDefault(b => !names.Contains(b.Name) && b.Keeper is null && b.Role == slot.Role)
                    ?? bench.FirstOrDefault(b => !names.Contains(b.Name) && b.Keeper is null && TacticTemplates.GroupOf(b.Role) == TacticTemplates.GroupOf(slot.Role))
                    ?? bench.FirstOrDefault(b => !names.Contains(b.Name) && b.Keeper is null);
            if (pick is null) continue;      // nobody left on the bench: he plays anyway
            bench.Remove(pick);
            bench.Add(slot with { Id = pick.Id, Role = pick.Role, Keeper = pick.Keeper is null ? null : slot.Keeper });
            starters[i] = pick with { Id = slot.Id, Role = slot.Role, Keeper = keeper ? pick.Keeper : null };
        }
        return squad with { Starters = starters, Bench = bench };
    }

    /// <summary>Suspensions only (no countdown, no injury roll): for red cards in a cup match, which sits between league rounds.</summary>
    public static List<Absence> Suspend(IEnumerable<Absence> current, IEnumerable<string> redCarded)
    {
        var next = current.ToList();
        foreach (var name in redCarded.Distinct(StringComparer.Ordinal))
        {
            next.RemoveAll(a => a.Player == name);
            next.Add(new Absence(name, Red, RedRounds));
        }
        return next;
    }

    /// <summary>
    /// The list after a round: everyone counts down one round (and drops out at zero), red cards from this round add a suspension,
    /// and each starter who was not absent rolls for an injury with <paramref name="rng"/> (seeded by the round, so it replays the same).
    /// </summary>
    public static List<Absence> AfterRound(IEnumerable<Absence> current, IEnumerable<string> redCarded, IEnumerable<string> starters, Rng rng)
    {
        var list = current.ToList();
        var next = list.Where(a => a.RoundsLeft > 1).Select(a => a with { RoundsLeft = a.RoundsLeft - 1 }).ToList();
        var wasAbsent = list.Select(a => a.Player).ToHashSet(StringComparer.Ordinal);
        foreach (var name in redCarded.Distinct(StringComparer.Ordinal))
        {
            next.RemoveAll(a => a.Player == name);
            next.Add(new Absence(name, Red, RedRounds));
        }
        foreach (var name in starters)      // fixed order: one draw per starter, so the result only depends on the seed
        {
            bool roll = rng.NextDouble() < InjuryChance;
            int rounds = 1 + (int)rng.NextUInt(MaxInjuryRounds);
            if (!roll || wasAbsent.Contains(name) || next.Any(a => a.Player == name)) continue;
            next.Add(new Absence(name, Injury, rounds));
        }
        return next;
    }
}
