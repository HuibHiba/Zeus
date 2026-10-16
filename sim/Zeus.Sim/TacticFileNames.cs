namespace Zeus.Sim;

/// <summary>Rules for tactic file names: what the editor accepts as a name and what counts as a bare file name in saved data.</summary>
public static class TacticFileNames
{
    public const int MaxLength = 60;

    public static bool IsReference(string? file) => IsBare(file)
        && (!file!.StartsWith(TacticCatalog.OwnPrefix, StringComparison.Ordinal)
            || IsBare(file[TacticCatalog.OwnPrefix.Length..]));

    static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>True for a reserved Windows device name, with or without an extension (e.g. "con", "NUL.json").</summary>
    public static bool IsReserved(string name)
    {
        var dot = name.IndexOf('.');
        var stem = (dot >= 0 ? name[..dot] : name).TrimEnd(' ');
        return Reserved.Contains(stem);
    }

    /// <summary>
    /// Keeps letters, digits, '_' and '-', cuts the result at <see cref="MaxLength"/> characters and returns "" when nothing usable
    /// is left or the name is a reserved Windows device name.
    /// </summary>
    public static string Sanitize(string? name)
    {
        var safe = new string((name ?? "").Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray());
        if (safe.Length > MaxLength) safe = safe[..MaxLength];
        return IsReserved(safe) ? "" : safe;
    }

    /// <summary>True when <paramref name="file"/> is a plain file name without any directory part (no path traversal).</summary>
    public static bool IsBare(string? file) =>
        !string.IsNullOrWhiteSpace(file) && file == Path.GetFileName(file) && file is not ("." or "..")
        && file.IndexOfAny(new[] { '/', (char)92, ':' }) < 0;
}
