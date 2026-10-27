// First start (design: 01 Ontwerp/Tutorial en onboarding.md §3): one screen, three steps. P1 name + coach look, P2 club (ClubPicker), P3 settings.
// Nothing is written until "To the club": the club picks live in ProfileStore.Editing, then profiel.json is saved. No language step (the game is English).
// ZEUS_TUTORIAL=skip hides it (autotests, screenshots); ZEUS_TUTORIAL=force shows it even in headless/screenshot runs.
using System;
using Godot;
using Zeus.Sim;

/// <summary>Placeholder coach portrait drawn from the four indices; real artwork layers (skin/hair/outfit PNGs) replace it later with the same indices.</summary>
public partial class CoachPortrait : Control
{
    public static readonly Color[] Skins = { new("F1D3B8"), new("E0B48E"), new("C08A5E"), new("94623D"), new("5E3A24") };
    static readonly Color[] Hairs = { new("2B2118"), new("8C8C88"), new("6B4A2E"), new("1B1410"), new("3A3A3A"), new("1F2F4F") };
    public int Skin, Hair, Outfit, Colour;

    public override void _Draw()
    {
        var c = Size / 2 + new Vector2(0, Size.Y * .02f);
        float w = Size.X * .22f, h = Size.Y * .26f;
        var shirt = ClubIdentity.Kits[Colour];
        var shoulders = new Rect2(c + new Vector2(-w * 1.9f, h * .95f), new Vector2(w * 3.8f, h * 1.2f));
        DrawRect(shoulders, Outfit == 3 ? shirt.Lightened(.2f) : shirt);                                          // 0 tracksuit, 1 coat, 2 shirt + tie, 3 polo
        if (Outfit == 1) DrawRect(new Rect2(shoulders.Position - new Vector2(8, 0), new Vector2(16, shoulders.Size.Y)), shirt.Darkened(.3f));
        if (Outfit == 2) DrawRect(new Rect2(c + new Vector2(-6, h * .95f), new Vector2(12, h * .9f)), UiTheme.Ink);
        DrawRect(new Rect2(c + new Vector2(-w * .35f, h * .6f), new Vector2(w * .7f, h * .5f)), Skins[Skin].Darkened(.1f));     // neck
        Sketch.Ellipse(this, c, w, h, Skins[Skin], UiTheme.Ink);
        foreach (float side in new[] { -1f, 1f }) DrawCircle(c + new Vector2(side * w * .4f, -h * .05f), 3.5f, UiTheme.Ink);
        DrawLine(c + new Vector2(-w * .25f, h * .5f), c + new Vector2(w * .25f, h * .5f), UiTheme.Ink, 3f);
        var hair = Hairs[Hair];
        if (Hair == 5) Sketch.Cap(this, c, w, h, hair);               // cap
        else if (Hair == 4) DrawRect(new Rect2(c + new Vector2(-w * .7f, h * .3f), new Vector2(w * 1.4f, h * .35f)), hair);     // beard, head shaved
        else DrawRect(new Rect2(c + new Vector2(-w * .8f, -h * .98f), new Vector2(w * 1.6f, h * (Hair == 2 ? .6f : Hair == 3 ? .5f : .3f))), hair);
    }
}

public partial class FirstStart : CanvasLayer
{
    static readonly string[] Paces = { "relaxed", "standard", "analysis" };
    readonly ProfileData _draft = new();
    Action _done = () => { };
    VBoxContainer _page = null!;
    Label _step = null!, _title = null!;
    Button _back = null!, _next = null!;
    LineEdit _name = null!;
    CoachPortrait _coach = null!;
    int _index;

    /// <summary>True when the active profile has no profiel.json yet (a first start, or an RC7 save meeting this build).</summary>
    public static bool Needed(bool windowed)
    {
        var mode = GameSession.DebugEnvironment("ZEUS_TUTORIAL");
        if (mode == "skip" || ProfileStore.Current is not null || ProfileStore.Blocked) return false;
        return mode == "force" || (windowed && GameSession.DebugEnvironment("ZEUS_SHOT").Length == 0);
    }

