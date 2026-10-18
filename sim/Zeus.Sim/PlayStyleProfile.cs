namespace Zeus.Sim;

public enum PlayStyle { None, HighPress, LowBlock, Possession }

/// <summary>What the opponent can see of one of your matches: tactic settings plus (when the match was simulated) your share of ball controls.</summary>
public sealed record StyleSample(double Pressing, double LineHeight, double Tempo, double? Possession = null);

/// <summary>Smart opponent v1 (roadmap B4, v0 hypothesis): reads your style from your last matches and picks a fixed counter tactic.</summary>
public static class PlayStyleProfile
{
    public const int Window = 3, MaxKept = 5;

    /// <summary>Possession share = your ball controls / all ball controls; null when nobody controlled the ball (or no statistics, e.g. a live match).</summary>
    public static StyleSample Sample(Tactic player, MatchStatistics? stats = null, bool playerIsHome = true)
    {
        double? share = null;
        if (stats is not null)
        {
            int mine = (playerIsHome ? stats.Home : stats.Away).BallControls, all = stats.Home.BallControls + stats.Away.BallControls;
            if (all > 0) share = (double)mine / all;
        }
        return new StyleSample(player.Team.PressingIntensity, player.Team.LineHeight, player.Team.Tempo, share);
    }

    public static PlayStyle Classify(StyleSample s)
    {
        if (s.Pressing >= .65 && s.LineHeight >= .5) return PlayStyle.HighPress;
        if (s.Pressing <= .4 && s.LineHeight <= .4) return PlayStyle.LowBlock;
        if (s.Possession is >= .55 || (s.Possession is null && s.Tempo <= .5 && s.LineHeight >= .5)) return PlayStyle.Possession;
        return PlayStyle.None;
    }

    /// <summary>The style when each of your last <paramref name="window"/> matches had the same one; None otherwise (also with fewer matches).</summary>
    public static PlayStyle Detect(IReadOnlyList<StyleSample>? history, int window = Window)
    {
        if (history is null || history.Count < window) return PlayStyle.None;
        var styles = history.TakeLast(window).Select(Classify).Distinct().ToList();
        return styles.Count == 1 ? styles[0] : PlayStyle.None;
    }

    /// <summary>Fixed counter per style: a tactic file from tactics/ and the text key of the reason shown to the player.</summary>
    public static (string TacticFile, string ReasonKey)? Counter(PlayStyle style) => style switch
    {
        PlayStyle.HighPress => ("lange_bal_442.json", "opponent.adapt.high_press"),
        PlayStyle.Possession => ("laag_blok_442.json", "opponent.adapt.possession"),
        PlayStyle.LowBlock => ("hoge_druk_433.json", "opponent.adapt.low_block"),
        _ => null,
    };
}

/// <summary>The opponent's tactic change for the player's next match, with the reason (a text key).</summary>
public sealed record OpponentAdaptation(PlayStyle Style, string TacticFile, string ReasonKey);
