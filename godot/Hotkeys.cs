// Autoload: keys that work on every screen. F11 toggles fullscreen.
using Godot;

public partial class Hotkeys : Node
{
    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.F11 }) return;
        var full = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;
        DisplayServer.WindowSetMode(full ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
        GetViewport().SetInputAsHandled();
    }
}
