// Full-screen menu backdrop: floodlit pitch, the chosen formations drifting on it, a fade for legibility, and the wordmark.
using System;
using Godot;
using Zeus.Sim;

public partial class MenuBackdrop : Control
{
    public string Title = GameInfo.ShortTitle;
    TacticEditorModel? _home, _away;
    double _t;
    static GradientTexture2D? _fade;

    /// <summary>Screen area covered by UI; players inside it are not drawn.</summary>
    public Rect2 Exclude;

    public void SetTeams(string homePath, string awayPath)
    {
        _home = TryLoad(homePath);
        _away = TryLoad(awayPath);
    }

    static TacticEditorModel? TryLoad(string path)
    {
        try { return TacticEditorModel.LoadFile(path); } catch { return null; }
    }

    public override void _Process(double delta) { _t += delta; QueueRedraw(); }

    public override void _Draw()
    {
        var area = new Rect2(Vector2.Zero, Size);
        DrawRect(area, UiTheme.Bg, true);
        float fw = 1500, fh = fw * 68f / 105f;
        var field = new Rect2(10, Size.Y / 2 - fh / 2, fw, fh);
        GameArt.DrawPitch(this, field, 60);

        DrawTeam(_home, field, UiTheme.Home, mirror: false, phase: 0);
        DrawTeam(_away, field, UiTheme.Away, mirror: true, phase: 3.1);

        _fade ??= new GradientTexture2D
        {
            Fill = GradientTexture2D.FillEnum.Linear, FillFrom = new Vector2(0, 0), FillTo = new Vector2(1, 0), Width = 256, Height = 4,
            Gradient = new Gradient { Offsets = new[] { 0f, 0.45f, 1f }, Colors = new[] { new Color(UiTheme.Bg, 0.9f), new Color(UiTheme.Bg, 0.5f), new Color(UiTheme.Bg, 0f) } },
        };
        DrawTextureRect(_fade, new Rect2(0, 0, 900, Size.Y), false);
        GameArt.DrawVignette(this, area, 0.7f);
        DrawBrand();
    }

    void DrawTeam(TacticEditorModel? m, Rect2 field, Color kit, bool mirror, double phase)
    {
        if (m is null) return;
        int i = 0;
        foreach (var id in m.PlayerIds)
        {
            var (x, y) = m.GetPosition("opbouw", id);
            if (mirror) { x = 1 - x; y = 1 - y; }
            var p = field.Position + new Vector2((float)x * field.Size.X, (float)y * field.Size.Y);
            p += new Vector2((float)Math.Sin(_t * 0.9 + i + phase) * 6, (float)Math.Cos(_t * 0.7 + i * 1.3 + phase) * 5);
            if (Exclude.HasPoint(p)) { i++; continue; }          // keep the area under the setup panel calm
            GameArt.DrawPlayer(this, p, kit, id, mirror ? Math.PI : 0, 16);
            i++;
        }
    }

    void DrawBrand()
    {
        // Lightning bolt + wordmark.
        var o = new Vector2(70, 62);
        float s = 1.55f;
        Vector2[] bolt = { new(34, 0), new(2, 52), new(22, 52), new(10, 100), new(58, 38), new(34, 38), new(52, 0) };
        var pts = new Vector2[bolt.Length];
        for (int i = 0; i < bolt.Length; i++) pts[i] = o + bolt[i] * s;
        DrawColoredPolygon(pts, UiTheme.Accent);
        DrawString(GameFonts.Display, o + new Vector2(120, 130), Title, HorizontalAlignment.Left, 610, 66, UiTheme.Text);
        DrawRect(new Rect2(o.X + 124, o.Y + 148, 190, 5), UiTheme.Accent, true);
        DrawString(GameFonts.Semi, o + new Vector2(124, 190), Loc.T("menu.tagline"), HorizontalAlignment.Left, -1, 22, UiTheme.Muted);
    }
}
