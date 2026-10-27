using System;
using System.Linq;
using Godot;
using Zeus.Sim;

/// <summary>Shared settings and navigation; opening it preserves the scene and its pause state.</summary>
public partial class SettingsMenu : CanvasLayer
{
    static SettingsMenu? _current;
    bool _wasPaused, _closed;
    Label _status = null!, _warning = null!;
    VBoxContainer _body = null!, _confirmation = null!;
    HBoxContainer _actions = null!;
    Action? _confirmed;

    public static bool IsOpen => _current is not null && GodotObject.IsInstanceValid(_current) && !_current._closed;
    public static double MatchSpeed => Loc.Setting("match_pace", "standard") switch { "relaxed" => 12, "analysis" => 4, _ => 20 };

    public static void Open(Node owner)
    {
        if (IsOpen || Feedback.IsOpen) return;
        if (owner.GetTree().CurrentScene is Menu menu) menu.Remember();
        _current = new SettingsMenu { Name = "SettingsMenu", Layer = 70, ProcessMode = ProcessModeEnum.Always };
        owner.AddChild(_current);
    }

    public static void Escape()
    {
        if (!IsOpen) return;
        if (_current!._confirmation.Visible) _current.CancelConfirmation();
        else _current.Close();
    }

    // First start is fullscreen, but not for headless, ZEUS_WINDOW or ZEUS_SHOT runs (checks and smoke runs need a normal window).
    internal static string DefaultFullscreen => DisplayServer.GetName() == "headless" || GameSession.DebugEnvironment("ZEUS_WINDOW").Length > 0 || GameSession.DebugEnvironment("ZEUS_SHOT").Length > 0 ? "off" : "on";

