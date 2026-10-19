using System;
using System.Collections.Generic;
using Zeus.Sim;

/// <summary>One player as sampled between two ticks. Positions and headings are in sim coordinates.</summary>
public struct PlayerFrame
{
    public Side Team;
    public string Id;
    public Vec2 Pos;
    public Vec2 Vel;        // m/s
    public double Heading;  // radians, 0 = +x
    public bool IsKeeper;
    public bool SentOff;
    /// <summary>True when the player exists in the base tick but not in the next one: frozen in place, no interpolation.</summary>
    public bool Leaving;
}

/// <summary>
/// Interpolated picture at a fractional tick. One instance is owned by the buffer and rewritten on every
/// <see cref="SnapshotBuffer.Sample"/>, so there are no allocations per frame; copy what must outlive the next call.
/// </summary>
public sealed class VisualFrame
{
    public double Tick;          // the sampled visual tick (clamped to the buffer range)
    public long BaseTick;        // floor(Tick)
    public double Alpha;         // Tick - BaseTick in [0, 1)
    public Snapshot? Base;       // snapshot at BaseTick: status, score, period, EndsSwapped
    public string? CarrierId;    // ball carrier at BaseTick, when the driver supplied it
    public PlayerFrame[] Players = new PlayerFrame[22];
    public int PlayerCount;
    public bool HasBall;
    public Vec3 BallPos;         // sim coordinates, z up
    public Vec3 BallVel;         // m/s
    public bool Teleported;      // a cut (> TeleportMeters) happened for the ball or a player in this interval
}

/// <summary>
/// Ring buffer of (Snapshot, events of that tick, carrier) with interpolated sampling (docs/3d-architectuur.md §2.3-2.4).
/// Ticks must be pushed consecutively; a gap or a rewind clears the buffer. Pure C#, deterministic, no Godot types.
/// </summary>
public sealed class SnapshotBuffer
{
    /// <summary>Default tick length; equals the MatchConfig default.</summary>
    public const double DefaultStepSeconds = 0.1;
    /// <summary>Tick length used to scale velocities (pass MatchConfig.StepSeconds when it is not the default).</summary>
    public double StepSeconds { get; }
    /// <summary>A move longer than this within one tick is a teleport: hold the old state, then jump.</summary>
    public const double TeleportMeters = 3;
    public const int DefaultCapacity = 64;

    struct Slot
    {
        public Snapshot Snapshot;
        public Event[] Events;
        public string? CarrierId;
    }

    readonly Slot[] _slots;
    readonly VisualFrame _frame = new();
    int _start, _count;

