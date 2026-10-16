using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Zeus.Sim;

public class FeedbackPackTests
{
    [Theory]
    [InlineData(@"open C:\Users\Jan Jansen\AppData\x failed", @"open %USERPROFILE%\AppData\x failed")]
    [InlineData("C:/Users/jan/Desktop/x.zip", "%USERPROFILE%/Desktop/x.zip")]
    [InlineData(@"{""p"":""c:\\Users\\jan\\x""}", @"{""p"":""%USERPROFILE%\\x""}")]
    [InlineData("file:///D:/Users/jan/a.png", "file:///%USERPROFILE%/a.png")]
    [InlineData("at /home/jan/.local/share", "at %USERPROFILE%/.local/share")]
    public void UserProfilePathsAreRemoved(string input, string expected)
        => Assert.Equal(expected, FeedbackPack.Anonymize(input));

    [Fact]
    public void UserNameDisappearsEverywhere()
    {
        var text = FeedbackPack.Anonymize(@"Error for henk at C:\Users\henk\x and HENK again; henkie stays", "henk");
        Assert.DoesNotContain("henk\\", text);
        Assert.DoesNotContain("HENK", text);
        Assert.Contains("%USERPROFILE%", text);
        Assert.Contains("henkie", text);          // only the whole word
    }

    [Fact]
    public void ShortOrEmptyUserNameIsIgnored() => Assert.Equal("a b", FeedbackPack.Anonymize("a b", "a"));

    [Fact]
    public void RingLogIsBoundedAndKeepsTheLatest()
    {
        var log = new RingLog(500);
        for (int i = 0; i < 1200; i++) log.Add("line " + i);
        var lines = log.Snapshot();
        Assert.Equal(500, lines.Length);
        Assert.Equal("line 700", lines[0]);
        Assert.Equal("line 1199", lines[^1]);
    }

    [Fact]
    public void RingLogAnonymizesAndStaysOnOneLine()
    {
        var log = new RingLog(5, "piet");
        log.Add("crash C:\\Users\\piet\\x\nsecond line piet");
        var line = Assert.Single(log.Snapshot());
        Assert.DoesNotContain("piet", line);
        Assert.DoesNotContain("\n", line);
    }

    [Fact]
    public void RingLogIsThreadSafe()
    {
        var log = new RingLog(100);
        Parallel.For(0, 8, t => { for (int i = 0; i < 2000; i++) { log.Add($"t{t} {i}"); _ = log.Snapshot(); } });
        Assert.Equal(100, log.Count);
    }

    [Fact]
    public void MissingProgressFilesAreSkipped()
    {
        var dir = Path.Combine(Path.GetTempPath(), "fb_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "zeus_progress.json"), "{}");
            File.WriteAllText(Path.Combine(dir, "secret.txt"), "no");
            var present = FeedbackPack.ExistingProgressFiles(dir, File.Exists, out var missing);
            Assert.Equal(new[] { "zeus_progress.json" }, present);
            Assert.Equal(FeedbackPack.ProgressFiles.Length - 1, missing.Count);
            Assert.DoesNotContain("secret.txt", present);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ThrowingExistsCheckCountsAsMissing()
    {
        var present = FeedbackPack.ExistingProgressFiles("x", _ => throw new IOException("locked"), out var missing);
        Assert.Empty(present);
        Assert.Equal(FeedbackPack.ProgressFiles.Length, missing.Count);
    }

    [Fact]
    public void ZipSkipsNullEntriesAndUsesForwardSlashes()
    {
        using var ms = new MemoryStream();
        FeedbackPack.WriteZip(ms, new (string, byte[]?)[] { ("a.txt", FeedbackPack.Utf8("hi")), ("screenshot.png", null), ("sub\\b.txt", FeedbackPack.Utf8("x")) });
        ms.Position = 0;
        using var zip = new ZipArchive(ms);
        Assert.Equal(new[] { "a.txt", "sub/b.txt" }, zip.Entries.Select(e => e.FullName).ToArray());
    }

    [Fact]
    public void TailKeepsTheLastLines() => Assert.Equal("c\nd", FeedbackPack.TailLines("a\r\nb\r\nc\r\nd", 2));

    [Fact]
    public void ZipNameHasTimestamp() => Assert.Equal("GaffersNotebook-feedback-20261016-0905.zip", FeedbackPack.ZipName(new DateTime(2026, 10, 16, 9, 5, 0)));
}
