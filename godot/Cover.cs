// Start screen: the closed notebook on the bench. Click, Enter or Space swings the cover open and fades into the menu (the inside).
using System;
using Godot;

public partial class Cover : Control
{
    const float W = 500, H = 660, X0 = (1600 - W) / 2, Y0 = 110;    // closed book in the 1600x900 design viewport; X0 is the spine
    const float Zoom = 1.1f;
    const double FlipTime = 1.05, FadeStart = 1.0, EndTime = 1.3;
    static bool _seen;
    public static bool IsOpen { get; private set; }

    Action _opened = null!;
    double _t = -1, _clock;      // _t: seconds since opening started (-1 = still closed)
    int _frames;
    static readonly string Shot = GameSession.DebugEnvironment("ZEUS_SHOT");
    static readonly int[] ShotSteps = { 12, 25, 33, 48, 68 };      // frames after opening (60 per second) saved as _cover_open1..5.png

    /// <summary>Only once per session, at game start; headless smoke and screenshot runs skip it unless ZEUS_COVER=1.</summary>
    public static bool ShouldShow => !_seen && (GameSession.DebugEnvironment("ZEUS_COVER") == "1"
        || (DisplayServer.GetName() != "headless" && Shot.Length == 0));

    /// <summary>Puts the cover over <paramref name="menu"/>; <paramref name="opened"/> runs once the book is open (tour/welcome start then).</summary>
    public static void Install(Node menu, Action opened)
    {
        _seen = IsOpen = true;
        // The whole layer is drawn 1.1x around the book's centre (book ~81% of the screen height, hint and opened spread still on screen).
        var pivot = new Vector2(X0 + W / 2, Y0 + H / 2);
        var layer = new CanvasLayer { Layer = 58, Scale = Vector2.One * Zoom, Offset = pivot * (1 - Zoom) };
        var cover = new Cover { _opened = opened, MouseFilter = MouseFilterEnum.Stop, PivotOffset = pivot };
        cover.SetAnchorsPreset(LayoutPreset.FullRect);
        layer.AddChild(cover);
        menu.AddChild(layer);
    }

