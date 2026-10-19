namespace Zeus.Sim;

/// <summary>One scouting observation: a language key (scout.&lt;Key&gt;) and whether the scout is sure (else "probably").</summary>
public sealed record ScoutNote(string Key, bool Sure);

/// <summary>
/// B4 scouting with uncertainty: before a league match the scout describes the opponent's pressing, line height and tempo. The
/// true values come from the tactic; the scout sees them through seeded noise that shrinks with the division level (amateur
/// scouts guess, professional scouts know). A reading near the middle gives no note; a reading inside the noise band is "probably".
/// </summary>
public static class Scouting
{
    public const double Band = 0.15;

    /// <summary>Noise amplitude per level: how far a reading may be off.</summary>
    public static double NoiseFor(string level) => level switch { "prof" => 0.05, "halfprof" => 0.12, "amateur" => 0.25, _ => 0.12 };

    public static IReadOnlyList<ScoutNote> Report(Tactic opponent, int season, int round, string level)
    {
        var truth = PlayStyleProfile.Sample(opponent);
        var rng = new Rng(Competition.Seed(season, 40 + round, 1, 2));
        double noise = NoiseFor(level);
        double Seen(double v) => Math.Clamp(v + (rng.NextDouble() * 2 - 1) * noise, 0, 1);
        var notes = new List<ScoutNote>();
        void Note(double seen, string high, string low)
        {
            double away = Math.Abs(seen - 0.5);
            if (away < Band) return;                                   // nothing worth saying
            notes.Add(new ScoutNote(seen > 0.5 ? high : low, away >= Band + noise));
        }
        Note(Seen(truth.Pressing), "press_high", "press_low");
        Note(Seen(truth.LineHeight), "line_high", "line_low");
        Note(Seen(truth.Tempo), "tempo_high", "tempo_low");
        return notes;
    }
}
