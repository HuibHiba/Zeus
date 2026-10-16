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
    // Levels are relative to the half-professional mean (normalized 0..1). Finishing and saving have separately
    // calibrated scales; the no-squad and disabled paths retain the historical base-config behaviour.
    private static double HalfProSpeed => HalfProMean.Speed;
    private static double HalfProAcceleration => HalfProMean.Acceleration;
    private static double RefVision => HalfProMean.Vision;
    private static double RefAnticipation => HalfProMean.Anticipation;
    private static double RefDecisions => HalfProMean.Decisions;
    private static double RefPositioning => HalfProMean.Positioning;
    private static double RefFirstTouch => HalfProMean.FirstTouch;
    private static double RefDefending => HalfProMean.Defending;
    private static double RefPassing => HalfProMean.Passing;
    private static double RefFinishing => HalfProMean.Finishing;
    private static double RefDiscipline => HalfProMean.Discipline;
    private static double RefEndurance => HalfProMean.Endurance;
    private const double RefKeeper = 11 / 19.0;
    // The spread between the levels is compressed: every attribute is pulled towards the half-professional mean by this factor.
    private const double LevelSpreadUp = .35, LevelSpreadDown = .20;
    private static readonly PlayerAttributes HalfProMean = new(12 / 19.0, 13 / 19.0, 12.6 / 19, 12 / 19.0, 12.8 / 19, 12 / 19.0,
        13 / 19.0, 14 / 19.0, 13 / 19.0, 12 / 19.0, 12 / 19.0, 11 / 19.0, 10 / 19.0, 13 / 19.0, 13 / 19.0);
    private static double Pull(double v, double mean) => Math.Clamp(mean + (v >= mean ? LevelSpreadUp : LevelSpreadDown) * (v - mean), 0, 1);
    internal static PlayerAttributes EffectiveAttributes(PlayerAttributes a, PlayerAttributes h) => new(Pull(a.Passing, h.Passing),
        Pull(a.FirstTouch, h.FirstTouch), Pull(a.Finishing, h.Finishing), Pull(a.Dribbling, h.Dribbling), Pull(a.Defending, h.Defending),
        Pull(a.Heading, h.Heading), Pull(a.Speed, h.Speed), Pull(a.Acceleration, h.Acceleration), Pull(a.Strength, h.Strength),
        Pull(a.Endurance, h.Endurance), Pull(a.Vision, h.Vision), Pull(a.Anticipation, h.Anticipation), Pull(a.Positioning, h.Positioning),
        Pull(a.Decisions, h.Decisions), Pull(a.Discipline, h.Discipline));
    internal static PlayerAttributes EffectiveOf(PlayerAttributes a) => EffectiveAttributes(a, HalfProMean);
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
    private PlayerAttributes Attributes(Player p) => p.Effective ?? p.SquadPlayer!.Attributes;
    private static double KeeperValue(double v) => Pull(v, RefKeeper);
    private static double KeeperFactor(double value)
    {
        double delta = KeeperValue(value) - RefKeeper;
        // Pull already compresses deficits more than improvements. Keep saving modest:
        // excessive keeper advantages suppress goals when two professional teams meet.
        return 1 + .3 * delta;
    }
    public SquadPlayer? SquadPlayerOf(Side side, string id) => _players.FirstOrDefault(p => p.Team == side && p.Id == id)?.SquadPlayer;

    private PlayerProfile BaseProfile(Player p) => !HasAttributes(p) || !_config.AttributeSettings.Movement ? _config.PlayerProfile
        : _config.PlayerProfile with { MaxSpeed = _config.PlayerProfile.MaxSpeed + 2.4 * (Attributes(p).Speed - HalfProSpeed),
            Acceleration = _config.PlayerProfile.Acceleration + 2.5 * (Attributes(p).Acceleration - HalfProAcceleration),
            TurnRate = _config.PlayerProfile.TurnRate + 4 * (Attributes(p).Acceleration - HalfProAcceleration) };

    private double Complexity(Player p) => Math.Clamp((p.Tactic.Triggers.Count +
        p.Tactic.Phases.Values.SelectMany(f => f.Players).Count(x => x.Mark is not null) / 6.0) / 8, 0, 1);
    private double Overload(Player p) => Complexity(p) * Math.Pow(Math.Max(0, RefPositioning - Math.Min(Attributes(p).Decisions,
        Attributes(p).Positioning)) / RefPositioning, 2);

    private PerceptionConfig SensingFor(Player p) => !HasAttributes(p) || !_config.AttributeSettings.Vision ? _sensing
        : _sensing with { ScanIntervalSeconds = Math.Max(.15, _sensing.ScanIntervalSeconds * (1 + .7 * (RefVision - Attributes(p).Vision))),
            FieldOfViewRadians = _sensing.FieldOfViewRadians + 40 * Math.PI / 180 * (Attributes(p).Vision - RefVision),
            AllRoundMeters = Math.Min(_sensing.AllRoundMeters * (1 + .6 * (Attributes(p).Vision - RefVision)), _sensing.SightRange),
            BallMemorySeconds = _sensing.BallMemorySeconds * (1 + .6 * (Attributes(p).Vision - RefVision)) };
    private long AttributeDecisionTicks(Player p) => !HasAttributes(p) ? _decisionTicks :
        Math.Max(1, (long)Math.Round(SensingFor(p).ScanIntervalSeconds / _config.StepSeconds));

    private bool DelayBallReaction(Player p, Perception view)
    {
        if (!HasAttributes(p) || !_config.AttributeSettings.Anticipation || view.HasBall || view.VisibleBall is not { } ball) return false;
        if (ball.Vel.Length < 4) { p.ReactionVelocity = ball.Vel; return false; }
        if ((ball.Vel - p.ReactionVelocity).Length <= 6) return false;
        p.ReactionVelocity = ball.Vel;
        double delay = .7 * Math.Max(0, RefAnticipation - Attributes(p).Anticipation);
        // Quantize like the scan interval: a tiny deficit must not always cost a full tick.
        long ticks = (long)Math.Round(delay / _config.StepSeconds);
        if (ticks <= 0) return false;
        p.NextDecisionTick = _tick + ticks;
        return true;
    }

    private bool ScatterFirstTouch(Player p, BallContact contact, double relativeSpeed)
    {
        if (!HasAttributes(p) || !_config.AttributeSettings.FirstTouch || p.IsGoalkeeper || relativeSpeed <= 9) return false;
        double skill = Attributes(p).FirstTouch;
        double pressure = p.View?.VisiblePlayers.Count(x => x.Team != p.Team && (x.Pos - p.Body.Pos).Length < 4) ?? 0;
        double spread = Math.Max(0, RefFirstTouch - skill) * relativeSpeed * .25 * (1 + .2 * pressure);
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
            double deficit = Math.Max(0, RefPositioning - a.Positioning);
            var target = view.FormationTarget;
            var centre = p.DefensiveCentre * (view.AttacksPositiveX ? 1 : -1);
            target += (target - centre) * (.14 * deficit + .45 * Overload(p));
            if (view.KnownBall is { } known) target += (known.Ball.Pos.XY - target) * (.18 * deficit);
            view = view with { FormationTarget = ClampToPitch(target), MarkGapExtra = 4 * deficit,
                CognitiveLoad = Overload(p) };
        }
        return view with { ChoiceTemperature = _config.AttributeSettings.Decisions ?   .35 * Math.Max(0, RefDecisions - a.Decisions) + .4 * Overload(p) : 0,
            ChoiceNoise = p.AttributeRng, PassOptionLimit = _config.AttributeSettings.Vision ? 8 + (int)Math.Round(10 * a.Vision) : int.MaxValue };
    }

    private PassConfig PassingFor(Player p)
    {
        if (!HasAttributes(p) || !_config.AttributeSettings.Passing) return _config.PassSettings;
        double spread = 1 + 2 * Overload(p);
        return _config.PassSettings with { Skill = Math.Clamp(_config.PassSettings.Skill + .35 * (Attributes(p).Passing - RefPassing), 0, 1),
            AngleErrorRadians = _config.PassSettings.AngleErrorRadians * spread,
            SpeedErrorFraction = Math.Min(.9, _config.PassSettings.SpeedErrorFraction * spread) };
    }

    private double FinishingFor(Player p) => !HasAttributes(p) || !_config.AttributeSettings.Finishing
        ? _config.PassSettings.Skill
        // Squad finishing has its own execution anchor. Pass accuracy is not shot accuracy;
        // the neutral/disabled path deliberately retains the historical pass-skill anchor.
        : Math.Clamp(_config.PassSettings.Skill - .24 + 2.0 * (Attributes(p).Finishing - RefFinishing), 0, 1);

    private BallControlConfig ControlFor(PlayerSnapshot snapshot, BallControlConfig limits)
    {
        if (!_hasSquadPlayers || !_squadPlayers.TryGetValue((snapshot.Team, snapshot.Id), out var p)) return limits;
        if (!HasAttributes(p)) return limits;
        if (snapshot.IsKeeper && p.SquadPlayer?.Keeper is { } k && _config.AttributeSettings.Keeper && ReferenceEquals(limits, _keeperControl))
            return limits with { ControlRadius = limits.ControlRadius * KeeperFactor(k.Positioning),
                MaxRelativeSpeed = limits.MaxRelativeSpeed * KeeperFactor(k.Reflexes) };
        if (!_config.AttributeSettings.FirstTouch || ReferenceEquals(limits, _shieldedControl)) return limits;
        double skill = Attributes(p).FirstTouch;
        // First-touch errors change physical reach and the speed a player can cushion; no success roll.
        return limits with { ControlRadius = limits.ControlRadius * (1 + .3 * (skill - RefFirstTouch)),
            MaxRelativeSpeed = limits.MaxRelativeSpeed * (1 + .3 * (skill - RefFirstTouch)) };
    }
}