    public static void ApplyDisplaySettings()
    {
        Engine.MaxFps = Loc.Setting("fps_limit", "60") switch { "30" => 30, "120" => 120, "0" => 0, _ => 60 };
        GameSession.OpponentSubstitutions = Loc.Setting("opponent_substitutions", "off") == "on";
        GameSession.AdaptiveOpponent = Loc.Setting("adaptive_opponent", "on") == "on";
        if (DisplayServer.GetName() == "headless") return;
        // Only switch when the fullscreen choice changes: forcing Windowed on every setting change would un-maximize a window the user maximized.
        bool wantFull = Loc.Setting("fullscreen", DefaultFullscreen) == "on", isFull = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;
        if (wantFull != isFull) DisplayServer.WindowSetMode(wantFull ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetVsyncMode(Loc.Setting("vsync", "on") == "off" ? DisplayServer.VSyncMode.Disabled : DisplayServer.VSyncMode.Enabled);
    }

    public static void ToggleFullscreen()
    {
        bool full = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;
        if (!Loc.SetSetting("fullscreen", full ? "off" : "on"))
        {
            if (IsOpen) _current!._status.Text = Loc.T("settings.save_failed");
            else GameSession.LastError = Loc.T("settings.save_failed");
        }
        ApplyDisplaySettings();
        if (_current?.FindChild("fullscreen", true, false) is OptionButton choice) choice.Select(full ? 0 : 1);
    }

    public override void _Ready()
    {
        _wasPaused = GetTree().Paused;
        GetTree().Paused = true;
        var root = new Control(); root.SetAnchorsPreset(Control.LayoutPreset.FullRect); AddChild(root);
        UiTheme.ApplyPaper(root);
        var shade = new ColorRect { Color = new Color(0, 0, 0, .65f) };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect); root.AddChild(shade);
        var center = new CenterContainer(); center.SetAnchorsPreset(Control.LayoutPreset.FullRect); root.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(1040, 0) }; center.AddChild(panel);
        var content = new VBoxContainer(); content.AddThemeConstantOverride("separation", 12); panel.AddChild(content);
        content.AddChild(UiTheme.Label(Loc.T("settings.title"), 38, UiTheme.Ink, true));
        content.AddChild(Wrapped(Loc.T("settings.intro")));
        _body = new VBoxContainer(); content.AddChild(_body);
        var tabs = new TabContainer { CustomMinimumSize = new Vector2(0, 380) }; _body.AddChild(tabs);
        var game = Tab(tabs, "settings.game");
        Choice(game, "settings.pace", "match_pace", new[] { "standard", "relaxed", "analysis" },
            new[] { Loc.T("settings.pace.standard"), Loc.T("settings.pace.relaxed"), Loc.T("settings.pace.analysis") }, "standard");
        game.AddChild(Wrapped(Loc.T("settings.pace_hint")));
        // Match popups: all / important only / off. Saved at once in instellingen.json (see MatchPopups.Mode).
        Choice(game, "popup.setting", "popups", new[] { "all", "important", "off" },
            new[] { Loc.T("popup.mode.all"), Loc.T("popup.mode.important"), Loc.T("popup.mode.off") }, "all");
        Choice(game, "settings.opponent_substitutions", "opponent_substitutions", new[] { "off", "on" },
            new[] { Loc.T("settings.off"), Loc.T("settings.on") }, "off", ApplyDisplaySettings);
        game.AddChild(Wrapped(Loc.T("settings.opponent_hint")));
        // The language only switches in Menu._Ready / RefreshLanguage; elsewhere the choice would show a language the screen does not use.
        if (GetTree().CurrentScene is Menu)
        {
            Choice(game, "menu.language", "language", new[] { "en", "nl" },
                new[] { Loc.T("common.language.en"), Loc.T("common.language.nl") }, Loc.Language);
            game.AddChild(Wrapped(Loc.T("settings.language_hint")));
        }
        // Last row of the Game tab so the fixed click positions of the existing autotests do not shift.
        Choice(game, "settings.adaptive_opponent", "adaptive_opponent", new[] { "on", "off" },
            new[] { Loc.T("settings.on"), Loc.T("settings.off") }, "on", ApplyDisplaySettings);
        game.AddChild(Wrapped(Loc.T("settings.adaptive_hint")));
        var display = Tab(tabs, "settings.display");
        Choice(display, "settings.window", "fullscreen", new[] { "off", "on" },
            new[] { Loc.T("settings.windowed"), Loc.T("settings.fullscreen") }, DefaultFullscreen, ApplyDisplaySettings);
        Choice(display, "settings.vsync", "vsync", new[] { "on", "off" },
            new[] { Loc.T("settings.on"), Loc.T("settings.off") }, "on", ApplyDisplaySettings);
        Choice(display, "settings.fps", "fps_limit", new[] { "30", "60", "120", "0" },
            new[] { "30", "60", "120", Loc.T("settings.unlimited") }, "60", ApplyDisplaySettings);
        Choice(display, "settings.animations", "animations", new[] { "on", "off" },
            new[] { Loc.T("settings.on"), Loc.T("settings.off") }, "on");
        display.AddChild(Wrapped(Loc.T("settings.display_hint")));
        var audio = Tab(tabs, "settings.audio");
        Choice(audio, "settings.sound", "audio", new[] { "on", "off" },
            new[] { Loc.T("settings.on"), Loc.T("settings.off") }, "on", AudioManager.ApplyVolumes);
        Volume(audio, "settings.music_volume", "music_volume", 30);
        Volume(audio, "settings.sfx_volume", "sfx_volume", 60);
        Volume(audio, "settings.crowd_volume", "crowd_volume", 50);
        audio.AddChild(Wrapped(Loc.T("settings.audio_hint")));
        var help = Tab(tabs, "settings.help");
        help.AddChild(Wrapped(Loc.T("settings.controls")));
        var screenHelp = UiTheme.Secondary(Loc.T("settings.screen_help"));
        screenHelp.Pressed += () =>
        {
            var guide = GetTree().CurrentScene?.GetChildren().OfType<Welcome>().FirstOrDefault();
            Close(); guide?.ShowHelp();
        };
        help.AddChild(screenHelp);
        var tour = UiTheme.Secondary(Loc.T("tour.restart"));
        tour.Pressed += () =>
        {
            Tour.Reset();
            if (GetTree().CurrentScene is Menu menu) { Close(); menu.StartTour(); }
            else _status.Text = Loc.T("tour.restart_later");
        };
        help.AddChild(tour);
        help.AddChild(Wrapped(Loc.T("settings.feedback_hint")));
        var feedback = UiTheme.Secondary(Loc.T("feedback.button")); feedback.Pressed += () => Feedback.Open(this); help.AddChild(feedback);
        help.AddChild(Wrapped(Loc.T("menu.version", GameInfo.Version)));
        var club = Tab(tabs, "settings.club");          // last tab, so the ZEUS_SETTINGS indices of the other tabs stay the same
        club.AddChild(new ClubPicker());
        tabs.TabChanged += i => { if (tabs.GetTabTitle((int)i) == Loc.T("settings.club")) Mentor.Explain(this, "settings.club"); };
        club.AddChild(Wrapped(Loc.T("settings.club_hint")));
        if (int.TryParse(GameSession.DebugEnvironment("ZEUS_SETTINGS"), out var tab)) tabs.CurrentTab = tab;      // visual check of a tab
        _status = Wrapped(""); content.AddChild(_status);
        _actions = new HBoxContainer(); content.AddChild(_actions);
        AddAction("settings.resume", Close, true);
        if (GetTree().CurrentScene is Main { CanCoach: true }) AddAction("settings.coach", () => { var match = GetTree().CurrentScene as Main; Close(); match?.RequestCoaching(); });
        if (GetTree().CurrentScene is not Menu) AddAction("settings.home", Home);
        AddAction("settings.quit", Quit);
        _confirmation = new VBoxContainer { Visible = false }; content.AddChild(_confirmation);
        _warning = Wrapped(""); _confirmation.AddChild(_warning);
        var confirm = UiTheme.Primary(Loc.T("common.ok")); confirm.Name = "settings_confirm"; confirm.Pressed += () => { var action = _confirmed; Close(); action?.Invoke(); }; _confirmation.AddChild(confirm);
        var cancel = UiTheme.Secondary(Loc.T("common.cancel")); cancel.Name = "settings_cancel"; cancel.Pressed += CancelConfirmation; _confirmation.AddChild(cancel);
    }

    int _frames;
    static readonly string Shot = GameSession.DebugEnvironment("ZEUS_SHOT"), ShotTab = GameSession.DebugEnvironment("ZEUS_SETTINGS");
    // ZEUS_SETTINGS=<tab> (opened by the menu) + ZEUS_SHOT=<prefix>: screenshot of that tab, then quit.
    public override void _Process(double delta)
    {
        if (Shot.Length == 0 || ShotTab.Length == 0 || ++_frames != 12) return;
        GetViewport().GetTexture().GetImage().SavePng(Shot + "_settings.png");
        GetTree().Quit();
    }

    static Label Wrapped(string text)
    {
        var label = UiTheme.Label(text, UiTheme.Small, UiTheme.Graphite);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(10, 0);
        return label;
    }

    static VBoxContainer Tab(TabContainer tabs, string title)
    {
        var tab = new VBoxContainer(); tab.AddThemeConstantOverride("separation", 12); tabs.AddChild(tab);
        tabs.SetTabTitle(tabs.GetTabCount() - 1, Loc.T(title));
        return tab;
    }

    void Choice(VBoxContainer page, string title, string key, string[] values, string[] labels, string fallback, Action? apply = null)
    {
        var row = new HBoxContainer(); page.AddChild(row);
        var label = UiTheme.Label(Loc.T(title), UiTheme.Body); label.CustomMinimumSize = new Vector2(340, 0); row.AddChild(label);
        var pick = new OptionButton { Name = key, FitToLongestItem = false, ClipText = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var text in labels) pick.AddItem(text);
        int selected = Array.IndexOf(values, Loc.Setting(key, fallback)); pick.Select(selected >= 0 ? selected : Array.IndexOf(values, fallback));
        pick.ItemSelected += index =>
        {
            bool saved = Loc.SetSetting(key, values[(int)index]);
            apply?.Invoke();
            _status.Text = Loc.T(saved ? "settings.saved" : "settings.save_failed");
            _status.AddThemeColorOverride("font_color", saved ? UiTheme.Graphite : UiTheme.PaperColor(UiTheme.Bad));
        };
        row.AddChild(pick);
    }

    // Applies on every step, saves when the drag ends or focus leaves (each save rewrites the settings file).
    void Volume(VBoxContainer page, string title, string key, int fallback)
    {
        var row = new HBoxContainer(); page.AddChild(row);
        var label = UiTheme.Label(Loc.T(title), UiTheme.Body); label.CustomMinimumSize = new Vector2(340, 0); row.AddChild(label);
        if (!int.TryParse(Loc.Setting(key, fallback.ToString()), out var value)) value = fallback;
        var slider = new HSlider { Name = key, MinValue = 0, MaxValue = 100, Step = 5, Value = value, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(0, 30) };
        bool dirty = false;
        var shown = UiTheme.Label($"{(int)slider.Value}%", UiTheme.Body); shown.CustomMinimumSize = new Vector2(70, 0);
        slider.ValueChanged += v =>
        {
            shown.Text = $"{(int)v}%";
            Loc.SetSetting(key, ((int)v).ToString(), save: false);
            AudioManager.ApplyVolumes();
            dirty = true;
        };
        void Save()
        {
            if (!dirty) return;
            dirty = false;
            _status.Text = Loc.T(Loc.SetSetting(key, ((int)slider.Value).ToString()) ? "settings.saved" : "settings.save_failed");
        }
        slider.DragEnded += _ => { Save(); if (key == "sfx_volume") AudioManager.Sfx("ui_click"); };
        slider.FocusExited += Save;
        row.AddChild(slider); row.AddChild(shown);
    }

    void AddAction(string text, Action action, bool primary = false)
    {
        var button = primary ? UiTheme.Primary(Loc.T(text)) : UiTheme.Secondary(Loc.T(text));
        button.Name = text.Replace('.', '_');
        button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; button.Pressed += action; _actions.AddChild(button);
    }

    void Confirm(Action action, string message)
    {
        _confirmed = action; _warning.Text = Loc.T(message);
        _body.Visible = _actions.Visible = false; _confirmation.Visible = true;
    }

    void CancelConfirmation()
    {
        _confirmed = null; _confirmation.Visible = false; _body.Visible = _actions.Visible = true;
    }

    void Home()
    {
        var scene = GetTree().CurrentScene;
        if (scene is Editor editor) { Close(); editor.RequestHome(); return; }
        if (scene is Main || GameSession.Challenge is not null)
        { Confirm(() => GetTree().ChangeSceneToFile("res://Menu.tscn"), "settings.leave_warning"); return; }
        Close();
        if (scene is SquadScreen squad) squad.RequestHome();
        else if (scene is not Menu) GetTree().ChangeSceneToFile("res://Menu.tscn");
    }

    void Quit()
    {
        if (GetTree().CurrentScene is Editor editor) { Close(); editor.RequestQuit(); return; }
        Confirm(() => GetTree().Quit(), "settings.quit_warning");
    }

    void Close()
    {
        if (_closed) return;
        _closed = true; _current = null;
        GetTree().Paused = _wasPaused;
        var language = Loc.Setting("language", Loc.Language);
        if (GetTree().CurrentScene is Menu menu && language is "en" or "nl" && language != Loc.Language)
            menu.RefreshLanguage();
        QueueFree();
    }

    public override void _ExitTree()
    {
        if (!_closed) GetTree().Paused = _wasPaused;
        if (_current == this) _current = null;
    }
}
