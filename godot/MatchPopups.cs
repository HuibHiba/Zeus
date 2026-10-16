// Match popups (goal, cards, substitution, fouls, half time, ...): a transparent overlay ABOVE the match picture. It knows nothing about how
// the pitch is drawn, so it stays the same over a later 3D stadium. It only gets the event list, the playback speed and team names/colours.
// The logic (which event becomes which popup, filtering, queueing) is in sim/Zeus.Sim/MatchNarrator.cs and is tested without Godot.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Zeus.Sim;
using Side = Zeus.Sim.Side;
using Popup = Zeus.Sim.Popup;

/// <summary>The one place for colours, fonts and sizes of the popups, so the later restyle (notebook look) touches only this object.</summary>
public static class PopupStyle
{
    public static Color Fill => new(UiTheme.Panel, 0.94f);
    public static Color Border => UiTheme.Accent;
    public static Color Text => UiTheme.Text;
    public static Color Muted => UiTheme.Muted;
    public static Color Headline => UiTheme.Accent;
    public static Color Yellow => new("FFD400");
    public static Color Red => new("D7263D");
    public static Color Good => UiTheme.Good;
    public static Color Bad => UiTheme.Bad;
    public static Color Team(Side side) => side == Side.Home ? UiTheme.Home : UiTheme.Away;

    public static Font Display => GameFonts.Display;
    public static Font Semi => GameFonts.Semi;

    public const int GoalSize = 92, TitleSize = 52, LineSize = 30, ScoreSize = 42, StripSize = 22, TagSize = 22;   // all main text >= 17 px
    public const float CardW = 40, CardH = 56;
    public static readonly Vector2 Center = new(600, 260);       // middle of the pitch area, a bit up so the play stays visible
    public static readonly Vector2 StripCenter = new(600, 694);  // bottom of the pitch
    public const float MainW = 660, StripW = 600, StripH = 44, BorderTop = 4;
    public const double FadeIn = 0.15, FadeOut = 0.25;
}

/// <summary>How the overlay turns ids into names; Main fills these from the squads (the overlay knows no Match).</summary>
public sealed record PopupNames(Func<Side, string, string> Slot, Func<Side, string, string> Squad, Func<Side, string[]> Starters, Func<Side, string[]> Bench)
{
    public static PopupNames From(Match match, Squad home, Squad away)
    {
        Squad Of(Side s) => s == Side.Home ? home : away;
        string Name(Side s, string? squadId) => Loc.Name(Of(s).Starters.Concat(Of(s).Bench).FirstOrDefault(p => p.Id == squadId)?.Name ?? squadId ?? "");
        return new PopupNames(
            (s, slot) => slot.Length == 0 ? "" : Name(s, match.SquadIdOf(s, slot) ?? slot),
            (s, id) => id.Length == 0 ? "" : Name(s, id),
            s => Of(s).Starters.Select(p => p.Id).ToArray(),
            s => Of(s).Bench.Select(p => p.Id).ToArray());
    }
}

public partial class MatchPopups : Control
{
    const string SettingKey = "popups";

    /// <summary>The player's choice: all / important only / off. Saved in instellingen.json next to the language.</summary>
    public static PopupMode Mode
    {
        get => PopupRules.ParseMode(Loc.Setting(SettingKey, "all"));
        set => Loc.SetSetting(SettingKey, PopupRules.ModeName(value));
    }

    readonly string _homeName, _awayName;
    readonly PopupNames _names;
    readonly MatchNarrator _narrator = new();
    readonly PopupQueue _queue = new();
    readonly Dictionary<string, string> _text = new();      // popup key -> text resolved at the moment it was queued (names as the player knew them)
    double _speed = 1;

    // ZEUS_POPUPDEMO: shows every popup type in turn and saves a screenshot of each (ZEUS_SHOT is the file prefix).
    readonly List<(string Name, Popup Popup)> _demo = new();
    int _demoNext, _demoShots;
    string _demoCurrent = "";
    double _demoEnd;
    public bool DemoRunning => _demo.Count > 0 && _demoShots < _demo.Count;

