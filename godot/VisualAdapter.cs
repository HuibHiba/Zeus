using System;
using Zeus.Sim;

public enum Locomotion { Idle, Walk, Jog, Sprint, Backpedal, Strafe }

public enum PlayerAction { None, Pass, Shot, Header, Control, Dribble, Tackle, Fall, KeeperDive, Celebrate, Disappointed, ThrowIn }

/// <summary>All adapter thresholds in one place (docs/3d-architectuur.md §3.3). Speeds in m/s, angles in degrees.</summary>
public static class AdapterThresholds
{
    public const double IdleBelow = 0.3;
    public const double WalkBelow = 2;
    public const double JogBelow = 5;               // at or above: sprint
    public const double StrafeFromDegrees = 60;     // angle between velocity and heading
    public const double BackpedalFromDegrees = 110;
    public const double HeaderBallHeight = 1.3;     // m, ball z at a contact event
    public const double KeeperDiveLateralSpeed = 3; // m/s perpendicular to the heading
    public const int DefaultWindUpTicks = 3;        // action starts this many ticks before the event tick
    public const int LookaheadTicks = 4;            // D in the architecture: buffer lead over the visual tick
    public const int GoalTeammates = 2;             // cheering teammates besides the scorer
    public const double RefereeMinBallDistance = 8; // m
    public const double RefereeLaneClearance = 3;   // m from the line of a ball in flight
    public const double RefereeSpeed = 6;           // m/s
    public const double RefereeMargin = 2;          // m outside the touchlines the referee may stand
}

/// <summary>Render state of one player. Position uses world axes: x along, y up, z across the pitch.</summary>
public struct PlayerVisual
{
    public Side Team;
    public string Id;
    public Vec3 Pos3;
    public double Yaw;               // about world +Y, includes the model correction
    public double Speed;             // m/s
    public Locomotion Locomotion;
    public PlayerAction Action;
    public long ActionStartTick;     // tick at which the action pose starts (wind-up included)
    public Vec3 LookAt;              // world position the head should face (the ball)
    public bool IsKeeper;
    public bool SentOff;             // walking off the pitch
    public bool Leaving;             // not in the next tick: hold position
}

public struct BallVisual
{
    public bool Visible;
    public Vec3 Pos3;                // world axes
    public Vec3 Vel3;
}

public struct RefereeVisual
{
    public Vec3 Pos3;
    public double Yaw;
    public Vec3 LookAt;
}

/// <summary>
/// Turns an interpolated <see cref="VisualFrame"/> plus the surrounding events into render state (docs/3d-architectuur.md §2.2, §3.3).
/// Pure C#: no Godot types, deterministic, and the output arrays are reused between calls.
/// </summary>
public sealed class VisualAdapter
{
    const int MaxHold = 50;

    readonly int _windUp;
    PlayerAction[] _action = new PlayerAction[22];
    long[] _start = new long[22];
    PlayerAction[] _prevAction = new PlayerAction[22];
    long[] _prevStart = new long[22];
    double _lastTick = double.NaN;
    double _stepSeconds = SnapshotBuffer.DefaultStepSeconds;
    Vec2 _refPos;

    public PlayerVisual[] Players { get; private set; } = new PlayerVisual[22];
    public int PlayerCount { get; private set; }
    public BallVisual Ball;
    public RefereeVisual Referee;

    public VisualAdapter(int windUpTicks = AdapterThresholds.DefaultWindUpTicks)
    {
        if (windUpTicks < 0 || windUpTicks > AdapterThresholds.LookaheadTicks)
            throw new ArgumentOutOfRangeException(nameof(windUpTicks), "Wind-up must fit inside the lookahead.");
        _windUp = windUpTicks;
    }

    // ---- Pure functions ----------------------------------------------------------------------------------------

