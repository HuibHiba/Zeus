namespace Zeus.Sim;

/// <summary>Independent provisional effects. Only active with a non-neutral supplied squad.</summary>
public sealed record AttributeConfig(bool Movement = true, bool Passing = true, bool FirstTouch = true,
    bool Finishing = true, bool Defending = true, bool Vision = true, bool Anticipation = true,
    bool Positioning = true, bool Decisions = true, bool Endurance = true, bool Discipline = true, bool Keeper = true)
{
    public static readonly AttributeConfig Default = new();
    public static readonly AttributeConfig Disabled = new(false, false, false, false, false, false, false, false, false, false, false, false);
}

public sealed partial class Match
{
    private bool _hasSquadPlayers;
    private Dictionary<(Side, string), Player> _squadPlayers = new();
    private void ApplyAttributeTriggers()
    {
        if (!_hasSquadPlayers) return;
        foreach (var p in _players)
        {
            for (int i = p.PendingTriggers.Count - 1; i >= 0; i--)
            {
                var pending = p.PendingTriggers[i];
                if (_tick < pending.At) continue;
                p.PendingTriggers.RemoveAt(i);
                if (p.SentOff || p.Team == _possession) continue;
                if (pending.Instruction == TriggerRules.StartPressing) p.PressUntilTick = Math.Max(p.PressUntilTick, pending.Until);
                else p.TriggerInstructions[pending.Instruction] = pending.Until;
                p.NextDecisionTick = Math.Min(p.NextDecisionTick, _tick);
                _events.Add(new Event(EventType.TriggerFired, p.Team, p.Id, _tick, TimeSeconds, p.Body.Pos, "vertraagde opdracht: " + pending.Instruction));
            }
        }
    }
    private bool HasAttributes(Player p) => p.SquadPlayer is { } s && !ReferenceEquals(s.Attributes, PlayerAttributes.Neutral);
    private PlayerAttributes Attributes(Player p) => p.SquadPlayer!.Attributes;
    public SquadPlayer? SquadPlayerOf(Side side, string id) => _players.FirstOrDefault(p => p.Team == side && p.Id == id)?.SquadPlayer;

    private PlayerProfile BaseProfile(Player p) => !HasAttributes(p) || !_config.AttributeSettings.Movement ? _config.PlayerProfile
        : _config.PlayerProfile with { MaxSpeed = 6.5 + 3 * Attributes(p).Speed,
            Acceleration = 2.5 + 2.5 * Attributes(p).Acceleration, TurnRate = 3 + 4 * Attributes(p).Acceleration };

    private double Complexity(Player p) => Math.Clamp((p.Tactic.Triggers.Count +
        p.Tactic.Phases.Values.SelectMany(f => f.Players).Count(x => x.Mark is not null) / 6.0) / 8, 0, 1);
    private double Overload(Player p) => Complexity(p) * Math.Pow(Math.Max(0, .8 - Math.Min(Attributes(p).Decisions,
        Attributes(p).Positioning)) / .8, 2);

    private PerceptionConfig SensingFor(Player p) => !HasAttributes(p) || !_config.AttributeSettings.Vision ? _sensing
        : _sensing with { ScanIntervalSeconds = .5 + 1 - Attributes(p).Vision,
            FieldOfViewRadians = (100 + 40 * Attributes(p).Vision) * Math.PI / 180,
            AllRoundMeters = _sensing.AllRoundMeters * (.5 + .5 * Attributes(p).Vision),
            BallMemorySeconds = _sensing.BallMemorySeconds * (.5 + .5 * Attributes(p).Vision) };
    private long AttributeDecisionTicks(Player p) => !HasAttributes(p) ? _decisionTicks :
        Math.Max(1, (long)Math.Ceiling(SensingFor(p).ScanIntervalSeconds / _config.StepSeconds - 1e-9));

    private bool DelayBallReaction(Player p, Perception view)
    {
        if (!HasAttributes(p) || !_config.AttributeSettings.Anticipation || view.HasBall || view.VisibleBall is not { } ball) return false;
        if (ball.Vel.Length < 4) { p.ReactionVelocity = ball.Vel; return false; }
        if ((ball.Vel - p.ReactionVelocity).Length <= 6) return false;
        p.ReactionVelocity = ball.Vel;
        p.NextDecisionTick = _tick + (long)Math.Ceiling((.25 + .6 * (1 - Attributes(p).Anticipation)) / _config.StepSeconds);
        return true;
    }

