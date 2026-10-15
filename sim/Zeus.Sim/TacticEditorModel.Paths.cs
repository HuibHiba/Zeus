using System.Text.Json.Nodes;

namespace Zeus.Sim;

public sealed partial class TacticEditorModel
{
    public IReadOnlyList<(double X, double Y)> GetPath(string phase, string playerId) =>
        FindPlayer(PhaseObject(phase), playerId)["pad"]?.AsArray()
            .Select(p => (p![0]!.GetValue<double>(), p[1]!.GetValue<double>())).ToArray()
        ?? Array.Empty<(double, double)>();

    public void SetPath(string phase, string playerId, IEnumerable<(double X, double Y)> points,
        string? speed = null, double? wait = null)
    {
        ArgumentNullException.ThrowIfNull(points);
        var copy = points.ToArray();
        if (copy.Length is < 1 or > 8 || copy.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)
            || p.X < 0 || p.X > 1 || p.Y < 0 || p.Y > 1))
            throw new ArgumentException("Path needs 1..8 points within the pitch (0..1).", nameof(points));
        Mutate(() =>
        {
            var player = EditablePlayer(phase, playerId);
            player["pad"] = new JsonArray(copy.Select(p => (JsonNode)new JsonArray(p.X, p.Y)).ToArray());
            if (speed is not null) player["pad_snelheid"] = speed;
            if (wait is not null) player["pad_wacht"] = wait.Value;
        });
    }

    public void ClearPath(string phase, string playerId) => Mutate(() =>
    {
        var player = EditablePlayer(phase, playerId);
        player.Remove("pad");
        player.Remove("pad_snelheid");
        player.Remove("pad_wacht");
    });

    /// <summary>Speed of the player's path: "wandelen", "joggen" (default) or "sprint".</summary>
    public string GetPathSpeed(string phase, string playerId) =>
        FindPlayer(PhaseObject(phase), playerId)["pad_snelheid"]?.GetValue<string>() ?? "joggen";

    /// <summary>Seconds to wait at every point of the player's path (0 when unset).</summary>
    public double GetPathWait(string phase, string playerId) =>
        FindPlayer(PhaseObject(phase), playerId)["pad_wacht"]?.GetValue<double>() ?? 0;
}
