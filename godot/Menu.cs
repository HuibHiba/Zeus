// Home screen: animated pitch backdrop with the chosen formations, big menu items on the left, kick-off setup on the right.
using System;
using System.IO;
using System.Linq;
using Godot;
using Zeus.Sim;

public partial class Menu : Control
{
    OptionButton _home = null!, _away = null!, _homeLevel = null!, _awayLevel = null!;
    FormationPreview _homeStats = null!, _awayStats = null!;
    MenuBackdrop _backdrop = null!;
    SpinBox _seed = null!;
    Label _message = null!;
    int _frames;

    public override void _Ready()
    {
        UiTheme.Apply(this);
        SetAnchorsPreset(LayoutPreset.FullRect);
        GameSession.InCompetition = false;      // a normal match from the menu is never a competition match
        GameSession.PlayerSide = Zeus.Sim.Side.Home;
        GameSession.RestoreMenuChoices();       // Season/Challenges borrowed the match fields: show the menu's own choices again
        GameSession.LastSavedTactic = null;
        _backdrop = new MenuBackdrop { MouseFilter = MouseFilterEnum.Ignore, Title = GameInfo.Title };
        _backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_backdrop);

        // Left: secondary actions as big menu items.
        var items = new VBoxContainer { Position = new Vector2(70, 264), CustomMinimumSize = new Vector2(430, 0) };
        items.AddThemeConstantOverride("separation", 4);
        AddChild(items);
        var season = UiTheme.MenuItem(Loc.T("menu.season")); season.Pressed += OpenSeason; items.AddChild(season);
        var challenges = UiTheme.MenuItem(Loc.T("menu.challenges")); challenges.Pressed += OpenChallenges; items.AddChild(challenges);
        var squad = UiTheme.MenuItem(Loc.T("menu.squad")); squad.Pressed += OpenSquad; items.AddChild(squad);
        var edit = UiTheme.MenuItem(Loc.T("menu.editor")); edit.Pressed += Edit; items.AddChild(edit);
        var meter = UiTheme.MenuItem(Loc.T("menu.meter")); meter.Pressed += OpenMeter; items.AddChild(meter);
        var quit = UiTheme.MenuItem(Loc.T("menu.quit")); quit.Pressed += () => GetTree().Quit(); items.AddChild(quit);
        var hint = UiTheme.Label(Loc.T(GameSession.DebugEnabled ? "menu.hint_debug" : "menu.hint"), UiTheme.Small, UiTheme.Muted);
        hint.Position = new Vector2(76, 744);
        AddChild(hint);

