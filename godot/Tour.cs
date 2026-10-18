// First-run tour: a dimmed overlay with a spotlight on one control and a short card (next / skip).
// Stages: "menu" (home screen, also replaces the first-run welcome), "match" (first match: coaching with B), "end" (first end card).
// Finished stages are a comma list in instellingen.json ("tour"); with ZEUS_NOSAVE they stay in memory only.
using System;
using System.Linq;
using Godot;

public partial class Tour : CanvasLayer
{
    static readonly string[] Stages = { "menu", "match", "end" };
    const float CardWidth = 500, Gap = 16, Margin = 10;
    static string? _seen;
    static Tour? _current;
    string _stage = "";
    (string Key, Control? Target)[] _steps = Array.Empty<(string, Control?)>();
    int _index, _frames;
    bool _wasPaused, _closed;
    readonly ColorRect[] _shade = new ColorRect[4];
    Panel _ring = null!;
    PanelContainer _card = null!;
    Label _count = null!, _title = null!, _text = null!;
    Button _next = null!;

    public static bool IsOpen => _current is not null && IsInstanceValid(_current) && !_current._closed;
    static string Seen => _seen ??= Loc.Setting("tour", "");
    // ZEUS_TOUR=<stage>[:<step>] (debug only) shows a stage even when seen, from that step; with ZEUS_SHOT it saves <prefix>_tour_<stage><step>.png and quits.
    static readonly string[] Forced = GameSession.DebugEnvironment("ZEUS_TOUR").Split(':');
    static readonly string Shot = GameSession.DebugEnvironment("ZEUS_SHOT");

    /// <summary>True the first time a stage comes up. Screenshot runs (ZEUS_SHOT) never get the tour unless ZEUS_TOUR asks for it.</summary>
    public static bool ShouldShow(string stage) => !IsOpen && (Forced[0] == stage ||
        (!Seen.Split(',').Contains(stage) && Shot.Length == 0 && !Loc.SaveBlocked));

    public static void Start(Node owner, string stage, params (string Key, Control? Target)[] steps)
    {
        if (!IsInstanceValid(owner) || IsOpen || steps.Length == 0) return;
        _current = new Tour { Name = "Tour", _stage = stage, _steps = steps, Layer = 50, ProcessMode = ProcessModeEnum.Always };
        if (Forced[0] == stage && Forced.Length > 1 && int.TryParse(Forced[1], out var step)) _current._index = Math.Clamp(step - 1, 0, steps.Length - 1);
        owner.AddChild(_current);
    }

    /// <summary>"Restart tour" in Settings: every stage shows again.</summary>
    public static void Reset() => Save("");

    /// <summary>Esc (Hotkeys) skips the whole tour.</summary>
    public static void Escape() { if (IsOpen) _current!.Finish(); }

    static void Save(string value)
    {
        _seen = value;
        if (!Loc.SetSetting("tour", value)) GD.PushWarning("tour: settings not saved");
    }

