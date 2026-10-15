namespace Zeus.Sim;

/// <summary>Provisional diagnostics: delay is the quantized scheduled reaction time, zero for misses.</summary>
public sealed record FamiliarityTriggerObservation(Side Team, string PlayerId, long Tick, double Deficit,
    bool Missed, double DelaySeconds);

public sealed partial class Match
{
    private readonly Dictionary<Side, FamiliarityState> _familiarity = new();
    private readonly Dictionary<(Side, string), Rng> _familiarityRng = new();
    private readonly Dictionary<(Side, string), long> _roleFallback = new();
    private readonly List<FamiliarityTriggerObservation> _familiarityObservations = new();
    private readonly List<PendingFamiliarityTrigger> _familiarityPending = new();
    private readonly Dictionary<Tactic, TacticDemand> _demands = new();
    private sealed record PendingFamiliarityTrigger(Player Player, string SquadId, long At, long Until,
        TriggerAction Action, Vec2 Location, string Description);

    public IReadOnlyList<FamiliarityTriggerObservation> FamiliarityTriggers => _familiarityObservations.AsReadOnly();

    private void InitFamiliarity(FamiliarityState? home, FamiliarityState? away)
    {
        // Snapshot input even when disabled; callers can train their state during a match without changing this match.
        if (home is not null) _familiarity[Side.Home] = FamiliarityState.FromJson(home.ToJson());
        if (away is not null) _familiarity[Side.Away] = FamiliarityState.FromJson(away.ToJson());
    }

    private bool HasFamiliarity(Player p) => _config.EnableFamiliarity
        && _familiarity.TryGetValue(p.Team, out var state) && state.Players.ContainsKey(p.SquadId);

    private bool RoleFallbackActive(Player p) => HasFamiliarity(p)
        && _tick < _roleFallback.GetValueOrDefault((p.Team, p.SquadId));

    private bool HandleFamiliarityTrigger(Player p, TriggerAction action, long until, Vec2 location, string description)
    {
        if (!HasFamiliarity(p) || p.IsGoalkeeper && !action.Path) return false;
        if (!_demands.TryGetValue(p.Tactic, out var demand)) _demands[p.Tactic] = demand = TacticDemand.From(p.Tactic);
        var attributes = p.SquadPlayer?.Attributes ?? PlayerAttributes.Neutral;
        double deficit = demand.Deficit(attributes, _familiarity[p.Team].Players[p.SquadId].Concepts);
        if (deficit <= 0) return false;
        var key = (p.Team, p.SquadId);
        if (!_familiarityRng.TryGetValue(key, out var rng))
            _familiarityRng[key] = rng = new Rng(_seed, (ulong)(3000 + Array.IndexOf(_players, p) + 100 * p.Substitutions));
        // Compose the existing attribute penalty without consuming its RNG stream.
        bool attributePenalty = HasAttributes(p) && _config.AttributeSettings.Anticipation;
        double missChance = Math.Min(.9, .45 * deficit + (attributePenalty ? .3 * Overload(p) : 0));
        bool missed = rng.NextDouble() < missChance;
        double delay = .8 * deficit + (attributePenalty
            ? .1 + Math.Max(0, .6 - Attributes(p).Anticipation) + .5 * Overload(p) : 0);
        long ticks = Math.Max(1, (long)Math.Ceiling(delay / _config.StepSeconds));
        _familiarityObservations.Add(new(p.Team, p.SquadId, _tick, deficit, missed, missed ? 0 : ticks * _config.StepSeconds));
        if (missed)
        {
            _roleFallback[key] = Math.Max(_roleFallback.GetValueOrDefault(key), until);
            p.PressUntilTick = 0;
            p.NextDecisionTick = Math.Min(p.NextDecisionTick, _tick);
            EndPath(p, "role fallback");
        }
        else _familiarityPending.Add(new(p, p.SquadId, _tick + ticks, until + ticks, action, location, description));
        return true;
    }

    private void ApplyFamiliarityTriggers()
    {
        for (int i = 0; i < _familiarityPending.Count;)
        {
            var pending = _familiarityPending[i];
            if (_tick < pending.At) { i++; continue; }
            _familiarityPending.RemoveAt(i);
            var p = pending.Player;
            if (p.SentOff || p.SquadId != pending.SquadId) continue;
            if (pending.Action.Path) StartPath(p, "delayed trigger");
            if (pending.Action.Instruction == TriggerRules.StartPressing)
                p.PressUntilTick = Math.Max(p.PressUntilTick, pending.Until);
            else if (pending.Action.Instruction is { } instruction) p.TriggerInstructions[instruction] = pending.Until;
            p.NextDecisionTick = Math.Min(p.NextDecisionTick, _tick);
            _events.Add(new Event(EventType.TriggerFired, p.Team, p.Id, _tick, TimeSeconds, pending.Location, pending.Description));
        }
    }

    private Perception FamiliarityView(Player p, Perception view)
    {
        if (!RoleFallbackActive(p)) return view;
        var role = p.Tactic.Roster.First(r => r.Id == p.Id).Role;
        return view with { Instructions = TacticalConcepts.RoleInstructions(role), MarkId = null,
            PressingForm = "zone", CanRunInBehind = false,
            IsPresser = _carrier >= 0 && p.Team != _possession && PressingAssignment.IsPresser(view) };
    }
}
