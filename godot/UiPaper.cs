using Godot;

// Shared notebook page, drawn only on first layout or resize.
public partial class UiPaper : Control
{
    public UiPaper() => MouseFilter = MouseFilterEnum.Ignore;
    public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }
    public override void _Draw() => DrawPage(this, new Rect2(Vector2.Zero, Size));

    public static void DrawPage(CanvasItem canvas, Rect2 rect)
    {
        canvas.DrawRect(rect, UiTheme.Paper);
        canvas.DrawRect(rect, UiTheme.PaperShade, false, 1);
        canvas.DrawLine(rect.Position + new Vector2(13, 0), rect.Position + new Vector2(13, rect.Size.Y),
            new Color(UiTheme.Rule, 0.55f), 1);
        for (float y = 18; y < rect.Size.Y - 10; y += 28)
            canvas.DrawCircle(rect.Position + new Vector2(5, y), 2, UiTheme.BenchLight);
    }
}
