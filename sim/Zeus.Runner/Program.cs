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
    string home = Arg(args, "--home") ?? throw new ArgumentException("missing --home <file>");
    string away = Arg(args, "--away") ?? throw new ArgumentException("missing --away <file>");
    ulong seed = ulong.Parse(Arg(args, "--seed") ?? "42", CultureInfo.InvariantCulture);
    int steps = int.Parse(Arg(args, "--steps") ?? "54000", CultureInfo.InvariantCulture);

    string? outPath = Arg(args, "--out");
    int every = int.Parse(Arg(args, "--every") ?? "1", CultureInfo.InvariantCulture);
    if (every < 1) throw new ArgumentException("--every must be >= 1");

    var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    options.Converters.Add(new JsonStringEnumConverter());

    var match = Match.Create(new MatchConfig(), TacticLoader.LoadFile(home), TacticLoader.LoadFile(away), seed);
    var sw = System.Diagnostics.Stopwatch.StartNew();
    using var writer = outPath is null ? null : new StreamWriter(outPath, false, new System.Text.UTF8Encoding(false));
    for (int i = 0; i < steps; i++)
    {
        match.Step();
        if (writer is not null && match.Tick % every == 0)
            writer.WriteLine(JsonSerializer.Serialize(match.GetSnapshot(), options));
    }
    sw.Stop();
    writer?.Flush();

    if (outPath is null)
        Console.WriteLine(JsonSerializer.Serialize(match.GetSnapshot(), new JsonSerializerOptions(options) { WriteIndented = true }));
    Console.WriteLine($"steps: {steps}");
    Console.WriteLine($"elapsed: {sw.Elapsed.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)} s");
    Console.WriteLine($"event hash: {match.EventHash()}");
    Console.WriteLine($"snapshot hash: {match.SnapshotHash()}");
    return 0;
}
catch (Exception e) when (e is ArgumentException or FormatException or TacticLoadException)
{
    Console.Error.WriteLine("Error: " + e.Message);
    Console.Error.WriteLine("Usage: Zeus.Runner --home <tactic.json> --away <tactic.json> [--seed N] [--steps N] [--out file.jsonl] [--every N]");
    return 1;
}
