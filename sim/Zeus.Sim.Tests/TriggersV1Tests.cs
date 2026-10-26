using System.Text.Json.Nodes;
using Zeus.Sim;
namespace Zeus.Sim.Tests;

/// <summary>Triggers v1 in the tactic maker (docs/triggers-v1.md): rules made with the editor model fire in a match,
/// can be edited in place, survive save/load, and the meter's "without rules" baseline equals a tactic without rules.</summary>
public class TriggersV1Tests
{
    // Editor model from the reference tactic with its own rules removed, plus one rule from the editor.
    private static TacticEditorModel WithOneRule(string condition, string? zone = null, string player = "RS", string instruction = "start_pressing")
    {
        var m = TacticEditorModel.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        while (m.Triggers.Count > 0) m.RemoveTrigger(0);
        m.AddTrigger("v1", TacticEditorModel.PhaseOfCondition(condition) ?? "verdedigen", condition, zone, new[] { (player, instruction) });
        return m;
    }

    private static Match Play(Tactic home, Tactic away, ulong seed = 3, int steps = 12000)
    {
        var m = Match.Create(new MatchConfig(), home, away, seed);
        for (int i = 0; i < steps; i++) m.Step();
        return m;
    }

    // Event conditions that fire in open play within 20 simulated minutes; the minute-70 state rule is covered by
    // TriggerTests.StateConditionsAreSupportedAndBehindNeedsMinuteSeventy (a fixed match needs > 70 minutes to reach it).
    [Theory]
    // balwinst_in_hoge_zone fires (TriggerTests) but showed no change in play here with start_pressing or kom_op: not one of the v1 five.
    [InlineData("balverlies_in_eigen_helft", null, "start_pressing")]
    [InlineData("slechte_aanname", null, "start_pressing")]
    [InlineData("tegenstander_heeft_bal_in_zone", "V-C", "start_pressing")]
    [InlineData("tegenstander_ontvangt_op_flank", null, "start_pressing")]
    [InlineData("bal_terug_naar_middenvelder", null, "start_pressing")]
    public void EditorMadeRuleFiresAndChangesAFixedMatch(string condition, string? zone, string instruction)
    {
        var rule = TacticLoader.Parse(WithOneRule(condition, zone, instruction: instruction).ToJson());
        var plain = rule.WithoutTriggers();
        // first seed (of four, 50 simulated minutes each) where the rule acts; rare events need more than one match
        var (seed, withRule) = Enumerable.Range(1, 4).Select(s => ((ulong)s, Play(rule, plain, (ulong)s, 30000)))
            .FirstOrDefault(x => x.Item2.Events.Events.Any(e => e.Type == EventType.TriggerFired && e.Team == Side.Home));
        Assert.NotNull(withRule);
        var fired = withRule.Events.Events.Where(e => e.Type == EventType.TriggerFired && e.Team == Side.Home).ToArray();
        Assert.All(fired, e => { Assert.Equal("RS", e.PlayerId); Assert.EndsWith(condition, e.Outcome); });
        Assert.NotEqual(Play(plain, plain, seed, 30000).SnapshotHash(), withRule.SnapshotHash());    // play changes, not only the TriggerFired events
    }

