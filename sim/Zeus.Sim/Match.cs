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
    PerceptionConfig? Perception = null)
{
    public PlayerProfile PlayerProfile => Profile ?? PlayerProfile.Default;
    public BallConfig BallPhysics => Ball ?? BallConfig.Default;
    public PerceptionConfig Sensing => Perception ?? new PerceptionConfig();
}

public sealed record PlayerSnapshot(Side Team, string Id, Vec2 Pos, Vec2 Vel, double Heading);

public sealed record BallSnapshot(Vec3 Pos, Vec3 Vel);

/// <summary>Immutable view of the match: tick, clock, 22 players (home first, then away) and the ball.</summary>
public sealed record Snapshot(long Tick, double TimeSeconds, IReadOnlyList<PlayerSnapshot> Players, BallSnapshot? Ball = null)
{
    public bool Equals(Snapshot? other) =>
        other is not null && Tick == other.Tick && TimeSeconds == other.TimeSeconds && Players.SequenceEqual(other.Players)
        && Ball == other.Ball;

    public override int GetHashCode()
    {
        var h = new HashCode();
        h.Add(Tick);
        foreach (var p in Players) h.Add(p);
        h.Add(Ball);
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
        var players = new List<Player>(22);
        // Start in a legal kick-off line-up (own half) from the base phase positions (home: opbouw, away: verdedigen).
        foreach (var p in home.Phase(GamePhase.Opbouw).Players) players.Add(MakePlayer(Side.Home, p, home));
        foreach (var p in away.Phase(GamePhase.Verdedigen).Players) players.Add(MakePlayer(Side.Away, p, away));
        foreach (var p in players) p.Brain = brainFactory?.Invoke(p.Team, p.Id) ?? new FormationBrain();
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

    /// <summary>Ball state (position and velocity in metres, m/s).</summary>
    public BallState Ball => _ball;

    /// <summary>Team that has the ball (the last holder while the ball is loose).</summary>
    public Side Possession => _possession;

    /// <summary>Id of the ball carrier, or null while the ball is loose.</summary>
    public string? CarrierId => _carrier >= 0 ? _players[_carrier].Id : null;

    /// <summary>Phase a team plays this tick: 'opbouw' with the ball, 'verdedigen' without.</summary>
    public GamePhase PhaseOf(Side team) => team == _possession ? GamePhase.Opbouw : GamePhase.Verdedigen;

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
        StartReady();
        // Validate control before moving: the ball and its carrier belong to the same snapshot.
        if (_carrier >= 0)
        {
            var carrier = _players[_carrier];
            var velocity = new Vec3(carrier.Body.Vel.X, carrier.Body.Vel.Y, 0);
            if (!CanControl(carrier, _ball.Vel - velocity)) _carrier = -1;
        }
        var profile = _config.PlayerProfile;
        // Every decision reads one pre-movement snapshot, independent of player iteration order.
        Snapshot? world = _players.Any(p => p.Started && _tick >= p.NextDecisionTick) ? GetSnapshot() : null;
        for (int i = 0; i < _players.Length; i++)
        {
            var p = _players[i];
            if (!p.Started || _tick < p.NextDecisionTick) continue;
            var view = PerceptionBuilder.Build(world!, world!.Players[i], PhaseOf(p.Team), _carrier == i,
                _sensing, _config.StepSeconds, p.View?.KnownBall);
            view = view with { FormationTarget = TargetFor(p, BallRel(p.Team, view.KnownBall?.Ball.Pos)) };
            var intent = p.Brain.Decide(view);
            if (intent is null || !Enum.IsDefined(intent.Kind)
                || !double.IsFinite(intent.Target.X) || !double.IsFinite(intent.Target.Y))
                throw new InvalidOperationException($"Invalid intent for {p.Team}/{p.Id}.");
            p.View = view;
            p.Intent = intent with { Target = new Vec2(Math.Clamp(intent.Target.X, -Pitch.HalfLength, Pitch.HalfLength),
                Math.Clamp(intent.Target.Y, -Pitch.HalfWidth, Pitch.HalfWidth)) };
            p.Target = p.Intent.Target;
            p.NextDecisionTick = _tick + _decisionTicks;
        }
        foreach (var p in _players)
        {
            if (!p.Started) continue;
            var b = Mover.Step(p.Body, p.Target, profile, _config.StepSeconds);
            p.Body = b with { Pos = new Vec2(
                Math.Clamp(b.Pos.X, -Pitch.HalfLength, Pitch.HalfLength),
                Math.Clamp(b.Pos.Y, -Pitch.HalfWidth, Pitch.HalfWidth)) };
        }
        UpdateBall();
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

    private void UpdateBall()
    {
        if (_carrier >= 0)
        {
            var c = _players[_carrier];
            var cv = new Vec3(c.Body.Vel.X, c.Body.Vel.Y, 0);
            var f = FeetOf(c);
            _ball = new BallState(new Vec3(f.X, f.Y, 0), cv);
            return;
        }

        _ball = BallPhysics.Step(_ball, _config.BallPhysics, _config.StepSeconds);

        int best = -1;
        double bestDist = double.MaxValue;
        for (int i = 0; i < _players.Length; i++)
        {
            var p = _players[i];
            if (!CanControl(p, _ball.Vel)) continue;
            double d = (p.Body.Pos - _ball.Pos.XY).Length;
            if (d < bestDist) { bestDist = d; best = i; }
        }
        if (best < 0) return;

        var n = _players[best];
        _carrier = best;
        if (n.Team != _possession)
        {
            _events.Add(new Event(EventType.PossessionChange, n.Team, n.Id, _tick, TimeSeconds, _ball.Pos.XY, "from " + _possession));
            _possession = n.Team;
        }
        var feet = FeetOf(n);
        _ball = new BallState(new Vec3(feet.X, feet.Y, 0), new Vec3(n.Body.Vel.X, n.Body.Vel.Y, 0));
    }

    private bool CanControl(Player p, Vec3 ballVel) =>
        (p.Body.Pos - _ball.Pos.XY).Length <= _config.PossessionRadius
        && _ball.Pos.Z < _config.PossessionMaxHeight
        && ballVel.Length < _config.PossessionMaxSpeed;

    public Snapshot GetSnapshot() =>
        new(_tick, TimeSeconds, _players.Select(p => new PlayerSnapshot(p.Team, p.Id, p.Body.Pos, p.Body.Vel, p.Body.Heading)).ToArray(),
            new BallSnapshot(_ball.Pos, _ball.Vel));

    public string EventHash() => _events.Hash();
    public string SnapshotHash() => Canonical.Hash(GetSnapshot());
}
