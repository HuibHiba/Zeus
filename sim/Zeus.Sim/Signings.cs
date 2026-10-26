namespace Zeus.Sim;

/// <summary>
/// Career v1: at the end of a season the club offers three new players (seeded per season and division); the chosen one joins
/// the squad and replaces the weakest starter in his role. No transfer market, no money: one pick, or none.
/// </summary>
public static class Signings
{
    public const int Offered = 3;
    /// <summary>Above the template mean of the role, so a signing is a visible but modest upgrade.</summary>
    public const double Bonus = 0.06, BonusSpread = 0.04;
    internal static readonly string[] First = { "Daan", "Sem", "Milan", "Jesse", "Ruben", "Finn", "Thijs", "Luuk", "Bram", "Noah", "Sven", "Joris" };
    internal static readonly string[] Last = { "de Vries", "Jansen", "Bakker", "Visser", "Smit", "Meijer", "Mulder", "Bos", "Vos", "Peters", "Hendriks", "Dekker" };

    public static double Mean(PlayerAttributes a) => new[] { a.Passing, a.FirstTouch, a.Finishing, a.Dribbling, a.Defending, a.Heading, a.Speed,
        a.Acceleration, a.Strength, a.Endurance, a.Vision, a.Anticipation, a.Positioning, a.Decisions, a.Discipline }.Average();

    /// <summary>Three outfield candidates in distinct roles of the division's template, each a little above that role's mean.</summary>
    public static IReadOnlyList<SquadPlayer> Candidates(int season, int division, string squadsDirectory, IEnumerable<string>? usedNames = null)
    {
        var template = SquadLoader.LoadFile(Path.Combine(squadsDirectory, Competition.Level(division) + ".json"));
        var rng = new Rng(Competition.Seed(season, 30, division, 0));
        var roles = template.Starters.Where(p => p.Keeper is null).Select(p => p.Role).Distinct().ToList();
        var used = template.Starters.Concat(template.Bench).Select(p => p.Name).Concat(usedNames ?? Enumerable.Empty<string>()).ToHashSet(StringComparer.Ordinal);
        var names = First.SelectMany(first => Last.Select(last => first + " " + last)).Where(name => !used.Contains(name)).ToList();
        var picks = new List<SquadPlayer>();
        for (int n = 0; n < Offered && roles.Count > 0 && names.Count > 0; n++)
        {
            var role = roles[(int)rng.NextUInt((uint)roles.Count)];
            roles.Remove(role);
            var peers = template.Starters.Concat(template.Bench).Where(p => p.Role == role && p.Keeper is null).ToList();
            double Up(Func<PlayerAttributes, double> f) => Math.Min(1, peers.Average(p => f(p.Attributes)) + Bonus + rng.NextDouble() * BonusSpread);
            var a = new PlayerAttributes(Up(x => x.Passing), Up(x => x.FirstTouch), Up(x => x.Finishing), Up(x => x.Dribbling), Up(x => x.Defending),
                Up(x => x.Heading), Up(x => x.Speed), Up(x => x.Acceleration), Up(x => x.Strength), Up(x => x.Endurance), Up(x => x.Vision),
                Up(x => x.Anticipation), Up(x => x.Positioning), Up(x => x.Decisions), Up(x => x.Discipline));
            var name = $"{First[rng.NextUInt((uint)First.Length)]} {Last[rng.NextUInt((uint)Last.Length)]}";
            if (!names.Contains(name)) name = names[(int)rng.NextUInt((uint)names.Count)];
            names.Remove(name);
            picks.Add(new SquadPlayer("S" + (season * 10 + n), name, role, peers[0].PreferredPosition, a, Age: 20 + (int)rng.NextUInt(10)));
        }
        return picks;
    }

    public static bool IsValid(SquadPlayer p) => p is not null && !string.IsNullOrWhiteSpace(p.Name) && SquadLoader.Roles.Contains(p.Role) && p.Role != "keeper"
        && p.Keeper is null && p.Attributes is not null && !double.IsNaN(Mean(p.Attributes)) && Mean(p.Attributes) is >= 0 and <= 1;

    /// <summary>
    /// The fitted squad with each signing in the eleven: he takes the slot of the weakest starter in his role (that starter goes to the
    /// bench in place of its weakest outfield player, so it stays 11 + 7); without a starter in his role he joins the bench.
    /// </summary>
    public static Squad Apply(Squad squad, IEnumerable<SquadPlayer> signings)
    {
        var starters = squad.Starters.ToList();
        var bench = squad.Bench.ToList();
        foreach (var signing in signings)
        {
            if (starters.Any(s => s.Name == signing.Name) || bench.Any(b => b.Name == signing.Name)) continue;
            int weakestBench = -1; double worstBench = double.MaxValue;
            for (int i = 0; i < bench.Count; i++)
                if (bench[i].Keeper is null && Mean(bench[i].Attributes) < worstBench) { worstBench = Mean(bench[i].Attributes); weakestBench = i; }
            int slot = -1; double worst = double.MaxValue;
            for (int i = 0; i < starters.Count; i++)
                if (starters[i].Role == signing.Role && starters[i].Keeper is null && Mean(starters[i].Attributes) < worst) { worst = Mean(starters[i].Attributes); slot = i; }
            if (slot < 0) { if (weakestBench >= 0) bench[weakestBench] = signing with { Id = bench[weakestBench].Id }; continue; }
            var replaced = starters[slot];
            starters[slot] = signing with { Id = replaced.Id, PreferredPosition = replaced.PreferredPosition };
            if (weakestBench >= 0) bench[weakestBench] = replaced with { Id = bench[weakestBench].Id };
        }
        return squad with { Starters = starters, Bench = bench };
    }
}
