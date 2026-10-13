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
    double RestartPauseSeconds = 3)
{
    public PlayerProfile PlayerProfile => Profile ?? PlayerProfile.Default;
    public BallConfig BallPhysics => Ball ?? BallConfig.Default;
    public PerceptionConfig Sensing => Perception ?? new PerceptionConfig();
    public PassConfig PassSettings => Passing ?? new PassConfig();
    public BallControlConfig ControlSettings => Control ?? new BallControlConfig(PossessionRadius, PossessionMaxHeight);
    public DuelConfig DuelSettings => Duels ?? new DuelConfig();
}

public enum MatchStatus { Running, Restart, Abandoned }

public sealed record PlayerSnapshot(Side Team, string Id, Vec2 Pos, Vec2 Vel, double Heading,
    int YellowCards = 0, bool SentOff = false);

public sealed record BallSnapshot(Vec3 Pos, Vec3 Vel);

/// <summary>Immutable view of the match: tick, clock, 22 players (home first, then away) and the ball.</summary>
public sealed record Snapshot(long Tick, double TimeSeconds, IReadOnlyList<PlayerSnapshot> Players, BallSnapshot? Ball = null,
    MatchStatus Status = MatchStatus.Running)
{
    public bool Equals(Snapshot? other) =>
        other is not null && Tick == other.Tick && TimeSeconds == other.TimeSeconds && Players.SequenceEqual(other.Players)
        && Ball == other.Ball && Status == other.Status;

    public override int GetHashCode()
    {
        var h = new HashCode();
        h.Add(Tick);
        foreach (var p in Players) h.Add(p);
        h.Add(Ball);
        h.Add(Status);
        return h.ToHashCode();
    }
}

public sealed class Match
{
    private sealed class Player
    {
        public Side Team;
        public string Id = "";
        public Vec2 Target;
        public BodyState Body;
        public long StartTick;
        public bool Started;
        public int YellowCards;
        public bool SentOff;
        public IPlayerBrain Brain = null!;
        public Perception? View;
        public Intent? Intent;
        public long NextDecisionTick;
        public PhasePlayer Build = null!;   // 'opbouw' entry (team has the ball)
        public PhasePlayer Defend = null!;  // 'verdedigen' entry (block variant if the tactic has them)
    }

    private readonly MatchConfig _config;
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
    private long _passerExcludedUntilTick;
    private long _nextDuelTick;
    private MatchStatus _status;
    private long _restartUntilTick;
    private int _restartTaker;

    private Match(MatchConfig config, Rng rng, Player[] players)
    {
        _config = config;
        _rng = rng;
        _players = players;
        _sensing = config.Sensing;
        _decisionTicks = Math.Max(1, (long)Math.Ceiling(_sensing.ScanIntervalSeconds / config.StepSeconds - 1e-9));
    }

    public static Match Create(MatchConfig config, Tactic home, Tactic away, ulong seed,
        Func<Side, string, IPlayerBrain>? brainFactory = null)
    {
        if (!double.IsFinite(config.StepSeconds) || config.StepSeconds <= 0)
            throw new ArgumentException("StepSeconds must be finite and > 0", nameof(config));
        config.Sensing.Validate();
        config.PassSettings.Validate();
        config.ControlSettings.Validate();
        config.DuelSettings.Validate();
        if (!double.IsFinite(config.RestartPauseSeconds) || config.RestartPauseSeconds < 0)
            throw new ArgumentException("Restart pause must be finite and nonnegative.");
        var players = new List<Player>(22);
        // Start in a legal kick-off line-up (own half) from the base phase positions (home: opbouw, away: verdedigen).
        foreach (var p in home.Phase(GamePhase.Opbouw).Players) players.Add(MakePlayer(Side.Home, p, home));
        foreach (var p in away.Phase(GamePhase.Verdedigen).Players) players.Add(MakePlayer(Side.Away, p, away));
        foreach (var p in players) p.Brain = brainFactory?.Invoke(p.Team, p.Id) ?? new PassingBrain(config.PassSettings);
        var match = new Match(config, new Rng(seed), players.ToArray());
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
            Id = start.Id,
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

    /// <summary>Ball state (position and velocity in metres, m/s).</summary>
    public BallState Ball => _ball;

    /// <summary>Team that has the ball (the last holder while the ball is loose).</summary>
    public Side Possession => _possession;

    /// <summary>Id of the ball carrier, or null while the ball is loose.</summary>
    public string? CarrierId => _carrier >= 0 ? _players[_carrier].Id : null;

    /// <summary>Phase a team plays this tick: 'opbouw' with the ball, 'verdedigen' without.</summary>
    public GamePhase PhaseOf(Side team) => _status == MatchStatus.Restart ? GamePhase.Stilstaand
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
    }

