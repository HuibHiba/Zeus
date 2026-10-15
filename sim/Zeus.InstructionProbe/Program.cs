using System.Globalization;
using Zeus.Sim;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
if (args.Length == 0 || args.Contains("--league")) { LeagueProbe.Run(args); return; }
var source = TacticLoader.LoadFile("tactics/laag_blok_442.json");
Match Finish(Tactic home, Tactic away, ulong seed, InstructionConfig config)
{
    var match = Match.Create(new(Instructions: config), home, away, seed);
    for (int i = 0; i < 150000 && match.Status != MatchStatus.Finished; i++) match.Step();
    if (match.Status != MatchStatus.Finished) throw new InvalidOperationException("Match did not finish.");
    return match;
}

if (args.Contains("--goldens"))
{
    foreach (var (away, seed) in new[] { ("man_om_man_433", 42UL), ("laag_blok_442", 7UL) })
    {
        var match = Finish(source, TacticLoader.LoadFile("tactics/" + away + ".json"), seed, new());
        Console.WriteLine($"{away} {seed}: {match.EventHash()}|{match.SnapshotHash()}");
        Console.WriteLine(MatchStatistics.FromMatch(match));
    }
    return;
}

// Paired ablations: identical tactic and seed; only one instruction switch differs.
var disabled = InstructionConfig.Disabled;
var cases = new (string Name, InstructionConfig On, string[] Players)[] {
    ("blijf_achter", disabled with { EnableStayBack = true }, new[] { "RB", "LB", "RCM" }),
    ("laat_zakken", disabled with { EnableDrop = true }, new[] { "RCM", "LCM" }),
    ("blijf_breed", disabled with { EnableWidth = true }, new[] { "RB", "LB", "RM", "LM" }),
    ("kom_op", disabled with { EnableAdvance = true }, new[] { "RB", "LB" }),
    ("sluit_naar_binnen", disabled with { EnableCutInside = true }, new[] { "RM", "LM" }),
    ("dek_man", disabled with { EnableMark = true }, new[] { "RC", "LC", "RCM", "LCM" }),
    ("loop_zestien_in", disabled with { EnableBoxRun = true }, new[] { "RM", "LM", "RCM", "LCM" }),
    ("dek_ruimte_achter_lijn", disabled with { EnableCoverDepth = true }, new[] { "RC", "LC" }),
    ("dribbel_in", disabled with { EnableDribbleInside = true }, new[] { "RM", "LM" }),
    ("vrije_man_zoeken", disabled with { EnableFindSpace = true }, new[] { "LS", "RCM", "LCM" }),
    ("pressing_man", disabled with { EnablePressingForm = true }, Array.Empty<string>()),
    ("pressing_gemengd", disabled with { EnablePressingForm = true }, Array.Empty<string>()),
    ("tempo", disabled with { EnableTempo = true }, Array.Empty<string>()),
    ("lijnhoogte", disabled with { EnableLineHeight = true }, Array.Empty<string>()),
    ("compactheid", disabled with { EnableCompactness = true }, Array.Empty<string>())
};

Tactic Prepare(string feature, string[] players)
{
    PhaseLayout Change(PhaseLayout layout) => layout with { Players = layout.Players.Select(p => p with {
        Instructions = players.Contains(p.Id) ? new[] { feature } : Array.Empty<string>(),
        Mark = feature == "dek_man" && players.Contains(p.Id) ? (p.Id is "RC" or "RCM" ? "LS" : "RS") : null
    }).ToArray() };
    return source with {
        Team = source.Team with { Tempo = .9, LineHeight = .4, Compactness = .9,
            PressingForm = feature == "pressing_man" ? "man" : feature == "pressing_gemengd" ? "gemengd" : "zone" },
        Phases = source.Phases.ToDictionary(p => p.Key, p => Change(p.Value)),
        DefendVariants = source.DefendVariants.ToDictionary(p => p.Key, p => Change(p.Value)),
        Triggers = Array.Empty<Trigger>()
    };
}
var opponent = Prepare("", Array.Empty<string>());
Console.WriteLine("| Instructie/parameter | Passes uit→aan | Schoten uit→aan | Goals uit→aan | Duels uit→aan | Overtredingen uit→aan |");
Console.WriteLine("| --- | ---: | ---: | ---: | ---: | ---: |");
foreach (var item in cases.Where(c => args.Length == 0 || args.Contains(c.Name)))
{
    var tactic = Prepare(item.Name, item.Players);
    var sums = new double[2, 5];
    for (ulong seed = 1; seed <= 3; seed++)
    foreach (bool home in new[] { true, false })
    for (int enabled = 0; enabled < 2; enabled++)
    {
        var match = Finish(home ? tactic : opponent, home ? opponent : tactic, seed, enabled == 0 ? disabled : item.On);
        var side = home ? Side.Home : Side.Away;
        var events = match.Events.Events.Where(e => e.Team == side).ToArray();
        var types = new[] { EventType.Pass, EventType.Shot, EventType.Goal, EventType.Duel, EventType.Foul };
        for (int i = 0; i < types.Length; i++) sums[enabled, i] += events.Count(e => e.Type == types[i]);
    }
    Console.WriteLine($"| {item.Name} | " + string.Join(" | ", Enumerable.Range(0, 5).Select(i => $"{sums[0, i] / 6:F2}→{sums[1, i] / 6:F2}")) + " |");
}
