using Xunit;

namespace Zeus.Sim.Tests;

public class TacticMakerTests
{
    public static IEnumerable<object[]> FormationKeys() => TacticTemplates.Formations.Keys.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(FormationKeys))]
    public void EveryFormationCreatesAValidTacticWithElevenPlayersInsideThePitch(string key)
    {
        var m = TacticTemplates.Create(key, "Test " + key);
        Assert.Equal(11, m.PlayerIds.Count);
        Assert.Equal(11, m.PlayerIds.Distinct().Count());
        foreach (var phase in m.AllPhases)
            foreach (var id in m.PlayerIds)
            {
                var (x, y) = m.GetPosition(phase, id);
                Assert.InRange(x, 0, 1);
                Assert.InRange(y, 0, 1);
            }
        TacticLoader.Parse(m.ToJson());
    }

    [Theory]
    [InlineData("433")]
    [InlineData("532")]
    public void ANewTacticPlaysAMatchWithoutErrors(string key)
    {
        var json = TacticTemplates.Create(key, "Nieuw").ToJson();
        var tactic = TacticLoader.Parse(json);
        var match = Match.Create(new MatchConfig(), tactic, tactic, 7);
        for (int i = 0; i < 3000; i++) match.Step();
        Assert.All(match.GetSnapshot().Players, p => Assert.InRange(Math.Abs(p.Pos.X), 0, 60));
    }

    [Fact]
    public void RoleCanBeChangedAndAnInvalidRoleIsRejectedWithoutChange()
    {
        var m = TacticTemplates.Create("433", "T");
        m.SetRole("ST", "targetman");
        Assert.Equal("targetman", m.GetRole("ST"));
        var before = m.ToJson();
        Assert.Throws<ArgumentException>(() => m.SetRole("ST", "bestaatniet"));
        Assert.Throws<ArgumentException>(() => m.SetRole("XX", "spits"));
        Assert.Equal(before, m.ToJson());
    }

    [Fact]
    public void MarkingSetsAndClearsDekAndInstructionTogether()
    {
        var m = TacticTemplates.Create("433", "T");
        m.SetMarking("verdedigen", "RCM", "LCM");
        Assert.Equal("LCM", m.GetMarking("verdedigen", "RCM"));
        Assert.Contains("dek_man", m.GetInstructions("verdedigen", "RCM"));
        TacticLoader.Parse(m.ToJson());
        m.SetMarking("verdedigen", "RCM", null);
        Assert.Null(m.GetMarking("verdedigen", "RCM"));
        Assert.DoesNotContain("dek_man", m.GetInstructions("verdedigen", "RCM"));
        Assert.Throws<ArgumentException>(() => m.SetMarking("verdedigen", "RCM", "lower-case"));
    }

    [Fact]
    public void FollowTheBallCanBeSetAndIsClamped()
    {
        var m = TacticTemplates.Create("433", "T");
        m.SetFollow("verdedigen", "RB", 1.4, -1);
        Assert.Equal((1.0, 0.0), m.GetFollow("verdedigen", "RB"));
    }

    [Fact]
    public void PressingFormIsValidated()
    {
        var m = TacticTemplates.Create("433", "T");
        m.SetPressingForm("man");
        Assert.Equal("man", m.GetPressingForm());
        Assert.Throws<ArgumentException>(() => m.SetPressingForm("chaos"));
    }

    [Fact]
    public void PathActionTriggerRoundTripsThroughJson()
    {
        var m = TacticTemplates.Create("433", "T");
        m.AddTrigger("Loop", "balwinst", "balwinst_in_hoge_zone", null, new[] { ("LW", TacticEditorModel.PathAction) });
        Assert.Equal(TacticEditorModel.PathAction, Assert.Single(m.Triggers).Actions[0].Instruction);
        var action = Assert.Single(Assert.Single(TacticLoader.Parse(m.ToJson()).Triggers).Then);
        Assert.True(action.Path);
        Assert.Null(action.Instruction);
        m.AddTrigger("Achter", "verdedigen", "achterstand_na_minuut_70", null, new[] { ("ST", "start_pressing") });
        Assert.Equal("achterstand_na_minuut_70", TacticLoader.Parse(m.ToJson()).Triggers[1].Condition);
    }

    [Fact]
    public void TriggersCanBeAddedAndRemovedAndInvalidOnesRollBack()
    {
        var m = TacticTemplates.Create("433", "T");
        m.AddTrigger("Druk op keeper", "verdedigen", "bal_naar_keeper", "A-C", new[] { ("ST", "start_pressing") });
        var t = Assert.Single(m.Triggers);
        Assert.Equal(("verdedigen", "bal_naar_keeper", "A-C"), (t.Phase, t.Condition, t.Zone));
        TacticLoader.Parse(m.ToJson());

        var before = m.ToJson();
        Assert.ThrowsAny<Exception>(() => m.AddTrigger("Fout", "verdedigen", "bal_naar_keeper", null, new[] { ("ZZ", "start_pressing") }));
        Assert.Equal(before, m.ToJson());
        Assert.Throws<ArgumentException>(() => m.AddTrigger("Fout", "verdedigen", "onbekend", null, new[] { ("ST", "start_pressing") }));
        Assert.Throws<ArgumentException>(() => m.AddTrigger("Fout", "verdedigen", "bal_naar_keeper", "X-Y", new[] { ("ST", "start_pressing") }));

        m.RemoveTrigger(0);
        Assert.Empty(m.Triggers);
        Assert.Throws<ArgumentOutOfRangeException>(() => m.RemoveTrigger(0));
    }

    [Fact]
    public void BlockSwitchCreatesBothVariantsAndTheVariantsCanBeEdited()
    {
        var m = TacticTemplates.Create("433", "T");
        Assert.Equal("midden", m.DefendingBlock);
        m.AddBlockSwitch("Voorsprong", "voor", 60, null, "laag");
        Assert.Contains("verdedigen@laag", m.AllPhases);
        Assert.Contains("verdedigen@midden", m.AllPhases);
        var sw = Assert.Single(m.BlockSwitches);
        Assert.Equal(("voor", 60, "laag"), (sw.Stand, sw.FromMinute, sw.To));

        var low = m.GetPosition("verdedigen@laag", "ST").X;
        var mid = m.GetPosition("verdedigen@midden", "ST").X;
        Assert.True(low < mid, "the low block must sit deeper than the mid block");
        m.SetPosition("verdedigen@laag", "ST", 0.33, 0.5);
        Assert.Equal(0.33, m.GetPosition("verdedigen@laag", "ST").X);
        TacticLoader.Parse(m.ToJson());

        Assert.Throws<ArgumentException>(() => m.AddBlockSwitch("Leeg", null, null, null, "hoog"));
        m.RemoveBlockSwitch(0);
        Assert.Empty(m.BlockSwitches);
    }

    [Fact]
    public void ApplyLineTemplateSetsHeightsFromRoleGroupsAndKeepsLanes()
    {
        var m = TacticTemplates.Create("433", "T");
        var yBefore = m.GetPosition("verdedigen", "RB").Y;
        m.ApplyLineTemplate("verdedigen", "hoog");
        Assert.Equal("hoog", m.DefendingBlock);
        Assert.Equal(TacticTemplates.Lines["hoog"][(int)TacticTemplates.Group.FB], m.GetPosition("verdedigen", "RB").X);
        Assert.Equal(yBefore, m.GetPosition("verdedigen", "RB").Y);
        Assert.Throws<ArgumentException>(() => m.ApplyLineTemplate("verdedigen", "bestaatniet"));
    }

    [Fact]
    public void CopyPhaseAliasAndMirrorWork()
    {
        var m = TacticTemplates.Create("433", "T");
        m.CopyPhase("aanval", "opbouw");
        Assert.Equal(m.GetPosition("aanval", "ST"), m.GetPosition("opbouw", "ST"));
        Assert.False(m.IsAlias("opbouw"));

        m.SetAlias("opbouw", "aanval");
        Assert.True(m.IsAlias("opbouw"));
        Assert.Throws<ArgumentException>(() => m.SetAlias("opbouw", "opbouw"));

        var y = m.GetPosition("aanval", "RW").Y;
        m.Mirror("aanval");
        Assert.Equal(Math.Round(1 - y, 3), m.GetPosition("aanval", "RW").Y);
        Assert.False(m.IsAlias("opbouw"));      // editing the source must not change or keep the alias
    }

    [Fact]
    public void AllExistingTacticFilesStillLoadInTheEditorModel()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Zeus.sln"))) dir = Path.GetDirectoryName(dir);
        foreach (var file in Directory.GetFiles(Path.Combine(dir!, "tactics"), "*.json"))
        {
            var m = TacticEditorModel.LoadFile(file);
            Assert.Equal(11, m.PlayerIds.Count);
            _ = m.Triggers;
            _ = m.BlockSwitches;
        }
    }

    [Fact]
    public void PathSpeedAndWaitRoundTripAndDefaultsApply()
    {
        var m = TacticTemplates.Create("433", "T");
        Assert.Equal("joggen", m.GetPathSpeed("opbouw", "RB"));
        Assert.Equal(0, m.GetPathWait("opbouw", "RB"));
        m.SetPath("opbouw", "RB", new[] { (0.4, 0.9), (0.6, 0.9) }, "sprint", 1.5);
        Assert.Equal("sprint", m.GetPathSpeed("opbouw", "RB"));
        Assert.Equal(1.5, m.GetPathWait("opbouw", "RB"));
        var again = TacticEditorModel.Parse(m.ToJson());
        Assert.Equal(2, again.GetPath("opbouw", "RB").Count);
        Assert.Equal("sprint", again.GetPathSpeed("opbouw", "RB"));
        m.ClearPath("opbouw", "RB");
        Assert.Empty(m.GetPath("opbouw", "RB"));
        Assert.Equal("joggen", m.GetPathSpeed("opbouw", "RB"));
    }
}
