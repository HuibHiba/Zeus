namespace Zeus.Sim;

// Popup logic without Godot: turns the event stream into a short list of match popups (goal, card, substitution, ...),
// filters them on the player's setting and the playback speed, and schedules them in two lanes. Display only: nothing here
// influences the simulation. The Godot overlay (MatchPopups) only draws what comes out of this file.

public enum PopupMode { All, Important, Off }

public enum PopupKind { Goal, YellowCard, RedCard, SecondYellow, Substitution, Foul, Offside, Penalty, Corner, HalfTime, SecondHalf, FullTime, Abandoned }

/// <summary>Minor = small strip at the bottom, Normal = card in the middle, Major = goal, red card, half time, full time.</summary>
public enum PopupWeight { Minor = 0, Normal = 1, Major = 2 }

public sealed record Popup(PopupKind Kind, PopupWeight Weight, Side Team, string PlayerId, string OtherPlayerId,
    int Minute, int HomeScore, int AwayScore, double Seconds, long Tick, string Detail = "")
{
    /// <summary>Same fact, whatever the order it arrived in; used to drop duplicates.</summary>
    public string Key => $"{Kind}|{Team}|{PlayerId}|{Tick}";
}

public static class PopupRules
{
    public const double FastSpeed = 8.0;             // from 16x only the very big moments remain (keys: 1x, 4x, 16x)

    public static PopupMode ParseMode(string? text) => text switch { "important" => PopupMode.Important, "off" => PopupMode.Off, _ => PopupMode.All };
    public static string ModeName(PopupMode mode) => mode switch { PopupMode.Important => "important", PopupMode.Off => "off", _ => "all" };

    static bool IsImportant(Popup p) => p.Kind is PopupKind.Goal or PopupKind.RedCard or PopupKind.SecondYellow or PopupKind.Penalty
        or PopupKind.HalfTime or PopupKind.SecondHalf or PopupKind.FullTime or PopupKind.Abandoned;

    static bool IsBigMoment(Popup p) => p.Kind is PopupKind.Goal or PopupKind.RedCard or PopupKind.SecondYellow
        or PopupKind.HalfTime or PopupKind.FullTime or PopupKind.Abandoned;

    /// <summary>Whether a popup is shown at all for this mode and playback speed. The final whistle and a suspended match always show unless popups are off.</summary>
    public static bool Allows(Popup p, PopupMode mode, double speed)
    {
        if (mode == PopupMode.Off) return false;
        if (speed >= FastSpeed && !IsBigMoment(p)) return false;
        return mode == PopupMode.All || IsImportant(p);
    }

    /// <summary>Display time shrinks a little at 4x, so a burst of events does not pile up.</summary>
    public static double Duration(Popup p, double speed) => p.Seconds * (speed >= 4.0 ? 0.75 : 1.0);

    public static List<Popup> Filter(IEnumerable<Popup> popups, PopupMode mode, double speed) =>
        popups.Where(p => Allows(p, mode, speed)).ToList();
}

/// <summary>Reads the event log once, in order (a cursor), so a replay, a pause or a skip never shows the same event twice.</summary>
public sealed class MatchNarrator
{
    int _cursor;
    int _home, _away;
    readonly HashSet<string> _seen = new();
    readonly Dictionary<Side, (string Player, double Seconds)> _lastShot = new();

    public int Cursor => _cursor;

    /// <summary>
    /// Processes the events not read yet. With <paramref name="suppress"/> (fast forward, skip to end) the score is still counted,
    /// but only the full time and suspended-match popups are returned.
    /// </summary>
    public List<Popup> Consume(IReadOnlyList<Event> events, bool suppress = false)
    {
        var raw = new List<Popup>();
        while (_cursor < events.Count)
        {
            var e = events[_cursor++];
            if (e.Type == EventType.Shot && e.PlayerId.Length > 0) _lastShot[e.Team] = (e.PlayerId, e.Seconds);
            if (Translate(e) is { } popup && _seen.Add(popup.Key)) raw.Add(popup);
        }
        // A penalty or a card replaces the foul of the same moment (one fact, one popup).
        raw.RemoveAll(p => p.Kind == PopupKind.Foul && raw.Any(q => q.Tick == p.Tick && (q.Kind == PopupKind.Penalty
            || (q.Kind is PopupKind.YellowCard or PopupKind.RedCard or PopupKind.SecondYellow && q.PlayerId == p.PlayerId))));
        if (suppress) raw.RemoveAll(p => p.Kind is not (PopupKind.FullTime or PopupKind.Abandoned));
        return raw;
    }

    static int MinuteOf(double seconds) => (int)(seconds / 60) + 1;

    Popup Make(PopupKind kind, PopupWeight weight, double seconds, Event e, string other = "", string detail = "") =>
        new(kind, weight, e.Team, e.PlayerId, other, MinuteOf(e.Seconds), _home, _away, seconds, e.Tick, detail);