    public override void _Ready()
    {
        _wasPaused = GetTree().Paused;
        GetTree().Paused = true;            // the match and the menu backdrop wait; Enter/B never reach the screen behind
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect); AddChild(root);
        for (int i = 0; i < _shade.Length; i++)
        {
            _shade[i] = new ColorRect { Color = new Color(0, 0, 0, .62f), MouseFilter = Control.MouseFilterEnum.Ignore };
            root.AddChild(_shade[i]);
        }
        _ring = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore }; root.AddChild(_ring);
        var ring = new StyleBoxFlat { DrawCenter = false, BorderColor = UiTheme.Accent };
        ring.SetBorderWidthAll(3); ring.SetCornerRadiusAll(6);
        _ring.AddThemeStyleboxOverride("panel", ring);
        _card = new PanelContainer { CustomMinimumSize = new Vector2(CardWidth, 0) }; root.AddChild(_card);
        UiTheme.ApplyPaper(_card);
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 8); _card.AddChild(box);
        _count = UiTheme.Label("", UiTheme.Small, UiTheme.Graphite); box.AddChild(_count);
        _title = UiTheme.Label("", 30, UiTheme.Ink, display: true); box.AddChild(_title);
        _text = UiTheme.Label("", UiTheme.Body, UiTheme.Ink);
        _text.AutowrapMode = TextServer.AutowrapMode.WordSmart; _text.CustomMinimumSize = new Vector2(CardWidth - 60, 0); box.AddChild(_text);      // wrap width known before the first layout
        var buttons = new HBoxContainer(); buttons.AddThemeConstantOverride("separation", 12); box.AddChild(buttons);
        var skip = UiTheme.Secondary(Loc.T("tour.skip")); skip.TooltipText = Loc.T("tour.skip_hint"); skip.Pressed += Finish; buttons.AddChild(skip);
        buttons.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _next = UiTheme.Primary(""); _next.CustomMinimumSize = new Vector2(180, 50); _next.Pressed += Advance; buttons.AddChild(_next);
        ShowStep();
    }

    void ShowStep()
    {
        var key = _steps[_index].Key;
        _count.Text = Loc.T("tour.step", _index + 1, _steps.Length);
        _count.Visible = _steps.Length > 1;
        _title.Text = Loc.T("tour." + key + ".title");
        _text.Text = Loc.T("tour." + key + ".text");
        _next.Text = Loc.T(_index == _steps.Length - 1 ? "tour.done" : "tour.next");
        _card.Modulate = new Color(1, 1, 1, 0);
        _card.CreateTween().TweenProperty(_card, "modulate:a", 1f, 0.2);
        _next.GrabFocus();
        _frames = 0;
        Place();
    }

    void Advance()
    {
        if (_index == _steps.Length - 1) { Finish(); return; }
        _index++;
        ShowStep();
    }

    void Finish()
    {
        if (_closed) return;
        _closed = true;
        var seen = Seen.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        seen.Add(_stage);
        Save(string.Join(',', Stages.Where(seen.Contains)));
        if (_stage == "menu") OnboardingProgress.SeenWelcome();       // the tour's intro card replaces the first-run welcome
        GetTree().Paused = _wasPaused;
        if (_current == this) _current = null;
        QueueFree();
    }

    public override void _ExitTree()
    {
        if (!_closed) GetTree().Paused = _wasPaused;        // the scene changed under the tour (Settings > home)
        if (_current == this) _current = null;
    }

    // Placed every frame: targets that size themselves after the first layout (the end card) still get the right spotlight.
    void Place()
    {
        var view = GetViewport().GetVisibleRect().Size;
        var target = _steps[_index].Target;
        bool lit = target is not null && IsInstanceValid(target) && target.IsVisibleInTree();
        var hole = lit ? target!.GetGlobalRect().Grow(8) : new Rect2(view / 2, Vector2.Zero);
        _shade[0].Position = Vector2.Zero; _shade[0].Size = new Vector2(view.X, hole.Position.Y);
        _shade[1].Position = new Vector2(0, hole.End.Y); _shade[1].Size = new Vector2(view.X, view.Y - hole.End.Y);
        _shade[2].Position = new Vector2(0, hole.Position.Y); _shade[2].Size = new Vector2(hole.Position.X, hole.Size.Y);
        _shade[3].Position = new Vector2(hole.End.X, hole.Position.Y); _shade[3].Size = new Vector2(view.X - hole.End.X, hole.Size.Y);
        _ring.Visible = lit; _ring.Position = hole.Position; _ring.Size = hole.Size;
        var size = _card.GetCombinedMinimumSize();
        _card.Position = CardPosition(view, lit, hole, size).Clamp(new Vector2(Margin, Margin), (view - size - new Vector2(Margin, Margin)).Max(new Vector2(Margin, Margin)));
        _card.Size = size;
    }

    /// <summary>The card goes beside the target, on the screen half the target does not use (the menu cover, your own players
    /// and the end card stay visible); above or below only when neither side fits; for a target that fills the screen (the end
    /// card) in the lower right corner of the screen, over the live panel.</summary>
    static Vector2 CardPosition(Vector2 view, bool lit, Rect2 hole, Vector2 size)
    {
        if (!lit) return (view - size) / 2;
        float midY = hole.GetCenter().Y - size.Y / 2;
        bool rightFits = hole.End.X + Gap + size.X <= view.X - Margin, leftFits = hole.Position.X - Gap - size.X >= Margin;
        bool preferRight = hole.GetCenter().X < view.X / 2;
        if (rightFits && (preferRight || !leftFits)) return new Vector2(hole.End.X + Gap, midY);
        if (leftFits) return new Vector2(hole.Position.X - Gap - size.X, midY);
        if (hole.End.Y + Gap + size.Y <= view.Y - Margin) return new Vector2(hole.Position.X, hole.End.Y + Gap);
        if (hole.Position.Y - Gap - size.Y >= Margin) return new Vector2(hole.Position.X, hole.Position.Y - Gap - size.Y);
        return view - size - new Vector2(Margin, Margin);
    }

    // Enter and Space press the yellow button (Next / Got it), also when the mouse has taken focus away from it.
    public override void _Input(InputEvent e)
    {
        if (_closed || e is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode is not (Key.Enter or Key.KpEnter or Key.Space)) return;
        GetViewport().SetInputAsHandled();
        Advance();
    }

    public override void _Process(double delta)
    {
        if (_closed) return;
        Place();
        if (Shot.Length == 0 || Forced[0] != _stage || ++_frames != 20) return;
        GetViewport().GetTexture().GetImage().SavePng($"{Shot}_tour_{_stage}{_index + 1}.png");
        GetTree().Quit();
    }
}
