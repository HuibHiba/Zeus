namespace Zeus.Sim;

// Live commands (pause-and-adjust, docs/wissels.md): substitutions and tactical adjustments.
// A command is accepted only at a rest moment and is rejected with a MatchCommandException otherwise (no queue).
// Accepted commands are recorded with their tick in Commands, which the replay file stores as the input timeline.
public sealed partial class Match
{
    private ulong _seed;
    private readonly Dictionary<Side, List<SquadPlayer>> _bench = new();
    private readonly Dictionary<Side, HashSet<string>> _gone = new();
    private readonly Dictionary<Side, int> _substitutionsUsed = new() { [Side.Home] = 0, [Side.Away] = 0 };
    private readonly Dictionary<Side, BlockHeight> _blockOverride = new();
    private readonly List<MatchCommand> _commands = new();
    private int _substitutionCount;

    private void InitSubstitutions(Squad home, Squad away)
    {
        _bench[Side.Home] = home.Bench.ToList(); _bench[Side.Away] = away.Bench.ToList();
        _gone[Side.Home] = new(StringComparer.Ordinal); _gone[Side.Away] = new(StringComparer.Ordinal);
    }

    /// <summary>Accepted live commands in order, with the tick they were applied at.</summary>
    public IReadOnlyList<MatchCommand> Commands => _commands;

    /// <summary>True when commands are accepted: before the first step, or while play is stopped (restart pause, goal, half-time).</summary>
    public bool IsRestMoment => _status == MatchStatus.Restart || (_tick == 0 && _status == MatchStatus.Running);

    public int SubstitutionsUsed(Side team) => _substitutionsUsed[team];
    public int SubstitutionsLeft(Side team) => _config.MaxSubstitutions - _substitutionsUsed[team];
    /// <summary>Reserves of a team that have not come on yet.</summary>
    public IReadOnlyList<SquadPlayer> BenchOf(Side team) => _bench[team];
    /// <summary>Identity (squad id) of the player now in a tactic slot, or null for an unknown slot.</summary>
    public string? SquadIdOf(Side team, string slot) => _players.FirstOrDefault(p => p.Team == team && p.Id == slot)?.SquadId;
    public Tactic TacticOf(Side team) => _players.First(p => p.Team == team).Tactic;

    /// <summary>Applies a recorded command; its tick must equal the current tick.</summary>
    public void Apply(MatchCommand command)
    {
        if (command.Tick != _tick) throw new MatchCommandException($"Commando hoort bij tick {command.Tick}, wedstrijd staat op tick {_tick}.");
        switch (command.Kind)
        {
            case MatchCommand.SubstituteKind:
                Substitute(command.Team, command.OutPlayerId ?? "", command.InPlayerId ?? "");
                break;
            case MatchCommand.AdjustKind:
                Adjust(command.Team, command.Adjustment ?? throw new MatchCommandException("Aanpassing ontbreekt."));
                break;
            default: throw new MatchCommandException($"Onbekend commando '{command.Kind}'.");
        }
    }

    private void RequireRestMoment(string what)
    {
        if (_status is MatchStatus.Finished or MatchStatus.Abandoned)
            throw new MatchCommandException($"{what} is niet mogelijk: de wedstrijd is afgelopen of gestaakt.");
        if (!IsRestMoment)
            throw new MatchCommandException($"{what} is alleen mogelijk op een rustmoment (spel stil of pauze); het spel loopt.");
    }

