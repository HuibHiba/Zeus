// Small UI animations (page turn, button hover, panel fade-in, goal flash). All <= 0.3 s; off via Settings > Display > Animations,
// and always off in headless, ZEUS_SHOT and ZEUS_AUTOTEST runs so tests and screenshots never wait.
using Godot;

public static class Motion
{
    public static bool Enabled => Loc.Setting("animations", "on") != "off" && DisplayServer.GetName() != "headless"
        && GameSession.DebugEnvironment("ZEUS_SHOT").Length == 0 && GameSession.DebugEnvironment("ZEUS_AUTOTEST").Length == 0;

    /// <summary>Hooks every Button (hover/press) and every overlay panel (layer 70) in the tree; call once from the autoload.</summary>
    public static void Install(SceneTree tree) => tree.NodeAdded += node =>
    {
        if (node is Button button) Hover(button);
        else if (node is CanvasLayer { Layer: 70 } layer)
            Callable.From(() => { if (Enabled && layer.GetChildCount() > 0 && layer.GetChild(0) is Control root) PanelIn(root); }).CallDeferred();
    };

    /// <summary>Cream page strip that sweeps off to the right over the new screen, like turning a page.</summary>
    public static void PageTurn(CanvasLayer layer)
    {
        if (!Enabled) return;
        float w = layer.GetViewport().GetVisibleRect().Size.X;
        var page = new ColorRect { Color = UiTheme.Paper, MouseFilter = Control.MouseFilterEnum.Ignore };
        page.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var edge = new ColorRect { Color = new Color(UiTheme.PaperShade, 0.9f), MouseFilter = Control.MouseFilterEnum.Ignore };
        edge.SetAnchorsPreset(Control.LayoutPreset.LeftWide); edge.OffsetLeft = -10; edge.OffsetRight = 0;
        page.AddChild(edge); layer.AddChild(page);
        var tween = page.CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.TweenProperty(page, "position:x", w + 10, 0.3);
        tween.TweenCallback(Callable.From(page.QueueFree));
    }

    /// <summary>Fade (and a small slide up) of a freshly opened panel, 0.15 s.</summary>
    public static void PanelIn(Control panel)
    {
        if (!Enabled) return;
        panel.Modulate = new Color(1, 1, 1, 0);
        var tween = panel.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(panel, "modulate:a", 1f, 0.15);
        tween.TweenProperty(panel, "position:y", 0f, 0.15).From(10f);
    }

    static void Hover(Button b)
    {
        var rest = Colors.White;
        Tween? tween = null;
        void To(float scale, Color tint, double time)
        {
            tween?.Kill();
            b.PivotOffset = b.ThemeTypeVariation == "MenuItem" ? new Vector2(0, b.Size.Y / 2) : b.Size / 2;
            tween = b.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(b, "scale", new Vector2(scale, scale), time);
            tween.TweenProperty(b, "modulate", tint, time);
        }
        b.MouseEntered += () => { if (Enabled && !b.Disabled) { rest = b.Modulate; To(1.02f, rest * new Color(1.08f, 1.08f, 1.08f), 0.1); } };
        b.MouseExited += () => { if (b.Scale != Vector2.One) To(1f, rest, 0.12); };
        b.ButtonDown += () => { if (Enabled && !b.Disabled) To(0.98f, rest.Darkened(0.06f), 0.05); };
        b.ButtonUp += () => { if (b.Scale != Vector2.One && b.GetGlobalRect().HasPoint(b.GetGlobalMousePosition())) To(1.02f, rest, 0.08); };
        b.VisibilityChanged += () => { if (!b.IsVisibleInTree()) { tween?.Kill(); b.Scale = Vector2.One; b.Modulate = rest; } };
    }

    // Goal flash on the scoreboard: a score that went up pulses for 0.3 s. State is kept per drawing canvas (a new match is a new canvas).
    static ulong _canvas; static int _home = -1, _away = -1; static ulong _homeAt, _awayAt;
    public static (float Home, float Away) GoalPulse(CanvasItem c, int home, int away)
    {
        ulong now = Time.GetTicksMsec();
        if (c.GetInstanceId() != _canvas || home < _home || away < _away) { _canvas = c.GetInstanceId(); _home = home; _away = away; _homeAt = _awayAt = 0; }
        if (home > _home) { _homeAt = now; _home = home; }
        if (away > _away) { _awayAt = now; _away = away; }
        float P(ulong at) => !Enabled || at == 0 || now - at >= 300 ? 0f : 1f - (now - at) / 300f;
        return (P(_homeAt), P(_awayAt));
    }
}