    public static void Open(Node owner, Action done)
    {
        // An RC7 save keeps its club: the draft starts from the old settings and the player only confirms or changes them.
        var first = new FirstStart { Name = "FirstStart", Layer = 55, _done = done };
        first._draft.ClubName = Competition.CleanClubName(Loc.Setting("club_name", "")) ?? "";
        first._draft.Kit1 = ClubIdentity.Kit1; first._draft.Kit2 = ClubIdentity.Kit2; first._draft.Crest = ClubIdentity.Crest;
        ProfileStore.Editing = first._draft;
        owner.GetTree().Root.AddChild(first);
    }

    public override void _Ready()
    {
        var shade = new ColorRect { Color = UiTheme.Paper, MouseFilter = Control.MouseFilterEnum.Stop };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect); AddChild(shade);
        var frame = new Control(); UiTheme.Apply(frame); UiFrame.Center(frame); AddChild(frame);
        UiTheme.ApplyPaper(frame);
        var box = new VBoxContainer { Position = new Vector2(250, 90), CustomMinimumSize = new Vector2(1100, 0) };
        box.AddThemeConstantOverride("separation", 16); frame.AddChild(box);
        _step = UiTheme.Label("", UiTheme.Small, UiTheme.Graphite); box.AddChild(_step);
        _title = UiTheme.Label("", 44, UiTheme.Ink, display: true); box.AddChild(_title);
        _page = new VBoxContainer { CustomMinimumSize = new Vector2(1100, 520) }; _page.AddThemeConstantOverride("separation", 12); box.AddChild(_page);
        var buttons = new HBoxContainer(); buttons.AddThemeConstantOverride("separation", 12); box.AddChild(buttons);
        _back = UiTheme.Secondary(Loc.T("profile.back")); _back.Pressed += () => Go(_index - 1); buttons.AddChild(_back);
        buttons.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _next = UiTheme.Primary(""); _next.CustomMinimumSize = new Vector2(220, 58); _next.Pressed += () => { if (_index == 2) Finish(); else Go(_index + 1); }; buttons.AddChild(_next);
        Go(0);
    }

    void Go(int index)
    {
        if (index < 0) return;
        _index = index;
        foreach (var child in _page.GetChildren()) child.QueueFree();
        _step.Text = Loc.T("profile.step", index + 1);
        _title.Text = Loc.T("profile.s" + (index + 1));
        _back.Visible = index > 0;
        _next.Text = Loc.T(index == 2 ? "profile.finish" : "profile.next");
        if (index == 0) { BuildCoach(); if (HasOldSave()) { var note = UiTheme.Label(Loc.T("profile.rc7_note"), UiTheme.Small, UiTheme.Graphite); note.AutowrapMode = TextServer.AutowrapMode.WordSmart; note.CustomMinimumSize = new Vector2(1000, 0); _page.AddChild(note); } }
        else if (index == 1) BuildClub(); else BuildSettings();
        Callable.From(() => { if (_index == 0 && _name.IsInsideTree()) _name.GrabFocus(); else _next.GrabFocus(); }).CallDeferred();
    }

    // An RC7 player meets this screen too (no profiel.json yet): say that nothing of the old save is touched.
    static bool HasOldSave() => FileAccess.FileExists("user://zeus_carriere.json") || FileAccess.FileExists("user://zeus_progress.json");

    void Validate() => _next.Disabled = _index == 0 && Competition.CleanClubName(_name.Text) is null;

    void BuildCoach()
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 40); _page.AddChild(row);
        _coach = new CoachPortrait { CustomMinimumSize = new Vector2(300, 380), Skin = _draft.Skin, Hair = _draft.Hair, Outfit = _draft.Outfit, Colour = _draft.Colour };
        row.AddChild(_coach);
        var col = new VBoxContainer(); col.AddThemeConstantOverride("separation", 12); row.AddChild(col);
        _name = new LineEdit { Text = _draft.Name, MaxLength = Competition.MaxClubName, PlaceholderText = Loc.T("profile.name_placeholder"), CustomMinimumSize = new Vector2(420, 40), Name = "coach_name" };
        _name.TextChanged += text => { _draft.Name = Competition.CleanClubName(text) ?? ""; Validate(); };
        col.AddChild(UiTheme.Label(Loc.T("profile.name"), 18, UiTheme.Muted, display: true)); col.AddChild(_name);
        Picks(col, "coach.skin", 5, () => _draft.Skin, v => _draft.Skin = v, i => Loc.T("coach.skin." + i), i => CoachPortrait.Skins[i]);
        Picks(col, "coach.hair", 6, () => _draft.Hair, v => _draft.Hair = v, i => Loc.T("coach.hair." + i), null);
        Picks(col, "coach.outfit", 4, () => _draft.Outfit, v => _draft.Outfit = v, i => Loc.T("coach.outfit." + i), null);
        Picks(col, "coach.colour", ClubIdentity.Kits.Length, () => _draft.Colour, v => _draft.Colour = v, ClubIdentity.KitName, i => ClubIdentity.Kits[i]);
        Validate();
    }

    void Picks(VBoxContainer col, string title, int count, Func<int> get, Action<int> set, Func<int, string> tip, Func<int, Color>? swatch)
    {
        col.AddChild(UiTheme.Label(Loc.T(title), 18, UiTheme.Muted, display: true));
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6); col.AddChild(row);
        var buttons = new Button[count];
        void Mark() { for (int i = 0; i < count; i++) buttons[i].Modulate = i == get() ? Colors.White : new Color(1, 1, 1, .55f); }
        for (int i = 0; i < count; i++)
        {
            int pick = i;
            var b = new Button { TooltipText = tip(i), CustomMinimumSize = swatch is null ? new Vector2(0, 40) : new Vector2(40, 40), Text = swatch is null ? tip(i) : "" };
            if (swatch is not null) b.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = swatch(i), BorderColor = UiTheme.Ink, BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2 });
            b.Pressed += () => { set(pick); _coach.Skin = _draft.Skin; _coach.Hair = _draft.Hair; _coach.Outfit = _draft.Outfit; _coach.Colour = _draft.Colour; _coach.QueueRedraw(); Mark(); };
            buttons[i] = b; row.AddChild(b);
        }
        Mark();
    }

    void BuildClub()
    {
        _next.Disabled = false;
        _page.AddChild(new ClubPicker());
        _page.AddChild(UiTheme.Label(Loc.T("profile.league"), UiTheme.Body, UiTheme.Graphite));
    }

    void BuildSettings()
    {
        _next.Disabled = false;
        Option("settings.pace", "match_pace", Paces, i => Loc.T("settings.pace." + Paces[i]), "standard", null);
        Option("settings.window", "fullscreen", new[] { "off", "on" }, i => Loc.T(i == 0 ? "settings.windowed" : "settings.fullscreen"), SettingsMenu.DefaultFullscreen, SettingsMenu.ApplyDisplaySettings);
        Option("settings.sound", "audio", new[] { "on", "off" }, i => Loc.T(i == 0 ? "settings.on" : "settings.off"), "on", AudioManager.ApplyVolumes);
        _page.AddChild(UiTheme.Label(Loc.T("profile.later"), UiTheme.Small, UiTheme.Graphite));
    }

    void Option(string title, string key, string[] values, Func<int, string> label, string fallback, Action? apply)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 16); _page.AddChild(row);
        var name = UiTheme.Label(Loc.T(title), 18, UiTheme.Muted, display: true); name.CustomMinimumSize = new Vector2(220, 0); row.AddChild(name);
        var choice = new OptionButton { CustomMinimumSize = new Vector2(260, 40) }; row.AddChild(choice);
        for (int i = 0; i < values.Length; i++) choice.AddItem(label(i));
        choice.Select(Math.Max(0, Array.IndexOf(values, Loc.Setting(key, fallback))));
        choice.ItemSelected += i => { Loc.SetSetting(key, values[(int)i]); apply?.Invoke(); };
    }

    void Finish()
    {
        _draft.Created = DateTime.UtcNow.ToString("yyyy-MM-dd");
        ProfileStore.Editing = null;
        if (!ProfileStore.Save(_draft)) GD.PushWarning("profile not saved");
        QueueFree();
        _done();
    }
}
