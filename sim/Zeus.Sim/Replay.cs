using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Zeus.Sim;

/// <summary>Self-contained inputs plus expected result; requires the identical simulator binary.</summary>
public sealed record ReplayFile(int FormatVersion, string BuildHash, ulong Seed, int Steps,
    MatchConfig Config, string HomeTacticJson, string AwayTacticJson, string ExpectedEventHash, string? HomeSquadJson = null, string? AwaySquadJson = null,
    IReadOnlyList<MatchCommand>? Commands = null,
    FamiliarityState? HomeFamiliarity = null, FamiliarityState? AwayFamiliarity = null)
{
    // Godot loads assemblies from bytes: Assembly.Location can be empty. The compiler module id
    // identifies the loaded build consistently in both the runner and Godot.
    public static string CurrentBuildHash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        typeof(Match).Assembly.ManifestModule.ModuleVersionId.ToString("N")))).ToLowerInvariant();

    public static ReplayFile Capture(MatchConfig config, string homeJson, string awayJson,
        ulong seed, int steps, string eventHash, string? homeSquadJson = null, string? awaySquadJson = null,
        IReadOnlyList<MatchCommand>? commands = null,
        FamiliarityState? homeFamiliarity = null, FamiliarityState? awayFamiliarity = null) => homeFamiliarity is not null || awayFamiliarity is not null
            ? new(3, CurrentBuildHash, seed, steps, config, homeJson, awayJson, eventHash, homeSquadJson, awaySquadJson,
                commands?.ToArray(), homeFamiliarity is null ? null : FamiliarityState.FromJson(homeFamiliarity.ToJson()),
                awayFamiliarity is null ? null : FamiliarityState.FromJson(awayFamiliarity.ToJson()))
            : commands is { Count: > 0 }
            ? new(2, CurrentBuildHash, seed, steps, config, homeJson, awayJson, eventHash, homeSquadJson, awaySquadJson, commands.ToArray())
            : new(1, CurrentBuildHash, seed, steps, config, homeJson, awayJson, eventHash, homeSquadJson, awaySquadJson);

    public void Validate()
    {
        // Version 1: no live commands. Version 2: adds the command timeline (Commands, required and ordered by tick).
        // Version 3: adds optional familiarity inputs and may also include a command timeline.
        if ((FormatVersion != 1 && FormatVersion != 2 && FormatVersion != 3) || (FormatVersion == 1 && Commands is { Count: > 0 })
            || (FormatVersion < 3 && (HomeFamiliarity is not null || AwayFamiliarity is not null))
            || (FormatVersion == 2 && Commands is null) || Steps < 0 || Config is null || string.IsNullOrWhiteSpace(HomeTacticJson)
            || string.IsNullOrWhiteSpace(AwayTacticJson) || string.IsNullOrWhiteSpace(ExpectedEventHash))
            throw new ArgumentException("Invalid replay format or inputs.");
        HomeFamiliarity?.Validate();
        AwayFamiliarity?.Validate();
        long previous = 0;
        foreach (var command in Commands ?? [])
        {
            if (command is null || command.Tick < previous || command.Tick > Steps
                || command.Kind is not (MatchCommand.SubstituteKind or MatchCommand.AdjustKind))
                throw new ArgumentException("Invalid replay command timeline.");
            previous = command.Tick;
        }
        if (BuildHash != CurrentBuildHash)
            throw new ArgumentException("Replay requires the original simulator build.");
    }

    public Match Run()
    {
        Validate();
        var match = Match.Create(Config, TacticLoader.Parse(HomeTacticJson), TacticLoader.Parse(AwayTacticJson), Seed,
            homeSquad: HomeSquadJson is null ? null : SquadLoader.Parse(HomeSquadJson),
            awaySquad: AwaySquadJson is null ? null : SquadLoader.Parse(AwaySquadJson),
            homeFamiliarity: HomeFamiliarity, awayFamiliarity: AwayFamiliarity);
        for (int i = 0; i < Steps; i++) { ApplyDue(match); match.Step(); }
        ApplyDue(match); // commands given after the last step
        Verify(match);
        return match;
    }

    /// <summary>
    /// Applies the recorded commands that are due at the match's current tick. Stateless: the position in the timeline is
    /// the number of commands the match has already accepted. Call before every <see cref="Match.Step"/> (and once after the last).
    /// </summary>
    public void ApplyDue(Match match)
    {
        var commands = Commands;
        if (commands is null) return;
        while (match.Commands.Count < commands.Count && commands[match.Commands.Count].Tick == match.Tick)
            match.Apply(commands[match.Commands.Count]);
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