    /// <summary>Advances one fixed step: targets, player movement, ball physics, possession.</summary>
    public void Step()
    {
        if (_status == MatchStatus.Abandoned) { _tick++; return; }
        StartReady();
        if (_status == MatchStatus.Restart)
        {
            if (_tick < _restartUntilTick) { _tick++; return; }
            _status = MatchStatus.Running;
            _carrier = _restartTaker;
            _controlSinceTick = _tick;
            _events.Add(new Event(EventType.RestartTaken, _possession, _players[_carrier].Id,
                _tick, TimeSeconds, _ball.Pos.XY));
        }
        // Validate control before moving: the ball and its carrier belong to the same snapshot.
        if (_carrier >= 0)
        {
            var carrier = _players[_carrier];
            var velocity = new Vec3(carrier.Body.Vel.X, carrier.Body.Vel.Y, 0);
            if (!CanControl(carrier, _ball.Vel - velocity)) _carrier = -1;
        }
        var profile = _config.PlayerProfile;
        // Every decision reads one pre-movement snapshot, independent of player iteration order.
        Snapshot? world = _players.Any(p => p.Started && !p.SentOff && _tick >= p.NextDecisionTick) ? GetSnapshot() : null;
        int? presser = world is not null && _carrier >= 0 ? PressingAssignment.Select(world,
            _possession == Side.Home ? Side.Away : Side.Home, _sensing, _config.StepSeconds) : null;
        for (int i = 0; i < _players.Length; i++)
        {
            var p = _players[i];
            if (!p.Started || p.SentOff || _tick < p.NextDecisionTick) continue;
            var view = PerceptionBuilder.Build(world!, world!.Players[i], PhaseOf(p.Team), _carrier == i,
                _sensing, _config.StepSeconds, p.View?.KnownBall);
            view = view with { FormationTarget = TargetFor(p, BallRel(p.Team, view.KnownBall?.Ball.Pos)),
                HeldBallSeconds = _carrier == i ? (_tick - _controlSinceTick) * _config.StepSeconds : 0,
                BallIsLoose = _carrier < 0, IsPresser = presser == i };
            var intent = p.Brain.Decide(view);
            if (intent is null || !Enum.IsDefined(intent.Kind)
                || !double.IsFinite(intent.Target.X) || !double.IsFinite(intent.Target.Y))
                throw new InvalidOperationException($"Invalid intent for {p.Team}/{p.Id}.");
            p.View = view;
            p.Intent = intent with { Target = new Vec2(Math.Clamp(intent.Target.X, -Pitch.HalfLength, Pitch.HalfLength),
                Math.Clamp(intent.Target.Y, -Pitch.HalfWidth, Pitch.HalfWidth)) };
            p.Target = p.Intent.Kind is IntentKind.Pass or IntentKind.Scan ? p.Body.Pos : p.Intent.Target;
            p.NextDecisionTick = _tick + _decisionTicks;
        }
        ExecutePass();
        var beforeMovement = _carrier < 0 ? GetSnapshot().Players : null;
        foreach (var p in _players)
        {
            if (!p.Started || p.SentOff) continue;
            var b = Mover.Step(p.Body, p.Target, profile, _config.StepSeconds);
            p.Body = b with { Pos = new Vec2(
                Math.Clamp(b.Pos.X, -Pitch.HalfLength, Pitch.HalfLength),
                Math.Clamp(b.Pos.Y, -Pitch.HalfWidth, Pitch.HalfWidth)) };
            if (p.Intent?.Kind == IntentKind.Scan)
                p.Body = p.Body with { Heading = Mover.NormalizeAngle(p.Body.Heading + profile.TurnRate * _config.StepSeconds * 0.4) };
            if (p.Intent?.Kind == IntentKind.Pass)
            {
                var aim = p.Intent.Target - p.Body.Pos;
                double turn = Mover.NormalizeAngle(Math.Atan2(aim.Y, aim.X) - p.Body.Heading);
                p.Body = p.Body with { Heading = Mover.NormalizeAngle(p.Body.Heading + Math.Clamp(turn,
                    -profile.TurnRate * _config.StepSeconds, profile.TurnRate * _config.StepSeconds)) };
            }
        }
        ResolveDuel();
        if (_status == MatchStatus.Restart) { _tick++; return; }
        UpdateBall(beforeMovement);
        CheckOutOfBounds();
        _tick++;
    }

