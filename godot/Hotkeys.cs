// Autoload: keys that work on every screen. F11 toggles fullscreen. Also sizes the window to the screen at start.
using System;
using Godot;

public partial class Hotkeys : Node
{
    // The layout lives in a fixed 1520x780 viewport (canvas_items, aspect keep): the window only scales it, so it must never open bigger than the screen.
    const int DesignW = 1520, DesignH = 780, MinW = 1100, MinH = 620;
    int _frames;

    public override void _Ready()
    {
        Loc.Initialize();
        GetWindow().Title = GameInfo.Title;
        FitWindow();
    }

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
        var full = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;
        DisplayServer.WindowSetMode(full ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
        GetViewport().SetInputAsHandled();
    }

    // ZEUS_FITCHECK=<frame> (debug only) lists visible controls that reach outside the 1520x780 viewport and page-wide scroll areas.
    public override void _Process(double delta)
    {
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
