namespace Zeus.Sim.Tests;

public sealed class ShareCodeTests
{
    static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Zeus.sln"))) dir = Path.GetDirectoryName(dir);
        return dir!;
    }

    static TacticEditorModel Shipped() => TacticEditorModel.Parse(File.ReadAllText(Path.Combine(Root(), "tactics", "positiespel_4231.json")));

    [Fact]
    public void TacticRoundTripsThroughOneLineCode()
    {
        var model = Shipped();
        var code = TacticExchange.ToCode(model);
        Assert.StartsWith("ZT1.", code);
        Assert.DoesNotContain('\n', code);
        Assert.Equal(model.ToJson(), TacticExchange.FromCode(code).ToJson());
        Assert.Equal(model.ToJson(), TacticExchange.FromText("  " + code + "\n").ToJson());      // pasted with whitespace
        Assert.Equal(model.ToJson(), TacticExchange.FromText(model.ToJson()).ToJson());          // plain JSON still works
        Assert.True(code.Length < model.ToJson().Length, "a share code is shorter than the JSON");
    }

    [Fact]
    public void DamagedTooLongNewerAndWrongKindCodesAreRejected()
    {
        var code = TacticExchange.ToCode(Shipped());
        var parts = code.Split('.');
        var flipped = parts[1].ToCharArray();
        flipped[flipped.Length / 2] = flipped[flipped.Length / 2] == 'A' ? 'B' : 'A';
        var damaged = $"{parts[0]}.{new string(flipped)}.{parts[2]}";
        Assert.Contains("checksum", Assert.Throws<FormatException>(() => TacticExchange.FromCode(damaged)).Message);
        Assert.Contains("too long", Assert.Throws<FormatException>(() => TacticExchange.FromCode(code + new string('A', ShareCode.MaxLength))).Message);
        Assert.Contains("newer", Assert.Throws<FormatException>(() => TacticExchange.FromCode("ZT2." + parts[1] + "." + parts[2])).Message);
        Assert.Throws<FormatException>(() => ShareCode.Decode(ShareCode.LadderKind, code));      // a tactic code is not a result code
        Assert.Throws<FormatException>(() => TacticExchange.FromCode(""));
        Assert.Throws<FormatException>(() => TacticExchange.FromCode("ZT1.abc"));
        Assert.Throws<FormatException>(() => TacticExchange.FromCode("ZTx." + parts[1] + "." + parts[2]));
        Assert.ThrowsAny<Exception>(() => TacticExchange.FromCode(ShareCode.Encode(ShareCode.TacticKind, "{\"naam\":\"Broken\"}")));      // valid code, invalid tactic
    }

    [Fact]
    public void OversizedContentIsRefusedBeforeAndAfterPacking()
    {
        Assert.Throws<ArgumentException>(() => ShareCode.Encode(ShareCode.TacticKind, new string('x', ShareCode.MaxBytes + 1)));
        var tiny = ShareCode.Encode(ShareCode.TacticKind, "{}");
        Assert.Equal("{}", ShareCode.Decode(ShareCode.TacticKind, tiny));
    }
}
