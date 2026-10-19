using System.Text.Json;

public sealed class UserSaveFileTests : IDisposable
{
    readonly string _directory = Path.Combine(Path.GetTempPath(), "zeus-save-" + Guid.NewGuid());
    string SavePath => Path.Combine(_directory, "save.json");
    public UserSaveFileTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Theory]
    [InlineData("{\"language\":\"nl\",\"popups\":\"all\"}")]
    [InlineData("{\"gehaald\":{\"first\":{\"tactiek\":\"counter\"}}}")]
    [InlineData("{\"geschiedenis\":[{\"tactiek\":\"counter\"}]}")]
    [InlineData("{\"halfprof|club\":\"{\\\"players\\\":{}}\"}")]
    [InlineData("{\"Round\":4,\"Extension\":{\"kept\":true}}")]
    public void LegacySavesSurviveUpgradeAndRestart(string legacy)
    {
        File.WriteAllText(SavePath, legacy);
        Assert.Equal(UserSaveFile.ReadStatus.Ok, UserSaveFile.Read(SavePath, out var loaded));
        UserSaveFile.Write(SavePath, loaded);
        Assert.Equal(legacy, File.ReadAllText(SavePath + ".bak"));
        Assert.Equal(UserSaveFile.ReadStatus.Ok, UserSaveFile.Read(SavePath, out var reloaded));
        Assert.Equal(JsonSerializer.Serialize(JsonDocument.Parse(legacy).RootElement), reloaded);
        Assert.DoesNotContain(UserSaveFile.VersionKey, reloaded);
        using var stored = JsonDocument.Parse(File.ReadAllText(SavePath));
        Assert.Equal("1", stored.RootElement.GetProperty(UserSaveFile.VersionKey).GetString());
    }

    [Fact]
    public void VersionMarkerStaysCompatibleWithOldStringDictionaryReaders()
    {
        UserSaveFile.Write(SavePath, "{\"language\":\"nl\"}");
        var oldReader = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(SavePath))!;
        Assert.Equal("nl", oldReader["language"]);
        Assert.Equal("1", oldReader[UserSaveFile.VersionKey]);
    }

    [Fact]
    public void BackupContainsThePreviousSuccessfulSave()
    {
        UserSaveFile.Write(SavePath, "{\"round\":1}");
        var first = File.ReadAllText(SavePath);
        UserSaveFile.Write(SavePath, "{\"round\":2}");
        Assert.Equal(first, File.ReadAllText(SavePath + ".bak"));
        UserSaveFile.Read(SavePath, out var loaded);
        Assert.Equal(2, JsonDocument.Parse(loaded).RootElement.GetProperty("round").GetInt32());
    }

    [Theory]
    [InlineData("999")]
    [InlineData("-1")]
    [InlineData("unknown")]
    public void UnsupportedVersionIsNeitherOverwrittenNorSetAside(string version)
    {
        var original = "{\"" + UserSaveFile.VersionKey + "\":\"" + version + "\",\"round\":9}";
        File.WriteAllText(SavePath, original);
        Assert.Equal(UserSaveFile.ReadStatus.Newer, UserSaveFile.Read(SavePath, out _));       // not "unreadable": the UI says why
        Assert.Throws<NotSupportedException>(() => UserSaveFile.Write(SavePath, "{\"round\":1}"));
        Assert.False(UserSaveFile.SetAside(SavePath));
        Assert.Equal(original, File.ReadAllText(SavePath));
        Assert.False(File.Exists(SavePath + ".bak"));
    }

    [Fact]
    public void DamagedSavesAreRetainedWithoutErasingAnOlderRecoveryFile()
    {
        var bad = Path.ChangeExtension(SavePath, ".bad");
        File.WriteAllText(bad, "older damage");
        File.WriteAllText(SavePath, "{broken");
        Assert.Equal(UserSaveFile.ReadStatus.Ok, UserSaveFile.Read(SavePath, out var damaged));
        Assert.Equal("{broken", damaged);
        Assert.ThrowsAny<JsonException>(() => UserSaveFile.Write(SavePath, "{}"));
        Assert.True(UserSaveFile.SetAside(SavePath));
        Assert.Equal("older damage", File.ReadAllText(bad));
        Assert.Equal("{broken", File.ReadAllText(bad + ".1"));
        UserSaveFile.Write(SavePath, "{}");
    }

    [Fact]
    public void LockedSaveOrBackupBlocksWritingAndLeavesTheCurrentSaveIntact()
    {
        UserSaveFile.Write(SavePath, "{\"round\":5}");
        var original = File.ReadAllText(SavePath);
        using (var locked = File.Open(SavePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Equal(UserSaveFile.ReadStatus.Unreadable, UserSaveFile.Read(SavePath, out _));
            Assert.Throws<IOException>(() => UserSaveFile.Write(SavePath, "{}"));
        }
        File.WriteAllText(SavePath + ".bak", "older backup");
        using (var locked = File.Open(SavePath + ".bak", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var error = Record.Exception(() => UserSaveFile.Write(SavePath, "{}"));
            Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString());
        }
        Assert.Equal(original, File.ReadAllText(SavePath));
        Assert.Equal("older backup", File.ReadAllText(SavePath + ".bak"));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp*"));
    }

    [Fact]
    public void InvalidNewDataNeverReplacesAGoodSave()
    {
        UserSaveFile.Write(SavePath, "{\"round\":4}");
        var original = File.ReadAllText(SavePath);
        Assert.Throws<JsonException>(() => UserSaveFile.Write(SavePath, "[]"));
        Assert.Equal(original, File.ReadAllText(SavePath));
        Assert.False(File.Exists(SavePath + ".bak"));
    }

    [Fact]
    public void RealCareerAndPendingLiveResultSurviveTheStorageUpgrade()
    {
        var career = new Zeus.Sim.SeasonState
        {
            Season = 3,
            Division = 1,
            PlayerTactic = "own@friends_tactic.json",
            Pending = new Zeus.Sim.PendingResult(0, 2, 1)
        };
        File.WriteAllText(SavePath, career.ToJson());
        Assert.Equal(UserSaveFile.ReadStatus.Ok, UserSaveFile.Read(SavePath, out var legacy));
        UserSaveFile.Write(SavePath, legacy);
        Assert.Equal(UserSaveFile.ReadStatus.Ok, UserSaveFile.Read(SavePath, out var upgraded));
        var restored = Zeus.Sim.SeasonState.Parse(upgraded);
        Assert.Equal(career.ToJson(), restored.ToJson());
        // The previous build's domain parser also tolerates the additional string marker.
        Assert.Equal(career.ToJson(), Zeus.Sim.SeasonState.Parse(File.ReadAllText(SavePath)).ToJson());
    }

    [Fact]
    public void VersionOneCareerCanMigrateTrainAndRestartWithABackupOfTheOriginal()
    {
        var old = new Zeus.Sim.SeasonState { Version = 1 }.ToJson();
        File.WriteAllText(SavePath, old);
        UserSaveFile.Read(SavePath, out var loaded);
        var career = Zeus.Sim.SeasonState.Parse(loaded);
        Assert.Equal(2, career.Version);
        career.Familiarity = Zeus.Sim.FamiliarityState.ForTeam(Zeus.Sim.Competition.PlayerClubName, new[] { "GK", "ST" });
        Zeus.Sim.Competition.Train(career, Zeus.Sim.TacticalConcepts.All[0], 1);
        UserSaveFile.Write(SavePath, career.ToJson());
        UserSaveFile.Read(SavePath, out var saved);
        var restarted = Zeus.Sim.SeasonState.Parse(saved);
        Assert.True(restarted.TrainedThisRound);
        Assert.Equal(career.ToJson(), restarted.ToJson());
        Assert.Equal(old, File.ReadAllText(SavePath + ".bak"));
    }
}
