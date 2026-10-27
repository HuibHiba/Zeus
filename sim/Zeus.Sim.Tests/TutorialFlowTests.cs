using Zeus.Sim;
using Xunit;

public class TutorialFlowTests
{
    [Theory]
    [InlineData("squad", 2, false)] [InlineData("squad", 3, true)]
    [InlineData("editor", 3, false)] [InlineData("library", 4, true)]
    [InlineData("meter", 4, false)] [InlineData("league", 5, true)]
    [InlineData("challenges", 5, false)] [InlineData("challenges", 6, true)]
    [InlineData("friends", 6, false)]
    [InlineData("settings", 0, true)]
    public void FeaturesOpenAtTheirStep(string feature, int step, bool open) => Assert.Equal(open, TutorialFlow.Unlocked(feature, step, done: false));

    [Fact]
    public void EverythingIsOpenWhenDone() => Assert.All(new[] { "squad", "editor", "meter", "league", "challenges", "friends" }, f => Assert.True(TutorialFlow.Unlocked(f, 0, done: true)));

    [Fact]
    public void LockedFeaturesNameTheStepThatOpensThem() => Assert.Equal("c", TutorialFlow.After("meter"));

    [Fact]
    public void ScenarioIndexCoversOnlyThePracticeSteps() =>
        Assert.Equal(new[] { -1, -1, 0, 1, 2, -1, -1 }, Enumerable.Range(0, 7).Select(TutorialFlow.ScenarioIndex));
}
