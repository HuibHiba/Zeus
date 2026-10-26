using System.Text;

namespace Zeus.Sim;

/// <summary>Portable tactic files use the existing JSON format, preserving all editor and extension fields.</summary>
public static class TacticExchange
{
    public const int MaxBytes = 1024 * 1024;
    /// <summary>Longest tactic name accepted from a shared file (same limit as file names).</summary>
    public const int MaxNameLength = TacticFileNames.MaxLength;

    public static bool IsValidName(string? name) => name is { Length: > 0 and <= MaxNameLength }
        && !name.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format);

    public static TacticEditorModel Import(string path)
    {
        RequireJson(path);
        using var stream = File.OpenRead(path);
        if (stream.Length > MaxBytes) throw new InvalidDataException("Tactic file is too large.");
        // Bound the read itself as well: a file may grow after its length was checked.
        using var buffer = new MemoryStream();
        var bytes = new byte[8192];
        int count;
        while ((count = stream.Read(bytes, 0, bytes.Length)) > 0)
        {
            if (buffer.Length + count > MaxBytes) throw new InvalidDataException("Tactic file is too large.");
            buffer.Write(bytes, 0, count);
        }
        buffer.Position = 0;
        using var reader = new StreamReader(buffer, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        var model = TacticEditorModel.Parse(reader.ReadToEnd());
        if (model.Name.Length > MaxNameLength) throw new InvalidDataException($"Tactic name is longer than {MaxNameLength} characters.");
        return model;
    }

    /// <summary>One-line share code of the same JSON (see <see cref="ShareCode"/>).</summary>
    public static string ToCode(TacticEditorModel model)
    {
        model.Validate();
        return ShareCode.Encode(ShareCode.TacticKind, model.ToJson());
    }

    public static TacticEditorModel FromCode(string code) => Checked(TacticEditorModel.Parse(ShareCode.Decode(ShareCode.TacticKind, code)));

    /// <summary>Pasted text: a share code or plain tactic JSON, both through the real loader.</summary>
    public static TacticEditorModel FromText(string text) =>
        text.TrimStart().StartsWith(ShareCode.TacticKind, StringComparison.Ordinal) ? FromCode(text) : Checked(TacticEditorModel.Parse(text));

    static TacticEditorModel Checked(TacticEditorModel model)
    {
        if (model.Name.Length > MaxNameLength) throw new InvalidDataException($"Tactic name is longer than {MaxNameLength} characters.");
        return model;
    }

    public static void Export(TacticEditorModel model, string path)
    {
        RequireJson(path);
        model.Validate();
        if (Encoding.UTF8.GetByteCount(model.ToJson()) > MaxBytes)
            throw new InvalidDataException("Tactic file is too large.");
        model.Save(path);
    }

    public static string AvailableName(string name, string directory)
    {
        var stem = TacticFileNames.Sanitize(name);
        if (stem.Length == 0) stem = "imported";
        var candidate = stem;
        for (int n = 2; File.Exists(Path.Combine(directory, candidate + ".json")); n++)
        {
            var suffix = "_" + n;
            candidate = stem[..Math.Min(stem.Length, TacticFileNames.MaxLength - suffix.Length)] + suffix;
        }
        return candidate;
    }

    static void RequireJson(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A tactic file must have the .json extension.", nameof(path));
    }
}
