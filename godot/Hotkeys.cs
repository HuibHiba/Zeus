// Autoload: keys that work on every screen. F11 toggles fullscreen, F9 makes a feedback file (see Feedback.cs). Also sizes the window to the screen at start.
using System;
using System.Threading.Tasks;
using Godot;

public partial class Hotkeys : Node
{
    // The layout lives in a fixed 1520x780 viewport (canvas_items, aspect keep): the window only scales it, so it must never open bigger than the screen.
    const int DesignW = 1520, DesignH = 780, MinW = 1100, MinH = 620;
    int _frames;

    public override void _Ready()
    {
        Loc.Initialize();
        ProcessMode = ProcessModeEnum.Always;       // F9 and the error catchers keep working while the feedback dialog pauses the game
        FeedbackLog.Install();
        GetWindow().Title = GameInfo.Title;
        FitWindow();
        SettingsMenu.ApplyDisplaySettings();
        var layer = new CanvasLayer { Layer = 60, ProcessMode = ProcessModeEnum.Always }; AddChild(layer);
        var settings = new Button { Text = Loc.T("settings.shortcut"), TooltipText = Loc.T("settings.title"),
            Position = new Vector2(1464, 8), CustomMinimumSize = new Vector2(40, 36), ThemeTypeVariation = "CompactButton" };
        UiTheme.Apply(settings); layer.AddChild(settings);
        settings.Pressed += () => SettingsMenu.Open(this);
    }

