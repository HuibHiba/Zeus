// Where a team spends its time: positions sampled from match snapshots, normalised so every team attacks to the right.
using System;
using Godot;
using Zeus.Sim;
using Side = Zeus.Sim.Side;

public sealed class Heatmap
{
    public const int Cols = 21, Rows = 14;
    public readonly double[,] Cells = new double[Cols, Rows];
    public double Total { get; private set; }

    /// <summary>In period 1 the home side attacks to +x; the sides swap after half-time.</summary>
    public static bool AttacksRight(Side team, int period) => (team == Side.Home) == (period % 2 == 1);

    /// <summary>Adds one sample at a world position (metres, origin at the centre spot).</summary>
    public void Add(double x, double y, bool attacksRight)
    {
        if (!attacksRight) { x = -x; y = -y; }
        int cx = (int)Math.Floor((x + Pitch.Length / 2) / Pitch.Length * Cols);
        int cy = (int)Math.Floor((y + Pitch.Width / 2) / Pitch.Width * Rows);
        if (cx < 0 || cy < 0 || cx >= Cols || cy >= Rows) return;      // ball boy territory: ignore
        Cells[cx, cy]++;
        Total++;
    }

    /// <summary>Samples every player of <paramref name="team"/> that is still on the pitch.</summary>
    public void AddSnapshot(Snapshot snapshot, Side team)
    {
        bool right = AttacksRight(team, snapshot.Period);
        foreach (var p in snapshot.Players)
            if (p.Team == team && !p.SentOff) Add(p.Pos.X, p.Pos.Y, right);
    }

    public void Merge(Heatmap other)
    {
        for (int x = 0; x < Cols; x++) for (int y = 0; y < Rows; y++) Cells[x, y] += other.Cells[x, y];
        Total += other.Total;
    }

    /// <summary>Share of samples in the own half, middle third and attacking third (x from the own goal).</summary>
    public (double Back, double Middle, double Front) Thirds()
    {
        if (Total <= 0) return (0, 0, 0);
        double a = 0, b = 0, c = 0;
        for (int x = 0; x < Cols; x++) for (int y = 0; y < Rows; y++)
        {
            if (x < Cols / 3) a += Cells[x, y]; else if (x < 2 * Cols / 3) b += Cells[x, y]; else c += Cells[x, y];
        }
        return (a / Total, b / Total, c / Total);
    }
}

/// <summary>A small pitch with a team's heat on it.</summary>
public partial class HeatmapView : Control
{
    Heatmap? _heat;
    Color _color = UiTheme.Home;
    string _title = "";

    float _scale = 1f;

    public HeatmapView() => CustomMinimumSize = new Vector2(330, 246);      // title band 30 + pitch 214 (330 x 68/105)

    /// <summary>Shrinks the pitch (not the text) so the view fits in a tighter layout; 1 = original 330 x 232.</summary>
    public float Scale { get => _scale; set { _scale = value; CustomMinimumSize = new Vector2(330, 246) * value; QueueRedraw(); } }

    public void Show(Heatmap heat, Color color, string title)
    {
        _heat = heat; _color = color; _title = title;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var field = new Rect2(0, 30, 330, 330f * 68f / 105f);
        DrawString(GameFonts.Display, new Vector2(0, 22 * _scale), _title.ToUpperInvariant(), HorizontalAlignment.Left, -1, 22, UiTheme.PaperColor(_color));
        DrawSetTransform(Vector2.Zero, 0, new Vector2(_scale, _scale));
        GameArt.DrawPitch(this, field, 0);
        if (_heat is null || _heat.Total <= 0) { DrawSetTransform(Vector2.Zero, 0, Vector2.One); return; }
        double max = 0;
        for (int x = 0; x < Heatmap.Cols; x++) for (int y = 0; y < Heatmap.Rows; y++) max = Math.Max(max, _heat.Cells[x, y]);
        float cw = field.Size.X / Heatmap.Cols, ch = field.Size.Y / Heatmap.Rows;
        for (int x = 0; x < Heatmap.Cols; x++)
            for (int y = 0; y < Heatmap.Rows; y++)
            {
                float t = (float)Math.Sqrt(_heat.Cells[x, y] / max);          // sqrt keeps quiet zones visible
                if (t < 0.04f) continue;
                DrawRect(new Rect2(field.Position.X + x * cw, field.Position.Y + y * ch, cw + 0.5f, ch + 0.5f), new Color(_color, 0.08f + 0.62f * t), true);
            }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        DrawString(GameFonts.Semi, new Vector2(field.Position.X + field.Size.X - 90 / _scale, field.End.Y - 6) * _scale, Loc.T("common.attack_direction"), HorizontalAlignment.Left, -1, 15, new Color(1, 1, 1, 0.8f));
    }
}