    private bool ScatterFirstTouch(Player p, BallContact contact, double relativeSpeed)
    {
        if (!HasAttributes(p) || !_config.AttributeSettings.FirstTouch || p.IsGoalkeeper || relativeSpeed <= 9) return false;
        double skill = Attributes(p).FirstTouch;
        double pressure = p.View?.VisiblePlayers.Count(x => x.Team != p.Team && (x.Pos - p.Body.Pos).Length < 4) ?? 0;
        double spread = (1 - skill) * relativeSpeed * .25 * (1 + .2 * pressure);
        double error = (p.AttributeRng!.NextDouble() + p.AttributeRng.NextDouble() - 1) * spread;
        double angle = Math.Atan2(_ball.Vel.Y, _ball.Vel.X) + error * .3;
        double residual = Math.Abs(error);
        // Small errors stay at the feet; larger errors produce a real loose ball, never a failed-pass draw.
        if (residual <= .7) return false;
        _ball = new BallState(contact.Position with { Z = 0 }, new Vec3(Math.Cos(angle) * residual, Math.Sin(angle) * residual, 0));
        _carrier = -1;
        _lastTouch = p.Team;
        _lastPasser = contact.PlayerIndex;
        _passerExcludedUntilTick = _tick + Math.Max(1, (long)Math.Ceiling(.2 / _config.StepSeconds));
        _passReceiver = -1; _kickOrigin = null; _offsideCandidates = Array.Empty<string>(); _lastShotTeam = null;
        _events.Add(new Event(EventType.FirstTouch, p.Team, p.Id, _tick, TimeSeconds, contact.Position.XY, "losse aanname"));
        FireTriggers(p.Team == Side.Home ? Side.Away : Side.Home, TriggerRules.BadTouch, contact.Position.XY);
        return true;
    }

    private Perception AttributeView(Player p, Perception view)
    {
        if (!HasAttributes(p)) return view;
        var a = Attributes(p);
        if (_config.AttributeSettings.Positioning && !p.IsGoalkeeper)
        {
            // Persistent ball attraction and expanded spacing: errors have a direction, rather than zero-mean noise.
            double deficit = 1 - a.Positioning;
            var target = view.FormationTarget;
            var centre = p.DefensiveCentre * (view.AttacksPositiveX ? 1 : -1);
            target += (target - centre) * (.10 * deficit + .45 * Overload(p));
            if (view.KnownBall is { } known) target += (known.Ball.Pos.XY - target) * (.12 * deficit);
            view = view with { FormationTarget = ClampToPitch(target), MarkGapExtra = 3 * deficit,
                CognitiveLoad = Overload(p) };
        }
        return view with { ChoiceTemperature = _config.AttributeSettings.Decisions ? .03 + .7 * (1 - a.Decisions) + .4 * Overload(p) : 0,
            ChoiceNoise = p.AttributeRng, PassOptionLimit = _config.AttributeSettings.Vision ? 2 + (int)Math.Round(8 * a.Vision) : int.MaxValue };
    }

    private PassConfig PassingFor(Player p)
    {
        if (!HasAttributes(p) || !_config.AttributeSettings.Passing) return _config.PassSettings;
        double spread = (1 + 2 * Overload(p)) * (1 + .3 * (1 - p.Energy.LongEnergy));
        return _config.PassSettings with { Skill = .5 + .35 * Attributes(p).Passing,
            AngleErrorRadians = _config.PassSettings.AngleErrorRadians * spread,
            SpeedErrorFraction = Math.Min(.9, _config.PassSettings.SpeedErrorFraction * spread) };
    }

    private BallControlConfig ControlFor(PlayerSnapshot snapshot, BallControlConfig limits)
    {
        if (!_hasSquadPlayers || !_squadPlayers.TryGetValue((snapshot.Team, snapshot.Id), out var p)) return limits;
        if (!HasAttributes(p)) return limits;
        if (snapshot.IsKeeper && p.SquadPlayer?.Keeper is { } k && _config.AttributeSettings.Keeper && ReferenceEquals(limits, _keeperControl))
            return limits with { ControlRadius = limits.ControlRadius * (.95 + .10 * k.Positioning),
                MaxRelativeSpeed = limits.MaxRelativeSpeed * (.95 + .10 * k.Reflexes) };
        if (!_config.AttributeSettings.FirstTouch || ReferenceEquals(limits, _shieldedControl)) return limits;
        double skill = Attributes(p).FirstTouch;
        // First-touch errors change physical reach and the speed a player can cushion; no success roll.
        return limits with { ControlRadius = limits.ControlRadius * (.85 + .2 * skill),
            MaxRelativeSpeed = limits.MaxRelativeSpeed * (.85 + .2 * skill) };
    }
}
