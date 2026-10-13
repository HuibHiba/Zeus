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
    int steps = int.Parse(Arg(args, "--steps") ?? "600", CultureInfo.InvariantCulture);

    var match = Match.Create(new MatchConfig(), TacticLoader.LoadFile(home), TacticLoader.LoadFile(away), seed);
    for (int i = 0; i < steps; i++) match.Step();

    var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    options.Converters.Add(new JsonStringEnumConverter());
    Console.WriteLine(JsonSerializer.Serialize(match.GetSnapshot(), options));
    return 0;
}
catch (Exception e) when (e is ArgumentException or FormatException or TacticLoadException)
{
    Console.Error.WriteLine("Error: " + e.Message);
    Console.Error.WriteLine("Usage: Zeus.Runner --home <tactic.json> --away <tactic.json> [--seed N] [--steps N]");
    return 1;
}
