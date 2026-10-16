// Logic behind the feedback file (no Godot): removing user names from paths, a bounded thread-safe ring log, deciding which
// files go into the zip, and writing the zip. Nothing here sends anything anywhere.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Zeus.Sim;

public static class FeedbackPack
{
    public const string ProfilePlaceholder = "%USERPROFILE%";

    /// <summary>The only game files that may go into the feedback zip (all in the user folder).</summary>
    public static readonly string[] ProgressFiles =
    {
        "zeus_progress.json", "zeus_history.json", "zeus_carriere.json", "onboarding.json", "instellingen.json", "zeus_vertrouwdheid.json",
    };

    // C:\Users\name, C:/Users/name, C:\\Users\\name (escaped in JSON), file:///C:/Users/name, /Users/name, /home/name.
    static readonly Regex ProfilePath = new(
        @"(?:[A-Za-z]:[\\/]+|(?<![A-Za-z0-9_])[\\/]+)(?:Users|home|Documents and Settings)[\\/]+(?:[^\\/\s""'<>|:*?\r\n]+(?: [^\\/\s""'<>|:*?\r\n]+){1,2}(?=[\\/])|[^\\/\s""'<>|:*?\r\n]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Replaces every user profile path with %USERPROFILE% and, when given, the bare user name with &lt;user&gt;.</summary>
    public static string Anonymize(string text, string? userName = null)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var result = ProfilePath.Replace(text, ProfilePlaceholder);
        if (!string.IsNullOrWhiteSpace(userName) && userName.Trim().Length >= 3)
            result = Regex.Replace(result, @"(?<![A-Za-z0-9])" + Regex.Escape(userName.Trim()) + @"(?![A-Za-z0-9])", "<user>", RegexOptions.IgnoreCase);
        return result;
    }

    public static string ZipName(DateTime now) => $"GaffersNotebook-feedback-{now:yyyyMMdd-HHmm}.zip";

    /// <summary>The progress files that exist in <paramref name="userDir"/>, in a fixed order; the rest goes into <paramref name="missing"/> (never an error).</summary>
    public static List<string> ExistingProgressFiles(string userDir, Func<string, bool> exists, out List<string> missing)
    {
        var present = new List<string>();
        missing = new List<string>();
        foreach (var name in ProgressFiles)
        {
            bool found;
            try { found = exists(Path.Combine(userDir, name)); } catch (Exception) { found = false; }
            (found ? present : missing).Add(name);
        }
        return present;
    }

    /// <summary>Keeps the last <paramref name="maxLines"/> lines of a text (a long Godot log).</summary>
    public static string TailLines(string text, int maxLines)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        return lines.Length <= maxLines ? string.Join("\n", lines) : string.Join("\n", lines.Skip(lines.Length - maxLines));
    }

    /// <summary>Writes the entries as a zip with '/' paths. A null body is skipped.</summary>
    public static void WriteZip(Stream output, IEnumerable<(string Name, byte[]? Data)> entries)
    {
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var (name, data) in entries)
        {
            if (data is null) continue;
            var entry = zip.CreateEntry(name.Replace('\\', '/'), CompressionLevel.Optimal);
            using var s = entry.Open();
            s.Write(data, 0, data.Length);
        }
    }

    public static byte[] Utf8(string text) => new UTF8Encoding(false).GetBytes(text);
}

/// <summary>Thread-safe ring of the most recent log lines (default 500). Lines are anonymized when added.</summary>
public sealed class RingLog
{
    readonly object _gate = new();
    readonly Queue<string> _lines = new();
    readonly string? _userName;

    public RingLog(int capacity = 500, string? userName = null)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
        _userName = userName;
    }

    public int Capacity { get; }

    public void Add(string line)
    {
        var clean = FeedbackPack.Anonymize(line ?? "", _userName).Replace("\r", "").Replace('\n', ' ');
        lock (_gate)
        {
            _lines.Enqueue(clean);
            while (_lines.Count > Capacity) _lines.Dequeue();
        }
    }

    public string[] Snapshot() { lock (_gate) return _lines.ToArray(); }

    public int Count { get { lock (_gate) return _lines.Count; } }
}