    // Ball position in the attack direction of a team: x 0 = own goal line, 1 = opponent goal line; y 0 = left, 1 = right.
    private static (double X, double Y) BallRel(Side side, Vec3? ball)
    {
        if (ball is null) return (0.5, 0.5); // No remembered position: fall back to the base formation.
        double x = ball.Value.X / Pitch.Length, y = ball.Value.Y / Pitch.Width;
        if (side == Side.Away) { x = -x; y = -y; }
        return (Math.Clamp(x + 0.5, 0, 1), Math.Clamp(y + 0.5, 0, 1));
    }

    private Vec2 TargetFor(Player p, (double X, double Y) ballRel)
    {
        var layout = p.Team == _possession ? p.Build : p.Defend;
        double x = layout.X, y = layout.Y;
        if (layout.FollowBall is { } f)
        {
            x += f.X * (ballRel.X - 0.5) * _config.BallFollowGain;
            y += f.Y * (ballRel.Y - 0.5) * _config.BallFollowGain;
        }
        return Pitch.ToMeters(Math.Clamp(x, 0, 1), Math.Clamp(y, 0, 1), p.Team);
    }

    private Vec2 FeetOf(Player p) =>
        p.Body.Pos + new Vec2(Math.Cos(p.Body.Heading), Math.Sin(p.Body.Heading)) * _config.CarryDistance;

    private void ExecutePass()
    {
        if (_carrier < 0) return;
        var passer = _players[_carrier];
        if (passer.Intent?.Kind != IntentKind.Pass || passer.View is null || !passer.Started) return;
        var intent = passer.Intent;
        // Validate against the actual observed option, never locate an unseen receiver in the world.
        if (!passer.View.VisiblePlayers.Any(p => p.Team == passer.Team && p.Id == intent.ReceiverId)) return;
        var direction = intent.Target - passer.Body.Pos;
        if (direction.Length < 1e-9 || Math.Abs(Mover.NormalizeAngle(Math.Atan2(direction.Y, direction.X) - passer.Body.Heading)) > 0.2) return;
        double pressure = passer.View.VisiblePlayers.Count(p => p.Team != passer.Team && (p.Pos - passer.View.Self.Pos).Length < 4);
        var feet = FeetOf(passer);
        _ball = new BallState(new Vec3(feet.X, feet.Y, 0), PassExecution.Velocity(feet, intent.Target, pressure, _config.PassSettings, _rng));
        _events.Add(new Event(EventType.Pass, passer.Team, passer.Id, _tick, TimeSeconds, feet, "to " + intent.ReceiverId));
        _lastPasser = _carrier;
        _passerExcludedUntilTick = _tick + Math.Max(1, (long)Math.Ceiling(0.3 / _config.StepSeconds));
        _carrier = -1;
        passer.Intent = new Intent(IntentKind.HoldPosition, passer.Body.Pos);
    }

