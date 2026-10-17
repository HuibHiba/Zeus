// Full-screen menu backdrop: floodlit pitch, the chosen formations drifting on it, a fade for legibility, and the wordmark.
using System;
using Godot;
using Zeus.Sim;

public partial class MenuBackdrop : Control
{
    public string Title = GameInfo.ShortTitle;
    TacticEditorModel? _home, _away;
    const double _t = 0;

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

    public override void _Process(double delta) { }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), UiTheme.Bench);
        for (int y = 0; y < Size.Y; y += 56)
            DrawLine(new Vector2(0, y), new Vector2(Size.X, y), UiTheme.BenchLight, 1);
        DrawRect(new Rect2(62, 38, 728, 690), new Color(0, 0, 0, 0.35f));
        DrawRect(new Rect2(54, 30, 728, 690), UiTheme.Cover);
        DrawRect(new Rect2(68, 30, 8, 690), UiTheme.BenchLight);
        DrawRect(new Rect2(754, 30, 10, 690), UiTheme.Bench);
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
        // The original bolt is replaced by a paper label on the notebook cover.
        DrawRect(new Rect2(102, 66, 620, 176), UiTheme.Paper);
        var words = Title.Split(' ', 2);
        DrawString(GameFonts.Display, new Vector2(126, 139), words[0].ToUpperInvariant(), HorizontalAlignment.Left, 560, 76, UiTheme.Ink);
        DrawString(GameFonts.Display, new Vector2(126, 215), words.Length > 1 ? words[1].ToUpperInvariant() : "", HorizontalAlignment.Left, 560, 76, UiTheme.Ink);
        DrawRect(new Rect2(510, 222, 180, 6), UiTheme.Accent);
    }
}
