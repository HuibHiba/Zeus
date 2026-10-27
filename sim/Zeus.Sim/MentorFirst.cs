namespace Zeus.Sim;

/// <summary>The explanation moments of the mentor (design section 6): id, the help screen they belong to (names of <c>Welcome</c> screens) and whether it is a blocking card (else a subtitle). Text: <c>mentor.first.&lt;id&gt;</c>. Add new ids, never reuse one.</summary>
public static class MentorFirst
{
    public sealed record Moment(string Id, string Screen, bool Card)
    {
        public string Key => "first." + Id;
    }

    public static IReadOnlyList<Moment> All { get; } = new Moment[]
    {
        new("home", "menu", true),
        new("season", "season", true),
        new("season.simulate", "season", true),
        new("season.familiarity", "season", false),
        new("season.cup", "season", true),
        new("season.absence", "season", false),
        new("season.scout", "season", false),
        new("season.end", "season", true),
        new("season.signing", "season", true),
        new("squad", "squad", true),
        new("squad.train", "squad", true),
        new("editor", "editor", true),
        new("editor.drag", "editor", false),
        new("editor.roles", "editor", true),
        new("editor.triggers", "editor", true),
        new("editor.paths", "editor", false),
        new("editor.test", "editor", true),
        new("editor.save", "editor", false),
        new("editor.unsaved", "editor", false),
        new("library", "menu", true),
        new("meter", "meter", true),
        new("meter.report", "meter", true),
        new("challenges", "challenges", true),
        new("challenges.live", "challenges", true),
        new("challenges.scenario", "challenges", false),
        new("challenges.star", "challenges", false),
        new("friends", "menu", true),
        new("friendly", "menu", true),
        new("match.card", "match", false),
        new("match.skip", "match", false),
        new("match.highlights", "match", false),
        new("match.popups", "match", false),
        new("settings.club", "menu", false),
        new("help", "menu", false),
    };

    public static IEnumerable<Moment> ForScreen(string screen) => All.Where(m => m.Screen == screen);
}