    private void ResolveDuel()
    {
        if (_carrier < 0 || _tick < _nextDuelTick) return;
        var holder = _players[_carrier];
        int defenderIndex = -1;
        double nearest = _config.DuelSettings.Reach;
        for (int i = 0; i < _players.Length; i++)
        {
            var player = _players[i];
            if (!player.Started || player.SentOff || player.Team == holder.Team || player.Intent?.Kind != IntentKind.Press) continue;
            double distance = (player.Body.Pos - holder.Body.Pos).Length;
            if (distance <= nearest) { nearest = distance; defenderIndex = i; }
        }
        if (defenderIndex < 0) return;
        var defender = _players[defenderIndex];
        var offset = defender.Body.Pos - holder.Body.Pos;
        double angle = Mover.NormalizeAngle(Math.Atan2(offset.Y, offset.X) - holder.Body.Heading);
        var relative = defender.Body.Vel - holder.Body.Vel;
        double closing = nearest < 1e-9 ? 0 : -(relative.X * offset.X + relative.Y * offset.Y) / nearest;
        var outcome = DuelModel.Resolve(nearest, closing, angle, 0.6, 0.75, 0, _rng, _config.DuelSettings);
        _nextDuelTick = _tick + Math.Max(1, (long)Math.Ceiling(1.0 / _config.StepSeconds));
        _events.Add(new Event(EventType.Duel, defender.Team, defender.Id, _tick, TimeSeconds, holder.Body.Pos, outcome.ToString()));
        if (outcome == DuelOutcome.WonBall)
        {
            _carrier = defenderIndex;
            _controlSinceTick = _tick;
            _possession = defender.Team;
            _events.Add(new Event(EventType.PossessionChange, defender.Team, defender.Id, _tick, TimeSeconds, holder.Body.Pos, "duel"));
        }
        else if (outcome == DuelOutcome.LooseBall)
        {
            _ball = new BallState(new Vec3(holder.Body.Pos.X, holder.Body.Pos.Y, 0),
                new Vec3(offset.X * 3, offset.Y * 3, 0));
            _lastPasser = _carrier;
            _passerExcludedUntilTick = _tick + Math.Max(1, (long)Math.Ceiling(0.3 / _config.StepSeconds));
            _carrier = -1;
        }
        else if (outcome == DuelOutcome.Foul)
        {
            // The v0 action model samples an unsafe challenge, not the referee's judgement.
            // Its explicit Careless classification is synthetic until real tackle/contact facts are modelled.
            ReportIncident(defender.Team, defender.Id, holder.Body.Pos,
                new RefereeIncident(ContactAction.Charge, Conduct.Careless, true, true,
                    TacticalContext: TacticalContext.NoTacticalOffence));
        }
    }

    public RefereeDecision ReportIncident(Side offendingTeam, string playerId, Vec2 location, RefereeIncident incident)
    {
        var player = _players.FirstOrDefault(p => p.Team == offendingTeam && p.Id == playerId)
            ?? throw new ArgumentException("Unknown incident player.");
        var decision = Referee.Assess(incident);
        if (!decision.Complete) throw new ArgumentException("Incomplete or unsupported incident assessment: " + decision.Reason);
        if (decision.Restart == RefereeRestart.Penalty)
            throw new NotSupportedException("Penalty execution belongs to phase 1b; no free-kick fallback.");
        if (decision.Restart == RefereeRestart.None) return decision;
        var card = decision.Card;
        if (card == CardDecision.Yellow)
        {
            player.YellowCards++;
            if (player.YellowCards >= 2) card = CardDecision.SecondYellowRed;
        }
        if (card is CardDecision.Red or CardDecision.SecondYellowRed) player.SentOff = true;
        if (card != CardDecision.None)
            _events.Add(new Event(EventType.Card, offendingTeam, playerId, _tick, TimeSeconds, location, card.ToString()));
        if (decision.Restart == RefereeRestart.DirectFreeKick)
        {
            _events.Add(new Event(EventType.Foul, offendingTeam, playerId, _tick, TimeSeconds, location, incident.Conduct.ToString() ?? "Hold"));
            BeginRestart(offendingTeam == Side.Home ? Side.Away : Side.Home, ClampToPitch(location), "free_kick");
        }
        return decision with { Card = card };
    }

    private void CheckOutOfBounds()
    {
        if (Math.Abs(_ball.Pos.X) <= Pitch.HalfLength && Math.Abs(_ball.Pos.Y) <= Pitch.HalfWidth) return;
        var spot = new Vec2(Math.Clamp(_ball.Pos.X, -Pitch.HalfLength, Pitch.HalfLength),
            Math.Clamp(_ball.Pos.Y, -Pitch.HalfWidth, Pitch.HalfWidth));
        _events.Add(new Event(EventType.Out, _possession, "", _tick, TimeSeconds, spot));
        // Task 22 fallback: every boundary exit is a simple ground restart, not yet throw-in/corner/goal-kick rules.
        BeginRestart(_possession == Side.Home ? Side.Away : Side.Home, spot, "boundary");
    }

