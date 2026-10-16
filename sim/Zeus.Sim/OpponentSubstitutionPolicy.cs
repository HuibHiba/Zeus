namespace Zeus.Sim;

/// <summary>Provisional assumptions, not calibrated. Minutes use the simulation clock, including stoppage time.</summary>
public sealed record OpponentSubstitutionConfig(
    double FatigueMinute = 60, double EnergyThreshold = .65, double AttackingMinute = 70, int MaxSubstitutions = 3);

/// <summary>Deterministic, stateless live policy. Replay applies its recorded commands instead of running the policy.</summary>
public sealed class OpponentSubstitutionPolicy
{
    public OpponentSubstitutionConfig Config { get; }

    public OpponentSubstitutionPolicy(OpponentSubstitutionConfig? config = null)
    {
        Config = config ?? new();
        if (!double.IsFinite(Config.FatigueMinute) || Config.FatigueMinute < 0
            || !double.IsFinite(Config.AttackingMinute) || Config.AttackingMinute < 0
            || !double.IsFinite(Config.EnergyThreshold) || Config.EnergyThreshold is < 0 or > 1
            || Config.MaxSubstitutions is < 0 or > 3)
            throw new ArgumentException("Ongeldige instellingen voor tegenstanderwissels.", nameof(config));
    }

    /// <summary>At most one substitution per team and uninterrupted rest moment; returns whether one was applied.</summary>
    public bool TrySubstitute(Match match, Side team)
    {
        if (!match.IsRestMoment || match.SubstitutionsLeft(team) <= 0
            || match.SubstitutionsUsed(team) >= Config.MaxSubstitutions || match.BenchOf(team).Count == 0) return false;

        // RestartAwarded is emitted once at the start of a restart, not on every paused tick.
        long restTick = match.Events.Events.LastOrDefault(e => e.Type == EventType.RestartAwarded)?.Tick ?? 0;
        if (match.Commands.Any(c => c.Team == team && c.Kind == MatchCommand.SubstituteKind && c.Tick >= restTick)) return false;

        var snapshot = match.GetSnapshot();
        var players = snapshot.Players.Where(p => p.Team == team && !p.SentOff && !p.IsKeeper)
            .OrderBy(p => p.LongEnergy).ThenBy(p => p.Id, StringComparer.Ordinal).ToArray();
        var bench = match.BenchOf(team).Where(p => p.Role != "keeper" && p.Keeper is null)
            .OrderBy(p => p.Id, StringComparer.Ordinal).ToArray();
        double minute = snapshot.TimeSeconds / 60;
        bool losing = team == Side.Home ? snapshot.HomeScore < snapshot.AwayScore : snapshot.AwayScore < snapshot.HomeScore;
        string Role(PlayerSnapshot p) => match.TacticOf(team).Roster.First(r => r.Id == p.Id).Role;

        if (minute >= Config.AttackingMinute && losing)
        {
            var defender = players.FirstOrDefault(p => Group(Role(p)) == "defence");
            var attacker = bench.FirstOrDefault(p => Group(p.Role) == "attack");
            if (defender is not null && attacker is not null) return Apply(defender, attacker);
        }
        if (minute < Config.FatigueMinute) return false;
        foreach (var player in players.Where(p => p.LongEnergy < Config.EnergyThreshold))
        {
            var reserve = bench.FirstOrDefault(p => Group(p.Role) == Group(Role(player)));
            if (reserve is not null) return Apply(player, reserve);
        }
        return false;

        bool Apply(PlayerSnapshot outgoing, SquadPlayer incoming)
        {
            match.Substitute(team, match.SquadIdOf(team, outgoing.Id)!, incoming.Id);
            return true;
        }
    }

    // Broad line groups deliberately include wingbacks with defenders and wide players with attackers.
    private static string Group(string role) => role switch
    {
        "back" or "centrale_verdediger" or "wingback" => "defence",
        "controlerende_middenvelder" or "box_to_box" or "aanvallende_middenvelder" => "midfield",
        "buitenspeler" or "spits" or "targetman" or "hangende_spits" => "attack",
        _ => "keeper"
    };
}
