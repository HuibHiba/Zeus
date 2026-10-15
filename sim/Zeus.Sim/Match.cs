namespace Zeus.Sim;

public sealed record MatchConfig(
    double StepSeconds = 0.1,
    PlayerProfile? Profile = null,
    double MaxReactionSeconds = 0.3,
    BallConfig? Ball = null,
    // Weight of the tactic's volg_bal factor: rel target += volg * (ball_rel - 0.5) * gain.
    // First agreement, not calibrated (0.5: volg_bal = 1 shifts a target at most a quarter of the pitch); tune in phase 1c.
    double BallFollowGain = 0.5,
    double PossessionRadius = 1.0,     // m: player must be this close to the ball
    double PossessionMaxHeight = 0.3,  // m: ball must be lower than this
    double PossessionMaxSpeed = 4.0,   // m/s: ball must be slower than this (relative to the carrier while carried)
    double CarryDistance = 0.4,        // m: a carried ball lies this far in front of the carrier's feet
    PerceptionConfig? Perception = null,
    PassConfig? Passing = null,
    BallControlConfig? Control = null,
    DuelConfig? Duels = null,
    double RestartPauseSeconds = 3,
    double HalfSeconds = 2700,
    BallControlConfig? KeeperControl = null,
    string RuleEdition = Referee.RuleEdition,
    bool EnableFatigue = true,
    double Endurance = .5,
    FatigueConfig? Fatigue = null,
    bool EnableTeamPhases = true,
    double TransitionSeconds = 3,
    double AttackThirdThreshold = .66,
    bool EnableBlockSwitches = true,
    // Control limits for field players of the team that did not make the last touch (interception of a ball in flight).
    // A fast pass is hard to stop for a defender who is not the intended receiver (no anticipation, no first-touch setup).
    // Default: same radius/height as the field control but MaxRelativeSpeed 9 m/s instead of 18; calibrated in docs/kalibratie.md.
    BallControlConfig? InterceptControl = null,
    // An opponent cannot touch a pass/shot until it has travelled this far from the kicker's foot: the ball leaves a
    // marking defender's reach at once (without it a presser next to the passer took 85% of passes in the baseline).
    double KickShieldMeters = 2.5,
    ShotConfig? Shots = null,
    // Tactic triggers issue temporary instructions in their condition's phase; see TriggerRules.
    bool EnableTriggers = true,
    TriggerConfig? Triggers = null,
    PenaltyConfig? Penalties = null,
    // A sent-off player walks off to this distance beyond the nearest touchline at this speed (m/s, a brisk walk) and then stays
    // there, outside the lines: he no longer takes part in play, perception, offside or pressing (docs/scheidsrechter-regels.md).
    double SentOffWalkSpeed = 2.0,
    double SentOffExitMeters = 2.5,
    AttackingRunConfig? AttackingRuns = null,
    InstructionConfig? Instructions = null,
    AttributeConfig? Attributes = null,
    // Maximum substitutions per team (Match.Substitute); the squad has seven reserves, so values above 7 are never reachable.
    int MaxSubstitutions = 5,
    bool EnablePaths = true,
    bool EnableFamiliarity = false)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public AttributeConfig AttributeSettings => Attributes ?? AttributeConfig.Default;
    [System.Text.Json.Serialization.JsonIgnore]
    public PlayerProfile PlayerProfile => Profile ?? PlayerProfile.Default;
    [System.Text.Json.Serialization.JsonIgnore]
    public BallConfig BallPhysics => Ball ?? BallConfig.Default;
    [System.Text.Json.Serialization.JsonIgnore]
    public PerceptionConfig Sensing => Perception ?? new PerceptionConfig();
    [System.Text.Json.Serialization.JsonIgnore]
    public PassConfig PassSettings => Passing ?? new PassConfig();
    [System.Text.Json.Serialization.JsonIgnore]
    public BallControlConfig InterceptSettings => InterceptControl ?? new BallControlConfig(0.6, ControlSettings.MaxHeight, 9.0);
    [System.Text.Json.Serialization.JsonIgnore]
    public BallControlConfig ControlSettings => Control ?? new BallControlConfig(PossessionRadius, PossessionMaxHeight);
    // Keeper reach (diving/stretch) inside his own penalty area; calibrated on the goal rate per shot distance (docs/kalibratie.md).
    [System.Text.Json.Serialization.JsonIgnore]
    public BallControlConfig KeeperSettings => KeeperControl ?? new BallControlConfig(2.5, 2.2, 35);
    [System.Text.Json.Serialization.JsonIgnore]
    public ShotConfig ShotSettings => Shots ?? new ShotConfig();
    [System.Text.Json.Serialization.JsonIgnore]
    public TriggerConfig TriggerSettings => Triggers ?? new TriggerConfig();
    [System.Text.Json.Serialization.JsonIgnore]
    public PenaltyConfig PenaltySettings => Penalties ?? PenaltyConfig.Default;
    [System.Text.Json.Serialization.JsonIgnore]
    public DuelConfig DuelSettings => Duels ?? new DuelConfig();
    [System.Text.Json.Serialization.JsonIgnore]
    public FatigueConfig FatigueSettings => Fatigue ?? FatigueConfig.Default;
    [System.Text.Json.Serialization.JsonIgnore]
    public AttackingRunConfig AttackingRunSettings => AttackingRuns ?? new AttackingRunConfig();
    [System.Text.Json.Serialization.JsonIgnore]
    public InstructionConfig InstructionSettings => Instructions ?? new InstructionConfig();
}

public enum MatchStatus { Running, Restart, Abandoned, Finished }

public sealed record PlayerSnapshot(Side Team, string Id, Vec2 Pos, Vec2 Vel, double Heading,
    int YellowCards = 0, bool SentOff = false, bool IsGoalkeeper = false,
    double ShortEnergy = 1, double LongEnergy = 1)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsKeeper => IsGoalkeeper || Id == "GK";
}

public sealed record BallSnapshot(Vec3 Pos, Vec3 Vel);