    private void BeginRestart(Side team, Vec2 spot, string reason, int? preferredTaker = null)
    {
        int taker = preferredTaker ?? -1;
        if (taker < 0 || _players[taker].SentOff || _players[taker].Team != team)
        {
            double nearest = double.MaxValue;
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i].Team != team || _players[i].SentOff) continue;
                double distance = (_players[i].Body.Pos - spot).Length;
                if (distance < nearest) { nearest = distance; taker = i; }
            }
        }
        if (taker < 0)
        {
            _carrier = -1;
            _status = MatchStatus.Abandoned;
            _ball = new BallState(new Vec3(spot.X, spot.Y, 0), Vec3.Zero);
            _events.Add(new Event(EventType.Abandoned, team, "", _tick, TimeSeconds, spot, "no active restart taker"));
            return;
        }
        foreach (var player in _players)
        {
            var pos = player.Body.Pos;
            if (!player.SentOff && player.Team != team && (pos - spot).Length < 9.15)
            {
                var away = pos - spot;
                if (away.Length < 1e-9) away = new Vec2(1, 0);
                pos = ClampToPitch(spot + away * (9.15 / away.Length));
                if ((pos - spot).Length < 9.15 - 1e-8)
                {
                    var inside = -spot;
                    if (inside.Length < 1e-9) inside = new Vec2(1, 0);
                    pos = spot + inside * (9.15 / inside.Length);
                }
            }
            player.Body = player.Body with { Pos = pos, Vel = Vec2.Zero };
            player.Intent = null;
            player.NextDecisionTick = _tick;
        }
        var inward = -spot;
        if (inward.Length < 1e-9) inward = new Vec2(team == Side.Home ? 1 : -1, 0);
        // Leave space for the carried ball even when the player turns at a boundary/corner.
        var takerPos = ClampToPitch(spot + inward * (0.7 / inward.Length));
        _players[taker].Body = _players[taker].Body with { Pos = takerPos,
            Heading = Math.Atan2(inward.Y, inward.X) };
        _ball = new BallState(new Vec3(spot.X, spot.Y, 0), Vec3.Zero);
        _possession = team;
        _carrier = -1;
        _status = MatchStatus.Restart;
        _restartTaker = taker;
        _restartUntilTick = _tick + Math.Max(1, (long)Math.Ceiling(_config.RestartPauseSeconds / _config.StepSeconds));
        _events.Add(new Event(EventType.RestartAwarded, team, _players[taker].Id, _tick, TimeSeconds, spot, reason));
    }

    private static Vec2 ClampToPitch(Vec2 pos) => new(Math.Clamp(pos.X, -Pitch.HalfLength, Pitch.HalfLength),
        Math.Clamp(pos.Y, -Pitch.HalfWidth, Pitch.HalfWidth));

    private void UpdateBall(IReadOnlyList<PlayerSnapshot>? beforeMovement)
    {
        if (_carrier >= 0)
        {
            var c = _players[_carrier];
            var cv = new Vec3(c.Body.Vel.X, c.Body.Vel.Y, 0);
            var f = FeetOf(c);
            _ball = new BallState(new Vec3(f.X, f.Y, 0), cv);
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
                _lastPasser >= 0 && _tick < _passerExcludedUntilTick ? _lastPasser : null);
            if (contact is not null) break;
        }
        if (contact is null) return;
        var n = _players[contact.Value.PlayerIndex];
        _carrier = contact.Value.PlayerIndex;
        _controlSinceTick = _tick;
        _events.Add(new Event(n.Team == _possession ? EventType.BallControl : EventType.Interception,
            n.Team, n.Id, _tick, TimeSeconds, contact.Value.Position.XY));
        if (n.Team != _possession)
        {
            _events.Add(new Event(EventType.PossessionChange, n.Team, n.Id, _tick, TimeSeconds, _ball.Pos.XY, "from " + _possession));
            _possession = n.Team;
        }
        var feet = FeetOf(n);
        _ball = new BallState(new Vec3(feet.X, feet.Y, 0), new Vec3(n.Body.Vel.X, n.Body.Vel.Y, 0));
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
            p.Body.Heading, p.YellowCards, p.SentOff)).ToArray(), new BallSnapshot(_ball.Pos, _ball.Vel), _status);

    public string EventHash() => _events.Hash();
    public string SnapshotHash() => Canonical.Hash(GetSnapshot());
}
