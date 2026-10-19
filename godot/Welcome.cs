using Godot;
using Zeus.Sim;

// Static help in a modal window: focus and input stay here until it is closed.
public partial class Welcome : CanvasLayer
{
    string _screen = "menu";
    AcceptDialog? _dialog;
    public static Welcome Install(Node owner, string screen, Container? header = null)
    {
        var help = new Welcome { _screen = screen, Layer = 20 };
        owner.AddChild(help);
        var button = new Button { Text = Loc.T("help.button"), TooltipText = Loc.T("help.shortcut"),
            CustomMinimumSize = new Vector2(40, 36), ThemeTypeVariation = "CompactButton" };
        button.Pressed += help.ShowHelp;
        if (header is not null) header.AddChild(button);
        else
        {
            button.SetAnchorsPreset(Control.LayoutPreset.TopRight); button.OffsetLeft = -112; button.OffsetRight = -72; button.OffsetTop = 8;      // 16 px left of the Esc button of Hotkeys (top-right of the screen)
            UiTheme.Apply(button);
            help.AddChild(button);
        }
        if (GameSession.DebugEnvironment("ZEUS_HELP").Length > 0) Callable.From(help.ShowHelp).CallDeferred();
        return help;
    }

    public void ShowHelp() => Show(false);
    public void ShowWelcome() => Show(true);

    void Show(bool welcome)
    {
        if (_dialog is not null && GodotObject.IsInstanceValid(_dialog)) { _dialog.PopupCentered(); return; }
        _dialog = new AcceptDialog { Title = Loc.T(welcome ? "welcome.title" : "help.title"),
            Exclusive = true, Transient = true, Unresizable = true, MinSize = new Vector2I(880, 0) };
        _dialog.Theme = UiTheme.PaperTheme();
        _dialog.AddThemeStyleboxOverride("panel", UiTheme.PanelBox());
        _dialog.GetOkButton().Text = Loc.T("welcome.continue");
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 10);
        box.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        box.OffsetLeft = 26; box.OffsetTop = 20; box.OffsetRight = -26; box.OffsetBottom = -60;
        _dialog.AddChild(box);
        void Line(string text, int size = 20)
        {
            var label = UiTheme.Label(text, size);
            label.CustomMinimumSize = new Vector2(810, 0);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            box.AddChild(label);
        }
        if (welcome)
        {
            Line(Loc.T("welcome.game"));
            Line(Loc.T("onboarding.title"), 28);
            foreach (var step in OnboardingState.Steps) Line(Loc.T("onboarding.step." + step));
            Line(Loc.T("welcome.keys"));
            Line(Loc.T("welcome.early"));
        }
        else
            for (int i = 1; i <= 4; i++) Line(Loc.T("help." + _screen + "." + i));
        var dialog = _dialog;
        void Close()
        {
            if (welcome) OnboardingProgress.SeenWelcome();
            dialog.QueueFree(); _dialog = null;
        }
        dialog.Confirmed += Close;
        dialog.Canceled += Close;
        dialog.WindowInput += e =>
        {
            if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F1 }) { dialog.Hide(); Close(); }
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(900, welcome ? 620 : 320));
    }

    public override void _Input(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.F1 }) return;
        ShowHelp(); GetViewport().SetInputAsHandled();
    }
}
