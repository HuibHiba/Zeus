// Start screen: the closed notebook on the bench. Click, Enter or Space swings the cover open and fades into the menu (the inside).
using System;
using Godot;

public partial class Cover : Control
{
    const float W = 500, H = 660, X0 = (1520 - W) / 2, Y0 = 50;     // closed book in the 1520x780 design viewport; X0 is the spine
    const double FlipTime = 0.8, FadeStart = 0.75, EndTime = 1.2;
    static bool _seen;
    public static bool IsOpen { get; private set; }

    Action _opened = null!;
    double _t = -1, _clock;      // _t: seconds since opening started (-1 = still closed)
    int _frames;
    static readonly string Shot = GameSession.DebugEnvironment("ZEUS_SHOT");
    static readonly int[] ShotSteps = { 12, 27, 42, 54 };      // frames after opening (60 per second) saved as _cover_open1..4.png

    /// <summary>Only once per session, at game start; headless smoke and screenshot runs skip it unless ZEUS_COVER=1.</summary>
    public static bool ShouldShow => !_seen && (GameSession.DebugEnvironment("ZEUS_COVER") == "1"
        || (DisplayServer.GetName() != "headless" && Shot.Length == 0));

    /// <summary>Puts the cover over <paramref name="menu"/>; <paramref name="opened"/> runs once the book is open (tour/welcome start then).</summary>
    public static void Install(Node menu, Action opened)
    {
        _seen = IsOpen = true;
        var layer = new CanvasLayer { Layer = 58 };
        var cover = new Cover { _opened = opened, MouseFilter = MouseFilterEnum.Stop, PivotOffset = new Vector2(X0 + W / 2, Y0 + H / 2) };
        cover.SetAnchorsPreset(LayoutPreset.FullRect);
        layer.AddChild(cover);
        menu.AddChild(layer);
    }

    public override void _Process(double delta)
    {
        if (Shot.Length > 0) delta = 1.0 / 60;      // screenshot runs: fixed steps, so the frames land at the same moments
        _clock += delta;
        if (Shot.Length > 0) ShotStep();
        if (_t >= 0)
        {
            _t += delta;
            double fade = Math.Clamp((_t - FadeStart) / (EndTime - FadeStart), 0, 1);
            Modulate = new Color(1, 1, 1, 1 - (float)Ease(fade));
            Scale = Vector2.One * (1 + 0.18f * (float)Ease(fade));      // a slight zoom into the page while fading: "into the notebook"
            if (_t >= EndTime) { Finish(); return; }
        }
        QueueRedraw();
    }

    void ShotStep()
    {
        _frames++;
        if (_frames == 8) Save("_cover.png");
        if (_frames == 10) Open();
        int step = Array.IndexOf(ShotSteps, _frames - 10);
        if (step >= 0) Save($"_cover_open{step + 1}.png");
    }

    void Save(string suffix) => GetViewport().GetTexture().GetImage().SavePng(Shot + suffix);

    void Open()
    {
        if (_t >= 0) return;
        _t = 0;
        AudioManager.Sfx("ui_click");
    }

    void Finish()
    {
        if (!IsOpen) return;
        IsOpen = false;
        GetParent().QueueFree();
        _opened();
    }

