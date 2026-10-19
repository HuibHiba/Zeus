using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Zeus.Sim;

/// <summary>
/// One-line text packaging of a JSON document for chat and mail: <c>&lt;kind&gt;&lt;version&gt;.&lt;payload&gt;.&lt;check&gt;</c>, where the
/// payload is the deflated UTF-8 JSON in base64url (no padding) and the check is the first 8 hex characters of SHA-256 over the
/// payload text. Decoding checks length, kind, version, checksum and the unpacked size before anything is parsed.
/// </summary>
public static class ShareCode
{
    public const string TacticKind = "ZT", LadderKind = "ZL";
    public const int Version = 1;
    /// <summary>Longest code accepted or produced (characters).</summary>
    public const int MaxLength = 65536;
    /// <summary>Largest unpacked JSON accepted (same bound as a shared tactic file).</summary>
    public const int MaxBytes = TacticExchange.MaxBytes;

    public static string Encode(string kind, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        if (bytes.Length > MaxBytes) throw new ArgumentException("Content is too large for a share code.");
        using var packed = new MemoryStream();
        using (var deflate = new DeflateStream(packed, CompressionLevel.SmallestSize, leaveOpen: true)) deflate.Write(bytes);
        var payload = Convert.ToBase64String(packed.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var code = $"{kind}{Version}.{payload}.{Check(payload)}";
        if (code.Length > MaxLength) throw new ArgumentException("Content is too large for a share code.");
        return code;
    }

    public static string Decode(string kind, string code)
    {
        code = code.Trim();
        if (code.Length > MaxLength) throw new FormatException("Share code is too long.");
        var parts = code.Split('.');
        if (parts.Length != 3 || parts[1].Length == 0) throw new FormatException("Share code must have three parts.");
        if (!parts[0].StartsWith(kind, StringComparison.Ordinal)) throw new FormatException($"Share code is not a {kind} code.");
        if (!int.TryParse(parts[0].AsSpan(kind.Length), out var version) || version < 1) throw new FormatException("Share code has no version.");
        if (version > Version) throw new FormatException("Share code comes from a newer version.");
        if (!string.Equals(parts[2], Check(parts[1]), StringComparison.Ordinal)) throw new FormatException("Share code checksum does not match.");
        byte[] packed;
        try
        {
            var b64 = parts[1].Replace('-', '+').Replace('_', '/');
            packed = Convert.FromBase64String(b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '='));
        }
        catch (FormatException) { throw new FormatException("Share code payload is not valid base64url."); }
        using var input = new MemoryStream(packed);
        using var output = new MemoryStream();
        try
        {
            using var inflate = new DeflateStream(input, CompressionMode.Decompress);
            var buffer = new byte[8192];
            int count;
            while ((count = inflate.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + count > MaxBytes) throw new FormatException("Share code content is too large.");   // bounded while inflating
                output.Write(buffer, 0, count);
            }
        }
        catch (InvalidDataException e) { throw new FormatException("Share code payload is damaged.", e); }
        try { return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(output.ToArray()); }
        catch (DecoderFallbackException e) { throw new FormatException("Share code content is not valid UTF-8.", e); }
    }

    static string Check(string payload) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(payload)), 0, 4).ToLowerInvariant();
}
