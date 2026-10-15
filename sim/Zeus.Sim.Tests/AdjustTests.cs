using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class AdjustTests
{
    private const string Tactic = "testcase_a_433"; // has verdedigen_varianten hoog and laag
    private static Match Create(ulong seed, string tactic = Tactic) =>
        Match.Create(new MatchConfig(HalfSeconds: 300), SubstitutionTests.Tactic(tactic), SubstitutionTests.Tactic(tactic), seed);

    private static string Mark(Match m) => m.TacticOf(Side.Home).Roster.Last().Id;
    private static string State(Match m) => m.SnapshotHash() + m.EventHash() + m.Commands.Count;

    private static void AssertRejected(Match match, TacticAdjustment adjustment, string messagePart)
    {
        string before = State(match);
        var tactic = match.TacticOf(Side.Home);
        var error = Assert.Throws<MatchCommandException>(() => match.Adjust(Side.Home, adjustment));
        Assert.Contains(messagePart, error.Message);
        Assert.Equal(before, State(match));
        Assert.Same(tactic, match.TacticOf(Side.Home));
    }

    // Mean depth (metres from the own goal line) of the home field players while the home team defends.
    private static double MeanDefendingDepth(Match match, int steps)
    {
        double sum = 0; int n = 0;
        for (int i = 0; i < steps; i++)
        {
            match.Step();
            if (i < 100 || i % 10 != 0 || match.PhaseOf(Side.Home) != GamePhase.Verdedigen) continue;
            var field = match.GetSnapshot().Players.Where(p => p.Team == Side.Home && p.Id != "GK" && !p.SentOff).ToArray();
            sum += field.Average(p => p.Pos.X + Pitch.HalfLength); n++;
        }
        Assert.True(n > 20, "too few defending samples");
        return sum / n;
    }

    [Fact]
    public void LowBlockGivesALowerAverageDefendingLineThanHighBlockAcrossSeeds()
    {
        double low = 0, high = 0;
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4 })
        {
            var a = Create(seed); a.Adjust(Side.Home, new TacticAdjustment(Block: BlockHeight.Laag));
            var b = Create(seed); b.Adjust(Side.Home, new TacticAdjustment(Block: BlockHeight.Hoog));
            Assert.Equal(BlockHeight.Laag, a.BlockOf(Side.Home));
            Assert.Equal(BlockHeight.Hoog, b.BlockOf(Side.Home));
            low += MeanDefendingDepth(a, 2500); high += MeanDefendingDepth(b, 2500);
        }
        Assert.True(low / 4 + 3 < high / 4, $"low {low / 4:F1} m, high {high / 4:F1} m");
    }

    [Fact]
    public void AdjustChangesTheTacticAndKeepsTheChosenBlockAgainstAutomaticSwitches()
    {
        var match = Create(9);
        var before = match.TacticOf(Side.Home);
        match.Adjust(Side.Home, new TacticAdjustment(Tempo: .9, PressingIntensity: .2, LineHeight: .4, Compactness: .7,
            PressingForm: "gemengd", Block: BlockHeight.Laag));
        var team = match.TacticOf(Side.Home).Team;
        Assert.Equal((.9, .2, .4, .7, "gemengd"), (team.Tempo, team.PressingIntensity, team.LineHeight, team.Compactness, team.PressingForm));
        Assert.NotEqual(before.Team, team);
        Assert.Equal(before.Team, match.TacticOf(Side.Away).Team); // the other team is untouched
        for (int i = 0; i < 3000; i++) match.Step(); // 1 minute: block switches are evaluated every step and must not override
        Assert.Equal(BlockHeight.Laag, match.BlockOf(Side.Home));
        var e = Assert.Single(match.Events.Events, e => e.Type == EventType.TacticChange);
        Assert.Equal("tempo=0.900;pressing=0.200;lijn=0.400;compact=0.700;vorm=gemengd;blok=Laag", e.Outcome);
    }

    [Fact]
    public void AdjustChangesBehaviourMeasurably()
    {
        // The tactic starts in the high block; adjusting to the low block moves the defending line back in the same matches.
        double plain = 0, adjusted = 0;
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4 })
        {
            var a = Create(seed); var b = Create(seed);
            Assert.Equal(BlockHeight.Hoog, a.BlockOf(Side.Home));
            b.Adjust(Side.Home, new TacticAdjustment(Block: BlockHeight.Laag, Compactness: 1));
            plain += MeanDefendingDepth(a, 2500); adjusted += MeanDefendingDepth(b, 2500);
            Assert.NotEqual(a.EventHash(), b.EventHash());
        }
        Assert.True(adjusted / 4 + 3 < plain / 4, $"adjusted {adjusted / 4:F1} m, plain {plain / 4:F1} m");
    }

    [Fact]
    public void InstructionsCanBeAddedAndRemovedForAPlayer()
    {
        var match = Create(3);
        match.Adjust(Side.Home, new TacticAdjustment(Instructions: [new InstructionChange("RB", Add: ["blijf_achter", "kom_op"])]));
        var phases = match.TacticOf(Side.Home).Phases.Values.Concat(match.TacticOf(Side.Home).DefendVariants.Values);
        Assert.All(phases, l => Assert.Contains("blijf_achter", l.Players.Single(p => p.Id == "RB").Instructions));
        Assert.All(match.TacticOf(Side.Home).Phases.Values, l => Assert.DoesNotContain("blijf_achter", l.Players.Single(p => p.Id == "LB").Instructions));
        match.Adjust(Side.Home, new TacticAdjustment(Instructions: [new InstructionChange("RB", Remove: ["blijf_achter"])]));
        Assert.All(match.TacticOf(Side.Home).Phases.Values, l =>
        {
            var rb = l.Players.Single(p => p.Id == "RB").Instructions;
            Assert.DoesNotContain("blijf_achter", rb);
            Assert.Contains("kom_op", rb);
        });
    }

    [Fact]
    public void MarkingNeedsAMarkAndRemovingItClearsTheMark()
    {
        var match = Create(3);
        AssertRejected(match, new TacticAdjustment(Instructions: [new InstructionChange("RB", Add: ["dek_man"])]), "dek_man requires dek");
        match.Adjust(Side.Home, new TacticAdjustment(Instructions: [new InstructionChange("RB", Add: ["dek_man"], Mark: Mark(match))]));
        Assert.All(match.TacticOf(Side.Home).Phases.Values, l =>
        {
            var rb = l.Players.Single(p => p.Id == "RB");
            Assert.Equal(Mark(match), rb.Mark);
            Assert.Contains("dek_man", rb.Instructions);
        });
        match.Adjust(Side.Home, new TacticAdjustment(Instructions: [new InstructionChange("RB", Remove: ["dek_man"])]));
        Assert.All(match.TacticOf(Side.Home).Phases.Values, l => Assert.Null(l.Players.Single(p => p.Id == "RB").Mark));
        AssertRejected(match, new TacticAdjustment(Instructions: [new InstructionChange("RB", Mark: Mark(match))]), "dek requires dek_man");
    }

    [Fact]
    public void InvalidAdjustmentsFailClearlyWithoutPartialApplication()
    {
        var match = Create(3);
        AssertRejected(match, new TacticAdjustment(), "Lege aanpassing");
        // A valid part followed by an invalid part: nothing may be applied.
        AssertRejected(match, new TacticAdjustment(Tempo: .9, PressingIntensity: 1.5), "pressing_intensiteit");
        AssertRejected(match, new TacticAdjustment(Tempo: .9, PressingForm: "chaos"), "pressing_vorm");
        AssertRejected(match, new TacticAdjustment(Tempo: double.NaN), "tempo");
        AssertRejected(match, new TacticAdjustment(Tempo: .9, Instructions: [new InstructionChange("ZZ", Add: ["kom_op"])]), "Onbekende speler");
        AssertRejected(match, new TacticAdjustment(Tempo: .9, Instructions: [new InstructionChange("RB", Add: ["toverschot"])]), "Onbekende instructie");
        AssertRejected(match, new TacticAdjustment(Tempo: .9, Instructions: [new InstructionChange("RB", Add: ["dek_man"], Mark: "QQQ")]), "unknown player");
        AssertRejected(match, new TacticAdjustment(Block: (BlockHeight)7), "bestaat niet");
        Assert.NotEqual(.9, match.TacticOf(Side.Home).Team.Tempo);
    }

    [Fact]
    public void BlockWithoutVariantIsRejected()
    {
        var match = Create(3, "hoge_druk_433");
        AssertRejected(match, new TacticAdjustment(Block: BlockHeight.Laag), "bestaat niet");
        match = Create(3, "laag_blok_442");
        AssertRejected(match, new TacticAdjustment(Block: BlockHeight.Hoog), "bestaat niet");
    }
}
