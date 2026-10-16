namespace Zeus.Sim;

public sealed record TeamStatistics(int Passes, int BallControls, int Interceptions, int Fouls,
    int YellowCards, int RedCards, int Shots = 0, int Goals = 0);
public sealed record MatchStatistics(double TimeSeconds, TeamStatistics Home, TeamStatistics Away,
    MatchStatus Status = MatchStatus.Finished, Side? AbandonedBy = null)
{
    /// <summary>True when the referee stopped the match (fewer than seven players, or no restart taker).</summary>
    public bool Abandoned => Status == MatchStatus.Abandoned;

    /// <summary>Minimum margin by which the team that caused the stoppage loses (assumption: simple 0-3 forfeit floor).</summary>
    public const int ForfeitMargin = 3;

    /// <summary>
    /// The result that counts. A finished match: the score. An abandoned match: the score at the moment of stopping, except that
    /// the team that caused it (fewer than seven players) loses by at least <see cref="ForfeitMargin"/> goals: if it was not
    /// already that far behind, the opponent is credited extra goals up to that margin.
    /// </summary>
    public (int Home, int Away) AwardedGoals()
    {
        int h = Home.Goals, a = Away.Goals;
        if (!Abandoned || AbandonedBy is null) return (h, a);
        if (AbandonedBy == Side.Home && a - h < ForfeitMargin) a = h + ForfeitMargin;
        else if (AbandonedBy == Side.Away && h - a < ForfeitMargin) h = a + ForfeitMargin;
        return (h, a);
    }

    public static MatchStatistics FromMatch(Match match)
    {
        TeamStatistics Count(Side team)
        {
            var events = match.Events.Events.Where(e => e.Team == team).ToArray();
            int CountType(EventType type) => events.Count(e => e.Type == type);
            return new TeamStatistics(CountType(EventType.Pass), CountType(EventType.BallControl),
                CountType(EventType.Interception), CountType(EventType.Foul),
                events.Count(e => e.Type == EventType.Card && e.Outcome is "Yellow" or "SecondYellowRed"),
                events.Count(e => e.Type == EventType.Card && e.Outcome is "Red" or "SecondYellowRed"),
                CountType(EventType.Shot), team == Side.Home ? match.HomeScore : match.AwayScore);
        }
        Side? abandonedBy = match.Status == MatchStatus.Abandoned
            ? match.Events.Events.Where(e => e.Type == EventType.Abandoned).Select(e => (Side?)e.Team).FirstOrDefault()
            : null;
        return new MatchStatistics(match.TimeSeconds, Count(Side.Home), Count(Side.Away), match.Status, abandonedBy);
    }
}
