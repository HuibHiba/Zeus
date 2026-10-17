using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>Versioned user JSON, readable by legacy consumers; an atomic update retains the previous file.</summary>
public static class UserSaveFile
{
    public const string VersionKey = "_gaffersNotebookSaveVersion";
    public const int CurrentVersion = 1;
    public enum ReadStatus { Missing, Ok, Unreadable }
    static readonly object Sync = new();
    static readonly Encoding Utf8 = new UTF8Encoding(false, true);

    static JsonObject Object(string text) => JsonNode.Parse(text) as JsonObject
        ?? throw new JsonException("User save must be an object.");

    static void CheckVersion(JsonObject root)
    {
        if (!root.TryGetPropertyValue(VersionKey, out var node)) return; // Unversioned demo saves are version zero.
        if (node is not JsonValue value || !value.TryGetValue<string>(out var text)
            || !int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            || version < 1 || version > CurrentVersion)
            throw new NotSupportedException("Unsupported user save version; keep the file unchanged.");
    }

    public static ReadStatus Read(string path, out string text)
    {
        lock (Sync)
        {
            text = "";
            try
            {
                var raw = File.ReadAllText(path, Utf8);
                JsonObject root;
                try { root = Object(raw); }
                catch (JsonException) { text = raw; return ReadStatus.Ok; } // Let the domain reader recover damaged JSON.
                CheckVersion(root);
                root.Remove(VersionKey);
                text = root.ToJsonString();
                return ReadStatus.Ok;
            }
            catch (FileNotFoundException) { return ReadStatus.Missing; }
            catch (DirectoryNotFoundException) { return ReadStatus.Missing; }
            catch (Exception) { return ReadStatus.Unreadable; }
        }
    }

    /// <summary>Never replaces an unreadable, damaged or newer save. Failed backup also blocks the update.</summary>
    public static void Write(string path, string text)
    {
        lock (Sync)
        {
            var root = Object(text);
            CheckVersion(root);
            root[VersionKey] = CurrentVersion.ToString(CultureInfo.InvariantCulture);
            var encoded = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            string? previous = null;
            try { previous = File.ReadAllText(path, Utf8); }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            if (previous is not null) CheckVersion(Object(previous));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var backupTemporary = temporary + ".bak";
            try
            {
                WriteThrough(temporary, encoded);
                if (previous is not null)
                {
                    WriteThrough(backupTemporary, previous);
                    File.Move(backupTemporary, path + ".bak", overwrite: true);
                }
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                // Cleanup failure must not hide the original error or report a committed save as failed.
                TryDelete(temporary); TryDelete(backupTemporary);
            }
        }
    }

    static void WriteThrough(string path, string text)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var bytes = Utf8.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(flushToDisk: true);
    }

    static void TryDelete(string path) { try { File.Delete(path); } catch (Exception) { } }

    public static bool SetAside(string path)
    {
        lock (Sync)
        {
            try
            {
                // A future format is not corruption and must never be renamed by an older game.
                var raw = File.ReadAllText(path, Utf8);
                try { CheckVersion(Object(raw)); } catch (JsonException) { }
                var bad = Path.ChangeExtension(path, ".bad");
                var candidate = bad;
                for (int n = 1; File.Exists(candidate); n++) candidate = bad + "." + n;
                File.Move(path, candidate); // No overwrites, including a concurrent recovery attempt.
                return true;
            }
            catch (Exception) { return false; }
        }
    }
}