    // The meter's own run (Meter.Compare: seeds 1..N, both legs, 2x45 min, A vs A without rules; the meter uses N = 10): each example
    // tactic's rule fires and moves one table row by more than three standard errors of the per-match difference. Vleugelspel needs
    // N = 20 (at N = 10 interceptions give t = -2.1). Hoge druk 4-3-3 is missing on purpose: no rule found that changes it
    // measurably (docs/triggers-v1.md).
    // Flankdruk 3-5-2 is not in this list: with jockeying + CautionedAvoidsBehind on (a294498) its touches effect is gone
    // (mean 3,33, se 6,84 at n = 60); its rule needs re-measuring (Review-fixes nieuwe demo, punt 22).
    [Theory]
    [InlineData("vleugelspel_433", "interceptions", -1, 20)]
    [InlineData("lange_bal_442", "shots", 1, 30)]
    public void ExampleTacticRuleFiresAndMovesTheMeter(string tactic, string stat, int sign, int seeds)
    {
        var a = TacticLoader.LoadFile(TacticLoaderTests.TacticPath(tactic));
        var b = a.WithoutTriggers();
        int n = 2 * seeds;
        var diff = new double[n];
        var fired = new int[n];
        Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
        {
            int leg = i % 2;
            var m = Match.Create(new MatchConfig(), leg == 0 ? a : b, leg == 0 ? b : a, (ulong)(i / 2 + 1));
            for (int steps = 0; m.Status is not (MatchStatus.Finished or MatchStatus.Abandoned) && steps < 150000; steps++) m.Step();
            var s = MatchStatistics.FromMatch(m);
            var (sa, sb) = leg == 0 ? (s.Home, s.Away) : (s.Away, s.Home);
            int Value(TeamStatistics t) => stat switch { "interceptions" => t.Interceptions, "shots" => t.Shots, _ => t.BallControls };
            diff[i] = Value(sa) - Value(sb);
            fired[i] = m.Events.Events.Count(e => e.Type == EventType.TriggerFired && e.Team == (leg == 0 ? Side.Home : Side.Away));
        });
        Assert.True(fired.Average() >= 10, $"rule actions per match {fired.Average()}");
        double mean = diff.Average(), se = Math.Sqrt(diff.Sum(d => (d - mean) * (d - mean)) / (n - 1) / n);
        Assert.True(sign * mean > 3 * se, $"{stat}: mean A-B {mean:F2}, standard error {se:F2}");
    }

    [Fact]
    public void EditorOffersOnlyInstructionsARuleCanCarryOut()
    {
        // the tactic maker's instruction list for rules (Editor.BuildRulesTab) is this set plus the drawn path
        foreach (var instruction in TriggerRules.SupportedInstructions.Where(i => i is not ("dek_man" or "vrije_man_zoeken")))
        {
            Assert.Contains(instruction, TacticEditorModel.AllowedInstructions);
            Assert.True(TriggerRules.IsSupported(new Trigger("t", GamePhase.Verdedigen, "bal_naar_keeper", null, new[] { new TriggerAction("ST", instruction) })));
        }
        Assert.False(TriggerRules.IsSupported(new Trigger("t", GamePhase.Verdedigen, "bal_naar_keeper", null, new[] { new TriggerAction("ST", "loop_diep") })));
    }

    [Fact]
    public void WithoutTriggersIsTheSameMatchAsATacticSavedWithoutRules()
    {
        var withRules = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        Assert.NotEmpty(withRules.Triggers);
        var m = TacticEditorModel.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        while (m.Triggers.Count > 0) m.RemoveTrigger(0);
        var saved = TacticLoader.Parse(m.ToJson());
        var opponent = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433"));
        Assert.Equal(Play(saved, opponent, steps: 6000).EventHash(), Play(withRules.WithoutTriggers(), opponent, steps: 6000).EventHash());
        Assert.NotEmpty(withRules.Triggers);          // the original is untouched
    }

    [Fact]
    public void ReplaceTriggerEditsInPlaceAndRollsBackInvalidEdits()
    {
        var m = WithOneRule("bal_naar_keeper", "A-C");
        m.AddTrigger("tweede", "balwinst", "balwinst_in_hoge_zone", null, new[] { ("LS", "start_pressing") });
        m.ReplaceTrigger(0, "nieuw", "balverlies", "balverlies_in_eigen_helft", null, new[] { ("LM", "start_pressing"), ("RM", "start_pressing") });
        var t = m.Triggers[0];
        Assert.Equal(("nieuw", "balverlies", "balverlies_in_eigen_helft", (string?)null), (t.Name, t.Phase, t.Condition, t.Zone));
        Assert.Equal(new[] { ("LM", "start_pressing"), ("RM", "start_pressing") }, t.Actions);
        Assert.Equal("tweede", m.Triggers[1].Name);
        var before = m.ToJson();
        Assert.ThrowsAny<Exception>(() => m.ReplaceTrigger(0, "x", "verdedigen", "bal_naar_keeper", null, new[] { ("ZZ", "start_pressing") }));
        Assert.Throws<ArgumentException>(() => m.ReplaceTrigger(0, "x", "verdedigen", "onbekend", null, new[] { ("ST", "start_pressing") }));
        Assert.Throws<ArgumentOutOfRangeException>(() => m.ReplaceTrigger(2, "x", "verdedigen", "bal_naar_keeper", null, new[] { ("RS", "start_pressing") }));
        Assert.Equal(before, m.ToJson());
        // and it survives save/load
        var loaded = TacticLoader.Parse(m.ToJson());
        Assert.Equal(2, loaded.Triggers.Count);
        Assert.Equal(2, loaded.Triggers[0].Then.Count);
    }

    [Fact]
    public void OldTacticWithoutRulesLoadsAndReturnsToTheOldFormatWhenRulesAreRemoved()
    {
        var path = TacticLoaderTests.TacticPath("positiespel_4231");
        Assert.Null(JsonNode.Parse(File.ReadAllText(path))!["triggers"]);
        var m = TacticEditorModel.LoadFile(path);
        Assert.Empty(m.Triggers);
        var id = m.PlayerIds[^1];
        m.AddTrigger("r", "verdedigen", "bal_naar_keeper", null, new[] { (id, "start_pressing") });
        Assert.Single(TacticLoader.Parse(m.ToJson()).Triggers);
        m.RemoveTrigger(0);
        Assert.Null(JsonNode.Parse(m.ToJson())!["triggers"]);
        TacticLoader.Parse(m.ToJson());
    }
}
