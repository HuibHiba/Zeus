using System.Text.Json.Nodes;
using Zeus.Sim;
namespace Zeus.Sim.Tests;

/// <summary>
/// The example tactics gained volg_bal in opbouw and aanval in the second AI-fix round (docs/ai-diagnose.md, Ronde 2), so they stand
/// no longer still there. Tests that pin historical hashes or scenario-sensitive counts load the tactic without those entries.
/// </summary>
internal static class HistoricalTactics
{
    public static Tactic Load(string name)
    {
        var root = JsonNode.Parse(File.ReadAllText(TacticLoaderTests.TacticPath(name)))!;
        foreach (string phase in new[] { "opbouw", "aanval" })
            if (root["fases"]![phase]!["spelers"] is JsonArray players)
                foreach (var player in players) player!.AsObject().Remove("volg_bal");
        return TacticLoader.Parse(root.ToJsonString());
    }
}
