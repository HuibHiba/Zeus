using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Zeus.Sim;

/// <summary>Self-contained inputs plus expected result; requires the identical simulator binary.</summary>
public sealed record ReplayFile(int FormatVersion, string BuildHash, ulong Seed, int Steps,
    MatchConfig Config, string HomeTacticJson, string AwayTacticJson, string ExpectedEventHash)
{
    // Godot loads assemblies from bytes: Assembly.Location can be empty. The compiler module id
    // identifies the loaded build consistently in both the runner and Godot.
    public static string CurrentBuildHash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        typeof(Match).Assembly.ManifestModule.ModuleVersionId.ToString("N")))).ToLowerInvariant();

    public static ReplayFile Capture(MatchConfig config, string homeJson, string awayJson,
        ulong seed, int steps, string eventHash) => new(1, CurrentBuildHash, seed, steps, config,
            homeJson, awayJson, eventHash);

    public void Validate()
    {
        if (FormatVersion != 1 || Steps < 0 || Config is null || string.IsNullOrWhiteSpace(HomeTacticJson)
            || string.IsNullOrWhiteSpace(AwayTacticJson) || string.IsNullOrWhiteSpace(ExpectedEventHash))
            throw new ArgumentException("Invalid replay format or inputs.");
        if (BuildHash != CurrentBuildHash)
            throw new ArgumentException("Replay requires the original simulator build.");
    }

    public Match Run()
    {
        Validate();
        var match = Match.Create(Config, TacticLoader.Parse(HomeTacticJson), TacticLoader.Parse(AwayTacticJson), Seed);
        for (int i = 0; i < Steps; i++) match.Step();
        Verify(match);
        return match;
    }

    public void Verify(Match match)
    {
        if (match.EventHash() != ExpectedEventHash) throw new InvalidOperationException("Replay event hash differs from recorded result.");
    }

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this,
        new JsonSerializerOptions { WriteIndented = true }));

    public static ReplayFile Load(string path)
    {
        var replay = JsonSerializer.Deserialize<ReplayFile>(File.ReadAllText(path))
            ?? throw new ArgumentException("Empty replay.");
        replay.Validate();
        return replay;
    }
}