    public SnapshotBuffer(int capacity = DefaultCapacity, double stepSeconds = DefaultStepSeconds)
    {
        if (!double.IsFinite(stepSeconds) || stepSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(stepSeconds));
        StepSeconds = stepSeconds;
        if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity), "At least two ticks are needed to interpolate.");
        _slots = new Slot[capacity];
    }

    public int Capacity => _slots.Length;
    public int Count => _count;
    /// <summary>Oldest buffered tick, or -1 when empty.</summary>
    public long OldestTick => _count == 0 ? -1 : _slots[_start].Snapshot.Tick;
    /// <summary>Newest buffered tick, or -1 when empty.</summary>
    public long NewestTick => _count == 0 ? -1 : OldestTick + _count - 1;

    public void Clear()
    {
        Array.Clear(_slots);
        _start = _count = 0;
    }

    /// <summary>Appends the snapshot of one tick with the events produced by that tick. Events are copied.</summary>
    public void Push(Snapshot snapshot, IReadOnlyList<Event> events, string? carrierId = null)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        if (_count > 0 && snapshot.Tick != NewestTick + 1) Clear();
        int index = (_start + _count) % Capacity;
        if (_count == Capacity) _start = (_start + 1) % Capacity; else _count++;
        Event[] copy = Array.Empty<Event>();
        if (events is { Count: > 0 })
        {
            copy = new Event[events.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = events[i];
        }
        _slots[index] = new Slot { Snapshot = snapshot, Events = copy, CarrierId = carrierId };
    }

    int IndexOf(long tick)
    {
        if (_count == 0 || tick < OldestTick || tick > NewestTick) return -1;
        return (int)((_start + (tick - OldestTick)) % Capacity);
    }

    public Snapshot? At(long tick)
    {
        int i = IndexOf(tick);
        return i < 0 ? null : _slots[i].Snapshot;
    }

    public IReadOnlyList<Event> EventsAt(long tick)
    {
        int i = IndexOf(tick);
        return i < 0 ? Array.Empty<Event>() : _slots[i].Events;
    }

    public string? CarrierAt(long tick)
    {
        int i = IndexOf(tick);
        return i < 0 ? null : _slots[i].CarrierId;
    }

    /// <summary>Pass, Shot, BallControl, FirstTouch, Save, Interception: the ball changes direction at this tick.</summary>
    public static bool IsContactEvent(EventType type) => type is EventType.Pass or EventType.Shot or EventType.BallControl
        or EventType.FirstTouch or EventType.Save or EventType.Interception;

    public bool HasContactEvent(long tick)
    {
        int i = IndexOf(tick);
        if (i < 0) return false;
        var events = _slots[i].Events;
        for (int k = 0; k < events.Length; k++) if (IsContactEvent(events[k].Type)) return true;
        return false;
    }

    /// <summary>Appends the events with fromTick &lt; Tick &lt;= toTick, in tick order, to <paramref name="into"/>.</summary>
    public void EventsBetween(long fromTickExclusive, long toTickInclusive, List<Event> into)
    {
        if (_count == 0) return;
        long first = Math.Max(fromTickExclusive + 1, OldestTick);
        long last = Math.Min(toTickInclusive, NewestTick);
        for (long t = first; t <= last; t++)
        {
            var events = _slots[IndexOf(t)].Events;
            for (int k = 0; k < events.Length; k++) into.Add(events[k]);
        }
    }

    /// <summary>Allocating convenience form of <see cref="EventsBetween(long, long, List{Event})"/>.</summary>
    public List<Event> EventsBetween(long fromTickExclusive, long toTickInclusive)
    {
        var list = new List<Event>();
        EventsBetween(fromTickExclusive, toTickInclusive, list);
        return list;
    }

    /// <summary>Events with fromTick &lt;= Tick &lt;= fromTick + lookaheadTicks: the window the adapter uses to start actions early.</summary>
    public void UpcomingEvents(long fromTick, int lookaheadTicks, List<Event> into) =>
        EventsBetween(fromTick - 1, fromTick + lookaheadTicks, into);

    public List<Event> UpcomingEvents(long fromTick, int lookaheadTicks) => EventsBetween(fromTick - 1, fromTick + lookaheadTicks);

    /// <summary>Cubic Hermite between p0 and p1 with velocities in m/s over one tick of <see cref="StepSeconds"/>.</summary>
    public static Vec2 Hermite(Vec2 p0, Vec2 v0, Vec2 p1, Vec2 v1, double t, double stepSeconds = DefaultStepSeconds)
    {
        // Written as p0 + delta terms so that a standing player (p0 == p1, v == 0) is reproduced exactly.
        double t2 = t * t, t3 = t2 * t;
        double h10 = t3 - 2 * t2 + t, h01 = -2 * t3 + 3 * t2, h11 = t3 - t2;
        return new Vec2(
            p0.X + (p1.X - p0.X) * h01 + v0.X * (h10 * stepSeconds) + v1.X * (h11 * stepSeconds),
            p0.Y + (p1.Y - p0.Y) * h01 + v0.Y * (h10 * stepSeconds) + v1.Y * (h11 * stepSeconds));
    }

    public static Vec3 Hermite(Vec3 p0, Vec3 v0, Vec3 p1, Vec3 v1, double t, double stepSeconds = DefaultStepSeconds)
    {
        double t2 = t * t, t3 = t2 * t;
        double h10 = t3 - 2 * t2 + t, h01 = -2 * t3 + 3 * t2, h11 = t3 - t2;
        return new Vec3(
            p0.X + (p1.X - p0.X) * h01 + v0.X * (h10 * stepSeconds) + v1.X * (h11 * stepSeconds),
            p0.Y + (p1.Y - p0.Y) * h01 + v0.Y * (h10 * stepSeconds) + v1.Y * (h11 * stepSeconds),
            p0.Z + (p1.Z - p0.Z) * h01 + v0.Z * (h10 * stepSeconds) + v1.Z * (h11 * stepSeconds));
    }

    /// <summary>Interpolates along the shortest arc from a to b; the result is normalised to (-pi, pi].</summary>
    public static double LerpAngle(double a, double b, double t) => Mover.NormalizeAngle(a + Mover.NormalizeAngle(b - a) * t);

    static Vec2 Lerp(Vec2 a, Vec2 b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    static Vec3 Lerp(Vec3 a, Vec3 b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

    /// <summary>
    /// Interpolated picture at <paramref name="visualTick"/>, clamped to the buffered range. Players of the base tick are
    /// output; a player missing from the next tick is frozen (<see cref="PlayerFrame.Leaving"/>). Returns the buffer-owned frame.
    /// </summary>
    public VisualFrame Sample(double visualTick)
    {
        var f = _frame;
        f.Teleported = false;
        if (_count == 0)
        {
            f.Tick = f.Alpha = 0; f.BaseTick = 0; f.Base = null; f.CarrierId = null;
            f.PlayerCount = 0; f.HasBall = false;
            return f;
        }
        if (double.IsNaN(visualTick)) visualTick = NewestTick;
        double clamped = Math.Clamp(visualTick, OldestTick, NewestTick);
        long baseTick = (long)Math.Floor(clamped);
        double alpha = clamped - baseTick;
        var a = _slots[IndexOf(baseTick)].Snapshot;
        var b = baseTick < NewestTick ? _slots[IndexOf(baseTick + 1)].Snapshot : null;
        if (b is null) alpha = 0;

        f.Tick = clamped; f.BaseTick = baseTick; f.Alpha = alpha; f.Base = a; f.CarrierId = CarrierAt(baseTick);

        var players = a.Players;
        if (f.Players.Length < players.Count) f.Players = new PlayerFrame[players.Count];
        f.PlayerCount = players.Count;
        for (int i = 0; i < players.Count; i++)
        {
            var pa = players[i];
            var pb = b is null ? null : FindPlayer(b.Players, i, pa.Team, pa.Id);
            ref var o = ref f.Players[i];
            o.Team = pa.Team; o.Id = pa.Id; o.IsKeeper = pa.IsKeeper; o.SentOff = pa.SentOff;
            o.Leaving = b is not null && pb is null;
            if (pb is null || alpha == 0)
            {
                o.Pos = pa.Pos; o.Vel = pb is null && b is not null ? Vec2.Zero : pa.Vel; o.Heading = pa.Heading;
                continue;
            }
            if ((pb.Pos - pa.Pos).Length > TeleportMeters)
            {
                o.Pos = pa.Pos; o.Vel = pa.Vel; o.Heading = pa.Heading;
                f.Teleported = true;
                continue;
            }
            o.Pos = Hermite(pa.Pos, pa.Vel, pb.Pos, pb.Vel, alpha, StepSeconds);
            o.Vel = Lerp(pa.Vel, pb.Vel, alpha);
            o.Heading = LerpAngle(pa.Heading, pb.Heading, alpha);
        }

        f.HasBall = a.Ball is not null;
        if (a.Ball is { } ball)
        {
            var next = b?.Ball;
            if (next is null || alpha == 0)
            {
                f.BallPos = ball.Pos; f.BallVel = ball.Vel;
            }
            else if ((next.Pos - ball.Pos).Length > TeleportMeters)
            {
                f.BallPos = ball.Pos; f.BallVel = ball.Vel;
                f.Teleported = true;
            }
            else if (HasContactEvent(baseTick + 1))
            {
                // The kick happens at the start of the next tick's step: straight segment, no Hermite bulge over the kink.
                f.BallPos = Lerp(ball.Pos, next.Pos, alpha);
                f.BallVel = next.Vel;
            }
            else
            {
                var h = Hermite(ball.Pos, ball.Vel, next.Pos, next.Vel, alpha, StepSeconds);
                f.BallPos = h.Z < 0 ? new Vec3(h.X, h.Y, 0) : h; // the spline may dip below the grass at a bounce
                f.BallVel = Lerp(ball.Vel, next.Vel, alpha);
            }
        }
        return f;
    }

    static PlayerSnapshot? FindPlayer(IReadOnlyList<PlayerSnapshot> players, int hint, Side team, string id)
    {
        if (hint < players.Count && players[hint].Team == team && players[hint].Id == id) return players[hint];
        for (int i = 0; i < players.Count; i++)
            if (players[i].Team == team && players[i].Id == id) return players[i];
        return null;
    }
}