    public override void _Input(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } key || Feedback.IsOpen) return;
        if (key.Keycode == Key.F9) Feedback.Open(this);
        else if (key.Keycode == Key.Escape && !HasOpenWindow(GetTree().Root))
        {
            if (SettingsMenu.IsOpen) SettingsMenu.Escape();
            else if (GetTree().CurrentScene is Main match && match.TryClosePauseMenu()) { }
            else SettingsMenu.Open(this);
        }
        else return;
        GetViewport().SetInputAsHandled();
    }

    static bool HasOpenWindow(Node root)
    {
        foreach (var child in root.GetChildren(includeInternal: true))
            if (child is Window { Visible: true } || HasOpenWindow(child)) return true;
        return false;
    }

    Node? _lastScene;
    // Debug only: ZEUS_THROW=1 an unobserved task exception, 2 an exception inside a Godot callback; ZEUS_FEEDBACK=ask|error|done opens the dialog by itself.
    void Watch()
    {
        var scene = GetTree().CurrentScene;
        if (scene is not null && scene != _lastScene) { _lastScene = scene; FeedbackLog.Screen(scene.Name); }
        ++_watchFrames;
        var throwMode = GameSession.DebugEnvironment("ZEUS_THROW");
        if (throwMode == "1" && _watchFrames == 5) Task.Run(() => throw new InvalidOperationException("ZEUS_THROW test exception"));
        if (throwMode == "1" && _watchFrames == 8) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        if (throwMode == "2" && _watchFrames == 5) throw new InvalidOperationException("ZEUS_THROW test exception in a callback");
        if (_watchFrames == 3 && GameSession.DebugEnvironment("ZEUS_FEEDBACK") is { Length: > 0 } mode)
            Feedback.Open(this, error: mode == "error");
    }
    int _watchFrames;

    static void FitWindow()
    {
        if (DisplayServer.GetName() == "headless" || DisplayServer.WindowGetMode() != DisplayServer.WindowMode.Windowed) return;
        var usable = DisplayServer.ScreenGetUsableRect(DisplayServer.WindowGetCurrentScreen());
        var decoration = DisplayServer.WindowGetSizeWithDecorations() - DisplayServer.WindowGetSize();
        // ZEUS_WINDOW=WxH (debug only) forces a window size to check a smaller screen; without it the window fits the usable screen area.
        var wanted = ParseSize(GameSession.DebugEnvironment("ZEUS_WINDOW"));
        Vector2I size;
        if (wanted is { } w) size = w;
        else
        {
            double scale = Math.Min(1.0, Math.Min((usable.Size.X - decoration.X) / (double)DesignW, (usable.Size.Y - decoration.Y) / (double)DesignH));
            size = new Vector2I((int)(DesignW * scale), (int)(DesignH * scale));
        }
        var min = new Vector2I(Math.Min(MinW, size.X), Math.Min(MinH, size.Y));
        DisplayServer.WindowSetMinSize(min);
        DisplayServer.WindowSetSize(size);
        DisplayServer.WindowSetPosition(usable.Position + (usable.Size - size - decoration) / 2 + new Vector2I(0, decoration.Y / 2));
        // Keep the whole window, title bar included, inside the usable area (taskbar).
        var outerPos = DisplayServer.WindowGetPositionWithDecorations();
        var outerSize = DisplayServer.WindowGetSizeWithDecorations();
        var shift = new Vector2I(Math.Max(0, usable.Position.X - outerPos.X), Math.Max(0, usable.Position.Y - outerPos.Y));
        shift -= new Vector2I(Math.Max(0, outerPos.X + outerSize.X - usable.End.X), Math.Max(0, outerPos.Y + outerSize.Y - usable.End.Y));
        if (shift != Vector2I.Zero) DisplayServer.WindowSetPosition(DisplayServer.WindowGetPosition() + shift);
        if (GameSession.DebugEnvironment("ZEUS_FITCHECK").Length > 0)
            GD.Print($"FITWINDOW usable={usable} decoration={decoration} client={DisplayServer.WindowGetSize()} outer={DisplayServer.WindowGetPositionWithDecorations()}+{DisplayServer.WindowGetSizeWithDecorations()}");
    }

    static Vector2I? ParseSize(string text)
    {
        var parts = text.Split('x');
        return parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h) && w > 200 && h > 200 ? new Vector2I(w, h) : null;
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.F11 }) return;
        SettingsMenu.ToggleFullscreen();
        GetViewport().SetInputAsHandled();
    }

    // ZEUS_FITCHECK=<frame> (debug only) lists visible controls that reach outside the 1520x780 viewport and page-wide scroll areas.
    public override void _Process(double delta)
    {
        Watch();
        if (!int.TryParse(GameSession.DebugEnvironment("ZEUS_FITCHECK"), out var at)) return;
        ++_frames;
        if (_frames == 3) GD.Print($"FITVIEW window={DisplayServer.WindowGetSize()} viewport={GetViewport().GetVisibleRect().Size}");
        // A negative number repeats the check every 10 frames from that frame on and prints only changes (screens that fill in later, like the meter).
        if (at > 0 ? _frames != at : _frames < -at || _frames % 10 != 0) return;
        var scene = GetTree().CurrentScene;
        if (scene is null) return;
        _report.Clear();
        Walk(scene, new Rect2(0, 0, DesignW, DesignH), scene.Name);
        var text = string.Join(" | ", _report);
        if (text == _lastReport) return;
        _lastReport = text;
        GD.Print($"FIT frame {_frames}: {text}");
    }

    readonly System.Collections.Generic.List<string> _report = new();
    string _lastReport = "";

    void Walk(Node node, Rect2 bounds, string path)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is not Control c || !c.IsVisibleInTree()) { if (child is not Control) Walk(child, bounds, path); continue; }
            var p = path + "/" + c.Name;
            var r = c.GetGlobalRect();
            if (c is ScrollContainer sc)
            {
                var need = sc.GetCombinedMinimumSize();
                var inner = sc.GetChildCount() > 0 && sc.GetChild(0) is Control content ? content.GetCombinedMinimumSize() : need;
                _report.Add($"FIT scroll {p} rect={r} content={inner}{(r.End.Y > bounds.End.Y + 0.5f ? " OUTSIDE" : "")}");
                continue;      // inside a scroll area overflow is expected
            }
            if (r.End.Y > bounds.End.Y + 0.5f || r.End.X > bounds.End.X + 0.5f) _report.Add($"FIT over {p} ({c.GetType().Name}) rect={r}");
            Walk(c, bounds, p);
        }
    }
}
