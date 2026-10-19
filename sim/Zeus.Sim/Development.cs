namespace Zeus.Sim;

/// <summary>
/// Career v1: development and ageing of the player's squad, one step per completed season. Young players grow, players in their
/// prime hold, older players decline. Kept per career as a delta per player name (the templates themselves never change), and
/// applied to every attribute (keeper attributes too) when the eleven is built.
/// </summary>
public static class Development
{
    public const int PrimeFrom = 24, DeclineFrom = 30, SteepFrom = 33, UnknownAge = 27;
    public const double Grow = 0.02, Decline = -0.02, SteepDecline = -0.04, MaxUp = 0.10, MaxDown = -0.20;

    /// <summary>The attribute change for one season at this age.</summary>
    public static double StepFor(int age) => age < PrimeFrom ? Grow : age < DeclineFrom ? 0 : age < SteepFrom ? Decline : SteepDecline;

    /// <summary>Age of a template player after this many completed seasons (0 in the file counts as a player in his prime).</summary>
    public static int AgeAfter(SquadPlayer player, int seasonsCompleted) => (player.Age == 0 ? UnknownAge : player.Age) + seasonsCompleted;

    /// <summary>
    /// The deltas after one more completed season: every player of <paramref name="squad"/> (template of the division just played,
    /// plus signings) moves by <see cref="StepFor"/> at his age in that season; bounded to <see cref="MaxDown"/>..<see cref="MaxUp"/>.
    /// </summary>
    public static Dictionary<string, double> AfterSeason(IReadOnlyDictionary<string, double> current, IEnumerable<SquadPlayer> squad, int seasonsCompletedBefore)
    {
        var next = new Dictionary<string, double>(current, StringComparer.Ordinal);
        foreach (var player in squad)
        {
            double step = StepFor(AgeAfter(player, seasonsCompletedBefore));
            if (step == 0) continue;
            next[player.Name] = Math.Clamp(next.GetValueOrDefault(player.Name) + step, MaxDown, MaxUp);
        }
        foreach (var name in next.Where(kv => kv.Value == 0).Select(kv => kv.Key).ToList()) next.Remove(name);
        return next;
    }

    /// <summary>The squad with each player's attributes shifted by his delta (clamped to 0..1); ages shown as of this season.</summary>
    public static Squad Apply(Squad squad, IReadOnlyDictionary<string, double> deltas, int seasonsCompleted = 0)
    {
        SquadPlayer Shift(SquadPlayer p)
        {
            double d = deltas.GetValueOrDefault(p.Name);
            int age = p.Age == 0 ? 0 : p.Age + seasonsCompleted;
            if (d == 0) return age == p.Age ? p : p with { Age = age };
            double C(double v) => Math.Clamp(v + d, 0, 1);
            var a = p.Attributes;
            return p with
            {
                Age = age,
                Attributes = new PlayerAttributes(C(a.Passing), C(a.FirstTouch), C(a.Finishing), C(a.Dribbling), C(a.Defending), C(a.Heading), C(a.Speed),
                    C(a.Acceleration), C(a.Strength), C(a.Endurance), C(a.Vision), C(a.Anticipation), C(a.Positioning), C(a.Decisions), C(a.Discipline)),
                Keeper = p.Keeper is { } k ? new KeeperAttributes(C(k.Reflexes), C(k.Positioning), C(k.RushingOut)) : null,
            };
        }
        return squad with { Starters = squad.Starters.Select(Shift).ToList(), Bench = squad.Bench.Select(Shift).ToList() };
    }

    public const int RetireAt = 36;
    public const double YouthFactor = 0.85;

    /// <summary>Players who reach <see cref="RetireAt"/> after this many completed seasons; they stop and a youth player takes their place.</summary>
    public static IEnumerable<string> Retiring(IEnumerable<SquadPlayer> squad, int seasonsCompleted) =>
        squad.Where(p => p.Age != 0 && AgeAfter(p, seasonsCompleted) >= RetireAt).Select(p => p.Name);

    /// <summary>The youth player who replaces a retired one: same role and slot, attributes at <see cref="YouthFactor"/>, age 18-21, name seeded by the old name.</summary>
    public static SquadPlayer Youth(SquadPlayer retired)
    {
        ulong seed = 1469598103934665603UL;
        foreach (char c in retired.Name) seed = (seed ^ c) * 1099511628211UL;
        var rng = new Rng(seed, 3);
        double Y(double v) => Math.Clamp(v * YouthFactor, 0, 1);
        var a = retired.Attributes;
        return retired with
        {
            Name = $"{Signings.First[rng.NextUInt((uint)Signings.First.Length)]} {Signings.Last[rng.NextUInt((uint)Signings.Last.Length)]} (jeugd)",
            Age = 18 + (int)rng.NextUInt(4),
            Attributes = new PlayerAttributes(Y(a.Passing), Y(a.FirstTouch), Y(a.Finishing), Y(a.Dribbling), Y(a.Defending), Y(a.Heading), Y(a.Speed),
                Y(a.Acceleration), Y(a.Strength), Y(a.Endurance), Y(a.Vision), Y(a.Anticipation), Y(a.Positioning), Y(a.Decisions), Y(a.Discipline)),
            Keeper = retired.Keeper is { } k ? new KeeperAttributes(Y(k.Reflexes), Y(k.Positioning), Y(k.RushingOut)) : null,
        };
    }

    /// <summary>The squad with every retired player (by name) replaced by his youth successor, in the same slot.</summary>
    public static Squad WithYouth(Squad squad, IEnumerable<string> retired)
    {
        var names = retired.ToHashSet(StringComparer.Ordinal);
        if (names.Count == 0) return squad;
        SquadPlayer Swap(SquadPlayer p) => names.Contains(p.Name) ? Youth(p) : p;
        return squad with { Starters = squad.Starters.Select(Swap).ToList(), Bench = squad.Bench.Select(Swap).ToList() };
    }

    public static bool IsValid(IReadOnlyDictionary<string, double> deltas) =>
        deltas.All(kv => !string.IsNullOrWhiteSpace(kv.Key) && double.IsFinite(kv.Value) && kv.Value >= MaxDown - 1e-9 && kv.Value <= MaxUp + 1e-9);
}