    // Keys: before the menu (its focused Start button would take Enter/Space) and before the Esc settings hotkey.
    public override void _Input(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Escape || _t >= 0) Finish();
        else if (key.Keycode is Key.Enter or Key.KpEnter or Key.Space) Open();
        else return;
        GetViewport().SetInputAsHandled();
    }

    // Mouse through the GUI, so the settings button above this layer stays clickable.
    public override void _GuiInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
        if (_t >= 0) Finish(); else Open();
        AcceptEvent();
    }

    static double Ease(double x) => x * x * (3 - 2 * x);

    public override void _Draw()
    {
        // Bench (the table) with the same faint lines as the menu backdrop.
        DrawRect(new Rect2(Vector2.Zero, Size), UiTheme.Bench);
        for (int y = 0; y < Size.Y; y += 56) DrawLine(new Vector2(0, y), new Vector2(Size.X, y), UiTheme.BenchLight, 1);

        double flip = _t < 0 ? 0 : Ease(Math.Clamp(_t / FlipTime, 0, 1));
        float sx = (float)Math.Cos(flip * Math.PI), lift = (float)Math.Sin(flip * Math.PI);
        float dx = W / 2 * (float)flip;      // the book slides right while opening, so the open spread ends up centred
        DrawSetTransform(new Vector2(dx, 0), 0, Vector2.One);
        var book = new Rect2(X0, Y0, W, H);
        // Back cover with the shadow on the table, then the page block inside it.
        DrawStyleBox(Board(UiTheme.Cover.Darkened(0.25f), shadow: true), book);
        var pages = new Rect2(X0 + 4, Y0 + 7, W - 12, H - 14);
        for (int i = 3; i >= 1; i--) DrawRect(new Rect2(pages.Position + new Vector2(i * 1.5f, i * 1.5f), pages.Size), UiTheme.PaperShade.Darkened(0.06f * i));
        UiPaper.DrawPage(this, pages);
        for (float y = pages.Position.Y + 64; y < pages.End.Y - 20; y += 28)
            DrawLine(new Vector2(pages.Position.X + 26, y), new Vector2(pages.End.X - 10, y), new Color(UiTheme.Rule, 0.35f), 1);

        // Shadow of the turning cover on the pages, narrowing as the cover passes upright.
        if (lift > 0.01f && sx > 0)
        {
            float edge = X0 + W * sx + 40 * lift;
            DrawPolygon(new[] { new Vector2(X0, Y0), new Vector2(edge, Y0), new Vector2(edge, Y0 + H), new Vector2(X0, Y0 + H) },
                new[] { new Color(0, 0, 0, 0.35f * lift), new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), new Color(0, 0, 0, 0.35f * lift) });
        }

        // Front cover, turned around the spine: horizontal scale cos(a), a little taller as it lifts towards the viewer.
        DrawSetTransform(new Vector2(X0 + dx, Y0 + H / 2), 0, new Vector2(sx, 1 + 0.1f * lift));
        var local = new Rect2(0, -H / 2, W, H);
        if (sx >= 0) DrawFront(local, lift);
        else
        {
            DrawStyleBox(Board(UiTheme.Cover.Darkened(0.2f * lift)), local);
            DrawRect(local.Grow(-8), UiTheme.Paper.Darkened(0.06f + 0.25f * lift));      // endpaper on the inside of the cover
        }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);

        if (_t < 0)
        {
            float pulse = 0.55f + 0.35f * (float)Math.Sin(_clock * 2.4);
            DrawString(GameFonts.Hand, new Vector2(0, Y0 + H + 44), Loc.T("cover.open"), HorizontalAlignment.Center, Size.X, 22, new Color(UiTheme.Muted, pulse));
        }
    }

    void DrawFront(Rect2 r, float lift)
    {
        var dim = 0.3f * lift;      // the cover darkens as it turns away from the light
        DrawStyleBox(Board(UiTheme.Cover.Darkened(dim)), r);
        DrawRect(new Rect2(r.Position.X, r.Position.Y, 24, r.Size.Y), UiTheme.Cover.Darkened(0.3f + dim));        // spine
        DrawLine(new Vector2(25, r.Position.Y + 2), new Vector2(25, r.End.Y - 2), new Color(1, 1, 1, 0.08f), 2);

        // Paper label with the title, as on the menu's cover.
        var label = new Rect2(66, r.Position.Y + 130, 350, 200);
        DrawRect(new Rect2(label.Position + new Vector2(4, 5), label.Size), new Color(0, 0, 0, 0.25f));
        DrawRect(label, UiTheme.Paper.Darkened(dim));
        DrawRect(label.Grow(-8), UiTheme.PaperShade.Darkened(dim), false, 1);
        var words = GameInfo.Title.Split(' ', 2);
        DrawString(GameFonts.Display, label.Position + new Vector2(28, 92), words[0].ToUpperInvariant(), HorizontalAlignment.Left, 320, 84, UiTheme.Ink);
        DrawString(GameFonts.Display, label.Position + new Vector2(28, 172), words.Length > 1 ? words[1].ToUpperInvariant() : "", HorizontalAlignment.Left, 320, 84, UiTheme.Ink);
        DrawRect(new Rect2(label.End.X - 150, label.End.Y - 22, 120, 6), UiTheme.Accent);
        DrawString(GameFonts.Hand, new Vector2(70, label.End.Y + 54), Loc.T("cover.subtitle"), HorizontalAlignment.Left, 350, 26, new Color(UiTheme.Paper, 0.85f));

        // Elastic band: slips off to the right in the first moments of opening.
        float band = Math.Clamp(lift * 3, 0, 1);
        var bandColor = new Color(UiTheme.Bench, 1 - band);
        DrawRect(new Rect2(W - 50 + 40 * band, r.Position.Y - 2, 14, r.Size.Y + 4), bandColor);
        DrawLine(new Vector2(W - 47 + 40 * band, r.Position.Y), new Vector2(W - 47 + 40 * band, r.End.Y), new Color(1, 1, 1, 0.07f * (1 - band)), 2);
    }

    static StyleBoxFlat Board(Color fill, bool shadow = false)
    {
        var s = new StyleBoxFlat { BgColor = fill, CornerRadiusTopRight = 12, CornerRadiusBottomRight = 12, CornerRadiusTopLeft = 3, CornerRadiusBottomLeft = 3 };
        if (shadow) { s.ShadowColor = new Color(0, 0, 0, 0.5f); s.ShadowSize = 24; s.ShadowOffset = new Vector2(10, 14); }
        return s;
    }
}
