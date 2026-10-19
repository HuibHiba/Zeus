using System.Globalization;

namespace Zeus.Sim;

/// <summary>
/// Challenge of the day: generated from the date alone, so everyone gets the same one and a friend can replay a shared result.
/// Opponent, level, kick-off scenario and two goals are drawn with a seeded Rng from fixed lists; thresholds are deliberately
/// not calibrated (it is a daily surprise, not a measured ladder). Id "dag_YYYYMMDD"; progress is saved under that id like any challenge.
/// </summary>
public static class DailyChallenge
{
    public const string IdPrefix = "dag_";
    static readonly string[] Opponents = { "hoge_druk_433.json", "positiespel_4231.json", "counter_532.json", "laag_blok_442.json", "lange_bal_442.json", "flankdruk_352.json", "vleugelspel_433.json" };
    static readonly string[] Levels = { "amateur", "halfprof", "prof" };
    static readonly (string Metric, string Op, double Value)[] Goals =
    {
        ("ongeslagen", ">=", 3), ("winst", ">=", 2), ("doelpunten_voor", ">=", 1.0), ("doelpunten_tegen", "<=", 1.6),
        ("balbezit", ">=", 50), ("schoten_tegen", "<=", 20), ("schoten_voor", ">=", 14), ("gele_kaarten", "<=", 1.5),
    };

    public static string IdFor(DateOnly date) => IdPrefix + date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    public static bool IsDaily(string id) => id.StartsWith(IdPrefix, StringComparison.Ordinal);

    /// <summary>The spec for an id "dag_YYYYMMDD", or null when the id is not a daily one.</summary>
    public static ChallengeSpec? FromId(string id, string language = "nl") =>
        IsDaily(id) && DateOnly.TryParseExact(id[IdPrefix.Length..], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? ForDate(date, language) : null;

    public static ChallengeSpec ForDate(DateOnly date, string language = "nl")
    {
        bool nl = language != "en";
        var rng = new Rng((ulong)date.DayNumber * 0x9E3779B97F4A7C15UL + 7);
        var opponent = Opponents[rng.NextUInt((uint)Opponents.Length)];
        var level = Levels[rng.NextUInt((uint)Levels.Length)];
        int first = (int)rng.NextUInt((uint)Goals.Length), second = (first + 1 + (int)rng.NextUInt((uint)(Goals.Length - 1))) % Goals.Length;
        var criteria = new[] { Goals[first], Goals[second] }.Select(g => new ChallengeCriterion(g.Metric, g.Op, g.Value)).ToList();
        // Two days in five are a scenario: a man down for them, or a lead to defend from the 65th minute.
        int twist = (int)rng.NextUInt(5);
        bool tenMen = twist == 0, lead = twist == 1;
        var c0 = criteria[0];
        var stricter = c0 with { Value = c0.Operator == ">=" ? c0.Value + (c0.Metric is "winst" or "ongeslagen" ? 1 : c0.Metric == "balbezit" ? 5 : 0.5)
            : c0.Value - (c0.Metric == "schoten_tegen" ? 4 : 0.5) };
        string name = nl ? "Uitdaging van de dag " + date.ToString("d MMM", CultureInfo.GetCultureInfo("nl-NL")) : "Challenge of the day " + date.ToString("d MMM", CultureInfo.InvariantCulture);
        string opp = Path.GetFileNameWithoutExtension(opponent).Replace('_', ' ');
        string twistText = tenMen ? (nl ? " Zij beginnen met tien man." : " They start with ten men.") : lead ? (nl ? " Je begint in de 65e minuut met 1-0 voor." : " You start in minute 65 leading 1-0.") : "";
        string description = nl ? $"Elke dag een andere proef, voor iedereen dezelfde. Vandaag: {opp} op niveau {level}, vijf wedstrijden.{twistText}"
            : $"A different test every day, the same for everyone. Today: {opp} at {level} level, five matches.{twistText}";
        string hint = nl ? "Niet geijkt: soms makkelijk, soms onmogelijk. Deel je uitslag met een vriend via de uitdagingscode."
            : "Not calibrated: sometimes easy, sometimes impossible. Share your result with a friend through the challenge code.";
        return new ChallengeSpec(IdFor(date), name, description, opponent, 5, criteria, hint, OpponentLevel: level,
            StarCriteria: new[] { (IReadOnlyList<ChallengeCriterion>)new[] { stricter } }, Theme: nl ? "Vandaag" : "Today",
            StartFor: lead ? 1 : 0, OpponentSentOff: tenMen, StartMinute: lead ? 65 : 0);
    }
}
