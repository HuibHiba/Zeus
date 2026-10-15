namespace Zeus.Sim;

/// <summary>Provisional v0 concepts and role baseline. No calibrated football model.</summary>
public static class TacticalConcepts
{
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[] {
        "hoge_pressing", "gegenpressing", "positiespel", "lange_bal", "laag_blok", "man_om_man", "counter" });

    // The current role/template baseline has no explicit instructions: the brain follows its phase anchor,
    // observes the ball and uses the existing zonal pressing and on-ball decisions.
    public static IReadOnlyList<string> RoleInstructions(string role) => Array.Empty<string>();

    public static double Understanding(PlayerAttributes a, string concept)
    {
        if (!All.Contains(concept)) throw new ArgumentException("Unknown tactical concept.", nameof(concept));
        double specialist = concept switch {
            "hoge_pressing" or "gegenpressing" or "counter" => a.Anticipation,
            "positiespel" or "lange_bal" => a.Vision,
            _ => a.Positioning
        };
        return .4 * a.Decisions + .3 * a.Positioning + .3 * specialist;
    }
}

/// <summary>Deterministic provisional demand, including instructions whose execution is currently opt-in.</summary>
public sealed record TacticDemand(IReadOnlyDictionary<string, double> Weights, double Complexity)
{
    public double K => 1 + .5 * Complexity;
    public double this[string concept] => Weights[concept];

    public static TacticDemand From(Tactic tactic)
    {
        var layouts = tactic.Phases.Values.Concat(tactic.DefendVariants.Values).ToArray();
        var players = layouts.SelectMany(l => l.Players).ToArray();
        // A specialist instruction can define the style even when only one player executes it.
        double Spread(string instruction) => players.Any(p => p.Instructions.Contains(instruction)) ? 1 : 0;
        double low = tactic.Phase(GamePhase.Verdedigen).Block == BlockHeight.Laag
            ? 1 : 1 - tactic.Team.LineHeight;
        double marking = players.Count(p => p.Mark is not null) / (double)players.Length;
        double deviations = players.Sum(p => p.Instructions.Except(TacticalConcepts.RoleInstructions(
            tactic.Roster.First(r => r.Id == p.Id).Role)).Count()) / (double)players.Length;
        double paths = players.Count(p => p.Path is { Count: > 0 }) / (double)players.Length;
        double complexity = Math.Clamp(tactic.Triggers.Count / 8.0 + deviations / 8 + marking / 4 + paths / 4, 0, 1);
        var weights = new Dictionary<string, double>(StringComparer.Ordinal) {
            ["hoge_pressing"] = tactic.Team.PressingIntensity * tactic.Team.LineHeight,
            ["gegenpressing"] = tactic.Triggers.Any(t => t.Condition == TriggerRules.LossInOwnHalf)
                ? tactic.Team.PressingIntensity : .5 * Spread("start_pressing"),
            ["positiespel"] = Math.Min(1, 2 * (1 - tactic.Team.Tempo)) * Spread("vrije_man_zoeken"),
            ["lange_bal"] = tactic.Team.Tempo * Math.Max(Spread("loop_diep"),
                tactic.Roster.Any(p => p.Role == "targetman") ? .8 : 0),
            ["laag_blok"] = low * (1 - .5 * tactic.Team.PressingIntensity),
            ["man_om_man"] = Math.Max(Math.Min(1, 2 * marking), tactic.Team.PressingForm == "man" ? 1 : 0),
            ["counter"] = low * tactic.Team.Tempo * Spread("loop_diep")
        };
        return new(weights, complexity);
    }

    public double Deficit(PlayerAttributes attributes, IReadOnlyDictionary<string, double> familiarity) =>
        TacticalConcepts.All.Max(c => Math.Max(0, this[c] * K - TacticalConcepts.Understanding(attributes, c)
            * (.5 + .5 * familiarity.GetValueOrDefault(c))));
}
