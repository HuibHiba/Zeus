using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zeus.Sim;

/// <summary>
/// Reads partial <see cref="MatchConfig"/> overrides from JSON (runner <c>--config</c>). Only constructor parameters are
/// accepted, with their exact names; an unknown key (a typo) is an error instead of being ignored silently.
/// </summary>
public static class MatchConfigJson
{
    public static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    private static readonly HashSet<string> Settings = typeof(MatchConfig).GetConstructors()
        .SelectMany(c => c.GetParameters()).Select(p => p.Name!).ToHashSet(StringComparer.Ordinal);

    public static MatchConfig Parse(string json)
    {
        try
        {
            // Computed read-only properties (Sensing, DuelSettings, ...) are not settings: ignored by the serializer, so check here.
            using (var document = JsonDocument.Parse(json))
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                    foreach (var property in document.RootElement.EnumerateObject())
                        if (!Settings.Contains(property.Name))
                            throw new ArgumentException($"Unknown match setting '{property.Name}'.");
            return JsonSerializer.Deserialize<MatchConfig>(json, Options) ?? throw new ArgumentException("Empty match configuration.");
        }
        catch (JsonException e)
        {
            throw new ArgumentException("Invalid match configuration: " + e.Message, e);
        }
    }
}