    public MatchPopups(string homeName, string awayName, PopupNames names)
    {
        _homeName = homeName; _awayName = awayName; _names = names;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    string TeamName(Side side) => side == Side.Home ? _homeName : _awayName;

    /// <summary>Reads the events not seen yet. suppress: fast forward / skip to end, only the final result remains.</summary>
    public void Feed(IReadOnlyList<Event> events, double speed, bool suppress = false)
    {
        _speed = speed;
        if (_demo.Count > 0) return;
        var mode = Mode;
        foreach (var popup in PopupRules.Filter(_narrator.Consume(events, suppress), mode, speed))
            Enqueue(popup, speed);
        _queue.DropWaiting(p => !PopupRules.Allows(p, mode, speed));
    }

    void Enqueue(Popup popup, double speed)
    {
        _text[popup.Key] = Describe(popup);
        _queue.Enqueue(popup, PopupRules.Duration(popup, speed));
    }

    /// <summary>Real time, not match time. While the match is paused the popups stand still.</summary>
    public void Advance(double delta, bool paused)
    {
        if (!paused) _queue.Update(Math.Min(delta, 0.25));
        else _queue.Update(0);
        if (_demo.Count > 0) StepDemo(delta);
        QueueRedraw();
    }

    // ---- texts -------------------------------------------------------------------------------------------------

    string Describe(Popup p)       // second line of the main card or the whole strip text; the heading comes from the kind
    {
        string who = _names.Slot(p.Team, p.PlayerId);
        return p.Kind switch
        {
            PopupKind.Foul => Loc.T("popup.foul", who.Length > 0 ? who : TeamName(p.Team)),
            PopupKind.Offside => Loc.T("popup.offside", who.Length > 0 ? who : TeamName(p.Team)),
            PopupKind.Corner => Loc.T("popup.corner", TeamName(p.Team)),
            PopupKind.Penalty => Loc.T("popup.penalty_for", TeamName(p.Team)),
            PopupKind.Goal => who.Length > 0 ? $"{who}   {p.Minute}'" : $"{Loc.T("popup.goal_unknown", TeamName(p.Team))}   {p.Minute}'",
            PopupKind.Abandoned => Loc.T("popup.abandoned_sub", TeamName(p.Team)),
            _ => who.Length > 0 ? $"{who}   {p.Minute}'" : $"{p.Minute}'",
        };
    }

    static string Heading(Popup p) => p.Kind switch
    {
        PopupKind.Goal => Loc.T("popup.goal"),
        PopupKind.YellowCard => Loc.T("popup.yellow"),
        PopupKind.RedCard => Loc.T("popup.red"),
        PopupKind.SecondYellow => Loc.T("popup.second_yellow"),
        PopupKind.Substitution => Loc.T("popup.sub"),
        PopupKind.Penalty => Loc.T("popup.penalty"),
        PopupKind.HalfTime => Loc.T("popup.halftime"),
        PopupKind.SecondHalf => Loc.T("popup.second_half"),
        PopupKind.FullTime => Loc.T("popup.fulltime"),
        PopupKind.Abandoned => Loc.T("popup.abandoned"),
        _ => "",
    };

    static string Short(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

    // ---- drawing -----------------------------------------------------------------------------------------------

    static float Alpha(PopupQueue.Active a)
    {
        double fadeOut = Math.Max(0.01, PopupStyle.FadeOut);
        return (float)Math.Clamp(Math.Min(a.Age / PopupStyle.FadeIn, (a.Duration - a.Age) / fadeOut), 0, 1);
    }

    public override void _Draw()
    {
        if (_queue.Main is { } main) DrawMain(main);
        if (_queue.Strip is { } strip) DrawStrip(strip);
    }

    Color Fade(Color c, float alpha) => new(c, c.A * alpha);

    void Centered(Font font, float centerX, float baseline, string text, int size, Color color, float alpha) =>
        DrawString(font, new Vector2(centerX - PopupStyle.MainW / 2, baseline), text, HorizontalAlignment.Center, PopupStyle.MainW, size, Fade(color, alpha));

    void DrawMain(PopupQueue.Active a)
    {
        var p = a.Popup;
        float alpha = Alpha(a);
        float slide = (1 - alpha) * -14;                              // comes in from slightly above; no pulsing
        var c = PopupStyle.Center + new Vector2(0, slide);
        string line = _text.GetValueOrDefault(p.Key, "");
        var teamColor = PopupStyle.Team(p.Team);
        float w = PopupStyle.MainW;
        bool score = p.Kind is PopupKind.Goal or PopupKind.HalfTime or PopupKind.FullTime or PopupKind.Abandoned;
        float h = p.Kind switch { PopupKind.Goal => 250, PopupKind.Substitution => 170, PopupKind.Penalty => 150, _ when score => 190, _ => 132 };
        var box = new Rect2(c.X - w / 2, c.Y - 20, w, h);
        DrawStyleBox(new StyleBoxFlat { BgColor = Fade(PopupStyle.Fill, alpha), BorderColor = Fade(PopupStyle.Border, alpha), BorderWidthTop = (int)PopupStyle.BorderTop,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6, CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2 }, box);
        if (p.Kind is PopupKind.Goal or PopupKind.Penalty or PopupKind.Substitution)
            DrawRect(new Rect2(box.Position.X, box.Position.Y + box.Size.Y - 6, w, 6), Fade(teamColor, alpha), true);   // short home/away colour bar
        float x = c.X;
        switch (p.Kind)
        {
            case PopupKind.Goal:
                Centered(PopupStyle.Display, x, box.Position.Y + 98, Heading(p), PopupStyle.GoalSize, PopupStyle.Headline, alpha);
                Centered(PopupStyle.Semi, x, box.Position.Y + 150, Short(line, 44), PopupStyle.LineSize, PopupStyle.Text, alpha);
                DrawScoreLine(box.Position.Y + 208, p, alpha);
                break;
            case PopupKind.YellowCard:
            case PopupKind.RedCard:
            case PopupKind.SecondYellow:
                float cardX = box.Position.X + 34, cardY = box.Position.Y + 36;
                if (p.Kind == PopupKind.SecondYellow)
                {
                    DrawRect(new Rect2(cardX, cardY, PopupStyle.CardW, PopupStyle.CardH), Fade(PopupStyle.Yellow, alpha), true);
                    DrawRect(new Rect2(cardX + 22, cardY + 10, PopupStyle.CardW, PopupStyle.CardH), Fade(PopupStyle.Red, alpha), true);
                    DrawRect(new Rect2(cardX + 22, cardY + 10, PopupStyle.CardW, PopupStyle.CardH), Fade(Colors.White, alpha * 0.8f), false, 2);
                }
                else DrawRect(new Rect2(cardX + 10, cardY, PopupStyle.CardW, PopupStyle.CardH), Fade(p.Kind == PopupKind.RedCard ? PopupStyle.Red : PopupStyle.Yellow, alpha), true);
                float tx = box.Position.X + 120;
                int titleSize = PopupStyle.TitleSize;           // a long translation shrinks instead of being cut off
                while (titleSize > 30 && PopupStyle.Display.GetStringSize(Heading(p), HorizontalAlignment.Left, -1, titleSize).X > w - 140) titleSize -= 2;
                DrawString(PopupStyle.Display, new Vector2(tx, box.Position.Y + 62), Heading(p), HorizontalAlignment.Left, -1, titleSize, Fade(PopupStyle.Text, alpha));
                DrawString(PopupStyle.Semi, new Vector2(tx, box.Position.Y + 102), Short(line, 34), HorizontalAlignment.Left, w - 140, PopupStyle.LineSize, Fade(teamColor, alpha));
                break;
            case PopupKind.Substitution:
                Centered(PopupStyle.Display, x, box.Position.Y + 50, Heading(p), PopupStyle.TitleSize - 10, teamColor, alpha);
                SubLine(box.Position.X + 40, box.Position.Y + 92, Loc.T("popup.sub_in"), PopupStyle.Good, _names.Squad(p.Team, p.OtherPlayerId), alpha);
                SubLine(box.Position.X + 40, box.Position.Y + 132, Loc.T("popup.sub_out"), PopupStyle.Bad, _names.Squad(p.Team, p.Detail), alpha);
                DrawString(PopupStyle.Semi, new Vector2(box.End.X - 90, box.Position.Y + 50), $"{p.Minute}'", HorizontalAlignment.Right, 60, PopupStyle.LineSize, Fade(PopupStyle.Muted, alpha));
                break;
            case PopupKind.Penalty:
                Centered(PopupStyle.Display, x, box.Position.Y + 70, Heading(p), PopupStyle.TitleSize + 14, PopupStyle.Headline, alpha);
                Centered(PopupStyle.Semi, x, box.Position.Y + 112, Short(line, 40), PopupStyle.LineSize, teamColor, alpha);
                break;
            default:         // half time, second half, full time, abandoned
                Centered(PopupStyle.Display, x, box.Position.Y + 78, Heading(p), PopupStyle.TitleSize + 22, PopupStyle.Headline, alpha);
                if (score) DrawScoreLine(box.Position.Y + 134, p, alpha);
                if (p.Kind == PopupKind.Abandoned) Centered(PopupStyle.Semi, x, box.Position.Y + 172, Short(line, 44), PopupStyle.LineSize, PopupStyle.Muted, alpha);
                break;
        }
    }

    void SubLine(float x, float baseline, string tag, Color tagColor, string name, float alpha)
    {
        var tagBox = new Rect2(x, baseline - 26, 78, 32);
        DrawRect(tagBox, Fade(tagColor, alpha), true);
        DrawString(PopupStyle.Display, new Vector2(x, baseline - 2), tag, HorizontalAlignment.Center, 78, PopupStyle.TagSize, Fade(Colors.Black, alpha));
        DrawString(PopupStyle.Semi, new Vector2(x + 96, baseline), Short(name, 30), HorizontalAlignment.Left, PopupStyle.MainW - 180, PopupStyle.LineSize - 2, Fade(PopupStyle.Text, alpha));
    }

    // "HOME 2 - 1 AWAY" with the team names in their own colour (the names are always there, so colour is never the only cue).
    void DrawScoreLine(float baseline, Popup p, float alpha)
    {
        var font = PopupStyle.Display;
        int size = PopupStyle.ScoreSize;
        string mid = $"{p.HomeScore} - {p.AwayScore}";
        float midW = font.GetStringSize(mid, HorizontalAlignment.Left, -1, size + 14).X;
        float homeW = font.GetStringSize(_homeName, HorizontalAlignment.Left, -1, size).X;
        float awayW = font.GetStringSize(_awayName, HorizontalAlignment.Left, -1, size).X;
        float cx = PopupStyle.Center.X, gap = 22;
        DrawString(font, new Vector2(cx - midW / 2 - gap - homeW, baseline), _homeName, HorizontalAlignment.Left, -1, size, Fade(PopupStyle.Team(Side.Home), alpha));
        DrawString(font, new Vector2(cx - midW / 2, baseline + 4), mid, HorizontalAlignment.Left, -1, size + 14, Fade(Colors.White, alpha));
        DrawString(font, new Vector2(cx + midW / 2 + gap, baseline), _awayName, HorizontalAlignment.Left, -1, size, Fade(PopupStyle.Team(Side.Away), alpha));
    }

    void DrawStrip(PopupQueue.Active a)
    {
        var p = a.Popup;
        float alpha = Alpha(a);
        string line = _text.GetValueOrDefault(p.Key, "");
        var c = PopupStyle.StripCenter + new Vector2(0, (1 - alpha) * 10);
        var box = new Rect2(c.X - PopupStyle.StripW / 2, c.Y - PopupStyle.StripH / 2, PopupStyle.StripW, PopupStyle.StripH);
        DrawStyleBox(new StyleBoxFlat { BgColor = Fade(PopupStyle.Fill, alpha), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 }, box);
        DrawRect(new Rect2(box.Position.X, box.Position.Y, 8, box.Size.Y), Fade(PopupStyle.Team(p.Team), alpha), true);
        DrawString(PopupStyle.Semi, new Vector2(box.Position.X + 22, box.Position.Y + 30), Short(line, 60), HorizontalAlignment.Left, PopupStyle.StripW - 70, PopupStyle.StripSize, Fade(PopupStyle.Text, alpha));
        DrawString(PopupStyle.Semi, new Vector2(box.End.X - 64, box.Position.Y + 30), $"{p.Minute}'", HorizontalAlignment.Right, 50, PopupStyle.StripSize, Fade(PopupStyle.Muted, alpha));
    }

    // ---- ZEUS_POPUPDEMO (debug only) --------------------------------------------------------------------------

    public void StartDemo()
    {
        string a = _names.Starters(Side.Home).LastOrDefault() ?? "ST", b = _names.Starters(Side.Away).ElementAtOrDefault(3) ?? "CB";
        string c = _names.Starters(Side.Home).ElementAtOrDefault(5) ?? "LW", inId = _names.Bench(Side.Home).ElementAtOrDefault(1) ?? "B2";
        string outSquad = _names.Starters(Side.Home).ElementAtOrDefault(5) ?? "LW";
        Popup Make(PopupKind kind, PopupWeight w, Side team, string player, string other, double sec, int hs, int aws, int minute, string detail = "") =>
            new(kind, w, team, player, other, minute, hs, aws, sec, _demo.Count + 1, detail);
        _demo.Add(("goal", Make(PopupKind.Goal, PopupWeight.Major, Side.Home, a, "", 2.5, 2, 1, 37)));
        _demo.Add(("yellow", Make(PopupKind.YellowCard, PopupWeight.Normal, Side.Away, b, "", 2.0, 2, 1, 41)));
        _demo.Add(("red", Make(PopupKind.RedCard, PopupWeight.Major, Side.Away, b, "", 2.0, 2, 1, 44)));
        _demo.Add(("secondyellow", Make(PopupKind.SecondYellow, PopupWeight.Major, Side.Away, b, "", 2.0, 2, 1, 44)));
        _demo.Add(("sub", Make(PopupKind.Substitution, PopupWeight.Normal, Side.Home, c, inId, 2.0, 2, 1, 61, outSquad)));
        _demo.Add(("foul", Make(PopupKind.Foul, PopupWeight.Minor, Side.Away, b, "", 1.2, 2, 1, 63)));
        _demo.Add(("offside", Make(PopupKind.Offside, PopupWeight.Minor, Side.Home, a, "", 1.2, 2, 1, 64)));
        _demo.Add(("penalty", Make(PopupKind.Penalty, PopupWeight.Normal, Side.Home, a, "", 2.0, 2, 1, 70)));
        _demo.Add(("corner", Make(PopupKind.Corner, PopupWeight.Minor, Side.Away, b, "", 1.2, 2, 1, 71)));
        _demo.Add(("halftime", Make(PopupKind.HalfTime, PopupWeight.Major, Side.Home, "", "", 2.5, 2, 1, 46)));
        _demo.Add(("secondhalf", Make(PopupKind.SecondHalf, PopupWeight.Normal, Side.Away, "", "", 1.8, 2, 1, 46)));
        _demo.Add(("fulltime", Make(PopupKind.FullTime, PopupWeight.Major, Side.Home, "", "", 3.0, 2, 1, 91)));
        _demo.Add(("abandoned", Make(PopupKind.Abandoned, PopupWeight.Major, Side.Away, "", "", 3.0, 2, 1, 80)));
        _demoEnd = 0;
    }

    void StepDemo(double delta)
    {
        string prefix = GameSession.DebugEnvironment("ZEUS_SHOT");
        var shown = _queue.Main ?? _queue.Strip;
        if (shown is null)
        {
            if (_demoNext < _demo.Count)
            {
                var (name, popup) = _demo[_demoNext++];
                _demoCurrent = name;
                Enqueue(popup, popup.Seconds);
            }
            else if (_demoShots >= _demo.Count && (_demoEnd += delta) > 0.3) GetTree().Quit();
            return;
        }
        if (shown.Age >= 0.9 && _demoCurrent.Length > 0)
        {
            if (prefix.Length > 0) GetViewport().GetTexture().GetImage().SavePng($"{prefix}_popup_{_demoCurrent}.png");
            _demoCurrent = "";
            _demoShots++;
        }
    }
}