    /// <summary>
    /// Replaces a player on the pitch by a reserve. <paramref name="outPlayerId"/> and <paramref name="inPlayerId"/> are squad ids
    /// (a starter's id equals his tactic slot, reserves are B1..B7). The substitute takes the slot, position and role of the one
    /// who leaves, starts with fresh energy and no cards. Throws <see cref="MatchCommandException"/> without changing anything.
    /// </summary>
    public void Substitute(Side team, string outPlayerId, string inPlayerId)
    {
        RequireRestMoment("Wissel");
        var slot = _players.FirstOrDefault(p => p.Team == team && p.SquadId == outPlayerId);
        if (slot is null)
        {
            if (_gone[team].Contains(outPlayerId)) throw new MatchCommandException($"Speler {outPlayerId} is al gewisseld en kan niet meer wisselen.");
            if (_bench[team].Any(b => b.Id == outPlayerId)) throw new MatchCommandException($"Speler {outPlayerId} zit op de bank en staat niet op het veld.");
            throw new MatchCommandException($"Onbekende speler {outPlayerId} (geen veldspeler van {team}).");
        }
        var incoming = _bench[team].FirstOrDefault(b => b.Id == inPlayerId);
        if (incoming is null)
        {
            if (_gone[team].Contains(inPlayerId)) throw new MatchCommandException($"Speler {inPlayerId} is al gewisseld en kan niet terugkomen.");
            if (_players.Any(p => p.Team == team && p.SquadId == inPlayerId)) throw new MatchCommandException($"Speler {inPlayerId} staat al op het veld.");
            throw new MatchCommandException($"Onbekende reservespeler {inPlayerId}.");
        }
        if (slot.SentOff) throw new MatchCommandException($"Speler {outPlayerId} is uitgesloten en kan niet gewisseld worden.");
        if (_substitutionsUsed[team] >= _config.MaxSubstitutions)
            throw new MatchCommandException($"Maximaal aantal wissels ({_config.MaxSubstitutions}) voor {team} is bereikt.");
        bool incomingKeeper = incoming.Role == "keeper" || incoming.Keeper is not null;
        if (slot.IsGoalkeeper && !incomingKeeper)
            throw new MatchCommandException($"Een keeper ({outPlayerId}) kan alleen door een keeper gewisseld worden, niet door {inPlayerId}.");
        if (!slot.IsGoalkeeper && incomingKeeper)
            throw new MatchCommandException($"Keeper {inPlayerId} kan geen veldspeler ({outPlayerId}) vervangen.");

        // All checks passed: apply.
        _bench[team].Remove(incoming);
        _gone[team].Add(slot.SquadId);
        _substitutionsUsed[team]++;
        _substitutionCount++;
        _squadPlayers.Remove((team, slot.Id));
        slot.SquadId = incoming.Id;
        slot.SquadPlayer = incoming;
        slot.Substitutions++;
        slot.AttributeRng = !ReferenceEquals(incoming.Attributes, PlayerAttributes.Neutral)
            ? new Rng(_seed, (ulong)(2000 + 100 * _substitutionCount + Array.IndexOf(_players, slot))) : null;
        if (slot.AttributeRng is not null) _squadPlayers[(team, slot.Id)] = slot;
        _hasSquadPlayers = _squadPlayers.Count != 0;
        slot.Energy = FatigueState.Fresh;
        slot.YellowCards = 0;
        slot.ReactionVelocity = default;
        slot.PendingTriggers.Clear();
        slot.TriggerInstructions.Clear();
        slot.PressUntilTick = 0;
        slot.NextDecisionTick = _tick;
        _events.Add(new Event(EventType.Substitution, team, slot.Id, _tick, TimeSeconds, slot.Body.Pos, $"{outPlayerId}->{inPlayerId}"));
        _commands.Add(new MatchCommand(_tick, team, MatchCommand.SubstituteKind, outPlayerId, inPlayerId));
    }

