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
    Action _startTour = null!;
    bool _club;      // normal home: club card instead of the kick-off panel (HomeCard.Wanted)

    public override void _Ready()
    {
        var language = Loc.Setting("language", Loc.Language);
        if (language is "en" or "nl" && language != Loc.Language) Loc.SetLanguage(language);
        _club = HomeCard.Wanted;
        UiTheme.Apply(this);
        UiFrame.Center(this);          // absolute 1600x900 layout, centred; the backdrop fills the rest of the screen
        GameSession.InCompetition = false;      // a normal match from the menu is never a competition match
        GameSession.PlayerSide = Zeus.Sim.Side.Home;
        if (GameSession.Challenge is { Tutorial: true }) { GameSession.Challenge = null; Mentor.Dismiss(); }      // a practice match left early with Esc
        GameSession.RestoreMenuChoices();       // Season/Challenges borrowed the match fields: show the menu's own choices again
        GameSession.LastSavedTactic = null;
        _backdrop = new MenuBackdrop { MouseFilter = MouseFilterEnum.Ignore, Title = GameInfo.Title };
        _backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_backdrop);
        var beta = UiTheme.Label(Loc.T("menu.beta", GameInfo.Version), UiTheme.Small, UiTheme.Muted);
        beta.AddThemeColorOverride("font_color", UiTheme.Muted);
        beta.Position = new Vector2(142, 262); AddChild(beta);

        var welcome = Welcome.Install(this, "menu");

        // Left: secondary actions as big menu items.
        var items = new GridContainer { Columns = 2, Position = new Vector2(110, 296), CustomMinimumSize = new Vector2(710, 0) };
        items.AddThemeConstantOverride("v_separation", 0);
        items.AddThemeConstantOverride("h_separation", 24);
        AddChild(items);
        var season = UiTheme.MenuItem(Loc.T("menu.season")); season.Pressed += OpenSeason; items.AddChild(season);
        var challenges = UiTheme.MenuItem(Loc.T("menu.challenges")); challenges.Pressed += OpenChallenges; items.AddChild(challenges);
        var squad = UiTheme.MenuItem(Loc.T("menu.squad")); squad.Pressed += OpenSquad; items.AddChild(squad);
        var edit = UiTheme.MenuItem(Loc.T("menu.editor")); edit.Pressed += Edit; items.AddChild(edit);
        var meter = UiTheme.MenuItem(Loc.T("menu.meter")); meter.Pressed += OpenMeter; items.AddChild(meter);
        var help = UiTheme.MenuItem(Loc.T("menu.help")); help.Pressed += () => welcome.ShowWelcome(); items.AddChild(help);
        var settings = UiTheme.MenuItem(Loc.T("settings.title")); settings.Pressed += () => SettingsMenu.Open(GetNode("/root/Hotkeys")); items.AddChild(settings);
        var about = UiTheme.MenuItem(Loc.T("menu.about")); about.Pressed += () => AboutMenu.Open(GetNode("/root/Hotkeys")); items.AddChild(about);
        foreach (var child in items.GetChildren())
            if (child is Button button) { button.CustomMinimumSize = new Vector2(330, 52); button.AddThemeFontSizeOverride("font_size", 28); }
        // Same flag as the selection screen: the menu match uses the saved familiarity only when this is on, so make it visible and switchable here.
        var familiarity = new CheckBox { Text = Loc.T("squad.use_familiarity"), ButtonPressed = GameSession.UseFamiliarity, Position = new Vector2(132, 512) };
        familiarity.AddThemeFontSizeOverride("font_size", 16);
        // Same light text as the menu items above (the default CheckBox ink is dark brown on the dark backdrop).
        foreach (var color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
            familiarity.AddThemeColorOverride(color, new Color(UiTheme.Text, 0.85f));
        familiarity.Toggled += on => GameSession.UseFamiliarity = on;
        familiarity.Visible = !_club;      // it only steers the friendly match
        AddChild(familiarity);
        // Library sits under the item grid, beside the familiarity switch: no ninth item to unbalance the two columns.
        var library = new Button { Name = "menu_library", Text = Loc.T("library.button"), ThemeTypeVariation = "CompactButton", Position = new Vector2(616, 508), TooltipText = Loc.T("library.title") };
        library.Pressed += OpenLibrary; AddChild(library);
        var friends = new Button { Name = "menu_friends", Text = Loc.T("menu.friends"), ThemeTypeVariation = "CompactButton", Position = new Vector2(616, 536), TooltipText = Loc.T("friends.title") };
        friends.Pressed += () => { Remember(); Friends.Open(GetNode("/root/Hotkeys"), Reload); }; AddChild(friends);
        Tutorial.Lock(season, "league"); Tutorial.Lock(challenges, "challenges"); Tutorial.Lock(squad, "squad"); Tutorial.Lock(edit, "editor"); Tutorial.Lock(meter, "meter");
        Tutorial.Lock(library, "library"); Tutorial.Lock(friends, "friends");
        var checklist = Tutorial.Explaining ? null : BuildChecklist();      // the checklist leaves the home screen with the tutorial
        if (_club) AddButtonTo("menu_friendly", Loc.T("menu.friendly"), Loc.T("friendly.tip"), new Vector2(616, 480), () => { Remember(); GameSession.FriendlyMode = true; GetTree().ReloadCurrentScene(); });
        else if (GameSession.FriendlyMode) AddButtonTo("menu_back_club", Loc.T("menu.back_club"), "", new Vector2(616, 480), () => { Remember(); GameSession.FriendlyMode = false; GetTree().ReloadCurrentScene(); });
        var hint = UiTheme.Label(Loc.T(GameSession.DebugHints ? "menu.hint_debug" : "menu.hint"), UiTheme.Small, UiTheme.Muted);
        hint.AddThemeColorOverride("font_color", UiTheme.Muted);
        hint.Position = new Vector2(116, 862);
        AddChild(hint);
        // Version label sits under the title (menu.beta); the hash is only in About and VERSIE.txt.

        // Right: kick-off setup (glass panel).
        var panel = new PanelContainer { Position = new Vector2(880, 40), CustomMinimumSize = new Vector2(640, 810) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = UiTheme.Paper, BorderColor = UiTheme.PaperShade,
            BorderWidthTop = 3, ContentMarginLeft = 28, ContentMarginRight = 28, ContentMarginTop = 16, ContentMarginBottom = 18,
        });
        AddChild(panel);
        UiTheme.ApplyPaper(panel);
        panel.Resized += () => { if (!_club) _backdrop.Exclude = new Rect2(panel.Position, panel.Size).Grow(26); };      // the panel sizes itself to its content (it must stay inside 1600x900)
        if (_club)
        {
            panel.Visible = false;      // the controls stay (Remember and the other screens read them); the club card takes the place
            var card = HomeCard.Build(PlayLeagueMatch); AddChild(card);
            card.Resized += () => _backdrop.Exclude = new Rect2(card.Position, card.Size).Grow(26);
        }
        var setup = new VBoxContainer(); setup.AddThemeConstantOverride("separation", 12); panel.AddChild(setup);
        setup.AddChild(UiTheme.Label(Loc.T("menu.setup"), 34, UiTheme.Ink, display: true));
        var cols = new HBoxContainer(); cols.AddThemeConstantOverride("separation", 28); setup.AddChild(cols);
        (_home, _homeStats, _homeLevel) = Column(cols, Loc.T("menu.home"), UiTheme.HomeInk, GameSession.HomeLevel);
        (_away, _awayStats, _awayLevel) = Column(cols, Loc.T("menu.away"), UiTheme.AwayInk, GameSession.AwayLevel);
        _home.ItemSelected += _ => Refresh();
        _away.ItemSelected += _ => Refresh();

        setup.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        var seedRow = new HBoxContainer(); setup.AddChild(seedRow);
        seedRow.AddChild(UiTheme.Label(Loc.T("menu.seed"), 22, UiTheme.Graphite, display: true));
        _seed = new SpinBox { MinValue = 1, MaxValue = 999999, Value = GameSession.Seed, CustomMinimumSize = new Vector2(150, 0) };
        seedRow.AddChild(_seed);
        var dice = UiTheme.Secondary(Loc.T("menu.random")); dice.Pressed += () => _seed.Value = GD.RandRange(1, 999999); seedRow.AddChild(dice);
        var seedHint = UiTheme.Label(Loc.T("menu.seed_hint"), UiTheme.Small, UiTheme.Graphite);
        seedHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        seedHint.CustomMinimumSize = new Vector2(10, 0);
        setup.AddChild(seedHint);
        _message = UiTheme.Label("", UiTheme.Body, UiTheme.Bad); _message.Visible = false; _message.AutowrapMode = TextServer.AutowrapMode.WordSmart; _message.CustomMinimumSize = new Vector2(10, 0); setup.AddChild(_message);
        if (Tutorial.Scenario() is { } practice)
        {
            var training = UiTheme.Primary(Loc.T("tutorial.training", practice.Name)); training.Name = "menu_training"; training.CustomMinimumSize = new Vector2(0, 56); seedHint.Visible = false; training.Pressed += () => Tutorial.StartScenario(this); setup.AddChild(training);      // the practice button takes the room of the seed hint
        }
        var start = UiTheme.Primary(Loc.T("menu.start")); start.CustomMinimumSize = new Vector2(0, 72); start.Pressed += Start; setup.AddChild(start);
        if (!_club) start.GrabFocus();
        Reload();
        _startTour = () => { if (!_club) Tour.Start(this, "menu", ("menu.intro", null), ("menu.tactics", panel), ("menu.editor", edit),
            ("menu.start", start), ("menu.meter", meter), ("menu.steps", checklist)); };
        void Onboarding()
        {
            if (FirstStart.Needed(DisplayServer.GetName() != "headless")) Callable.From(() => FirstStart.Open(this, () => Tutorial.Intro(this))).CallDeferred();      // the intro cards replace the old tour and welcome
            else if (Tutorial.Active) Callable.From(() => Tutorial.OnHome(this)).CallDeferred();
            else if (GameSession.FriendlyMode && Tutorial.Explaining) Callable.From(() => Mentor.Explain(this, "friendly")).CallDeferred();
            else if (_club) Callable.From(() => Mentor.Explain(this, "home")).CallDeferred();      // no tour or welcome card on the club home       // the tutorial replaces the old tour and welcome
            else if (GameSession.DebugEnvironment("ZEUS_WELCOME") == "1") Callable.From(welcome.ShowWelcome).CallDeferred();
            else if (Tour.ShouldShow("menu")) Callable.From(_startTour).CallDeferred();       // deferred: the spotlight needs the laid-out controls
            else if (!OnboardingProgress.State.WelcomeSeen && GameSession.DebugEnvironment("ZEUS_SHOT").Length == 0)
                Callable.From(welcome.ShowWelcome).CallDeferred();
        }
        if (Cover.ShouldShow) Cover.Install(this, Onboarding);      // first start of the session: tour/welcome wait until the notebook is open
        else Onboarding();
    }

    /// <summary>Settings > "Restart tour" on the home screen.</summary>
    public void StartTour() => _startTour();

    PanelContainer BuildChecklist()
    {
        var panel = new PanelContainer { Position = new Vector2(110, 572), CustomMinimumSize = new Vector2(710, 0) };
        AddChild(panel);
        UiTheme.ApplyPaper(panel);
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 3); panel.AddChild(box);
        box.AddChild(UiTheme.Label(Loc.T("onboarding.title"), 26, UiTheme.Ink, display: true));
        box.AddChild(UiTheme.Label(Loc.T("onboarding.count", OnboardingProgress.State.CompletedCount, 5), UiTheme.Small, UiTheme.Graphite));
        var actions = new System.Action[] { OpenChallenges, Start, Edit, OpenMeter, OpenSeason };
        for (int i = 0; i < OnboardingState.Steps.Count; i++)
        {
            var step = OnboardingState.Steps[i];
            var row = new HBoxContainer(); box.AddChild(row);
            row.AddChild(UiTheme.Label(Loc.T(OnboardingProgress.State.IsDone(step) ? "onboarding.done" : "onboarding.open"), 18, OnboardingProgress.State.IsDone(step) ? new Color("3E6B3A") : UiTheme.Graphite));
            var label = UiTheme.Label(Loc.T("onboarding.step." + step), UiTheme.Small, UiTheme.Ink);
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(label);
            var go = new Button { Text = Loc.T("onboarding.go"), ThemeTypeVariation = "CompactButton" };
            go.Pressed += actions[i]; row.AddChild(go);
        }
        return panel;
    }

    void Say(string text) { _message.Text = text; _message.Visible = text.Length > 0; }

    static (OptionButton, FormationPreview, OptionButton) Column(Container parent, string title, Color color, string level)
    {
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(270, 0) }; box.AddThemeConstantOverride("separation", 12);
        parent.AddChild(box);
        box.AddChild(UiTheme.Label(title, 30, color, display: true));
        box.AddChild(new ColorRect { Color = color, CustomMinimumSize = new Vector2(0, 4) });
        var pick = new OptionButton { FitToLongestItem = false, ClipText = true, CustomMinimumSize = new Vector2(0, 46), Alignment = HorizontalAlignment.Left };
        box.AddChild(pick);
        var row = new HBoxContainer(); box.AddChild(row);
        row.AddChild(UiTheme.Label(Loc.T("menu.level"), 18, UiTheme.Graphite, display: true));
        var levels = new OptionButton { FitToLongestItem = false, CustomMinimumSize = new Vector2(0, 38), SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = HorizontalAlignment.Left };
        foreach (var l in SquadFactory.Levels) levels.AddItem(Loc.T("common.level." + l));
        levels.Select(Math.Max(0, Array.IndexOf(SquadFactory.Levels.ToArray(), level)));
        row.AddChild(levels);
        var stats = new FormationPreview(); box.AddChild(stats);
        return (pick, stats, levels);
    }

    static readonly string Shot = GameSession.DebugEnvironment("ZEUS_SHOT");
    // ZEUS_SHOT=<prefix> saves a screenshot of this screen after a few frames and quits (visual check).
    public override void _Process(double delta)
    {
        if (_frames == 3 && GameSession.DebugEnvironment("ZEUS_LIBRARY").Length > 0) OpenLibrary();      // visual check of the tactic library
        if (_frames == 3 && GameSession.DebugEnvironment("ZEUS_ABOUT").Length > 0) AboutMenu.Open(GetNode("/root/Hotkeys"));      // visual check of the About screen
        if (_frames == 3 && GameSession.DebugEnvironment("ZEUS_SETTINGS").Length > 0) SettingsMenu.Open(GetNode("/root/Hotkeys"));      // visual check of the Settings screen
        if (Shot.Length == 0 || Cover.IsOpen || ++_frames != 20) return;      // ZEUS_COVER=1: the cover saves its own frames first
        GetViewport().GetTexture().GetImage().SavePng(Shot + "_menu.png");
        GetTree().Quit();
    }

    void Reload()
    {
        var files = GameSession.Tactics().Select(t => t.Path).ToList();
        _home.Clear(); _away.Clear();
        ChallengeProgress.AddTacticChoices(_home, GameSession.HomePath ?? GameSession.DefaultPath(GameSession.DefaultHomeId), ChallengeProgress.TotalStars());      // your own team: tactics locked by challenge stars are shown but disabled
        foreach (var f in files) { _away.AddItem(Pretty(f)); _away.SetItemMetadata(_away.ItemCount - 1, f); }
        Select(_away, GameSession.AwayPath ?? GameSession.DefaultPath(GameSession.DefaultAwayId), Math.Min(1, files.Count - 1));
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
        _homeStats.Show(FullPath(_home), UiTheme.HomeInk);
        _awayStats.Show(FullPath(_away), UiTheme.AwayInk);
        _backdrop.SetTeams(FullPath(_home), FullPath(_away));
    }

    static string FullPath(OptionButton b) => (string)b.GetItemMetadata(b.Selected);

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Enter or Key.KpEnter }) { if (_club) PlayLeagueMatch(); else Start(); }
    }

    public void Remember()
    {
        if (_home.ItemCount == 0 || _away.ItemCount == 0) return;
        GameSession.HomePath = FullPath(_home);
        GameSession.AwayPath = FullPath(_away);
        GameSession.Seed = (ulong)_seed.Value;
        GameSession.HomeLevel = SquadFactory.Levels[_homeLevel.Selected];
        GameSession.AwayLevel = SquadFactory.Levels[_awayLevel.Selected];
    }

    public void RefreshLanguage()
    {
        Remember();
        GetTree().ReloadCurrentScene();
    }

    void AddButtonTo(string name, string text, string tip, Vector2 position, Action pressed)
    {
        var b = new Button { Name = name, Text = text, ThemeTypeVariation = "CompactButton", Position = position, TooltipText = tip };
        b.Pressed += pressed; AddChild(b);
    }

    /// <summary>"Play match" on the club card: Season starts the next league match at once (it handles cups, pending results and the intro itself).</summary>
    void PlayLeagueMatch()
    {
        if (_home.ItemCount > 0) Remember();
        GameSession.AutoPlayLive = true;
        GetTree().ChangeSceneToFile("res://Season.tscn");
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

    void OpenLibrary()
    {
        if (_home.ItemCount > 0) Remember();
        TacticLibrary.Open(GetNode("/root/Hotkeys"), home =>
        {
            if (!IsInstanceValid(this) || !IsInsideTree()) return;
            if (home is not null) GameSession.HomePath = home;      // "use as my tactic"
            Reload();
        });
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
