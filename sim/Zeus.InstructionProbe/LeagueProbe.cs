using System.Globalization;
using System.Text.Json;
using Zeus.Sim;

// Shared verbatim with the HEAD archive: no dependency on the instruction extension.
internal static class LeagueProbe
{
    public static void Run(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        string directory = args.SkipWhile(a => a != "--tactics").Skip(1).FirstOrDefault() ?? "tactics";
        var files = Directory.GetFiles(directory, "*.json").OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray();
        var tactics = files.Select(TacticLoader.LoadFile).ToArray();
        string? cachePath = args.SkipWhile(a => a != "--cache").Skip(1).FirstOrDefault();
        var cache = cachePath is not null && File.Exists(cachePath)
            ? File.ReadLines(cachePath).Select(line => JsonSerializer.Deserialize<Result>(line)!).ToDictionary(r => r.Key)
            : new Dictionary<string, Result>();
        var totals = new double[tactics.Length, 6];
        for (int a = 0; a < tactics.Length; a++)
        for (int b = a + 1; b < tactics.Length; b++)
        {
            for (ulong seed = 1; seed <= 8; seed++)
            foreach (bool swap in new[] { false, true })
            {
                int h = swap ? b : a, v = swap ? a : b;
                string key = $"{Path.GetFileName(files[h])}/{Path.GetFileName(files[v])}/{seed}";
                if (!cache.TryGetValue(key, out var result))
                {
                    var match = Match.Create(new MatchConfig(), tactics[h], tactics[v], seed);
                    var possession = new long[2];
                    for (int tick = 0; tick < 150000 && match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned); tick++)
                    {
                        match.Step();
                        if (match.Status == MatchStatus.Running) possession[match.Possession == Side.Home ? 0 : 1]++;
                    }
                    if (match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned))
                        throw new InvalidOperationException($"Match did not finish: {key}, {match.Status}");
                    var stats = MatchStatistics.FromMatch(match);
                    result = new Result(key, stats.Home, stats.Away, 100.0 * possession[0] / possession.Sum(), match.Status);
                    if (cachePath is not null) File.AppendAllText(cachePath, JsonSerializer.Serialize(result) + Environment.NewLine);
                    cache.Add(key, result);
                }
                void Add(int index, TeamStatistics team, double possession)
                {
                    totals[index, 0] += team.Passes;
                    totals[index, 1] += team.Shots;
                    totals[index, 2] += possession;
                    totals[index, 3] += team.Fouls;
                    totals[index, 4]++;
                    if (result.Status == MatchStatus.Abandoned) totals[index, 5]++;
                }
                Add(h, result.Home, result.HomePossession); Add(v, result.Away, 100 - result.HomePossession);
            }
            Console.Error.WriteLine($"Completed {Path.GetFileNameWithoutExtension(files[a])} / {Path.GetFileNameWithoutExtension(files[b])}");
        }
        Console.WriteLine("| Tactiek | Passes | Schoten | Balbezit % | Overtredingen | Wedstrijden | Gestaakt |");
        Console.WriteLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        for (int i = 0; i < tactics.Length; i++)
            Console.WriteLine($"| {Path.GetFileNameWithoutExtension(files[i])} | " + string.Join(" | ", Enumerable.Range(0, 4).Select(c => $"{totals[i, c] / totals[i, 4]:F2}")) + $" | {totals[i, 4]} | {totals[i, 5]} |");
    }
    private sealed record Result(string Key, TeamStatistics Home, TeamStatistics Away, double HomePossession, MatchStatus Status);
}
