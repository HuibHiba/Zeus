namespace Zeus.Sim;

/// <summary>One cause on the end card: a language key (why.&lt;Key&gt;), its number, and whether it helped (true) or hurt.</summary>
public sealed record MatchCause(string Key, double Value, bool Good, double Weight);

/// <summary>
/// The "why" of a result from the match statistics of one side: up to three causes, strongest first. Rules of thumb (v1), not a
/// model: few or many shots, chances not taken, shots against, possession, cards, fouls, passes intercepted, goals conceded.
/// </summary>
public static class MatchWhy
{
    public const int MaxCauses = 3;

    public static IReadOnlyList<MatchCause> Causes(MatchStatistics stats, Side side, bool scenario = false)
    {
        if (scenario) return Array.Empty<MatchCause>();
        var me = side == Side.Home ? stats.Home : stats.Away;
        var them = side == Side.Home ? stats.Away : stats.Home;
        double possession = 100.0 * me.BallControls / Math.Max(1, me.BallControls + them.BallControls);
        var causes = new List<MatchCause>();
        void Add(bool when, string key, double value, bool good, double weight) { if (when) causes.Add(new MatchCause(key, value, good, weight)); }
        Add(me.RedCards > 0, "red_card", me.RedCards, false, 0.9 + 0.1 * me.RedCards);
        Add(me.Shots < 8, "few_shots", me.Shots, false, 0.4 + (8 - me.Shots) / 8.0);
        Add(me.Shots >= 10 && me.Goals == 0, "no_finish", me.Shots, false, 0.6 + me.Shots / 40.0);
        Add(them.Shots >= 18, "shots_against", them.Shots, false, 0.3 + (them.Shots - 18) / 12.0);
        Add(them.Goals >= 3, "conceded", them.Goals, false, 0.5 + (them.Goals - 3) / 4.0);
        Add(possession < 40, "possession", possession, false, 0.2 + (40 - possession) / 40.0);
        Add(me.Fouls >= 16, "fouls", me.Fouls, false, 0.3 + (me.Fouls - 16) / 20.0);
        Add(them.Interceptions >= 25, "interceptions", them.Interceptions, false, 0.3 + (them.Interceptions - 25) / 30.0);
        Add(me.Shots >= 18, "good_shots", me.Shots, true, 0.4 + (me.Shots - 18) / 12.0);
        Add(possession >= 60, "good_possession", possession, true, 0.2 + (possession - 60) / 40.0);
        Add(them.Shots <= 8 && stats.TimeSeconds > 0, "good_defence", them.Shots, true, 0.4 + (8 - them.Shots) / 8.0);
        Add(me.Goals >= 3, "good_goals", me.Goals, true, 0.5 + (me.Goals - 3) / 4.0);
        return causes.OrderByDescending(c => c.Weight).ThenBy(c => c.Key, StringComparer.Ordinal).Take(MaxCauses).ToList();
    }
}