/// <summary>Immutable view of the match: tick, clock, 22 players (home first, then away) and the ball.</summary>
public sealed record Snapshot(long Tick, double TimeSeconds, IReadOnlyList<PlayerSnapshot> Players, BallSnapshot? Ball = null,
    MatchStatus Status = MatchStatus.Running, int HomeScore = 0, int AwayScore = 0,
    int Period = 1, bool EndsSwapped = false)
{
    public bool Equals(Snapshot? other) =>
        other is not null && Tick == other.Tick && TimeSeconds == other.TimeSeconds && Players.SequenceEqual(other.Players)
        && Ball == other.Ball && Status == other.Status && HomeScore == other.HomeScore && AwayScore == other.AwayScore
        && Period == other.Period && EndsSwapped == other.EndsSwapped;

    public override int GetHashCode()
    {
        var h = new HashCode();
        h.Add(Tick);
        foreach (var p in Players) h.Add(p);
        h.Add(Ball);
        h.Add(Status);
        h.Add(HomeScore); h.Add(AwayScore);
        h.Add(Period); h.Add(EndsSwapped);
        return h.ToHashCode();
    }
}

public sealed partial class Match
{
    private sealed class Player
    {
        public Side Team;
        public string Id = "";
        public string SquadId = "";        // identity of the player now in this slot (starter: the slot id; substitute: B1..B7)
        public int Substitutions;
        public SquadPlayer? SquadPlayer;
        public PlayerAttributes? Effective;
        public Rng? AttributeRng;
        public Vec3 ReactionVelocity;
        public List<(long At, long Until, string Instruction)> PendingTriggers = new();
        public Vec2 Target;
        public BodyState Body;
        public long StartTick;
        public bool Started;
        public int YellowCards;
        public bool SentOff;
        public bool IsGoalkeeper;
        public FatigueState Energy = FatigueState.Fresh;
        public Tactic Tactic = null!;
        public IPlayerBrain Brain = null!;
        public Perception? View;
        public Intent? Intent;
        public long NextDecisionTick;
        public long PressUntilTick;        // a fired trigger keeps this player pressing until this tick
        public Dictionary<string, long> TriggerInstructions = new(StringComparer.Ordinal);
        public long RunUntilTick;
        public long NextRunTick;
        public double DefensiveLine;
        public Vec2 DefensiveCentre;
        public GamePhase? PathPhase;
        public bool PathEndsSwapped;
        public PathProgress? Path;
        public PhasePlayer Build = null!;   // 'opbouw' entry (team has the ball)
        public PhasePlayer Defend = null!;  // 'verdedigen' entry (block variant if the tactic has them)
    }

    private readonly MatchConfig _config;
    private readonly InstructionConfig _instructions;
    private readonly bool _enableInstructions;
    private readonly Rng _rng;
    private readonly Player[] _players;
    private readonly EventLog _events = new();
    private readonly PerceptionConfig _sensing;
    private readonly long _decisionTicks;
    private long _tick;
    private BallState _ball = BallState.AtRest;
    private int _carrier = -1;           // index of the ball carrier, -1 = loose ball
    private Side _possession = Side.Home; // team with the ball (last holder while the ball is loose)
    private long _controlSinceTick;
    private int _lastPasser = -1;
    private int _passReceiver = -1;      // intended receiver of the pass in flight (-1 = none)
    private Vec2 _passTarget;
    private Vec2? _kickOrigin;           // where the last pass/shot left the foot; opponents cannot touch it within KickShieldMeters
    private long _passerExcludedUntilTick;
    private long _nextDuelTick;
    private MatchStatus _status;
    private long _restartUntilTick;
    private int _restartTaker;
    private string _restartReason = "";
    private Side _lastTouch = Side.Home;
    private int _homeScore, _awayScore;
    private IReadOnlyList<string> _offsideCandidates = Array.Empty<string>();
    private Side _offsideTeam;
    private int _restartKickPlayer = -1;
    private string? _uninterruptedRestart;
    private Side _uninterruptedRestartTeam;
    private int _period = 1;
    private bool _endsSwapped;
    private long _periodStartTick, _periodLostTicks;
    private readonly BallControlConfig _keeperControl;
    private readonly BallControlConfig _fieldControl;
    private readonly BallControlConfig _interceptControl;
    private readonly BallControlConfig _penaltyKeeperControl;
    private static readonly BallControlConfig _shieldedControl = new(1.0, 0.3, 0.0); // cannot control: ball too close to the kick
    private Side? _lastShotTeam;
    private readonly TeamPhases _phases;
    private BlockHeight? _homeBlock, _awayBlock;

    private Match(MatchConfig config, Rng rng, Player[] players)
    {
        _config = config;
        _hasPaths = config.EnablePaths && players.Any(p => HasPaths(p.Tactic));
        _instructions = config.InstructionSettings;
        _enableInstructions = _instructions != InstructionConfig.Disabled;
        _rng = rng;
        _players = players;
        _phases = new TeamPhases(Side.Home, config.TransitionSeconds, config.AttackThirdThreshold);
        _homeBlock = players.First(p => p.Team == Side.Home).Tactic.Phase(GamePhase.Verdedigen).Block;
        _awayBlock = players.First(p => p.Team == Side.Away).Tactic.Phase(GamePhase.Verdedigen).Block;
        foreach (var p in players) UpdateDefensiveGeometry(p);
        _sensing = config.Sensing;
        _fieldControl = config.ControlSettings;
        _interceptControl = config.InterceptSettings;
        _interceptControl.Validate();
        _keeperControl = config.KeeperSettings;
        _keeperControl.Validate();
        _penaltyKeeperControl = _keeperControl with { ControlRadius = config.PenaltySettings.KeeperReachMeters };
        _decisionTicks = Math.Max(1, (long)Math.Ceiling(_sensing.ScanIntervalSeconds / config.StepSeconds - 1e-9));
    }