    Popup? Translate(Event e)
    {
        switch (e.Type)
        {
            case EventType.Goal:
                if (e.Team == Side.Home) _home++; else _away++;
                string scorer = _lastShot.TryGetValue(e.Team, out var shot) && e.Seconds - shot.Seconds <= 10 ? shot.Player : "";
                return Make(PopupKind.Goal, PopupWeight.Major, 2.5, e with { PlayerId = scorer });
            case EventType.Card:
                return e.Outcome switch
                {
                    "Yellow" => Make(PopupKind.YellowCard, PopupWeight.Normal, 2.0, e),
                    "Red" => Make(PopupKind.RedCard, PopupWeight.Major, 2.0, e),
                    "SecondYellowRed" => Make(PopupKind.SecondYellow, PopupWeight.Major, 2.0, e),
                    _ => null,
                };
            case EventType.Substitution:
                var parts = e.Outcome.Split("->");
                return Make(PopupKind.Substitution, PopupWeight.Normal, 2.0, e, parts.Length == 2 ? parts[1] : "", parts[0]);
            case EventType.Foul: return Make(PopupKind.Foul, PopupWeight.Minor, 1.2, e);
            case EventType.Offside: return Make(PopupKind.Offside, PopupWeight.Minor, 1.2, e);
            case EventType.HalfTime: return Make(PopupKind.HalfTime, PopupWeight.Major, 2.5, e);
            case EventType.FullTime: return Make(PopupKind.FullTime, PopupWeight.Major, 2.2, e);
            case EventType.Abandoned: return Make(PopupKind.Abandoned, PopupWeight.Major, 3.0, e);
            case EventType.RestartAwarded:
                return e.Outcome switch
                {
                    "penalty" => Make(PopupKind.Penalty, PopupWeight.Normal, 2.0, e),
                    "Corner" => Make(PopupKind.Corner, PopupWeight.Minor, 1.2, e),
                    "second_half_kick_off" => Make(PopupKind.SecondHalf, PopupWeight.Normal, 1.8, e),
                    _ => null,       // free kicks follow from the foul popup, throw-ins and goal kicks are too frequent
                };
            default: return null;
        }
    }
}

/// <summary>
/// Two lanes: the main lane (goal, cards, substitution, ...) and the strip lane (short text at the bottom). Each lane shows one popup at
/// a time; a waiting Major popup jumps ahead of lower ones and cuts a lower popup that is showing short; a long backlog drops minor
/// popups. The lanes are independent, so a strip may appear while a card is showing.
/// </summary>
public sealed class PopupQueue
{
    public sealed class Active
    {
        public Popup Popup { get; }
        public double Duration { get; set; }
        public double Age { get; set; }
        public Active(Popup popup, double duration) { Popup = popup; Duration = duration; }
    }

    public const int MaxBacklog = 4;
    public const double CutShort = 0.35;                // remaining display time of a popup that makes way for a Major one

    readonly List<(Popup Popup, double Duration)> _waiting = new(), _waitingStrip = new();
    public Active? Main { get; private set; }
    public Active? Strip { get; private set; }
    public int Waiting => _waiting.Count + _waitingStrip.Count;

    public void Enqueue(Popup popup, double duration)
    {
        bool strip = popup.Weight == PopupWeight.Minor;
        var list = strip ? _waitingStrip : _waiting;
        if (list.Any(w => w.Popup.Key == popup.Key) || (strip ? Strip : Main)?.Popup.Key == popup.Key) return;
        int at = list.Count;
        if (popup.Weight == PopupWeight.Major)                                  // Major goes ahead of lower popups, behind other Major ones
            at = list.FindIndex(w => w.Popup.Weight < PopupWeight.Major) is var i and >= 0 ? i : list.Count;
        list.Insert(at, (popup, duration));
        while (list.Count > MaxBacklog)                                         // too busy: the oldest least important one goes
        {
            int drop = list.FindIndex(w => w.Popup.Weight == list.Min(x => x.Popup.Weight));
            list.RemoveAt(drop);
        }
        if (!strip && popup.Weight == PopupWeight.Major && Main is { } shown && shown.Popup.Weight < PopupWeight.Major)
            shown.Duration = Math.Min(shown.Duration, shown.Age + CutShort);
    }

    /// <summary>Playback sped up: everything waiting that is not a big moment is dropped.</summary>
    public void DropWaiting(Func<Popup, bool> drop)
    {
        _waiting.RemoveAll(w => drop(w.Popup));
        _waitingStrip.RemoveAll(w => drop(w.Popup));
    }

    public void Clear() { _waiting.Clear(); _waitingStrip.Clear(); Main = Strip = null; }

    /// <summary>Advances real time (not match time, so the speed does not change how long a popup stays). Pause: pass 0.</summary>
    public void Update(double dt)
    {
        Main = Advance(Main, _waiting, dt);
        Strip = Advance(Strip, _waitingStrip, dt);
    }

    static Active? Advance(Active? active, List<(Popup Popup, double Duration)> waiting, double dt)
    {
        if (active is not null)
        {
            active.Age += dt;
            if (active.Age >= active.Duration) active = null;
        }
        if (active is null && waiting.Count > 0)
        {
            active = new Active(waiting[0].Popup, waiting[0].Duration);
            waiting.RemoveAt(0);
        }
        return active;
    }
}
