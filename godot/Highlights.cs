// Display only: picks the goals, saves, penalties and big chances of a finished match for the end-card highlight reel.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Zeus.Sim;

/// <summary>One clip: the match ticks to play (Start..End) and its caption.</summary>
public sealed record Moment(long StartTick, long EndTick, string Label);

public static class Highlights
{
    public const double SecondsBefore = 6, SecondsAfter = 2;    // ~8 s per clip at 1x
    const double BigChance = 0.25, SaveWindow = 3;
    const int MaxMoments = 10;

    // Goals always make it; the other candidates compete on rank. Clips never overlap (anchors >= 8 s apart).
    public static List<Moment> Pick(IReadOnlyList<Event> events, double stepSeconds, ShotConfig shots)
    {
        var candidates = new List<(Event E, string Label, double Rank)>();
        foreach (var e in events)
        {
            int minute = (int)(e.Seconds / 60) + 1;
            if (e.Type == EventType.Goal) candidates.Add((e, Loc.T("highlight.goal", minute), 10));
            else if (e.Type == EventType.Shot && e.PlayerId.Length > 0 && !events.Any(g => g.Type == EventType.Goal && g.Team == e.Team && g.Tick >= e.Tick && g.Seconds - e.Seconds <= SaveWindow))
            {
                // shortcut: the shot is rated against the nearest goal (not the attacked one), fine for anything that is a real chance; revisit if far-out shots slip in.
                double goalX = Math.Sign(e.Location.X) * Pitch.HalfLength;
                double xg = ShotModel.GoalProbability(Math.Sqrt(Math.Pow(goalX - e.Location.X, 2) + e.Location.Y * e.Location.Y), e.Location.Y, shots);
                string chance = xg.ToString("0.00", CultureInfo.InvariantCulture);
                bool saved = events.Any(s => s.Type == EventType.Save && s.Team != e.Team && s.Tick >= e.Tick && s.Seconds - e.Seconds <= SaveWindow);
                if (e.Outcome == "restart penalty") candidates.Add((e, Loc.T("highlight.penalty", minute), 5));
                else if (saved) candidates.Add((e, Loc.T("highlight.save", minute, chance), 1 + xg));
                else if (xg >= BigChance) candidates.Add((e, Loc.T("highlight.chance", minute, chance), xg));
            }
        }
        var chosen = new List<(Event E, string Label, double Rank)>();
        foreach (var c in candidates.OrderByDescending(c => c.Rank).ThenBy(c => c.E.Tick))
            if (chosen.Count < MaxMoments && chosen.All(o => Math.Abs(o.E.Seconds - c.E.Seconds) >= SecondsBefore + SecondsAfter)) chosen.Add(c);
        return chosen.OrderBy(c => c.E.Tick)
            .Select(c => new Moment(Math.Max(0, c.E.Tick - (long)Math.Round(SecondsBefore / stepSeconds)), c.E.Tick + (long)Math.Round(SecondsAfter / stepSeconds), c.Label)).ToList();
    }
}
