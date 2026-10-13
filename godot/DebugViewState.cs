using System;
using System.Collections.Generic;
using System.Linq;
using Zeus.Sim;

public sealed record DebugViewConfig(double TriggerSeconds = 4, int RecentEventCount = 6)
{
    public void Validate()
    {
        if (!double.IsFinite(TriggerSeconds) || TriggerSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(TriggerSeconds));
        if (RecentEventCount < 1 || RecentEventCount > 6)
            throw new ArgumentOutOfRangeException(nameof(RecentEventCount));
    }
}

public sealed record DisplayPass(Event Event, string? ReceiverId, Vec2? ReceiverPosition);
public sealed record DisplayOffsideLine(Side Team, double X, IReadOnlyList<string> Candidates);

// Presentation state only: never changes the match or asks a brain for information.
public sealed class DebugViewState
{
    readonly DebugViewConfig _config;
    readonly List<Event> _recent = new();
    readonly List<Event> _triggers = new();
    GamePhase? _homePhase, _awayPhase;

    public DebugViewState(DebugViewConfig? config = null)
    {
        _config = config ?? new DebugViewConfig();
        _config.Validate();
    }

    public int PassCount { get; private set; }
    public DisplayPass? LastPass { get; private set; }
    public IReadOnlyList<Event> RecentEvents => _recent.AsReadOnly();
    public IReadOnlyList<Event> Triggers => _triggers.AsReadOnly();
    public GamePhase? PhaseOf(Side team) => team == Side.Home ? _homePhase : _awayPhase;
    public static string PhaseLabel(GamePhase? phase) => phase?.ToString().ToLowerInvariant() ?? "onbekend";

    public void Read(Event e, Snapshot snapshot)
    {
        _recent.Add(e);
        if (_recent.Count > _config.RecentEventCount) _recent.RemoveAt(0);
        if (e.Type == EventType.PhaseChange)
        {
            int arrow = e.Outcome.IndexOf("->", StringComparison.Ordinal);
            int colon = e.Outcome.IndexOf(':');
            if (arrow >= 0 && colon > arrow + 2
                && Enum.TryParse<GamePhase>(e.Outcome[(arrow + 2)..colon], out var phase)
                && Enum.IsDefined(phase))
            {
                if (e.Team == Side.Home) _homePhase = phase; else _awayPhase = phase;
            }
        }
        if (e.Type == EventType.TriggerFired) _triggers.Add(e);
        if (e.Type == EventType.Pass)
        {
            PassCount++;
            var fields = e.Outcome.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string? receiverId = fields.Length >= 2 && fields[0] == "to" ? fields[1] : null;
            var receiver = snapshot.Players.FirstOrDefault(p => p.Team == e.Team && p.Id == receiverId);
            LastPass = new DisplayPass(e, receiverId, receiver?.Pos);
        }
    }

    public void Advance(double seconds) => _triggers.RemoveAll(e => seconds >= e.Seconds + _config.TriggerSeconds);

    public static DisplayOffsideLine? OffsideLine(Snapshot snapshot, Side team)
    {
        if (snapshot.Ball is null) return null;
        bool positive = (team == Side.Home) != snapshot.EndsSwapped;
        double direction = positive ? 1 : -1;
        var opponents = snapshot.Players.Where(p => p.Team != team && !p.SentOff)
            .Select(p => direction * p.Pos.X).OrderByDescending(x => x).ToArray();
        if (opponents.Length < 2) return null;
        double line = Math.Max(0, Math.Max(opponents[1], direction * snapshot.Ball.Pos.X));
        return new DisplayOffsideLine(team, direction * line,
            OffsideRules.PositionsAtTouch(snapshot, team, positive));
    }
}