    /// <summary>Locomotion from speed (m/s) and the angle between velocity and heading.</summary>
    public static Locomotion ClassifyLocomotion(Vec2 vel, double heading)
    {
        double speed = vel.Length;
        if (speed < AdapterThresholds.IdleBelow) return Locomotion.Idle;
        double dot = (vel.X * Math.Cos(heading) + vel.Y * Math.Sin(heading)) / speed;
        double degrees = Math.Acos(Math.Clamp(dot, -1, 1)) * 180 / Math.PI;
        if (degrees > AdapterThresholds.BackpedalFromDegrees) return Locomotion.Backpedal;
        if (degrees >= AdapterThresholds.StrafeFromDegrees) return Locomotion.Strafe;
        if (speed < AdapterThresholds.WalkBelow) return Locomotion.Walk;
        return speed < AdapterThresholds.JogBelow ? Locomotion.Jog : Locomotion.Sprint;
    }

    public static bool IsHeader(double ballHeight) => ballHeight > AdapterThresholds.HeaderBallHeight;

    /// <summary>Speed perpendicular to the heading, in m/s (absolute).</summary>
    public static double LateralSpeed(Vec2 vel, double heading) =>
        Math.Abs(-vel.X * Math.Sin(heading) + vel.Y * Math.Cos(heading));

    /// <summary>Ticks an action stays visible after its event tick.</summary>
    static int HoldTicks(PlayerAction a) => a switch {
        PlayerAction.Pass => 4, PlayerAction.Shot => 5, PlayerAction.Header => 5, PlayerAction.Control => 4,
        PlayerAction.Tackle => 6, PlayerAction.Fall => 15, PlayerAction.KeeperDive => 12,
        PlayerAction.Celebrate => 50, PlayerAction.Disappointed => 40, PlayerAction.ThrowIn => 6, _ => 0 };

    /// <summary>Reactions (fall, celebrate, disappointed) start on the event tick; the others wind up before it.</summary>
    bool UsesWindUp(PlayerAction a) => a is not (PlayerAction.Fall or PlayerAction.Celebrate or PlayerAction.Disappointed);

    // ---- Update ------------------------------------------------------------------------------------------------

    /// <summary>Samples the buffer at <paramref name="visualTick"/> and rewrites <see cref="Players"/>, <see cref="Ball"/> and <see cref="Referee"/>.</summary>
    public void Update(SnapshotBuffer buffer, double visualTick)
    {
        var f = buffer.Sample(visualTick);
        _stepSeconds = buffer.StepSeconds;
        PlayerCount = f.PlayerCount;
        if (Players.Length < PlayerCount)
        {
            Players = new PlayerVisual[PlayerCount];
            _action = new PlayerAction[PlayerCount];
            _start = new long[PlayerCount];
            _prevAction = new PlayerAction[PlayerCount];
            _prevStart = new long[PlayerCount];
        }
        Ball = default;
        if (f.Base is null)
        {
            Referee = default;
            _lastTick = double.NaN;
            Array.Clear(_prevAction);
            return;
        }

        var lookAt = f.HasBall ? SimWorld.SimToWorld(f.BallPos) : default;
        for (int i = 0; i < PlayerCount; i++)
        {
            ref var src = ref f.Players[i];
            _action[i] = PlayerAction.None;
            _start[i] = 0;
            ref var d = ref Players[i];
            d.Team = src.Team; d.Id = src.Id; d.IsKeeper = src.IsKeeper; d.SentOff = src.SentOff; d.Leaving = src.Leaving;
            d.Pos3 = SimWorld.SimToWorld(src.Pos, 0);
            d.Yaw = SimWorld.YawFromHeading(src.Heading);
            d.Speed = src.Vel.Length;
            d.Locomotion = ClassifyLocomotion(src.Vel, src.Heading);
            d.LookAt = f.HasBall ? lookAt : SimWorld.SimToWorld(src.Pos + new Vec2(Math.Cos(src.Heading), Math.Sin(src.Heading)), 0);
        }

        ApplyEvents(buffer, f);
        ApplyMotionActions(f);
        for (int i = 0; i < PlayerCount; i++)
        {
            Players[i].Action = _action[i];
            Players[i].ActionStartTick = _start[i];
            _prevAction[i] = _action[i];
            _prevStart[i] = _start[i];
        }

        if (f.HasBall)
            Ball = new BallVisual { Visible = true, Pos3 = lookAt, Vel3 = SimWorld.SimToWorld(f.BallVel) };
        UpdateReferee(f);
        _lastTick = f.Tick;
    }

    int IndexOf(VisualFrame f, Side team, string id)
    {
        for (int i = 0; i < f.PlayerCount; i++)
            if (f.Players[i].Team == team && f.Players[i].Id == id) return i;
        return -1;
    }

