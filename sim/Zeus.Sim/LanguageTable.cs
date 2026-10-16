using System.Globalization;
using System.Text.Json;

namespace Zeus.Sim;

/// <summary>Flat string tables, with English fallback and visible missing keys.</summary>
public sealed class LanguageTable
{
    readonly Dictionary<string, string> _english, _local;
    public LanguageTable(string englishJson, string localJson)
    {
        _english = Parse(englishJson);
        _local = Parse(localJson);
    }
    public static Dictionary<string, string> Parse(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(json) is { } table && table.Values.All(v => v is not null)
            ? table : throw new FormatException("Invalid language table.");
    public string T(string key, params object[] args)
    {
        var text = _local.GetValueOrDefault(key) ?? _english.GetValueOrDefault(key) ?? key;
        return args.Length == 0 ? text : string.Format(CultureInfo.CurrentCulture, text, args);
    }
}
