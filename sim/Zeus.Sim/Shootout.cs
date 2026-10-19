namespace Zeus.Sim;

/// <summary>Result of a penalty shoot-out: kicks taken per side (true = scored) and who won.</summary>
public sealed record ShootoutResult(IReadOnlyList<bool> Home, IReadOnlyList<bool> Away, Side Winner)
{
    public int HomeScored => Home.Count(k => k);
    public int AwayScored => Away.Count(k => k);
}

/// <summary>
/// Cup tie-break (v1): a seeded penalty shoot-out on attributes, not on the pitch. Five kicks each, then sudden death. The chance
/// to score is a base rate shifted by the taker's finishing and decisions against the keeper's reflexes and positioning. Takers go
/// in order of finishing, the keeper excluded; a squad-less (neutral) side uses neutral attributes.
/// </summary>
public static class Shootout
{
    public const double BaseRate = 0.76, TakerWeight = 0.20, KeeperWeight = 0.16;
    public const int Regular = 5, MaxKicks = 30;

    public static ShootoutResult Play(Squad home, Squad away, ulong seed)
    {
        var rng = new Rng(seed, 11);
        var homeTakers = Takers(home); var awayTakers = Takers(away);
        double homeKeeper = KeeperQuality(home), awayKeeper = KeeperQuality(away);
        var homeKicks = new List<bool>(); var awayKicks = new List<bool>();
        for (int round = 0; round < MaxKicks; round++)
        {
            homeKicks.Add(Score(homeTakers[round % homeTakers.Count], awayKeeper, rng));
            if (Decided(homeKicks, awayKicks, out var winner)) return new(homeKicks, awayKicks, winner);
            awayKicks.Add(Score(awayTakers[round % awayTakers.Count], homeKeeper, rng));
            if (Decided(homeKicks, awayKicks, out winner)) return new(homeKicks, awayKicks, winner);
        }
        // ponytail: after 30 kicks each the seed decides, so the loop always ends.
        return new(homeKicks, awayKicks, rng.NextUInt(2) == 0 ? Side.Home : Side.Away);
    }

    static bool Score(SquadPlayer taker, double keeper, Rng rng)
    {
        double chance = BaseRate + TakerWeight * ((taker.Attributes.Finishing + taker.Attributes.Decisions) / 2 - 0.5) - KeeperWeight * (keeper - 0.5);
        return rng.NextDouble() < Math.Clamp(chance, 0.4, 0.95);
    }

    /// <summary>Settled when one side cannot be caught: within the five, or a lead after an equal number of kicks beyond them.</summary>
    static bool Decided(List<bool> home, List<bool> away, out Side winner)
    {
        int h = home.Count(k => k), a = away.Count(k => k);
        int homeLeft = Math.Max(0, Regular - home.Count), awayLeft = Math.Max(0, Regular - away.Count);
        winner = Side.Home;
        if (h > a + awayLeft) return true;
        if (a > h + homeLeft) { winner = Side.Away; return true; }
        if (home.Count >= Regular && home.Count == away.Count && h != a) { winner = h > a ? Side.Home : Side.Away; return true; }
        return false;
    }

    static List<SquadPlayer> Takers(Squad squad) =>
        squad.Starters.Where(p => p.Keeper is null && p.Role != "keeper").OrderByDescending(p => p.Attributes.Finishing).ThenBy(p => p.Id, StringComparer.Ordinal).ToList();

    static double KeeperQuality(Squad squad) =>
        squad.Starters.FirstOrDefault(p => p.Keeper is not null)?.Keeper is { } k ? (k.Reflexes + k.Positioning) / 2 : 0.5;
}
