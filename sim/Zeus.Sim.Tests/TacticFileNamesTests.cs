using Xunit;

namespace Zeus.Sim.Tests;

public class TacticFileNamesTests
{
    [Theory]
    [InlineData("CON")]
    [InlineData("con")]
    [InlineData("Nul")]
    [InlineData("PRN")]
    [InlineData("aux")]
    [InlineData("COM1")]
    [InlineData("com9")]
    [InlineData("LPT1")]
    [InlineData("lpt9")]
    [InlineData("con.json")]
    [InlineData("NUL.txt")]
    public void ReservedWindowsNamesAreRefused(string name)
    {
        Assert.True(TacticFileNames.IsReserved(name));
        if (!name.Contains('.')) Assert.Equal("", TacticFileNames.Sanitize(name));       // the editor strips dots before the check
    }

    [Theory]
    [InlineData("COM0")]
    [InlineData("CONSOLE")]
    [InlineData("mijn_433")]
    [InlineData("lpt10")]
    public void OrdinaryNamesAreNotReserved(string name) => Assert.False(TacticFileNames.IsReserved(name));

    [Fact]
    public void SanitizeKeepsSafeCharactersAndCutsAtSixtyCharacters()
    {
        Assert.Equal("abc-d", TacticFileNames.Sanitize("a b?c*-d."));
        Assert.Equal("", TacticFileNames.Sanitize("  ..  "));
        Assert.Equal("", TacticFileNames.Sanitize(null));
        Assert.Equal(60, TacticFileNames.Sanitize(new string('a', 100)).Length);
    }

    [Theory]
    [InlineData("hoge_druk_433.json", true)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("..", false)]
    [InlineData("../x.json", false)]
    [InlineData("map/x.json", false)]
    [InlineData("map\\x.json", false)]
    [InlineData("C:x.json", false)]
    public void IsBareOnlyAcceptsPlainFileNames(string file, bool expected) => Assert.Equal(expected, TacticFileNames.IsBare(file));
}
