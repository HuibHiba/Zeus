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
    public override void _Ready() => UiFrame.Follow(this, QueueRedraw);      // the bench follows the screen size

    public override void _Draw()
    {
        var screen = new Rect2(-GetGlobalTransformWithCanvas().Origin, GetViewportRect().Size);      // the bench fills the whole screen around the centred menu (UiFrame)
        DrawRect(screen, UiTheme.Bench);
        for (float y = screen.Position.Y; y < screen.End.Y; y += 56)
            DrawLine(new Vector2(screen.Position.X, y), new Vector2(screen.End.X, y), UiTheme.BenchLight, 1);
        DrawRect(new Rect2(102, 48, 728, 810), new Color(0, 0, 0, 0.35f));     // notebook cover, 1600x900 layout (see Menu.cs)
        DrawRect(new Rect2(94, 40, 728, 810), UiTheme.Cover);
        DrawRect(new Rect2(108, 40, 8, 810), UiTheme.BenchLight);
        DrawRect(new Rect2(794, 40, 10, 810), UiTheme.Bench);
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
        DrawRect(new Rect2(142, 80, 620, 176), UiTheme.Paper);
        var words = Title.Split(' ', 2);
        DrawString(GameFonts.Display, new Vector2(166, 153), words[0].ToUpperInvariant(), HorizontalAlignment.Left, 560, 76, UiTheme.Ink);
        DrawString(GameFonts.Display, new Vector2(166, 229), words.Length > 1 ? words[1].ToUpperInvariant() : "", HorizontalAlignment.Left, 560, 76, UiTheme.Ink);
        DrawRect(new Rect2(550, 236, 180, 6), UiTheme.Accent);
    }
}