    public override void _Process(double delta)
    {
        if (Shot.Length > 0) delta = 1.0 / 60;      // screenshot runs: fixed steps, so the frames land at the same moments
        if (GetParent() is CanvasLayer layer) layer.Offset = UiFrame.Offset(GetViewport()) + PivotOffset * (1 - Zoom);      // book centred in the 1600x900 frame (UiFrame)
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

    // Cover geometry: a point (u from the spine, v from the middle) of the turning cover, seen in perspective.
    const float Eye = 3200;      // viewing distance in px; the free edge grows while the cover lifts towards the viewer
    float _cos, _sin, _spineX;
    bool _inside;      // content coordinates then run from the cover's free edge (left) to the spine (right), so text reads normally

    Vector2 P(float u, float v)
    {
        float s = Eye / (Eye - u * _sin);
        return new Vector2(_spineX + u * _cos * s, Y0 + H / 2 + v * s);
    }

    Vector2 C(float c, float v) => P(_inside ? W - c : c, v);

    void Quad(Color c, float u0, float v0, float u1, float v1, Color? c1 = null)
    {
        Vector2[] pts = { C(u0, v0), C(u1, v0), C(u1, v1), C(u0, v1) };
        float lo = Math.Min(Math.Min(pts[0].X, pts[1].X), Math.Min(pts[2].X, pts[3].X)), hi = Math.Max(Math.Max(pts[0].X, pts[1].X), Math.Max(pts[2].X, pts[3].X));
        if (hi - lo < 1.5f) return;      // edge-on: the polygon would be degenerate
        var e = c1 ?? c;
        DrawPolygon(pts, new[] { c, e, e, c });
    }

    void Text(float c, float v, int size, Color col, string text, float width, HorizontalAlignment align = HorizontalAlignment.Left, Font? font = null)
    {
        var o = C(c, v);
        DrawSetTransformMatrix(new Transform2D(C(c + 1, v) - o, C(c, v + 1) - o, o));      // text follows the local axes of the turning cover
        DrawString(font ?? GameFonts.Hand, Vector2.Zero, text, align, width, size, col);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    void Rule(Color col, float u0, float v0, float u1, float v1, float w = 1) => DrawLine(C(u0, v0), C(u1, v1), col, w);

    public override void _Draw()
    {
        // Bench (the table) with the same faint lines as the menu backdrop.
        var bench = new Rect2(-UiFrame.Offset(GetViewport()), Size);      // the whole screen, also around the centred frame
        DrawRect(bench, UiTheme.Bench);
        for (float y = bench.Position.Y; y < bench.End.Y; y += 56) DrawLine(new Vector2(bench.Position.X, y), new Vector2(bench.End.X, y), UiTheme.BenchLight, 1);

        double flip = _t < 0 ? 0 : Ease(Math.Clamp(_t / FlipTime, 0, 1));
        float ang = (float)(flip * Math.PI), lift = (float)Math.Sin(ang);
        _cos = (float)Math.Cos(ang); _sin = lift;
        float dx = W / 2 * (float)flip;      // the book slides right while opening, so the open spread ends up centred
        _spineX = X0 + dx;
        DrawSetTransform(new Vector2(dx, 0), 0, Vector2.One);
        var book = new Rect2(X0, Y0, W, H);
        // Back cover with the shadow on the table, then the page block inside it (stacked sheet edges at the right and bottom).
        DrawStyleBox(Board(UiTheme.Cover.Darkened(0.25f), shadow: true), book);
        var pages = new Rect2(X0 + 4, Y0 + 7, W - 12, H - 14);
        for (int i = 5; i >= 1; i--)
        {
            var edge = new Rect2(pages.Position + new Vector2(i * 1.4f, i * 1.4f), pages.Size);
            DrawRect(edge, (i % 2 == 0 ? UiTheme.PaperShade : UiTheme.Paper).Darkened(0.05f * i));
        }
        UiPaper.DrawPage(this, pages);
        for (float y = pages.Position.Y + 64; y < pages.End.Y - 20; y += 28)
            DrawLine(new Vector2(pages.Position.X + 26, y), new Vector2(pages.End.X - 10, y), new Color(UiTheme.Rule, 0.35f), 1);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);

        // Shadows on the pages: a gutter in the fold, and what the cover casts while it is above them (darkest at the spine, soft beyond its free edge).
        float x0 = _spineX + 4, x1 = _spineX + pages.Size.X + 4, y0 = pages.Position.Y, y1 = pages.End.Y;
        Shade(x0, Math.Min(x0 + 34, x1), y0, y1, 0.16f + 0.1f * lift, 0);
        float eX = P(W, 0).X, k = Math.Clamp((eX - _spineX) / 80, 0, 1) * lift;
        if (k > 0)
        {
            Shade(x0, Math.Min(eX, x1), y0, y1, 0.3f * k, 0.12f * k);
            Shade(Math.Min(eX, x1), Math.Min(eX + 70 * lift, x1), y0, y1, 0.12f * k, 0);
        }

        // Front cover, turned around the spine in perspective: a trapezium whose free edge grows while the cover lifts.
        _inside = _cos < 0;
        DrawLine(P(W, -H / 2), P(W, H / 2), UiTheme.Cover.Darkened(0.5f), 6);      // board thickness, only visible near edge-on
        DrawCover(_inside ? 0.1f + 0.2f * lift : 0.3f * lift);
        if (_inside) DrawInside(lift); else DrawFront(lift);

        if (_t < 0)
        {
            float pulse = 0.55f + 0.35f * (float)Math.Sin(_clock * 2.4);
            DrawString(GameFonts.Hand, new Vector2(0, Y0 + H + 44), Loc.T("cover.open"), HorizontalAlignment.Center, Size.X, 22, new Color(UiTheme.Muted, pulse));
        }
    }

    void Shade(float xa, float xb, float ya, float yb, float aa, float ab)
    {
        if (xb - xa < 1) return;
        var a = new Color(0, 0, 0, aa); var b = new Color(0, 0, 0, ab);
        DrawPolygon(new[] { new Vector2(xa, ya), new Vector2(xb, ya), new Vector2(xb, yb), new Vector2(xa, yb) }, new[] { a, b, b, a });
    }

    /// <summary>The cover board as one polygon with rounded free corners; the free edge is shaded a bit darker than the spine.</summary>
    void DrawCover(float dim)
    {
        var pts = new System.Collections.Generic.List<Vector2> { P(0, -H / 2), P(0, H / 2) };
        const float r = 12;
        for (int i = 0; i <= 5; i++)
        {
            float a = i / 5f * Mathf.Pi / 2;
            pts.Add(P(W - r + r * (float)Math.Sin(a), H / 2 - r + r * (float)Math.Cos(a)));
        }
        for (int i = 0; i <= 5; i++)
        {
            float a = i / 5f * Mathf.Pi / 2;
            pts.Add(P(W - r + r * (float)Math.Cos(a), -H / 2 + r - r * (float)Math.Sin(a)));
        }
        var colors = new Color[pts.Count];
        for (int i = 0; i < colors.Length; i++) colors[i] = UiTheme.Cover.Darkened(Math.Clamp(dim * (i < 2 ? 0.6f : 1.4f), 0, 0.9f));
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var p in pts) { lo = Math.Min(lo, p.X); hi = Math.Max(hi, p.X); }
        if (hi - lo >= 1.5f) DrawPolygon(pts.ToArray(), colors);
    }