    void Set(int index, PlayerAction action, long eventTick)
    {
        if (index < 0) return;
        _action[index] = action;
        _start[index] = UsesWindUp(action) ? eventTick - _windUp : eventTick;
    }

    void ApplyEvents(SnapshotBuffer buffer, VisualFrame f)
    {
        if (buffer.Count == 0) return;
        long now = (long)Math.Floor(f.Tick);
        long first = Math.Max(buffer.OldestTick, now - MaxHold);
        long last = Math.Min(buffer.NewestTick, now + _windUp);
        for (long t = first; t <= last; t++)
        {
            var events = buffer.EventsAt(t);
            for (int k = 0; k < events.Count; k++)
            {
                var e = events[k];
                var action = ActionFor(e, buffer.At(t));
                if (action == PlayerAction.None) continue;
                // Active from the (wind-up) start until the hold has passed.
                double start = UsesWindUp(action) ? t - _windUp : t;
                int hold = e.Type == EventType.Foul ? HoldTicks(PlayerAction.Fall) : HoldTicks(action); // the victim's fall outlasts the tackle
                if (f.Tick < start || f.Tick >= t + hold) continue;
                Dispatch(buffer, f, e, action, t);
            }
        }
    }

    static PlayerAction ActionFor(Event e, Snapshot? at)
    {
        double ballZ = at?.Ball?.Pos.Z ?? 0;
        switch (e.Type)
        {
            case EventType.Pass:
                if (e.Outcome.StartsWith("restart ThrowIn", StringComparison.Ordinal)) return PlayerAction.ThrowIn;
                return e.Outcome == "clear" ? PlayerAction.Shot : PlayerAction.Pass;
            case EventType.Shot: return PlayerAction.Shot;
            case EventType.BallControl: case EventType.FirstTouch: case EventType.Interception:
                return IsHeader(ballZ) ? PlayerAction.Header : PlayerAction.Control;
            case EventType.Save: return PlayerAction.KeeperDive;
            case EventType.Duel: case EventType.Foul: return PlayerAction.Tackle;
            case EventType.Goal: return PlayerAction.Celebrate;
            default: return PlayerAction.None;
        }
    }

    void Dispatch(SnapshotBuffer buffer, VisualFrame f, Event e, PlayerAction action, long t)
    {
        switch (e.Type)
        {
            case EventType.Goal:
                Celebrate(buffer, f, e, t);
                return;
            case EventType.Foul:
                if (f.Tick < t + HoldTicks(PlayerAction.Tackle)) Set(IndexOf(f, e.Team, e.PlayerId), PlayerAction.Tackle, t);
                if (f.Tick >= t) Set(NearestOpponent(buffer.At(t) ?? f.Base!, f, e.Team, e.Location), PlayerAction.Fall, t);
                return;
            default:
                Set(IndexOf(f, e.Team, e.PlayerId), action, t);
                return;
        }
    }

