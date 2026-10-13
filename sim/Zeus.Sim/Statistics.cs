namespace Zeus.Sim;

public sealed record TeamStatistics(int Passes, int BallControls, int Interceptions, int Fouls,
    int YellowCards, int RedCards, int Shots = 0, int Goals = 0);
public sealed record MatchStatistics(double TimeSeconds, TeamStatistics Home, TeamStatistics Away)
{
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
        return new MatchStatistics(match.TimeSeconds, Count(Side.Home), Count(Side.Away));
    }
}