        // Right: kick-off setup (glass panel).
        var panel = new PanelContainer { Position = new Vector2(850, 30), CustomMinimumSize = new Vector2(620, 0) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(UiTheme.Panel, 0.86f), BorderColor = new Color(UiTheme.Accent, 0.55f),
            BorderWidthTop = 3, ContentMarginLeft = 28, ContentMarginRight = 28, ContentMarginTop = 16, ContentMarginBottom = 18,
        });
        AddChild(panel);
        panel.Resized += () => _backdrop.Exclude = new Rect2(panel.Position, panel.Size).Grow(26);      // the panel sizes itself to its content (it must stay inside 1520x780)
        var setup = new VBoxContainer(); setup.AddThemeConstantOverride("separation", 8); panel.AddChild(setup);
        setup.AddChild(UiTheme.Label(Loc.T("menu.setup"), 34, UiTheme.Text, display: true));
        var cols = new HBoxContainer(); cols.AddThemeConstantOverride("separation", 28); setup.AddChild(cols);
        (_home, _homeStats, _homeLevel) = Column(cols, Loc.T("menu.home"), UiTheme.Home, GameSession.HomeLevel);
        (_away, _awayStats, _awayLevel) = Column(cols, Loc.T("menu.away"), UiTheme.Away, GameSession.AwayLevel);
        _home.ItemSelected += _ => Refresh();
        _away.ItemSelected += _ => Refresh();

        setup.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        var substitutions = new CheckBox { Text = Loc.T("menu.substitutions"), ButtonPressed = GameSession.OpponentSubstitutions };
        substitutions.Toggled += on => GameSession.OpponentSubstitutions = on;
        setup.AddChild(substitutions);
        var seedRow = new HBoxContainer(); setup.AddChild(seedRow);
        seedRow.AddChild(UiTheme.Label(Loc.T("menu.seed"), 22, UiTheme.Muted, display: true));
        _seed = new SpinBox { MinValue = 1, MaxValue = 999999, Value = GameSession.Seed, CustomMinimumSize = new Vector2(150, 0) };
        seedRow.AddChild(_seed);
        var dice = UiTheme.Secondary(Loc.T("menu.random")); dice.Pressed += () => _seed.Value = GD.RandRange(1, 999999); seedRow.AddChild(dice);
        var seedHint = UiTheme.Label(Loc.T("menu.seed_hint"), UiTheme.Small, UiTheme.Muted);
        seedHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        seedHint.CustomMinimumSize = new Vector2(10, 0);
        setup.AddChild(seedHint);
        _message = UiTheme.Label("", UiTheme.Body, UiTheme.Bad); _message.Visible = false; _message.AutowrapMode = TextServer.AutowrapMode.WordSmart; _message.CustomMinimumSize = new Vector2(10, 0); setup.AddChild(_message);
        var start = UiTheme.Primary(Loc.T("menu.start")); start.CustomMinimumSize = new Vector2(0, 62); start.Pressed += Start; setup.AddChild(start);
        start.GrabFocus();
        var languageRow = new HBoxContainer { Position = new Vector2(76, 690) };
        languageRow.AddChild(UiTheme.Label(Loc.T("menu.language"), UiTheme.Small, UiTheme.Muted));
        var language = new OptionButton();
        language.AddItem("English"); language.AddItem("Nederlands");
        language.Select(Loc.Language == "nl" ? 1 : 0);
        language.ItemSelected += i =>
        {
            if (_home.ItemCount > 0) Remember();
            if (!Loc.SetLanguage(i == 1 ? "nl" : "en")) GameSession.LastError = Loc.T("menu.language_save_error");
            GetTree().ReloadCurrentScene();
        };
        languageRow.AddChild(language); AddChild(languageRow);
        Reload();
    }

    void Say(string text) { _message.Text = text; _message.Visible = text.Length > 0; }

    static (OptionButton, FormationPreview, OptionButton) Column(Container parent, string title, Color color, string level)
    {
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(270, 0) }; box.AddThemeConstantOverride("separation", 8);
        parent.AddChild(box);
        box.AddChild(UiTheme.Label(title, 30, color, display: true));
        box.AddChild(new ColorRect { Color = color, CustomMinimumSize = new Vector2(0, 4) });
        var pick = new OptionButton { CustomMinimumSize = new Vector2(0, 46), Alignment = HorizontalAlignment.Left };
        box.AddChild(pick);
        var row = new HBoxContainer(); box.AddChild(row);
        row.AddChild(UiTheme.Label(Loc.T("menu.level"), 18, UiTheme.Muted, display: true));
        var levels = new OptionButton { CustomMinimumSize = new Vector2(0, 38), SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = HorizontalAlignment.Left };
        foreach (var l in SquadFactory.Levels) levels.AddItem(Loc.T("level." + l));
        levels.Select(Math.Max(0, Array.IndexOf(SquadFactory.Levels.ToArray(), level)));
        row.AddChild(levels);
        var stats = new FormationPreview(); box.AddChild(stats);
        return (pick, stats, levels);
    }

    // ZEUS_SHOT=<prefix> saves a screenshot of this screen after a few frames and quits (visual check).
    public override void _Process(double delta)
    {
        var shot = GameSession.DebugEnvironment("ZEUS_SHOT");
        if (shot.Length == 0 || ++_frames != 20) return;
        GetViewport().GetTexture().GetImage().SavePng(shot + "_menu.png");
        GetTree().Quit();
    }

    void Reload()
    {
        var files = GameSession.Tactics().Select(t => t.Path).ToList();
        _home.Clear(); _away.Clear();
        foreach (var f in files)
        {
            _home.AddItem(Pretty(f)); _home.SetItemMetadata(_home.ItemCount - 1, f);
            _away.AddItem(Pretty(f)); _away.SetItemMetadata(_away.ItemCount - 1, f);
        }
        Select(_home, GameSession.HomePath, 0);
        Select(_away, GameSession.AwayPath, Math.Min(1, files.Count - 1));
        if (GameSession.LastError is { } err) { Say(err); GameSession.LastError = null; }
        else if (files.Count == 0) Say(Loc.T("menu.no_tactics"));
        Refresh();
    }

    static string Pretty(string file) => GameSession.TacticLabel(file);

    static void Select(OptionButton b, string? path, int fallback)
    {
        if (b.ItemCount == 0) return;
        int index = fallback;
        if (path is not null)
            for (int i = 0; i < b.ItemCount; i++)
                if ((string)b.GetItemMetadata(i) == path) index = i;
        b.Select(Math.Max(0, index));
    }

    void Refresh()
    {
        if (_home.ItemCount == 0) return;
        _homeStats.Show(FullPath(_home), UiTheme.Home);
        _awayStats.Show(FullPath(_away), UiTheme.Away);
        _backdrop.SetTeams(FullPath(_home), FullPath(_away));
    }

    static string FullPath(OptionButton b) => (string)b.GetItemMetadata(b.Selected);

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Enter or Key.KpEnter }) Start();
    }

    void Remember()
    {
        GameSession.HomePath = FullPath(_home);
        GameSession.AwayPath = FullPath(_away);
        GameSession.Seed = (ulong)_seed.Value;
        GameSession.HomeLevel = SquadFactory.Levels[_homeLevel.Selected];
        GameSession.AwayLevel = SquadFactory.Levels[_awayLevel.Selected];
    }

    void Start()
    {
        if (_home.ItemCount == 0) { Say(Loc.T("menu.no_tactics")); return; }
        Remember();
        GetTree().ChangeSceneToFile("res://Main.tscn");
    }

    void Edit()
    {
        if (_home.ItemCount == 0) return;
        Remember();
        GameSession.EditPath = FullPath(_home);
        GetTree().ChangeSceneToFile("res://Editor.tscn");
    }

    void OpenSeason()
    {
        if (_home.ItemCount > 0) Remember();
        GetTree().ChangeSceneToFile("res://Season.tscn");
    }

    void OpenSquad()
    {
        if (_home.ItemCount > 0) Remember();                 // the chosen home tactic and level are the defaults in the selection screen
        GetTree().ChangeSceneToFile("res://Squad.tscn");
    }

    void OpenChallenges()
    {
        if (_home.ItemCount > 0) Remember();                 // the chosen home tactic becomes the default in the challenge screen
        GetTree().ChangeSceneToFile("res://Challenges.tscn");
    }

    void OpenMeter()
    {
        if (_home.ItemCount == 0) { Say(Loc.T("menu.no_tactics")); return; }
        Remember();
        GetTree().ChangeSceneToFile("res://Meter.tscn");
    }
}
