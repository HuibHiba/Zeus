// Home in normal mode (design §3, decision 7): the club card with the trainer and the next league match instead of the kick-off panel.
// Read-only: the career file is parsed here, never written; "Play match" hands over to Season, which owns all the match logic.
using System;
using System.Linq;
using Godot;
using Zeus.Sim;

public static class HomeCard
{
    /// <summary>Only with a profile and a finished tutorial; autotests (ZEUS_TUTORIAL=skip) and the visual checks without a profile keep the kick-off panel.</summary>
    public static bool Wanted => !GameSession.FriendlyMode && Tutorial.Explaining;

    static SeasonState LoadState()
    {
        try { if (SafeFile.Read(Season.CareerFile(), out var text) == SafeFile.ReadStatus.Ok) return SeasonState.Parse(text); }
        catch (Exception e) { GD.PushWarning("Home: carrière niet gelezen: " + e.Message); }
        return new SeasonState();
    }

    static string GoalText(SeasonState state) => state.Goal switch
    {
        SeasonGoal.Promotion when state.Division == Competition.Divisions.Count - 1 => Loc.T("season.goal.top_two"),
        SeasonGoal.Promotion => Loc.T("season.goal.promotion"),
        SeasonGoal.TopHalf => Loc.T("season.goal.top_half"),
        SeasonGoal.Survival => Loc.T("season.goal.survival"),
        _ => "",
    };

    public static PanelContainer Build(Action playMatch)
    {
        var state = LoadState();
        var panel = new PanelContainer { Name = "home_card", Position = new Vector2(880, 40), CustomMinimumSize = new Vector2(640, 810) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = UiTheme.Paper, BorderColor = UiTheme.PaperShade,
            BorderWidthTop = 3, ContentMarginLeft = 28, ContentMarginRight = 28, ContentMarginTop = 16, ContentMarginBottom = 18,
        });
        UiTheme.ApplyPaper(panel);
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 14); panel.AddChild(box);

        var club = new HBoxContainer(); club.AddThemeConstantOverride("separation", 18); box.AddChild(club);
        var crest = CrestView.For(104); crest.Shape = ClubIdentity.Crest; crest.Fill = ClubIdentity.Kit1; crest.Trim = ClubIdentity.Kit2; club.AddChild(crest);
        var info = new VBoxContainer(); info.AddThemeConstantOverride("separation", 4); club.AddChild(info);
        info.AddChild(UiTheme.Label(ClubIdentity.Name, 40, UiTheme.Ink, display: true));
        info.AddChild(UiTheme.Label(Loc.Name(Competition.DivisionName(state.Division)) + "  ·  " + Loc.T("home.season", state.Season), UiTheme.Body, UiTheme.Graphite));
        int position = Math.Max(1, Competition.Standings(state).ToList().FindIndex(r => r.IsPlayer) + 1);
        info.AddChild(UiTheme.Label(Loc.T("home.standing", position, Competition.TeamsPerDivision), UiTheme.Body, UiTheme.Ink));
        if (GoalText(state) is { Length: > 0 } goal) info.AddChild(Wrapped(Loc.T("season.goal_line", goal), UiTheme.Small, UiTheme.Graphite, 440));

        box.AddChild(new ColorRect { Color = UiTheme.PaperShade, CustomMinimumSize = new Vector2(0, 2) });
        var coachRow = new HBoxContainer(); coachRow.AddThemeConstantOverride("separation", 18); box.AddChild(coachRow);
        var profile = ProfileStore.Current!;
        coachRow.AddChild(new CoachPortrait { CustomMinimumSize = new Vector2(120, 150), Skin = profile.Skin, Hair = profile.Hair, Outfit = profile.Outfit, Colour = profile.Colour });
        var who = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }; coachRow.AddChild(who);
        who.AddChild(UiTheme.Label(profile.Name, 30, UiTheme.Ink, display: true));
        who.AddChild(UiTheme.Label(Loc.T("home.trainer", state.Season), UiTheme.Body, UiTheme.Graphite));

        box.AddChild(new ColorRect { Color = UiTheme.PaperShade, CustomMinimumSize = new Vector2(0, 2) });
        box.AddChild(UiTheme.Label(Loc.T("home.next_match"), 26, UiTheme.Ink, display: true));
        var next = Competition.NextPlayerFixture(state);
        bool direct = next is not null && state.Pending is null && !Cup.Due(state) && state.Goal is not null;      // anything else needs the league screen first (cup round, intro, unfinished result)
        if (next is { } f)
        {
            bool home = f.Home == 0;
            var opponent = Competition.Clubs(state.Division)[home ? f.Away : f.Home].Name;
            box.AddChild(Wrapped(Loc.T(home ? "home.match_home" : "home.match_away", Loc.Name(opponent)), 30, UiTheme.Ink, 560));
            box.AddChild(UiTheme.Label(Loc.T("season.round_of", state.Round + 1, Competition.Rounds), UiTheme.Body, UiTheme.Graphite));
        }
        else box.AddChild(Wrapped(Loc.T("home.season_over"), UiTheme.Body, UiTheme.Graphite, 560));
        box.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        var play = UiTheme.Primary(Loc.T(direct ? "home.play" : "home.open_league")); play.Name = "home_play"; play.CustomMinimumSize = new Vector2(0, 72);
        play.Pressed += playMatch; box.AddChild(play);
        Tutorial.Lock(play, "league");
        return panel;
    }

    static Label Wrapped(string text, int size, Color colour, float width)
    {
        var l = UiTheme.Label(text, size, colour);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart; l.CustomMinimumSize = new Vector2(width, 0);
        return l;
    }
}
