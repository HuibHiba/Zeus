using Xunit;

namespace Zeus.Sim.Tests;

public class TacticEditorModelTests
{
    private static string TacticPath()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Zeus.sln"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir!, "tactics", "laag_blok_442.json");
    }

    [Fact]
    public void LoadsElevenPlayersAndPhases()
    {
        var m = TacticEditorModel.LoadFile(TacticPath());
        Assert.Equal(11, m.PlayerIds.Count);
        Assert.Contains("opbouw", m.Phases);
    }

    [Fact]
    public void SetPositionClampsAndRoundTripsThroughLoader()
    {
        var m = TacticEditorModel.LoadFile(TacticPath());
        m.SetPosition("opbouw", "RS", 1.4, -0.2);
        Assert.Equal((1.0, 0.0), m.GetPosition("opbouw", "RS"));
        var again = TacticEditorModel.Parse(m.ToJson());
        Assert.Equal((1.0, 0.0), again.GetPosition("opbouw", "RS"));
    }

    [Fact]
    public void EditingAnAliasPhaseDetachesItWithoutChangingTheSource()
    {
        var m = TacticEditorModel.LoadFile(TacticPath());
        Assert.True(m.IsAlias("aanval"));
        var before = m.GetPosition("opbouw", "RS");
        m.SetPosition("aanval", "RS", 0.9, 0.5);
        Assert.False(m.IsAlias("aanval"));
        Assert.Equal((0.9, 0.5), m.GetPosition("aanval", "RS"));
        Assert.Equal(before, m.GetPosition("opbouw", "RS"));
    }

    [Fact]
    public void EditingASourcePhaseDoesNotChangeAliasPhases()
    {
        var m = TacticEditorModel.LoadFile(TacticPath());
        var before = m.GetPosition("aanval", "RS");
        m.SetPosition("opbouw", "RS", 0.11, 0.22);
        Assert.False(m.IsAlias("aanval"));
        Assert.Equal(before, m.GetPosition("aanval", "RS"));
        Assert.Equal((0.11, 0.22), m.GetPosition("opbouw", "RS"));
    }

    [Fact]
    public void SaveWritesAFileTheRealLoaderAccepts()
    {
        var m = TacticEditorModel.LoadFile(TacticPath());
        m.Name = "Editor test";
        m.SetPosition("opbouw", "LS", 0.7, 0.3);
        var path = Path.Combine(Path.GetTempPath(), $"zeus_editor_{Guid.NewGuid():N}.json");
        try
        {
            m.Save(path);
            TacticLoader.LoadFile(path);
            Assert.Equal("Editor test", TacticEditorModel.LoadFile(path).Name);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void UnknownPlayerOrPhaseThrows()
    {
        var m = TacticEditorModel.LoadFile(TacticPath());
        Assert.Throws<ArgumentException>(() => m.SetPosition("opbouw", "XX", 0.5, 0.5));
        Assert.Throws<ArgumentException>(() => m.GetPosition("bestaatniet", "RS"));
    }
}
