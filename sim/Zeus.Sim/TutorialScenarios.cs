namespace Zeus.Sim;

/// <summary>The three guided practice matches of the tutorial (data/tutorial/*.json): ordinary challenge files that never count for progress.</summary>
public static class TutorialScenarios
{
    public static readonly string[] Ids = { "tutorial_a_houd_stand", "tutorial_b_kom_terug", "tutorial_c_lees_de_wedstrijd" };

    /// <summary>The scenarios in tutorial order (A, B, C); throws when one is missing or invalid, so a broken install shows at once.</summary>
    public static IReadOnlyList<ChallengeSpec> Load(string directory, string language = "en")
    {
        var all = ChallengeEvaluator.LoadAll(directory, out var errors, language);
        if (errors.Count > 0) throw new FormatException("Tutorial scenarios: " + string.Join("; ", errors));
        return Ids.Select(id => all.FirstOrDefault(s => s.Id == id) ?? throw new FormatException($"Tutorial scenario '{id}' is missing.")).ToList();
    }
}
