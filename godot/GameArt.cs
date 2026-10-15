// Shared drawing for the "floodlit match night" look: mown pitch, vignette, player tokens, ball, broadcast score bug.
using System;
using Godot;

public static class GameFonts
{
    static Font? _display, _semi, _body;
    static Font Load(string name)
    {
        var f = new FontFile { Data = FileAccess.GetFileAsBytes("res://assets/fonts/" + name) };
        return f.Data.Length > 0 ? f : ThemeDB.FallbackFont;
    }
    public static Font Display => _display ??= Load("BarlowCondensed-Bold.ttf");
    public static Font Semi => _semi ??= Load("BarlowCondensed-SemiBold.ttf");
    public static Font Body => _body ??= Load("Barlow-Medium.ttf");
}

public static class GameArt
{
    public static readonly Color GrassA = new("1D6B35"), GrassB = new("237A3D"), Surround = new("0F3B1D");
    static readonly Color Chalk = new(1, 1, 1, 0.82f);

    /// <summary>Draws the pitch inside <paramref name="field"/> (105x68 m proportions) including a grass surround.</summary>
    public static void DrawPitch(CanvasItem c, Rect2 field, float surround = 36)
    {
        float k = field.Size.X / 105f;
        c.DrawRect(field.Grow(surround), Surround, true);
        const int stripes = 14;
        float w = field.Size.X / stripes;
        for (int i = 0; i < stripes; i++)
            c.DrawRect(new Rect2(field.Position.X + i * w, field.Position.Y, w + 0.5f, field.Size.Y), i % 2 == 0 ? GrassA : GrassB, true);

        float lw = Math.Max(1.5f, 0.14f * k * 1.4f);
        Vector2 P(float xm, float ym) => field.Position + new Vector2(xm * k, ym * k);       // metres from top-left corner
        c.DrawRect(field, Chalk, false, lw);
        c.DrawLine(P(52.5f, 0), P(52.5f, 68), Chalk, lw);
        c.DrawArc(P(52.5f, 34), 9.15f * k, 0, Mathf.Tau, 64, Chalk, lw, true);
        c.DrawCircle(P(52.5f, 34), 0.35f * k + 1f, Chalk);

        foreach (bool left in new[] { true, false })
        {
            float dir = left ? 1 : -1, x0 = left ? 0 : 105;
            Rect2 Box(float depth, float width) =>
                new(left ? P(x0, 34 - width / 2) : P(x0 - depth, 34 - width / 2), new Vector2(depth * k, width * k));
            c.DrawRect(Box(16.5f, 40.32f), Chalk, false, lw);
            c.DrawRect(Box(5.5f, 18.32f), Chalk, false, lw);
            var spot = P(x0 + dir * 11f, 34);
            c.DrawCircle(spot, 0.3f * k + 1f, Chalk);
            float a = Mathf.Acos(5.5f / 9.15f);                                             // arc part outside the box
            float mid = left ? 0 : Mathf.Pi;
            c.DrawArc(spot, 9.15f * k, mid - a, mid + a, 32, Chalk, lw, true);
            // goal + net behind the line
            var goal = left ? new Rect2(P(x0 - 2.2f, 34 - 3.66f), new Vector2(2.2f * k, 7.32f * k))
                            : new Rect2(P(x0, 34 - 3.66f), new Vector2(2.2f * k, 7.32f * k));
            c.DrawRect(goal, new Color(1, 1, 1, 0.10f), true);
            c.DrawRect(goal, new Color(1, 1, 1, 0.55f), false, lw);
        }
        foreach (var (cx, cy, a0) in new[] { (0f, 0f, 0f), (105f, 0f, 90f), (105f, 68f, 180f), (0f, 68f, 270f) })
            c.DrawArc(P(cx, cy), 1f * k, Mathf.DegToRad(a0), Mathf.DegToRad(a0 + 90), 8, Chalk, lw);
    }

