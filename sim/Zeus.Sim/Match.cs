namespace Zeus.Sim;

public sealed record MatchConfig(
    double StepSeconds = 0.1,
    PlayerProfile? Profile = null,
    double MaxReactionSeconds = 0.3)
{
    public PlayerProfile PlayerProfile => Profile ?? PlayerProfile.Default;
}

public sealed record PlayerSnapshot(Side Team, string Id, Vec2 Pos, Vec2 Vel, double Heading);

/// <summary>Immutable view of the match: tick, clock and 22 players (home first, then away).</summary>
public sealed record Snapshot(long Tick, double TimeSeconds, IReadOnlyList<PlayerSnapshot> Players)
{
    public bool Equals(Snapshot? other) =>
        other is not null && Tick == other.Tick && TimeSeconds == other.TimeSeconds && Players.SequenceEqual(other.Players);

    public override int GetHashCode()
    {
        var h = new HashCode();
        h.Add(Tick);
        foreach (var p in Players) h.Add(p);
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
    }

    private readonly MatchConfig _config;
    private readonly Rng _rng;
    private readonly Player[] _players;
    private readonly EventLog _events = new();
    private long _tick;

    private Match(MatchConfig config, Rng rng, Player[] players)
    {
        _config = config;
        _rng = rng;
        _players = players;
    }

    public static Match Create(MatchConfig config, Tactic home, Tactic away, ulong seed)
    {
        if (config.StepSeconds <= 0) throw new ArgumentException("StepSeconds must be > 0", nameof(config));
        var players = new List<Player>(22);
        // Start in a legal kick-off line-up (own half); the target is the tactic position (home: opbouw, away: verdedigen).
        foreach (var p in home.Phase(GamePhase.Opbouw).Players) players.Add(MakePlayer(Side.Home, p));
        foreach (var p in away.Phase(GamePhase.Verdedigen).Players) players.Add(MakePlayer(Side.Away, p));
        var match = new Match(config, new Rng(seed), players.ToArray());
        match.Init();
        return match;
    }

    private static Player MakePlayer(Side side, PhasePlayer p) => new()
    {
        Team = side,
        Id = p.Id,
        Target = Pitch.ToMeters(p.X, p.Y, side),
        Body = new BodyState(Pitch.ToMeters(Math.Min(p.X, 0.5), p.Y, side), Vec2.Zero, side == Side.Home ? 0.0 : Math.PI),
    };

    private void Init()
    {
        _events.Add(new Event(EventType.KickOff, Side.Home, "", 0, 0.0, Vec2.Zero));
        // Fixed order: home then away, by player index; one Rng draw each.
        foreach (var p in _players)
        {
            double delay = _rng.NextDouble() * _config.MaxReactionSeconds;
            p.StartTick = (long)Math.Ceiling(delay / _config.StepSeconds - 1e-9);
        }
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

    /// <summary>Advances one fixed step: players that have reacted move towards their target.</summary>
    public void Step()
    {
        StartReady();
        var profile = _config.PlayerProfile;
        foreach (var p in _players)
        {
            if (!p.Started) continue;
            var b = Mover.Step(p.Body, p.Target, profile, _config.StepSeconds);
            p.Body = b with { Pos = new Vec2(
                Math.Clamp(b.Pos.X, -Pitch.HalfLength, Pitch.HalfLength),
                Math.Clamp(b.Pos.Y, -Pitch.HalfWidth, Pitch.HalfWidth)) };
        }
        _tick++;
    }

    public Snapshot GetSnapshot() =>
        new(_tick, TimeSeconds, _players.Select(p => new PlayerSnapshot(p.Team, p.Id, p.Body.Pos, p.Body.Vel, p.Body.Heading)).ToArray());

    public string EventHash() => _events.Hash();
    public string SnapshotHash() => Canonical.Hash(GetSnapshot());
}
