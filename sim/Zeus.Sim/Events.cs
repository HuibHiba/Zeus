using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Zeus.Sim;

public enum EventType { KickOff, MoveStart, PossessionChange, Pass, BallControl, Interception, Duel, Foul,
    Card, Out, RestartAwarded, RestartTaken, Abandoned }

/// <summary>Minimal StatsBomb-like event. Location in metres; Outcome may be empty.</summary>
public sealed record Event(
    EventType Type, Side Team, string PlayerId, long Tick, double Seconds, Vec2 Location, string Outcome = "");

public sealed class EventLog
{
    private readonly List<Event> _events = new();
    public IReadOnlyList<Event> Events => _events;
    public void Add(Event e) => _events.Add(e);

    public string Hash() => Canonical.Hash(Canonical.Serialize(_events));
}

/// <summary>Canonical text form (fixed field order, invariant culture, 3 decimals) and SHA256 hashing.</summary>
public static class Canonical
{
    public static string Num(double v)
    {
        var s = Math.Round(v, 3, MidpointRounding.AwayFromZero).ToString("F3", CultureInfo.InvariantCulture);
        return s == "-0.000" ? "0.000" : s;
    }

    public static string Serialize(IEnumerable<Event> events)
    {
        var sb = new StringBuilder();
        foreach (var e in events)
            sb.Append(e.Type).Append('|').Append(e.Team).Append('|').Append(e.PlayerId).Append('|')
              .Append(e.Tick.ToString(CultureInfo.InvariantCulture)).Append('|').Append(Num(e.Seconds)).Append('|')
              .Append(Num(e.Location.X)).Append('|').Append(Num(e.Location.Y)).Append('|').Append(e.Outcome).Append('\n');
        return sb.ToString();
    }

    public static string Serialize(Snapshot s)
    {
        var sb = new StringBuilder();
        sb.Append(s.Tick.ToString(CultureInfo.InvariantCulture)).Append('|').Append(Num(s.TimeSeconds)).Append('\n');
        if (s.Status != MatchStatus.Running) sb.Append("Status|").Append(s.Status).Append('\n');
        if (s.Ball is { } b)
            sb.Append("Ball|").Append(Num(b.Pos.X)).Append('|').Append(Num(b.Pos.Y)).Append('|').Append(Num(b.Pos.Z)).Append('|')
              .Append(Num(b.Vel.X)).Append('|').Append(Num(b.Vel.Y)).Append('|').Append(Num(b.Vel.Z)).Append('\n');
        foreach (var p in s.Players)
            sb.Append(p.Team).Append('|').Append(p.Id).Append('|').Append(Num(p.Pos.X)).Append('|').Append(Num(p.Pos.Y)).Append('|')
              .Append(Num(p.Vel.X)).Append('|').Append(Num(p.Vel.Y)).Append('|').Append(Num(p.Heading)).Append('\n');
        foreach (var p in s.Players.Where(p => p.YellowCards > 0 || p.SentOff))
            sb.Append("Discipline|").Append(p.Team).Append('|').Append(p.Id).Append('|').Append(p.YellowCards)
                .Append('|').Append(p.SentOff).Append('\n');
        return sb.ToString();
    }

    public static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    public static string Hash(Snapshot s) => Hash(Serialize(s));
}