    public static Match Create(MatchConfig config, Tactic home, Tactic away, ulong seed,
        Func<Side, string, IPlayerBrain>? brainFactory = null, Squad? homeSquad = null, Squad? awaySquad = null,
        FamiliarityState? homeFamiliarity = null, FamiliarityState? awayFamiliarity = null)
    {
        if (!double.IsFinite(config.StepSeconds) || config.StepSeconds <= 0)
            throw new ArgumentException("StepSeconds must be finite and > 0", nameof(config));
        if (config.RuleEdition != Referee.RuleEdition)
            throw new ArgumentException("Unsupported rule edition: " + config.RuleEdition);
        ValidatePaths(home);
        ValidatePaths(away);
        config.Sensing.Validate();
        config.PassSettings.Validate();
        config.AttackingRunSettings.Validate();
        config.InstructionSettings.Validate();
        config.ControlSettings.Validate();
        config.DuelSettings.Validate();
        config.ShotSettings.Validate();
        config.TriggerSettings.Validate();
        config.PenaltySettings.Validate();
        if (!double.IsFinite(config.SentOffWalkSpeed) || config.SentOffWalkSpeed <= 0 || config.SentOffWalkSpeed > 10
            || !double.IsFinite(config.SentOffExitMeters) || config.SentOffExitMeters < 0.5 || config.SentOffExitMeters > 20)
            throw new ArgumentException("Invalid sent-off exit settings.");
        if (config.MaxSubstitutions < 0 || config.MaxSubstitutions > 7) throw new ArgumentException("MaxSubstitutions must be in 0..7.");
        config.FatigueSettings.Validate();
        if (!double.IsFinite(config.Endurance) || config.Endurance < 0 || config.Endurance > 1)
            throw new ArgumentException("Endurance must be in [0,1].");
        if (!double.IsFinite(config.RestartPauseSeconds) || config.RestartPauseSeconds < 0)
            throw new ArgumentException("Restart pause must be finite and nonnegative.");
        if (!double.IsFinite(config.HalfSeconds) || config.HalfSeconds <= 0)
            throw new ArgumentException("Half duration must be finite and positive.");
        if (homeSquad is not null) SquadLoader.Validate(homeSquad, home);
        if (awaySquad is not null) SquadLoader.Validate(awaySquad, away);
        var players = new List<Player>(22);
        // Start in a legal kick-off line-up (own half) from the base phase positions (home: opbouw, away: verdedigen).
        foreach (var p in home.Phase(GamePhase.Opbouw).Players) players.Add(MakePlayer(Side.Home, p, home));
        foreach (var p in away.Phase(GamePhase.Verdedigen).Players) players.Add(MakePlayer(Side.Away, p, away));
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            p.SquadPlayer = (p.Team == Side.Home ? homeSquad : awaySquad)?.Starters.First(x => x.Id == p.Id);
            if (p.SquadPlayer is not null && !ReferenceEquals(p.SquadPlayer.Attributes, PlayerAttributes.Neutral))
            {
                p.AttributeRng = new Rng(seed, (ulong)(1000 + i));
                p.Effective = EffectiveOf(p.SquadPlayer.Attributes);
            }
        }
        foreach (var p in players) p.Brain = brainFactory?.Invoke(p.Team, p.Id) ?? new PassingBrain(config.PassSettings, config.ShotSettings, config.AttackingRunSettings, config.InstructionSettings);
        var match = new Match(config, new Rng(seed), players.ToArray());
        match._squadPlayers = players.Where(p => p.AttributeRng is not null).ToDictionary(p => (p.Team, p.Id));
        match._hasSquadPlayers = match._squadPlayers.Count != 0;
        match._seed = seed;
        match.InitSubstitutions(homeSquad ?? Squad.NeutralFor(home), awaySquad ?? Squad.NeutralFor(away));
        match.InitFamiliarity(homeFamiliarity, awayFamiliarity);
        match.Init();
        return match;
    }

    private static Player MakePlayer(Side side, PhasePlayer start, Tactic tactic)
    {
        var build = tactic.Phase(GamePhase.Opbouw).Players.First(x => x.Id == start.Id);
        var defend = DefendLayout(tactic).Players.First(x => x.Id == start.Id);
        return new Player
        {
            Team = side,
            Tactic = tactic,
            Id = start.Id,
            SquadId = start.Id,
            IsGoalkeeper = start.Id == "GK",
            Build = build,
            Defend = defend,
            Target = Pitch.ToMeters(start.X, start.Y, side),
            Body = new BodyState(Pitch.ToMeters(Math.Min(start.X, 0.5), start.Y, side), Vec2.Zero, side == Side.Home ? 0.0 : Math.PI),
        };
    }

    // Defending layout: the block variant when the tactic has block switches and variants, else plain 'verdedigen'.
    // Block switches on score/minute/fatigue are not evaluated yet (later task): the base block is used.
    private static PhaseLayout DefendLayout(Tactic t)
    {
        var basePhase = t.Phase(GamePhase.Verdedigen);
        if (t.BlockSwitches.Count > 0 && basePhase.Block is { } b && t.DefendVariants.TryGetValue(b, out var v)
            && v.Players.Count == basePhase.Players.Count)
            return v;
        return basePhase;
    }

    private void Init()
    {
        _events.Add(new Event(EventType.KickOff, Side.Home, "", 0, 0.0, Vec2.Zero));
        // Fixed order: home then away, by player index; one Rng draw each.
        foreach (var p in _players)
        {
            double delay = _rng.NextDouble() * _config.MaxReactionSeconds;
            p.StartTick = (long)Math.Ceiling(delay / _config.StepSeconds - 1e-9);
        }
        // Kick-off: ball on the centre spot; the home player nearest to it has possession.
        _ball = BallState.AtRest;
        _possession = Side.Home;
        double best = double.MaxValue;
        for (int i = 0; i < _players.Length; i++)
        {
            if (_players[i].Team != Side.Home) continue;
            double d = _players[i].Body.Pos.Length;
            if (d < best) { best = d; _carrier = i; }
        }
        // The kick-off taker starts at the ball, rather than claiming it from a distant formation position.
        _players[_carrier].Body = _players[_carrier].Body with { Pos = Vec2.Zero };
        StartReady();
    }

    // Sent-off players walk in a straight line to a spot outside the nearest touchline and stay there (also during restarts).
    private void WalkSentOffPlayersOff()
    {
        foreach (var p in _players)
        {
            if (!p.SentOff) continue;
            double side = p.Body.Pos.Y >= 0 ? 1 : -1;
            var exit = new Vec2(Math.Clamp(p.Body.Pos.X, -Pitch.HalfLength, Pitch.HalfLength),
                side * (Pitch.HalfWidth + _config.SentOffExitMeters));
            var toExit = exit - p.Body.Pos;
            double distance = toExit.Length;
            double stepLength = _config.SentOffWalkSpeed * _config.StepSeconds;
            if (distance <= 1e-9) { p.Body = p.Body with { Vel = Vec2.Zero }; continue; }
            if (distance <= stepLength) { p.Body = p.Body with { Pos = exit, Vel = Vec2.Zero }; continue; }
            var direction = toExit * (1.0 / distance);
            p.Body = new BodyState(p.Body.Pos + direction * stepLength, direction * _config.SentOffWalkSpeed,
                Math.Atan2(direction.Y, direction.X));
        }
    }

    private void StartReady()
    {
        foreach (var p in _players)
        {
            if (p.Started || _tick < p.StartTick) continue;
            p.Started = true;
            _events.Add(new Event(EventType.MoveStart, p.Team, p.Id, _tick, TimeSeconds, p.Body.Pos));
        }
    }

    public long Tick => _tick;
    public double TimeSeconds => _tick * _config.StepSeconds;
    public EventLog Events => _events;
    public MatchStatus Status => _status;
    public int HomeScore => _homeScore;
    public int AwayScore => _awayScore;
    public int Period => _period;
    public bool EndsSwapped => _endsSwapped;

    /// <summary>Ball state (position and velocity in metres, m/s).</summary>
    public BallState Ball => _ball;

    /// <summary>Team that has the ball (the last holder while the ball is loose).</summary>
    public Side Possession => _possession;

    /// <summary>Id of the ball carrier, or null while the ball is loose.</summary>
    public string? CarrierId => _carrier >= 0 ? _players[_carrier].Id : null;

    /// <summary>Current phase, including configurable possession transitions and stoppages.</summary>
    public GamePhase PhaseOf(Side team) => _config.EnableTeamPhases ? _phases.PhaseOf(team)
        : _status == MatchStatus.Restart ? GamePhase.Stilstaand
        : team == _possession ? GamePhase.Opbouw : GamePhase.Verdedigen;

    /// <summary>Current target of a player in metres (null if unknown).</summary>
    public Vec2? TargetOf(Side team, string id) => _players.FirstOrDefault(p => p.Team == team && p.Id == id)?.Target;

    public Perception? PerceptionOf(Side team, string id) => _players.FirstOrDefault(p => p.Team == team && p.Id == id)?.View;
    public Intent? IntentOf(Side team, string id) => _players.FirstOrDefault(p => p.Team == team && p.Id == id)?.Intent;

    /// <summary>Puts the ball somewhere with a velocity (kick, test set-up). Possession is re-evaluated in the next Step.</summary>
    public void SetBall(Vec3 pos, Vec3 vel)
    {
        _ball = new BallState(pos, vel);
        _carrier = -1;
        _offsideCandidates = Array.Empty<string>();
    }

    /// <summary>Advances one fixed step: targets, player movement, ball physics, possession.</summary>
    public void Step()
    {
        if (_status is MatchStatus.Abandoned or MatchStatus.Finished) return;
        CheckPeriodEnd();
        UpdatePhases();
        UpdateBlocks();
        if (_status == MatchStatus.Finished) return;
        StartReady();
        WalkSentOffPlayersOff();
        if (_status == MatchStatus.Restart)
        {
            if (_tick < _restartUntilTick) { UpdateFatigue(resting: true); _periodLostTicks++; _tick++; return; }
            _status = MatchStatus.Running;
            _carrier = _restartTaker;
            _controlSinceTick = _tick;
            _events.Add(new Event(EventType.RestartTaken, _possession, _players[_carrier].Id,
                _tick, TimeSeconds, _ball.Pos.XY));
            TakeRestartKick();
            UpdatePhases();
        }
        // Validate control before moving: the ball and its carrier belong to the same snapshot.
        if (_carrier >= 0)
        {
            var carrier = _players[_carrier];
            var velocity = new Vec3(carrier.Body.Vel.X, carrier.Body.Vel.Y, 0);
            if (!CanControl(carrier, _ball.Vel - velocity)) _carrier = -1;
        }
        // Every decision reads one pre-movement snapshot, independent of player iteration order.
        foreach (var p in _players)
            if (p.Intent?.Kind == IntentKind.RunInBehind && !CanRunInBehind(p))
                p.NextDecisionTick = _tick;
        ApplyAttributeTriggers();
        ApplyFamiliarityTriggers();
        Snapshot? world = _players.Any(p => p.Started && !p.SentOff && _tick >= p.NextDecisionTick) ? GetSnapshot() : null;
        for (int i = 0; i < _players.Length; i++)
        {
            var p = _players[i];
            if (!p.Started || p.SentOff || p.Path is not null || _tick < p.NextDecisionTick) continue;
            if (p.PressUntilTick > 0 && _carrier >= 0 && p.Team == _possession) p.PressUntilTick = 0; // his team has the ball again
            var view = PerceptionBuilder.Build(world!, world!.Players[i], PhaseOf(p.Team), _carrier == i,
                SensingFor(p), _config.StepSeconds, p.View?.KnownBall);
            if (DelayBallReaction(p, view)) continue;
            var layout = LayoutFor(p);
            if (_enableInstructions) view = view with { Instructions = p.TriggerInstructions.Count == 0 ? layout.Instructions
                    : layout.Instructions.Concat(p.TriggerInstructions.Where(a => _tick < a.Value).Select(a => a.Key)).Distinct().ToArray(),
                MarkId = layout.Mark, PressingForm = p.Tactic.Team.PressingForm, Tempo = p.Tactic.Team.Tempo,
                AttackPositiveX = OwnGoalSign(p.Team) < 0 };
            view = view with { FormationTarget = TargetFor(p, BallRel(p.Team, view.KnownBall?.Ball.Pos)),
                HeldBallSeconds = _carrier == i ? (_tick - _controlSinceTick) * _config.StepSeconds : 0,
                BallIsLoose = _carrier < 0,
                IsPresser = (_carrier >= 0 && p.Team != _possession && PressingAssignment.IsPresser(view))
                    || (_tick < p.PressUntilTick && !p.IsGoalkeeper && view.VisibleBall is not null),
                AttackPositiveX = OwnGoalSign(p.Team) < 0,
                ReceiveTarget = _carrier < 0 && _passReceiver == i ? _passTarget : null,
                TeamPassInFlight = _carrier < 0 && _passReceiver >= 0 && _players[_passReceiver].Team == p.Team,
                CanRunInBehind = CanRunInBehind(p),
                TeamBallCarrierId = _carrier >= 0 && _players[_carrier].Team == p.Team ? _players[_carrier].Id : null,
                LineMisjudgement = LineMisjudgement() };
            view = FamiliarityView(p, view);
            if (_enableInstructions)
            {
                var centre = p.DefensiveCentre * (view.AttacksPositiveX ? 1 : -1);
                view = view with { FormationTarget = InstructionRules.Target(view, layout, DefensivePlayer(p), p.Tactic.Team,
                    p.DefensiveLine, centre, _instructions) };
                bool presser = view.IsPresser;
                if (_instructions.EnablePressingForm && view.PressingForm == "man")
                    presser = view.IsPresser || InstructionRules.MarkedOpponent(view, _instructions) is { } mark
                        && view.VisibleBall is { } seen && (mark.Pos - seen.Pos.XY).Length < 3;
                view = view with { IsPresser = (_carrier >= 0 && p.Team != _possession && presser)
                    || (!p.IsGoalkeeper && view.VisibleBall is not null && ((_tick < p.PressUntilTick && !RoleFallbackActive(p))
                        || InstructionRules.Has(view, "start_pressing") && p.Team != _possession)) };
            }
            view = AttributeView(p, view);
            var intent = p.Brain.Decide(view);
            if (intent is null || !Enum.IsDefined(intent.Kind)
                || !double.IsFinite(intent.Target.X) || !double.IsFinite(intent.Target.Y)
                || (intent.FacingTarget is { } facingPoint && (!double.IsFinite(facingPoint.X) || !double.IsFinite(facingPoint.Y))))
                throw new InvalidOperationException($"Invalid intent for {p.Team}/{p.Id}.");
            p.View = view;
            if (intent.Kind == IntentKind.RunInBehind && p.Intent?.Kind != IntentKind.RunInBehind)
            {
                p.RunUntilTick = _tick + (long)Math.Ceiling(_config.AttackingRunSettings.RunSeconds / _config.StepSeconds);
                p.NextRunTick = _tick + (long)Math.Ceiling(_config.AttackingRunSettings.RunCooldownSeconds / _config.StepSeconds);
            }
            p.Intent = intent with { Target = new Vec2(Math.Clamp(intent.Target.X, -Pitch.HalfLength, Pitch.HalfLength),
                Math.Clamp(intent.Target.Y, -Pitch.HalfWidth, Pitch.HalfWidth)) };
            p.Target = p.Intent.Kind is IntentKind.Pass or IntentKind.Scan or IntentKind.Shoot or IntentKind.Clear ? p.Body.Pos : p.Intent.Target;
            p.NextDecisionTick = _tick + AttributeDecisionTicks(p) + DecisionFatigueTicks(p);
        }
        UpdatePathTargets();
        ExecutePass();
        var beforeMovement = _carrier < 0 ? GetSnapshot().Players : null;
        foreach (var p in _players)
        {
            if (!p.Started || p.SentOff) continue;
            var profile = EffectiveProfile(p);
            if (p.Path is { } path)
                profile = profile with { MaxSpeed = Math.Min(profile.MaxSpeed, path.Layout.PathSpeed switch
                    { "wandelen" => 2, "joggen" => 4, _ => profile.MaxSpeed }) };
            var b = Mover.Step(p.Body, p.Target, profile, _config.StepSeconds, p.Intent?.Kind == IntentKind.KeeperGuard);
            p.Body = b with { Pos = new Vec2(
                Math.Clamp(b.Pos.X, -Pitch.HalfLength, Pitch.HalfLength),
                Math.Clamp(b.Pos.Y, -Pitch.HalfWidth, Pitch.HalfWidth)) };
            if (p.Intent?.Kind == IntentKind.Scan)
                p.Body = p.Body with { Heading = Mover.NormalizeAngle(p.Body.Heading + profile.TurnRate * _config.StepSeconds * 0.4) };
            if (p.Intent?.Kind is IntentKind.Pass or IntentKind.Shoot or IntentKind.Clear)
            {
                var aim = p.Intent.Target - p.Body.Pos;
                double turn = Mover.NormalizeAngle(Math.Atan2(aim.Y, aim.X) - p.Body.Heading);
                p.Body = p.Body with { Heading = Mover.NormalizeAngle(p.Body.Heading + Math.Clamp(turn,
                    -profile.TurnRate * _config.StepSeconds, profile.TurnRate * _config.StepSeconds)) };
            }
            if (p.Intent?.FacingTarget is { } facing)
            {
                var offset = facing - p.Body.Pos;
                double turn = Mover.NormalizeAngle(Math.Atan2(offset.Y, offset.X) - p.Body.Heading);
                p.Body = p.Body with { Heading = Mover.NormalizeAngle(p.Body.Heading + Math.Clamp(turn,
                    -profile.TurnRate * _config.StepSeconds, profile.TurnRate * _config.StepSeconds)) };
            }
        }
        UpdateFatigue();
        ResolveDuel();
        UpdatePhases();
        if (_status == MatchStatus.Restart) { _periodLostTicks++; _tick++; return; }
        UpdateBall(beforeMovement);
        CheckOutOfBounds();
        if (_status == MatchStatus.Restart) _periodLostTicks++;
        _tick++;
        CheckPeriodEnd(); // Record the whistle at the final completed tick, so replay Steps is exact.
        UpdatePhases();
    }

    private bool CanRunInBehind(Player player)
    {
        if (!_config.AttackingRunSettings.EnableRuns
            || (_tick < player.NextRunTick && !(player.Intent?.Kind == IntentKind.RunInBehind && _tick < player.RunUntilTick))
            || _carrier < 0 || _players[_carrier].Team != player.Team) return false;
        // Team instruction, based on roles and identities only; no hidden opponent positions enter the brain.
        if (_instructions.EnableStayBack && LayoutFor(player).Instructions.Contains("blijf_achter")) return false;
        var runners = _players.Where(p => p.Team == player.Team && !p.SentOff && !p.IsGoalkeeper && p != _players[_carrier]
                && AttackingRuns.IsAttackingRole(p.Tactic.Roster.First(r => r.Id == p.Id).Role)
                && !(_instructions.EnableStayBack && LayoutFor(p).Instructions.Contains("blijf_achter")))
            .OrderBy(p => p.Tactic.Roster.First(r => r.Id == p.Id).Role == "buitenspeler" ? 1 : 0)
            .ThenBy(p => p.Id, StringComparer.Ordinal).Take(2);
        return runners.Contains(player);
    }

    private double LineMisjudgement()
    {
        var settings = _config.AttackingRunSettings;
        if ((!settings.EnableRuns && !settings.EnableThroughBalls) || settings.LineMisjudgementMeters == 0) return 0;
        // Box-Muller: two draws per decision, in the fixed player order, without a cached spare draw.
        double radius = Math.Sqrt(-2 * Math.Log(1 - _rng.NextDouble()));
        return radius * Math.Cos(2 * Math.PI * _rng.NextDouble()) * settings.LineMisjudgementMeters;
    }

    // Ball position in the attack direction of a team: x 0 = own goal line, 1 = opponent goal line; y 0 = left, 1 = right.
    private (double X, double Y) BallRel(Side side, Vec3? ball)
    {
        if (ball is null) return (0.5, 0.5); // No remembered position: fall back to the base formation.
        return TriggerRules.RelativePosition(ball.Value.XY, side, _endsSwapped);
    }

    private void UpdateDefensiveGeometry(Player p)
    {
        var layout = BlockOf(p.Team) is { } block && p.Tactic.DefendVariants.TryGetValue(block, out var variant)
            ? variant : p.Tactic.Phase(GamePhase.Verdedigen);
        var field = layout.Players.Where(a => a.Id != "GK").ToArray();
        p.DefensiveLine = field.Min(a => a.X);
        p.DefensiveCentre = Pitch.ToMeters(field.Average(a => a.X), field.Average(a => a.Y), Side.Home);
    }

    private PhasePlayer LayoutFor(Player p)
    {
        var phase = PhaseOf(p.Team);
        var layout = !_config.EnableTeamPhases ? (p.Team == _possession ? p.Build : p.Defend)
            : phase == GamePhase.Verdedigen ? DefensivePlayer(p)
            : p.Tactic.Phase(phase).Players.First(x => x.Id == p.Id);
        return p.IsGoalkeeper && p.Id != "GK"
            ? p.Tactic.Phase(phase).Players.First(x => x.Id == "GK") : layout;
    }

    private Vec2 TargetFor(Player p, (double X, double Y) ballRel)
    {
        var layout = LayoutFor(p);
        double x = layout.X, y = layout.Y;
        if (layout.FollowBall is { } f)
        {
            x += f.X * (ballRel.X - 0.5) * _config.BallFollowGain;
            y += f.Y * (ballRel.Y - 0.5) * _config.BallFollowGain;
        }
        return Pitch.ToMeters(Math.Clamp(x, 0, 1), Math.Clamp(y, 0, 1), p.Team, _endsSwapped);
    }

    private Vec2 FeetOf(Player p) =>
        p.Body.Pos + new Vec2(Math.Cos(p.Body.Heading), Math.Sin(p.Body.Heading)) * _config.CarryDistance;

    private void ExecutePass()
    {
        if (_carrier < 0) return;
        var passer = _players[_carrier];
        if (passer.Intent?.Kind is not (IntentKind.Pass or IntentKind.Shoot or IntentKind.Clear) || passer.View is null || !passer.Started) return;
        var intent = passer.Intent;
        // Validate against the actual observed option, never locate an unseen receiver in the world.
        if (intent.Kind == IntentKind.Pass && !passer.View.VisiblePlayers.Any(p => p.Team == passer.Team && p.Id == intent.ReceiverId)) return;
        if (intent.Kind == IntentKind.Pass && intent.CheckOffsideLine)
        {
            // Recheck at the kick: a runner may have crossed the line since the last scan. Only freshly visible
            // positions are used, with the passer's existing line error (no new draw or access from the brain).
            var snapshot = GetSnapshot();
            var fresh = PerceptionBuilder.Build(snapshot, snapshot.Players[_carrier], PhaseOf(passer.Team), true,
                _sensing, _config.StepSeconds) with { AttackPositiveX = OwnGoalSign(passer.Team) < 0,
                    LineMisjudgement = passer.View.LineMisjudgement };
            var receiver = fresh.VisiblePlayers.FirstOrDefault(p => p.Team == passer.Team && p.Id == intent.ReceiverId);
            double dir = fresh.AttacksPositiveX ? 1 : -1;
            if (receiver is null || (receiver.Pos.X * dir > 0 && receiver.Pos.X * dir > FeetOf(passer).X * dir
                && (AttackingRuns.EstimatedLine(fresh) is not { } line || receiver.Pos.X * dir > line)))
            {
                passer.NextDecisionTick = _tick + 1;
                return;
            }
        }
        var direction = intent.Target - passer.Body.Pos;
        if (direction.Length < 1e-9 || Math.Abs(Mover.NormalizeAngle(Math.Atan2(direction.Y, direction.X) - passer.Body.Heading)) > 0.2) return;
        double pressure = passer.View.VisiblePlayers.Count(p => p.Team != passer.Team && (p.Pos - passer.View.Self.Pos).Length < 4);
        var feet = FeetOf(passer);
        _ball = new BallState(new Vec3(feet.X, feet.Y, 0), intent.Kind == IntentKind.Shoot
            ? ShotExecution.Velocity(feet, intent.Target, HasAttributes(passer) && _config.AttributeSettings.Finishing ? Math.Clamp(_config.PassSettings.Skill + .18 * (Attributes(passer).Finishing - RefFinishing), 0, 1) : _config.PassSettings.Skill, _rng, _config.ShotSettings)
            : PassExecution.Velocity(feet, intent.Target, intent.Kind == IntentKind.Clear ? Math.Max(pressure, 3) : pressure, PassingFor(passer), _rng,
                intent.Kind == IntentKind.Clear && _config.PassSettings.ClearStopsAtTarget ? _config.BallPhysics.RollDeceleration : 0));
        _events.Add(new Event(intent.Kind == IntentKind.Shoot ? EventType.Shot : EventType.Pass,
            passer.Team, passer.Id, _tick, TimeSeconds, feet, intent.Kind == IntentKind.Pass
                ? "to " + intent.ReceiverId + " " + Canonical.Num((intent.Target - feet).Length) // + intended length (m), for calibration
                : intent.Kind == IntentKind.Clear ? "clear" : "to " + intent.ReceiverId));
        _lastTouch = passer.Team;
        _lastShotTeam = intent.Kind == IntentKind.Shoot ? passer.Team : null;
        RegisterKick(_carrier, OffsideSource.OpenPlay);
        _uninterruptedRestart = null;
        _lastPasser = _carrier;
        _kickOrigin = feet;
        _passReceiver = intent.Kind == IntentKind.Pass ? Array.FindIndex(_players, q => q.Team == passer.Team && q.Id == intent.ReceiverId) : -1;
        _passTarget = intent.Target;
        if (_passReceiver >= 0 && _players[_passReceiver].IsGoalkeeper)
            FireTriggers(passer.Team == Side.Home ? Side.Away : Side.Home, TriggerRules.BallToKeeper, feet);
        if (_passReceiver >= 0 && TriggerRules.IsBackPassToMidfielder(
            passer.Tactic.Roster.First(r => r.Id == intent.ReceiverId).Role,
            intent.Target - feet, OwnGoalSign(passer.Team) < 0, _config.TriggerSettings))
            FireTriggers(passer.Team == Side.Home ? Side.Away : Side.Home, TriggerRules.BackPassToMidfielder, feet);
        _passerExcludedUntilTick = _tick + Math.Max(1, (long)Math.Ceiling(0.3 / _config.StepSeconds));
        _carrier = -1;
        passer.Intent = new Intent(IntentKind.HoldPosition, passer.Body.Pos);
        foreach (var runner in _players)
        {
            if (runner.Intent?.Kind != IntentKind.RunInBehind) continue;
            runner.Target = runner == (_passReceiver >= 0 ? _players[_passReceiver] : null)
                ? _passTarget : runner.View!.FormationTarget;
            runner.Intent = new Intent(IntentKind.MoveToPosition, runner.Target);
            runner.NextDecisionTick = _tick + 1;
        }
    }

    private void UpdateBall(IReadOnlyList<PlayerSnapshot>? beforeMovement)
    {
        if (_carrier >= 0)
        {
            var oldBall = _ball;
            var c = _players[_carrier];
            var cv = new Vec3(c.Body.Vel.X, c.Body.Vel.Y, 0);
            var f = FeetOf(c);
            _ball = new BallState(new Vec3(f.X, f.Y, 0), cv);
            _lastTouch = c.Team;
            var exit = PitchRules.Evaluate(oldBall, _ball, _lastTouch, _config.BallPhysics.Radius, !_endsSwapped);
            if (exit is not null) HandlePitchExit(exit.Value);
            return;
        }

        var afterMovement = GetSnapshot().Players;
        beforeMovement ??= afterMovement;
        int steps = Math.Max(1, _config.BallPhysics.SubSteps);
        var subConfig = _config.BallPhysics with { SubSteps = 1 };
        BallContact? contact = null;
        for (int step = 0; step < steps; step++)
        {
            var oldBall = _ball;
            _ball = BallPhysics.Step(_ball, subConfig, _config.StepSeconds / steps);
            var from = InterpolatePlayers(beforeMovement, afterMovement, (double)step / steps);
            var to = InterpolatePlayers(beforeMovement, afterMovement, (double)(step + 1) / steps);
            contact = BallControl.FindContact(oldBall, _ball, from, to, _config.ControlSettings,
                _lastPasser >= 0 && _tick < _passerExcludedUntilTick ? _lastPasser : null,
                // Legal hands are not offered after a teammate's deliberate kick/throw in this v0 model.
                p => ControlFor(p, p.IsKeeper && InPenaltyArea(p.Team, oldBall.Pos.XY) && InPenaltyArea(p.Team, _ball.Pos.XY)
                    && _lastTouch != p.Team ? (_uninterruptedRestart == "penalty" ? _penaltyKeeperControl : _keeperControl)
                    : p.Team != _lastTouch ? (_kickOrigin is { } origin && (oldBall.Pos.XY - origin).Length < _config.KickShieldMeters
                        ? _shieldedControl : _interceptControl) : _fieldControl));
            var exit = PitchRules.Evaluate(oldBall, _ball, _lastTouch, _config.BallPhysics.Radius, !_endsSwapped);
            if (exit is not null && (contact is null || exit.Value.Fraction < contact.Value.Fraction))
            {
                HandlePitchExit(exit.Value);
                return;
            }
            if (contact is not null) break;
        }
        if (contact is null) return;
        var n = _players[contact.Value.PlayerIndex];
        if (contact.Value.PlayerIndex == _restartKickPlayer)
        {
            _events.Add(new Event(EventType.DoubleTouch, n.Team, n.Id, _tick, TimeSeconds, contact.Value.Position.XY));
            BeginRestart(n.Team == Side.Home ? Side.Away : Side.Home, ClampToPitch(contact.Value.Position.XY), "indirect_free_kick");
            return;
        }
        if (n.Team == _offsideTeam && _offsideCandidates.Contains(n.Id))
        {
            _events.Add(new Event(EventType.Offside, n.Team, n.Id, _tick, TimeSeconds, contact.Value.Position.XY));
            _offsideCandidates = Array.Empty<string>();
            BeginRestart(n.Team == Side.Home ? Side.Away : Side.Home, ClampToPitch(contact.Value.Position.XY), "indirect_free_kick");
            return;
        }
        _carrier = contact.Value.PlayerIndex;
        bool receivedPass = contact.Value.PlayerIndex == _passReceiver;
        double touchSpeed = (_ball.Vel - new Vec3(n.Body.Vel.X, n.Body.Vel.Y, 0)).Length;
        if (ScatterFirstTouch(n, contact.Value, touchSpeed)) return;
        _kickOrigin = null;
        _passReceiver = -1;
        bool wasSave = n.IsGoalkeeper && _lastShotTeam is { } shootingTeam && shootingTeam != n.Team;
        if (wasSave)
        {
            _events.Add(new Event(EventType.Save, n.Team, n.Id, _tick, TimeSeconds, contact.Value.Position.XY));
            // The keeper pushes some shots round the post instead of holding them: corner for the shooting team.
            if (_config.ShotSettings.ParryCornerRate > 0 && _rng.NextDouble() < _config.ShotSettings.ParryCornerRate)
            {
                var corner = new Vec2(Math.CopySign(Pitch.HalfLength - 0.4, contact.Value.Position.X),
                    Math.CopySign(Pitch.HalfWidth - 0.4, contact.Value.Position.Y == 0 ? 1 : contact.Value.Position.Y));
                _lastShotTeam = null;
                _lastTouch = n.Team;
                _offsideCandidates = Array.Empty<string>();
                _events.Add(new Event(EventType.Out, n.Team, "", _tick, TimeSeconds, corner, nameof(PitchExitKind.Corner)));
                BeginRestart(n.Team == Side.Home ? Side.Away : Side.Home, corner, nameof(PitchExitKind.Corner));
                return;
            }
        }
        _lastShotTeam = null;
        _lastTouch = n.Team;
        _restartKickPlayer = -1;
        _uninterruptedRestart = null;
        if (!wasSave) _offsideCandidates = Array.Empty<string>(); // A deliberate save does not reset Law 11 context.
        _controlSinceTick = _tick;
        _events.Add(new Event(n.Team == _possession ? EventType.BallControl : EventType.Interception,
            n.Team, n.Id, _tick, TimeSeconds, contact.Value.Position.XY));
        if (n.Team != _possession)
        {
            _events.Add(new Event(EventType.PossessionChange, n.Team, n.Id, _tick, TimeSeconds, _ball.Pos.XY, "from " + _possession));
            _possession = n.Team;
            _pendingPossessionTrigger = (n.Team, contact.Value.Position.XY);
        }
        var feet = FeetOf(n);
        _ball = new BallState(new Vec3(feet.X, feet.Y, 0), new Vec3(n.Body.Vel.X, n.Body.Vel.Y, 0));
        if (receivedPass)
        {
            var other = n.Team == Side.Home ? Side.Away : Side.Home;
            var at = contact.Value.Position.XY;
            if (TriggerRules.IsBadTouch(touchSpeed, _config.TriggerSettings)) FireTriggers(other, TriggerRules.BadTouch, at);
            if (TriggerRules.IsOnFlank(at.Y, _config.TriggerSettings)) FireTriggers(other, TriggerRules.OpponentOnFlank, at);
        }
    }

    // Issues the supported temporary orders to named players when condition, phase and zone match.
    private void FireTriggers(Side team, string condition, Vec2 location)
    {
        if (!_config.EnableTriggers) return;
        var tactic = _players.First(p => p.Team == team).Tactic;
        long until = _tick + Math.Max(1, (long)Math.Ceiling(_config.TriggerSettings.PressSeconds / _config.StepSeconds));
        foreach (var trigger in tactic.Triggers)
        {
            if (trigger.Condition != condition || !TriggerRules.IsSupported(trigger) || PhaseOf(team) != trigger.Phase
                || !TriggerRules.IsInZone(trigger.Zone, TriggerRules.RelativePosition(location, team, _endsSwapped), _config.TriggerSettings)) continue;
            foreach (var action in trigger.Then)
            {
                var player = _players.FirstOrDefault(p => p.Team == team && p.Id == action.Player);
                if (player is null || player.SentOff) continue;
                if (HandleFamiliarityTrigger(player, action, until, location, (trigger.Name ?? condition) + ": " + condition)) continue;
                if (action.Path) StartPath(player, "trigger");
                if (player.IsGoalkeeper && !action.Path) continue;
                if (HasAttributes(player) && _config.AttributeSettings.Anticipation)
                {
                    if (player.AttributeRng!.NextDouble() < .3 * Overload(player)) continue;
                    long delay = (long)Math.Ceiling((.1 + Math.Max(0, .6 - Attributes(player).Anticipation) + .5 * Overload(player)) / _config.StepSeconds);
                    player.PendingTriggers.Add((_tick + delay, until + delay, action.Instruction));
                    continue;
                }
                if (action.Instruction == TriggerRules.StartPressing) player.PressUntilTick = Math.Max(player.PressUntilTick, until);
                else if (action.Instruction is { } instruction) player.TriggerInstructions[instruction] = until;
                _events.Add(new Event(EventType.TriggerFired, team, player.Id, _tick, TimeSeconds, location,
                    (trigger.Name ?? condition) + ": " + condition));
            }
        }
    }

    private static PlayerSnapshot[] InterpolatePlayers(IReadOnlyList<PlayerSnapshot> before,
        IReadOnlyList<PlayerSnapshot> after, double fraction) => before.Select((p, i) => p with {
            Pos = p.Pos + (after[i].Pos - p.Pos) * fraction,
            Vel = p.Vel + (after[i].Vel - p.Vel) * fraction
        }).ToArray();

    private bool CanControl(Player p, Vec3 ballVel) =>
        (p.Body.Pos - _ball.Pos.XY).Length <= _config.PossessionRadius
        && _ball.Pos.Z < _config.PossessionMaxHeight
        && ballVel.Length < _config.PossessionMaxSpeed;

    public Snapshot GetSnapshot() =>
        new(_tick, TimeSeconds, _players.Select(p => new PlayerSnapshot(p.Team, p.Id, p.Body.Pos, p.Body.Vel,
            p.Body.Heading, p.YellowCards, p.SentOff, p.IsGoalkeeper, p.Energy.ShortEnergy, p.Energy.LongEnergy)).ToArray(), new BallSnapshot(_ball.Pos, _ball.Vel), _status,
            _homeScore, _awayScore, _period, _endsSwapped);

    public string EventHash() => _events.Hash();
    public string SnapshotHash() => Canonical.Hash(GetSnapshot());
}
