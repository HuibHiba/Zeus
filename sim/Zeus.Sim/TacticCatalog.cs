namespace Zeus.Sim;

/// <summary>Stable portable references keep built-in and own files with identical names distinct.</summary>
public static class TacticCatalog
{
    public const string OwnPrefix = "own@";
    public sealed record Entry(string Id, string Path, bool IsOwn)
    {
        public string Label => System.IO.Path.GetFileNameWithoutExtension(Path).Replace('_', ' ') + (IsOwn ? " (eigen)" : "");
    }

    public static Entry[] List(string builtInDirectory, string ownDirectory)
    {
        IEnumerable<Entry> Read(string dir, bool own) => Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.json").OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => new Entry((own ? OwnPrefix : "") + System.IO.Path.GetFileName(p), System.IO.Path.GetFullPath(p), own))
            : Enumerable.Empty<Entry>();
        return Read(builtInDirectory, false).Concat(Read(ownDirectory, true)).ToArray();
    }

    public static string Resolve(string id, string builtInDirectory, string? ownDirectory)
    {
        if (!TacticFileNames.IsBare(id)) throw new ArgumentException("Invalid tactic reference.", nameof(id));
        bool own = id.StartsWith(OwnPrefix, StringComparison.Ordinal);
        var file = own ? id[OwnPrefix.Length..] : id;
        if (!TacticFileNames.IsBare(file)) throw new ArgumentException("Invalid tactic reference.", nameof(id));
        return System.IO.Path.Combine(own ? ownDirectory ?? throw new ArgumentException("Own tactics directory required.") : builtInDirectory, file);
    }
}
