using System.Text.Json;
using Xunit;
using Zeus.Sim;

public sealed class MentorFirstTests
{
    static Dictionary<string, string> Lang(string lang)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "data", "lang"))) dir = dir.Parent;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(dir!.FullName, "data", "lang", lang + ".json")))!;
    }

    [Fact]
    public void EveryMomentHasUniqueIdAndTextInBothLanguages()
    {
        Assert.Equal(MentorFirst.All.Count, MentorFirst.All.Select(m => m.Id).Distinct().Count());
        foreach (var lang in new[] { "en", "nl" })
        {
            var text = Lang(lang);
            Assert.All(MentorFirst.All, m => Assert.True(text.TryGetValue("mentor." + m.Key, out var line) && line.Length is > 0 and <= MentorTiming.MaxLineLength, lang + " " + m.Key));
        }
    }

    [Fact]
    public void MomentsBelongToAHelpScreenAndFitTheSeenList()
    {
        var screens = new[] { "menu", "season", "squad", "editor", "meter", "challenges", "match" };
        Assert.All(MentorFirst.All, m => Assert.Contains(m.Screen, screens));
        Assert.All(MentorFirst.All, m => Assert.True(m.Key.Length <= 80));      // OnboardingState.MaxIdLength
        Assert.True(MentorFirst.All.Count < 256);                                // well under OnboardingState.MaxSeen (512) together with the tutorial ids
        Assert.Equal(MentorFirst.All.Count(m => m.Screen == "season"), MentorFirst.ForScreen("season").Count());
    }

    [Fact]
    public void SeenIsRememberedOncePerProfileAndCanBeResetAndSurvivesSaving()
    {
        var state = new OnboardingState();
        var id = MentorFirst.All[0].Key;
        Assert.False(state.HasSeen(id));
        Assert.True(state.MarkSeen(id));
        Assert.False(state.MarkSeen(id));         // second time: nothing new to save, so the line stays silent
        Assert.True(OnboardingState.Parse(state.ToJson()).HasSeen(id));
        state.ClearSeen();                        // "explanations again"
        Assert.False(state.HasSeen(id));
    }
}

public sealed class ProfileAdminTests
{
    [Fact]
    public void NewProfilesGetTheFirstFreeNameAndWipedOnesCanNeverBePickedAgain()
    {
        Assert.Equal("p2", ProfileLayout.NextName(Array.Empty<string>()));
        Assert.Equal("p3", ProfileLayout.NextName(new[] { "p2" }));
        Assert.Equal("p2", ProfileLayout.NextName(new[] { "p3" }));
        Assert.True(ProfileLayout.IsValidName(ProfileLayout.NextName(new[] { "p2", "p3" })));
        var wiped = ProfileLayout.WipedName("p2", new DateTime(2026, 10, 27));
        Assert.Equal("p2.verwijderd-20261027", wiped);
        Assert.False(ProfileLayout.IsValidName(wiped));
        Assert.Contains("profiel.json", ProfileLayout.MainFiles);
        Assert.DoesNotContain("instellingen.json", ProfileLayout.MainFiles);      // settings stay global
    }
}