    void DrawInside(float lift)
    {
        float dim = 0.03f + 0.22f * lift;
        var sage = UiTheme.Cover.Lerp(UiTheme.Paper, 0.6f);
        // Endpaper glued to the board, darker at the hinge, with the cloth showing as a narrow border.
        Quad(sage.Darkened(dim), 14, -H / 2 + 14, W - 14, H / 2 - 14, sage.Darkened(dim + 0.16f));
        Rule(new Color(0, 0, 0, 0.18f), 14, -H / 2 + 14, 14, H / 2 - 14);
        // Sheet edges peeking out at the hinge.
        for (int i = 0; i < 4; i++) Rule(new Color(UiTheme.PaperShade, 0.9f), W - 12 - i * 2.5f, -H / 2 + 24, W - 12 - i * 2.5f, H / 2 - 24, 1.5f);
        Rule(new Color(0, 0, 0, 0.25f), W - 6, -H / 2, W - 6, H / 2, 2);      // hinge groove

        // Small paper label: "Property of the gaffer" with a line for the name.
        const float lu = 100, lv = -190, lw = 300, lh = 124;
        Quad(new Color(0, 0, 0, 0.22f), lu + 3, lv + 4, lu + lw + 3, lv + lh + 4);
        Quad(UiTheme.Paper.Lightened(0.04f).Darkened(dim * 0.5f), lu, lv, lu + lw, lv + lh);
        var ink = new Color(UiTheme.Ink, 0.85f);
        var frame = new Color(UiTheme.Ink, 0.4f);
        Rule(frame, lu + 7, lv + 7, lu + lw - 7, lv + 7); Rule(frame, lu + 7, lv + lh - 7, lu + lw - 7, lv + lh - 7);
        Rule(frame, lu + 7, lv + 7, lu + 7, lv + lh - 7); Rule(frame, lu + lw - 7, lv + 7, lu + lw - 7, lv + lh - 7);
        Text(lu, lv + 52, 24, ink, Loc.T("cover.property"), lw, HorizontalAlignment.Center);
        Rule(frame, lu + 26, lv + 94, lu + lw - 26, lv + 94);
        Text(lu + 26, lv + 90, 17, new Color(UiTheme.Muted, 0.9f), Loc.T("cover.name") + ":", lw - 52);
    }

    void DrawFront(float lift)
    {
        var dim = 0.3f * lift;
        Quad(UiTheme.Cover.Darkened(0.3f + dim), 0, -H / 2, 24, H / 2);      // spine
        Rule(new Color(1, 1, 1, 0.08f), 25, -H / 2 + 2, 25, H / 2 - 2, 2);

        // Paper label with the title, as on the menu's cover.
        float lu = 66, lv = -H / 2 + 130, lw = 350, lh = 200;
        Quad(new Color(0, 0, 0, 0.25f), lu + 4, lv + 5, lu + lw + 4, lv + lh + 5);
        Quad(UiTheme.Paper.Darkened(dim), lu, lv, lu + lw, lv + lh);
        var frame = UiTheme.PaperShade.Darkened(dim);
        Rule(frame, lu + 8, lv + 8, lu + lw - 8, lv + 8); Rule(frame, lu + 8, lv + lh - 8, lu + lw - 8, lv + lh - 8);
        Rule(frame, lu + 8, lv + 8, lu + 8, lv + lh - 8); Rule(frame, lu + lw - 8, lv + 8, lu + lw - 8, lv + lh - 8);
        var words = GameInfo.Title.Split(' ', 2);
        Text(lu + 28, lv + 92, 84, UiTheme.Ink, words[0].ToUpperInvariant(), 320, font: GameFonts.Display);
        Text(lu + 28, lv + 172, 84, UiTheme.Ink, words.Length > 1 ? words[1].ToUpperInvariant() : "", 320, font: GameFonts.Display);
        Quad(UiTheme.Accent, lu + lw - 150, lv + lh - 22, lu + lw - 30, lv + lh - 16);
        Text(70, lv + lh + 54, 26, new Color(UiTheme.Paper, 0.85f), Loc.T("cover.subtitle"), 350);

        // Elastic band: slips off to the right in the first moments of opening.
        float band = Math.Clamp(lift * 3, 0, 1);
        Quad(new Color(UiTheme.Bench, 1 - band), W - 50 + 40 * band, -H / 2 - 2, W - 36 + 40 * band, H / 2 + 2);
        Rule(new Color(1, 1, 1, 0.07f * (1 - band)), W - 47 + 40 * band, -H / 2, W - 47 + 40 * band, H / 2, 2);
    }

    static StyleBoxFlat Board(Color fill, bool shadow = false)
    {
        var s = new StyleBoxFlat { BgColor = fill, CornerRadiusTopRight = 12, CornerRadiusBottomRight = 12, CornerRadiusTopLeft = 3, CornerRadiusBottomLeft = 3 };
        if (shadow) { s.ShadowColor = new Color(0, 0, 0, 0.5f); s.ShadowSize = 24; s.ShadowOffset = new Vector2(10, 14); }
        return s;
    }
}
