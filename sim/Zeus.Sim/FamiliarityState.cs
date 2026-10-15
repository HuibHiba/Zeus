using System.Text.Json;

namespace Zeus.Sim;

/// <summary>Provisional v0 learning. One state belongs to one team; keys are squad identities, including reserves.</summary>
public sealed class FamiliarityState
{
    public string TeamId { get; set; } = "";
    public int Period { get; set; }
    public Dictionary<string, FamiliarityPlayer> Players { get; set; } = new(StringComparer.Ordinal);
    public const double PeriodLimit = .12;

    public static FamiliarityState ForTeam(string teamId, IEnumerable<string> playerIds, double initial = 0)
    {
        Unit(initial);
        var state = new FamiliarityState { TeamId = teamId };
        foreach (var id in playerIds)
            state.Players.Add(id, new FamiliarityPlayer {
                Concepts = TacticalConcepts.All.ToDictionary(c => c, _ => initial, StringComparer.Ordinal) });
        state.Validate();
        return state;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TeamId) || Period < 0 || Players is null)
            throw new ArgumentException("Invalid familiarity team or period.");
        foreach (var (id, p) in Players)
        {
            if (string.IsNullOrWhiteSpace(id) || p is null || p.Concepts is null || !double.IsFinite(p.Learned)
                || p.Learned < 0 || p.Learned > PeriodLimit + 1e-12
                || p.Concepts.Keys.Any(c => !TacticalConcepts.All.Contains(c)))
                throw new ArgumentException("Invalid familiarity player.");
            foreach (double value in p.Concepts.Values) Unit(value);
        }
    }

    /// <summary>The caller advances e.g. a calendar week, never a football half. No automatic budget reset.</summary>
    public void BeginPeriod(int period)
    {
        Validate();
        if (period <= Period) throw new ArgumentException("Learning periods must increase.");
        Period = period;
        foreach (var p in Players.Values) p.Learned = 0;
    }

    /// <summary>Only players listed in this state are treated as participants. Supply a participant state/subset if needed.</summary>
    public static void AfterMatch(FamiliarityState state, TacticDemand demand)
    {
        state.Validate();
        if (!double.IsFinite(demand.Complexity) || demand.Complexity < 0 || demand.Complexity > 1)
            throw new ArgumentException("Invalid tactic demand.");
        foreach (string c in TacticalConcepts.All) Unit(demand[c]);
        foreach (var p in state.Players.Values)
        {
            double total = TacticalConcepts.All.Sum(c => demand[c]);
            var gains = TacticalConcepts.All.ToDictionary(c => c, c => total == 0 ? 0
                : .025 * demand[c] / total * (1 - p.Concepts.GetValueOrDefault(c)));
            double totalGain = gains.Values.Sum();
            double scale = totalGain == 0 ? 0 : Math.Min(1, Math.Max(0, PeriodLimit - p.Learned) / totalGain);
            foreach (string c in TacticalConcepts.All)
            {
                double value = p.Concepts.GetValueOrDefault(c);
                if (demand[c] == 0) p.Concepts[c] = value * .995;
                else Learn(p, c, gains[c] * scale);
            }
        }
    }

    public static void Train(FamiliarityState state, string concept, double intensity)
    {
        state.Validate();
        if (!TacticalConcepts.All.Contains(concept)) throw new ArgumentException("Unknown tactical concept.");
        Unit(intensity);
        foreach (var p in state.Players.Values)
            Learn(p, concept, .06 * intensity * (1 - p.Concepts.GetValueOrDefault(concept)));
    }

    private static void Learn(FamiliarityPlayer p, string concept, double amount)
    {
        double gain = Math.Min(amount, Math.Max(0, PeriodLimit - p.Learned));
        p.Concepts[concept] = Math.Min(1, p.Concepts.GetValueOrDefault(concept) + gain);
        p.Learned += gain;
    }

    private static void Unit(double value)
    {
        if (!double.IsFinite(value) || value < 0 || value > 1) throw new ArgumentException("Expected a finite value in 0..1.");
    }

    public string ToJson() { Validate(); return JsonSerializer.Serialize(this); }
    public static FamiliarityState FromJson(string json)
    {
        var state = JsonSerializer.Deserialize<FamiliarityState>(json) ?? throw new ArgumentException("Empty familiarity state.");
        state.Validate();
        return state;
    }
}

public sealed class FamiliarityPlayer
{
    public Dictionary<string, double> Concepts { get; set; } = new(StringComparer.Ordinal);
    // Total gross gain across all concepts in this period; decay never refunds it.
    public double Learned { get; set; }
}
