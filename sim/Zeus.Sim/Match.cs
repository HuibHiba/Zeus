namespace Zeus.Sim;

public sealed record MatchConfig(double StepSeconds = 0.1);

public sealed record PlayerSnapshot(Side Team, string Id, Vec2 Pos);

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
    private readonly MatchConfig _config;
    private readonly Rng _rng;
    private readonly (Side Team, string Id, Vec2 Pos)[] _players;
    private long _tick;

    private Match(MatchConfig config, Rng rng, (Side, string, Vec2)[] players)
    {
        _config = config;
        _rng = rng;
        _players = players;
    }

    public static Match Create(MatchConfig config, Tactic home, Tactic away, ulong seed)
    {
        if (config.StepSeconds <= 0) throw new ArgumentException("StepSeconds must be > 0", nameof(config));
        var players = new List<(Side, string, Vec2)>(22);
        foreach (var p in home.Phase(GamePhase.Opbouw).Players)
            players.Add((Side.Home, p.Id, Pitch.ToMeters(p.X, p.Y, Side.Home)));
        foreach (var p in away.Phase(GamePhase.Verdedigen).Players)
            players.Add((Side.Away, p.Id, Pitch.ToMeters(p.X, p.Y, Side.Away)));
        return new Match(config, new Rng(seed), players.ToArray());
    }

    public long Tick => _tick;
    public double TimeSeconds => _tick * _config.StepSeconds;

    /// <summary>Advances one fixed step. Players do not move yet (movement comes in task 8).</summary>
    public void Step()
    {
        _tick++;
    }

    public Snapshot GetSnapshot() =>
        new(_tick, TimeSeconds, _players.Select(p => new PlayerSnapshot(p.Team, p.Id, p.Pos)).ToArray());
}
