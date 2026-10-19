// The home crowd without Godot: turns the events of a sim tick, the score, the minute and the danger of the ball into an
// intensity 0..1 (AudioManager mixes three looping beds on it) and a few one-shot reactions. Runs on sim time only, so a
// replay or a highlight sounds the same every time. Roadmap E1; later fan trust and attendance (F5) scale it.
using System;
using System.Collections.Generic;
using Zeus.Sim;

public sealed class CrowdModel
{
    public enum Reaction { Ooh, Sigh, Cheer, Groan, Whistles, Applause, Anticipation }

    const double Decay = 1.3;               // s: an impulse is down to 5% after 4 s
    const double DangerSmoothing = 0.8;     // s: the ball reaching the box lifts the crowd gradually
    static readonly double[] Gap = { 1.5, 3, 5, 5, 3, 4, 4 };       // s of sim time between two one-shots of the same Reaction

    readonly Side _home;
    readonly double _totalSeconds;
    readonly double[] _last = { -99, -99, -99, -99, -99, -99, -99 };
    readonly List<Reaction> _out = new();
    double _time = -1, _excite, _danger, _lastHomeShot = -99;

    public double Intensity { get; private set; }

    public CrowdModel(Side home = Side.Home, double totalSeconds = 5400) { _home = home; _totalSeconds = totalSeconds; }

    /// <summary>Calm level: grows a little with the minute; a difference of at most one goal after minute 75 keeps the crowd up.</summary>
    public static double Base(int homeScore, int awayScore, double minute) =>
        0.2 + 0.1 * Math.Clamp(minute / 90, 0, 1) + (Math.Abs(homeScore - awayScore) <= 1 ? 0.2 * Math.Clamp((minute - 75) / 15, 0, 1) : 0);

    /// <summary>0..1: ball in a final third, more the closer to that goal; the home attack counts fully, an away attack for 0.6.</summary>
    public double Danger(Snapshot s)
    {
        if (s.Ball is not { } ball || Math.Abs(ball.Pos.X) < Pitch.Length / 6) return 0;
        double dx = Pitch.HalfLength - Math.Abs(ball.Pos.X);
        double dist = Math.Sqrt(dx * dx + ball.Pos.Y * ball.Pos.Y);
        int homeSign = (_home == Side.Home ? 1 : -1) * (s.EndsSwapped ? -1 : 1);    // x runs in the home attacking direction (Pitch.cs), swapped after half time
        return Math.Clamp(1 - dist / 35, 0, 1) * (ball.Pos.X * homeSign > 0 ? 1 : 0.6);
    }

    /// <summary>One sim tick: the snapshot after the step and the events of that step. Returns the one-shots for this tick (rate-limited per kind).</summary>
    public IReadOnlyList<Reaction> Update(Snapshot s, IReadOnlyList<Event> events)
    {
        _out.Clear();
        double dt = _time < 0 ? 0 : Math.Max(0, s.TimeSeconds - _time);
        _time = s.TimeSeconds;
        _excite *= Math.Exp(-dt / Decay);
        _danger += (Danger(s) - _danger) * (1 - Math.Exp(-dt / DangerSmoothing));
        foreach (var e in events)
        {
            var (impulse, reaction) = Classify(e);
            _excite = Math.Max(_excite, impulse);
            if (reaction is { } r && s.TimeSeconds - _last[(int)r] >= Gap[(int)r]) { _last[(int)r] = s.TimeSeconds; _out.Add(r); }
        }
        Intensity = Math.Clamp(Base(s.HomeScore, s.AwayScore, s.TimeSeconds / _totalSeconds * 90) + 0.3 * _danger + _excite, 0, 1);
        return _out;
    }

    (double Impulse, Reaction? Reaction) Classify(Event e)
    {
        bool home = e.Team == _home;
        switch (e.Type)
        {
            case EventType.Goal: return (1.0, home ? Reaction.Cheer : Reaction.Groan);
            case EventType.Shot: if (home) _lastHomeShot = e.Seconds; return (0.5, null);
            case EventType.Save: return (0.5, Reaction.Ooh);
            case EventType.Out when e.Outcome == nameof(PitchExitKind.GoalKick):      // a home shot wide or over: a sigh
                return home && e.Seconds - _lastHomeShot < 5 ? (0.4, Reaction.Sigh) : (0, null);
            case EventType.Card: return (0.4, home ? Reaction.Whistles : null);       // against the referee
            case EventType.Foul: return (0.2, home ? null : Reaction.Whistles);       // foul on a home player
            case EventType.Offside: return (0.15, home ? Reaction.Whistles : null);
            case EventType.Out when e.Outcome == nameof(PitchExitKind.Corner):
            case EventType.RestartAwarded when e.Outcome is "Corner" or "penalty":
                return (e.Outcome == "penalty" ? 0.6 : 0.35, Reaction.Anticipation);
            case EventType.Substitution: case EventType.KickOff: return (0.2, Reaction.Applause);
            case EventType.HalfTime: case EventType.FullTime: return (0.4, Reaction.Applause);
            default: return (0, null);
        }
    }
}
