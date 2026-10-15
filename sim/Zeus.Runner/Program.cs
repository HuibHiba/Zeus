using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zeus.Sim;

static string? Arg(string[] a, string name)
{
    int i = Array.IndexOf(a, name);
    return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
}

try
{
    string? replayPath = Arg(args, "--replay");
    var replay = replayPath is null ? null : ReplayFile.Load(replayPath);
    string homeJson = replay?.HomeTacticJson ?? File.ReadAllText(Arg(args, "--home")
        ?? throw new ArgumentException("missing --home <file>"));
    string awayJson = replay?.AwayTacticJson ?? File.ReadAllText(Arg(args, "--away")
        ?? throw new ArgumentException("missing --away <file>"));
    ulong seed = replay?.Seed ?? ulong.Parse(Arg(args, "--seed") ?? "42", CultureInfo.InvariantCulture);
    int steps = replay?.Steps ?? int.Parse(Arg(args, "--steps") ?? "54000", CultureInfo.InvariantCulture);
    if (steps < 0) throw new ArgumentException("--steps must be nonnegative");
    // --config <json>: partial MatchConfig overrides (same shape as a replay's Config); omitted fields keep their defaults.
    // --replay-out stores the resulting config, so such a run stays reproducible.
    string? configPath = Arg(args, "--config");
    if (replay is not null && configPath is not null) throw new ArgumentException("--config cannot be combined with --replay");
    var config = replay?.Config ?? (configPath is null ? new MatchConfig()
        : MatchConfigJson.Parse(File.ReadAllText(configPath))); // an unknown key (typo) is an error, not ignored

    string? outPath = Arg(args, "--out");
    int every = int.Parse(Arg(args, "--every") ?? "1", CultureInfo.InvariantCulture);
    if (every < 1) throw new ArgumentException("--every must be >= 1");

    var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    options.Converters.Add(new JsonStringEnumConverter());

    string? homeSquadPath = Arg(args, "--home-squad");
    string? awaySquadPath = Arg(args, "--away-squad");
    if (replay is not null && (homeSquadPath is not null || awaySquadPath is not null)) throw new ArgumentException("Squad overrides cannot be combined with --replay");
    string? homeSquadJson = replay?.HomeSquadJson ?? (homeSquadPath is null ? null : File.ReadAllText(homeSquadPath));
    string? awaySquadJson = replay?.AwaySquadJson ?? (awaySquadPath is null ? null : File.ReadAllText(awaySquadPath));
    var match = Match.Create(config, TacticLoader.Parse(homeJson), TacticLoader.Parse(awayJson), seed,
        homeSquad: homeSquadJson is null ? null : SquadLoader.Parse(homeSquadJson),
        awaySquad: awaySquadJson is null ? null : SquadLoader.Parse(awaySquadJson));
    var sw = System.Diagnostics.Stopwatch.StartNew();
    using var writer = outPath is null ? null : new StreamWriter(outPath, false, new System.Text.UTF8Encoding(false));
    bool untilFinished = replay is null && args.Contains("--until-finished");
    int stepLimit = untilFinished ? 150000 : steps;
    for (int i = 0; i < stepLimit; i++)
    {
        replay?.ApplyDue(match);
        match.Step();
        if (writer is not null && match.Tick % every == 0)
            writer.WriteLine(JsonSerializer.Serialize(match.GetSnapshot(), options));
        if (match.Status is MatchStatus.Finished or MatchStatus.Abandoned) break;
    }
    if (untilFinished && match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned))
        throw new InvalidOperationException("Match did not finish within 150000 ticks.");
    replay?.ApplyDue(match);
    sw.Stop();
    if (writer is not null && match.Tick % every != 0)
        writer.WriteLine(JsonSerializer.Serialize(match.GetSnapshot(), options));
    writer?.Flush();
    replay?.Verify(match);

    if (outPath is null)
        Console.WriteLine(JsonSerializer.Serialize(match.GetSnapshot(), new JsonSerializerOptions(options) { WriteIndented = true }));
    Console.WriteLine($"steps: {match.Tick}");
    Console.WriteLine($"elapsed: {sw.Elapsed.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)} s");
    Console.WriteLine($"event hash: {match.EventHash()}");
    Console.WriteLine($"snapshot hash: {match.SnapshotHash()}");
    foreach (var group in match.Events.Events.GroupBy(e => e.Type))
        Console.WriteLine($"events {group.Key}: {group.Count()}");
    string? eventsPath = Arg(args, "--events");
    if (eventsPath is not null)
        File.WriteAllLines(eventsPath, match.Events.Events.Select(e => JsonSerializer.Serialize(e, options)));
    string? summaryPath = Arg(args, "--summary");
    var summary = MatchStatistics.FromMatch(match);
    Console.WriteLine(JsonSerializer.Serialize(summary, options));
    if (summaryPath is not null) File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, options));
    string? replayOut = Arg(args, "--replay-out");
    if (replayOut is not null) ReplayFile.Capture(config, homeJson, awayJson, seed, (int)match.Tick, match.EventHash(), homeSquadJson, awaySquadJson, match.Commands).Save(replayOut);
    if (match.Status == MatchStatus.Abandoned)
    {
        // An abandoned match is not a normal result; do not let scripts treat exit 0 as one.
        Console.Error.WriteLine("Warning: match was Abandoned (exit code 2).");
        return 2;
    }
    if (match.Status != MatchStatus.Finished)
        Console.Error.WriteLine($"Warning: match status is {match.Status}, not Finished; the summary covers a partial match.");
    return 0;
}
catch (Exception e) when (e is ArgumentException or FormatException or TacticLoadException or IOException
    or JsonException or InvalidOperationException or SquadLoadException)
{
    Console.Error.WriteLine("Error: " + e.Message);
    Console.Error.WriteLine("Usage: Zeus.Runner --home <file> --away <file> (or --replay <file>) [--home-squad file] [--away-squad file] [--seed N] [--steps N or --until-finished] [--config overrides.json] [--out snapshots.jsonl] [--every N] [--events events.jsonl] [--summary summary.json] [--replay-out replay.json]");
    return 1;
}
