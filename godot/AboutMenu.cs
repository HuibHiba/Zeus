using Godot;

/// <summary>About and credits overlay. Same pause handling and Esc route as SettingsMenu.</summary>
public partial class AboutMenu : CanvasLayer
{
    static AboutMenu? _current;
    bool _wasPaused, _closed;

    public static bool IsOpen => _current is not null && GodotObject.IsInstanceValid(_current) && !_current._closed;

    public static void Open(Node owner)
    {
        if (IsOpen || SettingsMenu.IsOpen || Feedback.IsOpen) return;
        _current = new AboutMenu { Name = "AboutMenu", Layer = 70, ProcessMode = ProcessModeEnum.Always };
        owner.AddChild(_current);
    }

    public static void Escape() { if (IsOpen) _current!.Close(); }

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
        content.AddChild(UiTheme.Label(Loc.T("about.title"), 38, UiTheme.Ink, true));
        var scroll = new ScrollContainer { Name = "about_scroll", CustomMinimumSize = new Vector2(0, 640), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        content.AddChild(scroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; body.AddThemeConstantOverride("separation", 12); scroll.AddChild(body);
        body.AddChild(UiTheme.Label(GameInfo.Title, 30, UiTheme.Ink, true));
        body.AddChild(Wrapped(Loc.T("menu.version", GameInfo.FullVersion)));
        foreach (var key in new[] { "maker", "engine", "fonts", "data", "wyscout", "music", "disclaimer", "provisional", "privacy", "licenses" })
        {
            body.AddChild(UiTheme.Label(Loc.T($"about.{key}.head"), UiTheme.Body, UiTheme.Ink));
            body.AddChild(Wrapped(Loc.T($"about.{key}")));
        }
        if (GameInfo.DiscordUrl.Length > 0)
        {
            body.AddChild(UiTheme.Label(Loc.T("about.community.head"), UiTheme.Body, UiTheme.Ink));
            body.AddChild(Wrapped(Loc.T("about.community")));
            var discord = UiTheme.Primary(Loc.T("about.discord")); discord.Name = "about_discord";
            discord.Pressed += () => OS.ShellOpen(GameInfo.DiscordUrl);
            body.AddChild(discord);
        }
        var back = UiTheme.Primary(Loc.T("about.back")); back.Name = "about_back"; back.Pressed += Close; content.AddChild(back);
        back.GrabFocus();      // keyboard: Enter/Space closes, the menu behind is paused
    }

    static Label Wrapped(string text)
    {
        var label = UiTheme.Label(text, UiTheme.Small, UiTheme.Graphite);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.CustomMinimumSize = new Vector2(10, 0);
        return label;
    }

    int _frames;
    static readonly string Shot = GameSession.DebugEnvironment("ZEUS_SHOT");
    // ZEUS_SHOT=<prefix> saves a screenshot after a few frames and quits (visual check; the menu itself is paused behind this overlay).
    public override void _Process(double delta)
    {
        if (Shot.Length == 0 || ++_frames != 12) return;
        GetViewport().GetTexture().GetImage().SavePng(Shot + "_about.png");
        GetTree().Quit();
    }

    void Close()
    {
        if (_closed) return;
        _closed = true; _current = null;
        GetTree().Paused = _wasPaused;
        QueueFree();
    }

    public override void _ExitTree()
    {
        if (!_closed) GetTree().Paused = _wasPaused;
        if (_current == this) _current = null;
    }
}
