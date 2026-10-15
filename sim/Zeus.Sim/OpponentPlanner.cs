using System.Globalization;

namespace Zeus.Sim;

/// <summary>What an opponent can learn about a tactic by watching it: a coarse style, not exact positions (v0 assumption).</summary>
public sealed record StyleProfile(string Block, double Pressing, double Tempo, double LineHeight, int Markers)
{
    static readonly string[] Blocks = { "hoog", "midden", "laag" };

    public static StyleProfile From(Tactic tactic)
    {
        var layout = tactic.Phases[GamePhase.Verdedigen];
        var block = (layout.Block ?? BlockHeight.Midden) switch { BlockHeight.Hoog => "hoog", BlockHeight.Laag => "laag", _ => "midden" };
        return new StyleProfile(block, tactic.Team.PressingIntensity, tactic.Team.Tempo, tactic.Team.LineHeight,
            layout.Players.Count(p => p.Mark is not null));
    }

    /// <summary>0 = identical style, 1 = opposite. Mean of five normalised differences.</summary>
    public double DistanceTo(StyleProfile other) =>
        (Math.Abs(Array.IndexOf(Blocks, Block) - Array.IndexOf(Blocks, other.Block)) / 2.0 + Math.Abs(Pressing - other.Pressing)
            + Math.Abs(Tempo - other.Tempo) + Math.Abs(LineHeight - other.LineHeight) + Math.Abs(Markers - other.Markers) / 11.0) / 5.0;

    public string Describe() =>
        $"{Block} blok met {(Pressing >= 0.65 ? "hoge" : Pressing <= 0.35 ? "lage" : "gematigde")} druk en {(Tempo >= 0.65 ? "hoog" : Tempo <= 0.4 ? "laag" : "gemiddeld")} tempo"
        + (Markers >= 6 ? " (man-dekking)" : "");
}

public sealed record HistoryEntry(string TacticName, StyleProfile Style);

public sealed record CandidateScore(string Name, double Points, double GoalDifference, int Matches);

public sealed record OpponentPlan(bool Prepared, string OpponentName, string Reason, IReadOnlyList<CandidateScore> Scores);

/// <summary>
/// The 'smart opponent' (roadmap phase 7, v0 hypothesis). It only reacts to what it can know: a coarse style of your last
/// matches. If you keep playing the same style, it prepares: it plays a few practice matches with each candidate tactic
/// against your tactic and picks the best result. Without such a pattern it keeps its normal tactic.
/// </summary>
public static class OpponentPlanner
{
    public const int Window = 5, MinSimilar = 3;
    public const double SimilarityThreshold = 0.2;

    /// <summary>The opponent starts preparing when at least <paramref name="minSimilar"/> of your last <paramref name="window"/> matches look like <paramref name="current"/>.</summary>
    public static bool ShouldPrepare(IReadOnlyList<HistoryEntry> history, StyleProfile current, int window = Window, int minSimilar = MinSimilar,
        double threshold = SimilarityThreshold) =>
        history.TakeLast(window).Count(h => h.Style.DistanceTo(current) <= threshold) >= minSimilar;

    public static int SimilarCount(IReadOnlyList<HistoryEntry> history, StyleProfile current, int window = Window, double threshold = SimilarityThreshold) =>
        history.TakeLast(window).Count(h => h.Style.DistanceTo(current) <= threshold);

    public static OpponentPlan Plan(Tactic player, IReadOnlyList<(string Name, Tactic Tactic)> pool, string defaultOpponent,
        IReadOnlyList<HistoryEntry> history, CancellationToken token = default, int matchesPerCandidate = 4)
    {
        var style = StyleProfile.From(player);
        if (!ShouldPrepare(history, style))
            return new OpponentPlan(false, defaultOpponent, "De tegenstander speelt zijn gewone tactiek: je bent nog niet voorspelbaar genoeg.", Array.Empty<CandidateScore>());

        var candidates = pool.Where(c => c.Tactic.Name != player.Name).OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
        if (candidates.Count == 0)
            return new OpponentPlan(false, defaultOpponent, "De tegenstander had geen alternatief om mee voor te bereiden.", Array.Empty<CandidateScore>());

        var scores = new CandidateScore[candidates.Count];
        Parallel.For(0, candidates.Count, new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, i =>
        {
            double points = 0, diff = 0;
            for (int s = 0; s < matchesPerCandidate; s++)
            {
                token.ThrowIfCancellationRequested();
                var match = Match.Create(new MatchConfig(), player, candidates[i].Tactic, (ulong)(1000 + s));   // practice seeds, apart from the challenge seeds
                for (int steps = 0; match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned) && steps < 150000; steps++) match.Step();
                var stats = MatchStatistics.FromMatch(match);
                int opponentGoals = stats.Away.Goals, playerGoals = stats.Home.Goals;
                points += opponentGoals > playerGoals ? 3 : opponentGoals == playerGoals ? 1 : 0;
                diff += opponentGoals - playerGoals;
            }
            scores[i] = new CandidateScore(candidates[i].Name, points, diff, matchesPerCandidate);
        });
        var best = scores.OrderByDescending(c => c.Points).ThenByDescending(c => c.GoalDifference).ThenBy(c => c.Name, StringComparer.Ordinal).First();
        int similar = SimilarCount(history, style);
        var reason = string.Create(CultureInfo.GetCultureInfo("nl-NL"),
            $"Je speelde {similar} van je laatste {Math.Min(Window, history.Count)} wedstrijden {style.Describe()}. De tegenstander heeft zich daarop voorbereid en speelt nu '{best.Name}' ({best.Points:0} van {best.Matches * 3} punten in de oefenwedstrijden).");
        return new OpponentPlan(true, best.Name, reason, scores);
    }
}