    static GradientTexture2D? _vignette;
    /// <summary>Dark edges + soft light in the middle, drawn over <paramref name="area"/>.</summary>
    public static void DrawVignette(CanvasItem c, Rect2 area, float strength = 0.6f)
    {
        _vignette ??= new GradientTexture2D
        {
            Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1.05f, 0.5f),
            Width = 256, Height = 256,
            Gradient = new Gradient { Offsets = new[] { 0f, 0.55f, 1f }, Colors = new[] { new Color(0, 0, 0, 0), new Color(0, 0, 0, 0.18f), new Color(0, 0, 0, 1) } },
        };
        c.DrawTextureRect(_vignette, area, false, new Color(1, 1, 1, strength));
    }

    /// <summary>A player: soft shadow, kit-coloured disc with rim, label, and a heading wedge.</summary>
    public static void DrawPlayer(CanvasItem c, Vector2 pos, Color kit, string label, double heading, float r = 12, bool selected = false, bool sentOff = false)
    {
        c.DrawCircle(pos + new Vector2(2.5f, 3.5f), r, new Color(0, 0, 0, 0.35f));
        var dir = new Vector2((float)Math.Cos(heading), (float)Math.Sin(heading));
        var n = new Vector2(-dir.Y, dir.X);
        c.DrawColoredPolygon(new[] { pos + dir * (r + 7), pos + dir * (r - 1) + n * 5, pos + dir * (r - 1) - n * 5 }, new Color(1, 1, 1, 0.9f));
        c.DrawCircle(pos, r, sentOff ? kit.Darkened(0.6f) : kit);
        c.DrawArc(pos, r, 0, Mathf.Tau, 28, kit.Darkened(0.45f), 2.5f, true);
        c.DrawArc(pos, r - 3, -2.4f, -0.7f, 10, new Color(1, 1, 1, 0.35f), 2f, true);        // highlight
        if (selected) c.DrawArc(pos, r + 5, 0, Mathf.Tau, 36, UiTheme.Accent, 3f, true);
        var font = GameFonts.Display;
        int size = label.Length > 2 ? 11 : 13;
        var sz = font.GetStringSize(label, HorizontalAlignment.Left, -1, size);
        c.DrawString(font, pos + new Vector2(-sz.X / 2, size * 0.36f), label, HorizontalAlignment.Left, -1, size, Colors.White);
    }

    public static void DrawBall(CanvasItem c, Vector2 pos)
    {
        c.DrawCircle(pos + new Vector2(2, 3), 5.5f, new Color(0, 0, 0, 0.4f));
        c.DrawCircle(pos, 5.5f, new Color("F4F4F0"));
        c.DrawArc(pos, 5.5f, 0, Mathf.Tau, 16, new Color("2A2A2A"), 1.2f, true);
        c.DrawCircle(pos, 2f, new Color("2A2A2A"));
    }

    /// <summary>Broadcast-style score bug with slanted ends. Returns the width used.</summary>
    public static float DrawScoreBug(CanvasItem c, Vector2 pos, string home, string away, int hs, int aws, string clock, string extra = "")
    {
        var disp = GameFonts.Display;
        float h = 46, slant = 14;
        string hs_ = hs.ToString(), as_ = aws.ToString();
        float homeW = disp.GetStringSize(home.ToUpperInvariant(), HorizontalAlignment.Left, -1, 26).X + 34;
        float awayW = disp.GetStringSize(away.ToUpperInvariant(), HorizontalAlignment.Left, -1, 26).X + 34;
        float scoreW = 108, clockW = 120;
        float total = homeW + scoreW + awayW + clockW;
        void Slab(float x, float w, Color col)
        {
            c.DrawColoredPolygon(new[] { new Vector2(x + slant, pos.Y), new Vector2(x + w + slant, pos.Y),
                new Vector2(x + w, pos.Y + h), new Vector2(x, pos.Y + h) }, col);
        }
        float x0 = pos.X;
        Slab(x0, homeW, UiTheme.Home.Darkened(0.15f));
        Slab(x0 + homeW, scoreW, new Color("0B121B"));
        Slab(x0 + homeW + scoreW, awayW, UiTheme.Away.Darkened(0.15f));
        Slab(x0 + homeW + scoreW + awayW, clockW, new Color("0B121B").Lightened(0.06f));
        c.DrawString(disp, new Vector2(x0 + slant + 8, pos.Y + 33), home.ToUpperInvariant(), HorizontalAlignment.Left, -1, 26, Colors.White);
        c.DrawString(disp, new Vector2(x0 + homeW + scoreW / 2 - 8 - disp.GetStringSize(hs_, HorizontalAlignment.Left, -1, 34).X, pos.Y + 36), hs_, HorizontalAlignment.Left, -1, 34, UiTheme.Accent);
        c.DrawString(disp, new Vector2(x0 + homeW + scoreW / 2 - 4, pos.Y + 34), "-", HorizontalAlignment.Left, -1, 26, UiTheme.Muted);
        c.DrawString(disp, new Vector2(x0 + homeW + scoreW / 2 + 12, pos.Y + 36), as_, HorizontalAlignment.Left, -1, 34, UiTheme.Accent);
        c.DrawString(disp, new Vector2(x0 + homeW + scoreW + 18, pos.Y + 33), away.ToUpperInvariant(), HorizontalAlignment.Left, -1, 26, Colors.White);
        c.DrawString(GameFonts.Semi, new Vector2(x0 + homeW + scoreW + awayW + 22, pos.Y + 30), clock, HorizontalAlignment.Left, -1, 24, UiTheme.Text);
        if (extra.Length > 0)
            c.DrawString(GameFonts.Semi, new Vector2(x0 + homeW + scoreW + awayW + 22, pos.Y + h + 18), extra, HorizontalAlignment.Left, -1, 16, UiTheme.Muted);
        c.DrawRect(new Rect2(x0 + slant, pos.Y + h, total - slant, 3), UiTheme.Accent, true);
        return total;
    }
}