    int NearestOpponent(Snapshot at, VisualFrame f, Side offenderTeam, Vec2 where)
    {
        int best = -1; double bestD = double.MaxValue;
        for (int i = 0; i < at.Players.Count; i++)
        {
            var p = at.Players[i];
            if (p.Team == offenderTeam || p.SentOff) continue;
            double d = (p.Pos - where).Length;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best < 0 ? -1 : IndexOf(f, at.Players[best].Team, at.Players[best].Id);
    }

    void Celebrate(SnapshotBuffer buffer, VisualFrame f, Event goal, long t)
    {
        var at = buffer.At(t) ?? f.Base!;
        // The Goal event carries no player: the scorer is the latest shooter of that team, else the nearest player to the goal spot.
        string? scorerId = null;
        for (long s = t; s >= buffer.OldestTick && scorerId is null; s--)
        {
            var ev = buffer.EventsAt(s);
            for (int k = ev.Count - 1; k >= 0; k--)
                if (ev[k].Type == EventType.Shot && ev[k].Team == goal.Team && ev[k].PlayerId.Length > 0) { scorerId = ev[k].PlayerId; break; }
        }
        int scorer = -1;
        if (scorerId is not null)
            for (int i = 0; i < at.Players.Count; i++)
                if (at.Players[i].Team == goal.Team && at.Players[i].Id == scorerId) { scorer = i; break; }
        if (scorer < 0)
        {
            double bestD = double.MaxValue;
            for (int i = 0; i < at.Players.Count; i++)
            {
                var p = at.Players[i];
                if (p.Team != goal.Team || p.SentOff) continue;
                double d = (p.Pos - goal.Location).Length;
                if (d < bestD) { bestD = d; scorer = i; }
            }
        }
        if (scorer >= 0)
        {
            Set(IndexOf(f, at.Players[scorer].Team, at.Players[scorer].Id), PlayerAction.Celebrate, t);
            // Two nearest teammates (fixed-size selection, no allocation).
            int a = -1, b = -1; double da = double.MaxValue, db = double.MaxValue;
            for (int i = 0; i < at.Players.Count; i++)
            {
                var p = at.Players[i];
                if (i == scorer || p.Team != goal.Team || p.SentOff) continue;
                double d = (p.Pos - at.Players[scorer].Pos).Length;
                if (d < da) { b = a; db = da; a = i; da = d; }
                else if (d < db) { b = i; db = d; }
            }
            if (a >= 0) Set(IndexOf(f, at.Players[a].Team, at.Players[a].Id), PlayerAction.Celebrate, t);
            if (b >= 0) Set(IndexOf(f, at.Players[b].Team, at.Players[b].Id), PlayerAction.Celebrate, t);
        }
        for (int i = 0; i < at.Players.Count; i++)
        {
            var p = at.Players[i];
            if (p.Team == goal.Team || p.SentOff || f.Tick >= t + HoldTicks(PlayerAction.Disappointed)) continue;
            Set(IndexOf(f, p.Team, p.Id), PlayerAction.Disappointed, t);
        }
    }

    /// <summary>Actions derived from the current velocity and carrier, only where no event action is active.</summary>
    void ApplyMotionActions(VisualFrame f)
    {
        // Ids are unique per team only ("GK" exists on both sides): the carrier is the matching player nearest to the ball.
        int carrier = -1; double best = double.MaxValue;
        if (f.CarrierId is not null && f.HasBall)
            for (int i = 0; i < f.PlayerCount; i++)
            {
                if (f.Players[i].Id != f.CarrierId) continue;
                double d = (f.Players[i].Pos - f.BallPos.XY).Length;
                if (d < best) { best = d; carrier = i; }
            }
        for (int i = 0; i < f.PlayerCount; i++)
        {
            if (_action[i] != PlayerAction.None) continue;
            ref var p = ref f.Players[i];
            // A motion-derived action keeps the start tick of the previous frame while it continues.
            if (p.IsKeeper && LateralSpeed(p.Vel, p.Heading) > AdapterThresholds.KeeperDiveLateralSpeed)
            {
                _action[i] = PlayerAction.KeeperDive;
                _start[i] = _prevAction[i] == PlayerAction.KeeperDive ? _prevStart[i] : f.BaseTick;
            }
            else if (i == carrier && p.Vel.Length >= AdapterThresholds.IdleBelow)
            {
                _action[i] = PlayerAction.Dribble;
                _start[i] = _prevAction[i] == PlayerAction.Dribble ? _prevStart[i] : f.BaseTick;
            }
        }
    }

    // ---- Referee -----------------------------------------------------------------------------------------------

    const double DiagonalSlope = 0.45;

    /// <summary>Referee target before constraints: follows the ball along x, on a diagonal that flips with the ends.</summary>
    public static Vec2 RefereeTarget(Vec2 ball, bool endsSwapped)
    {
        double x = Math.Clamp(ball.X, -(Pitch.HalfLength - 8), Pitch.HalfLength - 8);
        return new Vec2(x, (endsSwapped ? -1 : 1) * DiagonalSlope * x);
    }

    /// <summary>Applies the pass-lane and minimum-ball-distance constraints and clamps near the pitch.</summary>
    public static Vec2 ConstrainReferee(Vec2 pos, Vec2 ball, Vec2 ballVelXY)
    {
        double speed = ballVelXY.Length;
        if (speed > 2)
        {
            var dir = ballVelXY * (1 / speed);
            var rel = pos - ball;
            double along = Math.Clamp(rel.X * dir.X + rel.Y * dir.Y, 0, 40);
            var closest = ball + dir * along;
            var off = pos - closest;
            double dist = off.Length;
            if (dist < AdapterThresholds.RefereeLaneClearance)
            {
                // Step sideways to the side the referee is already on (left of the lane when exactly on it).
                double side = dir.X * rel.Y - dir.Y * rel.X;
                double sign = side < 0 ? -1 : 1;
                var normal = new Vec2(-dir.Y * sign, dir.X * sign);
                pos = closest + normal * (AdapterThresholds.RefereeLaneClearance + 0.5);
            }
        }
        // Moving out of the lane can bring the referee back to the ball and the reverse: check the ball distance last
        // (a hard rule), then the lane once more, and the ball distance again if the second lane step broke it.
        pos = EnforceBallDistance(Clamp(pos), ball);
        if (speed > 2)
        {
            var dir = ballVelXY * (1 / speed);
            var rel = pos - ball;
            double along = Math.Clamp(rel.X * dir.X + rel.Y * dir.Y, 0, 40);
            var closest = ball + dir * along;
            if ((pos - closest).Length < AdapterThresholds.RefereeLaneClearance)
            {
                double side = dir.X * rel.Y - dir.Y * rel.X;
                double sign = side < 0 ? -1 : 1;
                var normal = new Vec2(-dir.Y * sign, dir.X * sign);
                pos = EnforceBallDistance(Clamp(closest + normal * (AdapterThresholds.RefereeLaneClearance + 0.5)), ball);
            }
        }
        return pos;
    }

    static Vec2 Clamp(Vec2 p) => new(
        Math.Clamp(p.X, -(Pitch.HalfLength + AdapterThresholds.RefereeMargin), Pitch.HalfLength + AdapterThresholds.RefereeMargin),
        Math.Clamp(p.Y, -(Pitch.HalfWidth + AdapterThresholds.RefereeMargin), Pitch.HalfWidth + AdapterThresholds.RefereeMargin));

    static Vec2 EnforceBallDistance(Vec2 pos, Vec2 ball)
    {
        const double min = AdapterThresholds.RefereeMinBallDistance;
        var away = pos - ball;
        double len = away.Length;
        if (len >= min) return pos;
        var dir = len < 1e-6 ? new Vec2(0, 1) : away * (1 / len);
        // Try the away direction first, then rotate in 45 degree steps when the pitch margin clamps the point back inside.
        for (int k = 0; k < 8; k++)
        {
            double a = k * Math.PI / 4;
            double c = Math.Cos(a), s = Math.Sin(a);
            var d = new Vec2(dir.X * c - dir.Y * s, dir.X * s + dir.Y * c);
            var candidate = Clamp(ball + d * min);
            if ((candidate - ball).Length >= min - 1e-9) return candidate;
        }
        return Clamp(ball + dir * min);
    }

    // Note: the referee's smoothed position depends on how often Update is called (the step size of the visual tick),
    // so it is render state and must never feed back into the simulation.
    void UpdateReferee(VisualFrame f)
    {
        if (!f.HasBall || f.Base is null) { Referee = default; return; }
        var ball = f.BallPos.XY;
        var target = RefereeTarget(ball, f.Base.EndsSwapped);
        double dt = double.IsNaN(_lastTick) || f.Tick < _lastTick ? double.PositiveInfinity : (f.Tick - _lastTick) * _stepSeconds;
        if (dt > 1 || f.Teleported)
            _refPos = target;
        else
        {
            var delta = target - _refPos;
            double dist = delta.Length;
            double maxStep = AdapterThresholds.RefereeSpeed * dt;
            _refPos = dist <= maxStep || dist < 1e-9 ? target : _refPos + delta * (maxStep / dist);
        }
        _refPos = ConstrainReferee(_refPos, ball, f.BallVel.XY);
        var toBall = ball - _refPos;
        Referee = new RefereeVisual {
            Pos3 = SimWorld.SimToWorld(_refPos, 0),
            Yaw = SimWorld.YawFromHeading(Math.Atan2(toBall.Y, toBall.X)),
            LookAt = SimWorld.SimToWorld(f.BallPos)
        };
    }
}