    /// <summary>
    /// Changes the tactic of a team live: team settings, defending block (existing variants only, replaces automatic block switches)
    /// and instructions per player (applied to every phase). Validated like <see cref="TacticLoader"/>; an invalid adjustment
    /// throws <see cref="MatchCommandException"/> and changes nothing.
    /// </summary>
    public void Adjust(Side team, TacticAdjustment adjustment)
    {
        RequireRestMoment("Bijsturen");
        if (adjustment is null || adjustment.IsEmpty) throw new MatchCommandException("Lege aanpassing: er is niets om te wijzigen.");
        var teamPlayers = _players.Where(p => p.Team == team).ToArray();
        var tactic = teamPlayers[0].Tactic;
        var ids = tactic.Roster.Select(r => r.Id).ToArray();
        Tactic updated;
        try
        {
            var settings = tactic.Team with
            {
                Tempo = adjustment.Tempo ?? tactic.Team.Tempo,
                PressingIntensity = adjustment.PressingIntensity ?? tactic.Team.PressingIntensity,
                LineHeight = adjustment.LineHeight ?? tactic.Team.LineHeight,
                Compactness = adjustment.Compactness ?? tactic.Team.Compactness,
                PressingForm = adjustment.PressingForm ?? tactic.Team.PressingForm
            };
            TacticLoader.ValidateTeamSettings(settings);
            if (adjustment.Block is { } block && (!Enum.IsDefined(block) || !tactic.DefendVariants.ContainsKey(block)))
                throw new MatchCommandException($"Blokhoogte {adjustment.Block} bestaat niet als variant in de tactiek van {team}.");

            var phases = tactic.Phases.ToDictionary(kv => kv.Key, kv => kv.Value);
            var variants = new SortedDictionary<BlockHeight, PhaseLayout>(tactic.DefendVariants.ToDictionary(kv => kv.Key, kv => kv.Value));
            foreach (var change in adjustment.Instructions ?? [])
            {
                if (change is null || !ids.Contains(change.PlayerId))
                    throw new MatchCommandException($"Onbekende speler/positie '{change?.PlayerId}' in de instructies.");
                foreach (var name in (change.Add ?? []).Concat(change.Remove ?? []))
                    if (!TacticEditorModel.AllowedInstructions.Contains(name))
                        throw new MatchCommandException($"Onbekende instructie '{name}'.");
                foreach (var key in phases.Keys.ToArray()) phases[key] = EditLayout(phases[key], change, ids, $"fase {key}");
                foreach (var key in variants.Keys.ToArray()) variants[key] = EditLayout(variants[key], change, ids, $"blok {key}");
            }
            updated = tactic with { Team = settings, Phases = phases, DefendVariants = variants };
        }
        catch (TacticLoadException e) { throw new MatchCommandException("Ongeldige aanpassing: " + e.Message, e); }

        // Validated: apply.
        if (adjustment.Block is { } chosen)
        {
            _blockOverride[team] = chosen;
            if (team == Side.Home) _homeBlock = chosen; else _awayBlock = chosen;
        }
        foreach (var p in teamPlayers)
        {
            p.Tactic = updated;
            p.Build = updated.Phase(GamePhase.Opbouw).Players.First(x => x.Id == p.Id);
            p.Defend = _blockOverride.ContainsKey(team) ? DefensivePlayer(p) : DefendLayout(updated).Players.First(x => x.Id == p.Id);
            if (adjustment.Block is not null) UpdateDefensiveGeometry(p);
        }
        _events.Add(new Event(EventType.TacticChange, team, "", _tick, TimeSeconds, _ball.Pos.XY, Describe(adjustment)));
        _commands.Add(new MatchCommand(_tick, team, MatchCommand.AdjustKind, Adjustment: adjustment));
    }

    private static PhaseLayout EditLayout(PhaseLayout layout, InstructionChange change, IReadOnlyCollection<string> ids, string where)
    {
        var players = layout.Players.Select(player =>
        {
            if (player.Id != change.PlayerId) return player;
            var instructions = player.Instructions.ToList();
            string? mark = player.Mark;
            foreach (var remove in change.Remove ?? [])
            {
                instructions.RemoveAll(x => x == remove);
                if (remove == "dek_man") mark = null;
            }
            foreach (var add in change.Add ?? [])
                if (!instructions.Contains(add)) instructions.Add(add);
            if (change.Mark is not null) mark = change.Mark;
            var edited = player with { Instructions = instructions, Mark = mark };
            TacticLoader.ValidatePhasePlayer(edited, ids, $"{where}.{player.Id}");
            return edited;
        }).ToArray();
        return layout with { Players = players };
    }

    private static string Describe(TacticAdjustment a)
    {
        var parts = new List<string>();
        string N(double v) => Canonical.Num(v);
        if (a.Tempo is { } t) parts.Add("tempo=" + N(t));
        if (a.PressingIntensity is { } pi) parts.Add("pressing=" + N(pi));
        if (a.LineHeight is { } lh) parts.Add("lijn=" + N(lh));
        if (a.Compactness is { } c) parts.Add("compact=" + N(c));
        if (a.PressingForm is { } pf) parts.Add("vorm=" + pf);
        if (a.Block is { } b) parts.Add("blok=" + b);
        foreach (var i in a.Instructions ?? [])
            parts.Add(i.PlayerId + ":" + string.Join(",", (i.Add ?? []).Select(x => "+" + x).Concat((i.Remove ?? []).Select(x => "-" + x)))
                + (i.Mark is null ? "" : "@" + i.Mark));
        return string.Join(";", parts);
    }
}
