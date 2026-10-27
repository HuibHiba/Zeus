// The mentor (Pike): a blocking card (Card) and a non-blocking subtitle bar with a queue (Say). Text comes from the language table
// ("mentor.<id>"); audio would live in data/voice/<lang>/<id>.ogg (not built yet). The portrait is drawn in code until real artwork exists.
using System;
using System.Collections.Generic;
using Godot;
using Zeus.Sim;

/// <summary>Placeholder drawing helpers (DrawCircle under a non-uniform DrawSetTransform drifted off-centre, so ellipses are polygons).</summary>
public static class Sketch
{
    public static void Ellipse(CanvasItem item, Vector2 c, float rx, float ry, Color fill, Color? line)
    {
        var points = new Vector2[49];
        for (int i = 0; i < 48; i++) points[i] = c + new Vector2(Mathf.Cos(i * Mathf.Tau / 48) * rx, Mathf.Sin(i * Mathf.Tau / 48) * ry);
        points[48] = points[0];
        item.DrawColoredPolygon(points, fill);
        if (line is Color outline) item.DrawPolyline(points, outline, 2f);
    }

    /// <summary>Flat cap hugging the top of the head ellipse (centre c, radii w and h), with a brim line.</summary>
    public static void Cap(CanvasItem item, Vector2 c, float w, float h, Color colour)
    {
        float edge = -h * .5f, half = w * Mathf.Sqrt(.75f);        // the head ellipse is this wide at y = -h/2
        var points = new List<Vector2>();
        for (int i = 0; i <= 24; i++) { float a = -5 * Mathf.Pi / 6 + i * (2 * Mathf.Pi / 3) / 24; points.Add(c + new Vector2(Mathf.Cos(a) * w, Mathf.Sin(a) * h)); }
        item.DrawColoredPolygon(points.ToArray(), colour);
        item.DrawLine(c + new Vector2(-half - 6, edge), c + new Vector2(half + 6, edge), UiTheme.Ink, 3f);
    }
}

public enum Expression { Neutral, Happy, Stern, Thoughtful, Surprised }

/// <summary>Placeholder head: oval face, cap, eyebrows by expression, mouth by <see cref="Mouth"/>. Same indices a PNG set would use later.</summary>
public partial class MentorPortrait : Control
{
    public Expression Expression { get; set; }
    public Mouth Mouth { get; set; }

    public override void _Draw()
    {
        var c = Size / 2 + new Vector2(0, Size.Y * .05f);
        float w = Size.X * .30f, h = Size.Y * .36f;
        Sketch.Ellipse(this, c, w, h, UiTheme.PaperShade, UiTheme.Ink);
        Sketch.Cap(this, c, w, h, new Color("1F2F4F"));
        float brow = Expression switch { Expression.Stern => 4f, Expression.Surprised => -10f, _ => 0f };
        float tilt = Expression == Expression.Stern ? 6f : 0f;
        foreach (float side in new[] { -1f, 1f })
        {
            var eye = c + new Vector2(side * w * .42f, -h * .1f);
            DrawCircle(eye, 3.5f, UiTheme.Ink);
            DrawLine(eye + new Vector2(-w * .2f, -14 + brow + tilt * side), eye + new Vector2(w * .2f, -14 + brow - tilt * side), UiTheme.Ink, 3f);
        }
        var mouth = c + new Vector2(0, h * .5f);
        switch (Mouth)
        {
            case Mouth.Closed: DrawLine(mouth + new Vector2(-w * .25f, 0), mouth + new Vector2(w * .25f, Expression == Expression.Happy ? -4 : 0), UiTheme.Ink, 3f); break;
            case Mouth.Small: DrawArc(mouth, w * .16f, 0, Mathf.Pi, 12, UiTheme.Ink, 3f); break;
            case Mouth.Open: Sketch.Ellipse(this, mouth, w * .18f, w * .22f, UiTheme.Ink, null); break;
            default: Sketch.Ellipse(this, mouth, w * .3f, w * .12f, UiTheme.Ink, null); break;
        }
    }
}

public partial class Mentor : CanvasLayer
{
    record Line(string Id, Expression Expression, string Text);
    static Mentor? _current;
    readonly Queue<Line> _queue = new();
    Control _root = null!;
    PanelContainer _bar = null!;
    MentorPortrait _face = null!;
    Label _name = null!, _text = null!;
    Line? _showing;
    double _elapsed;

    /// <summary>The text of "mentor.&lt;id&gt;", formatted with <paramref name="args"/>.</summary>
    static string TextOf(string id, object[] args) => Loc.T("mentor." + id, args);

    static Mentor Instance(Node owner)
    {
        if (_current is not null && IsInstanceValid(_current)) return _current;
        _current = new Mentor { Name = "Mentor", Layer = 50, ProcessMode = ProcessModeEnum.Always };
        owner.GetTree().Root.AddChild(_current);
        return _current;
    }

    /// <summary>Non-blocking subtitle; lines wait their turn.</summary>
    public static void Say(Node owner, string id, Expression expression = Expression.Neutral, params object[] args) =>
        Instance(owner).Enqueue(new Line(id, expression, TextOf(id, args)));

    /// <summary>Like <see cref="Say"/>, but only the first time in this profile (remembered in onboarding.json). False when already heard.</summary>
    public static bool First(Node owner, string id, Expression expression = Expression.Neutral, params object[] args)
    {
        if (OnboardingProgress.State.HasSeen(id)) return false;
        OnboardingProgress.MarkSeen(id);
        Say(owner, id, expression, args);
        return true;
    }

    /// <summary>Hides the subtitle bar and forgets queued lines (a screen change must not carry a match hint into the menu).</summary>
    public static void Dismiss()
    {
        if (_current is null || !IsInstanceValid(_current)) return;
        _current._queue.Clear(); _current._showing = null; _current._bar.Visible = false;
    }

    /// <summary>Drops waiting lines (fast-forward, leaving the screen); the one on screen finishes.</summary>
    public static void DropWaiting() { if (_current is not null && IsInstanceValid(_current)) _current._queue.Clear(); }

    /// <summary>Blocking card with the portrait, "Got it" and an optional action after closing. Pauses the tree unless <paramref name="pause"/> is false.</summary>
    public static void Card(Node owner, string id, Action? onClose = null, bool pause = true, Expression expression = Expression.Neutral, params object[] args)
    {
        var layer = new CanvasLayer { Layer = 51, ProcessMode = ProcessModeEnum.Always };
        var tree = owner.GetTree();
        bool was = tree.Paused;
        if (pause) tree.Paused = true;
        var shade = new ColorRect { Color = new Color(0, 0, 0, .62f), MouseFilter = Control.MouseFilterEnum.Stop };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect); layer.AddChild(shade);
        var frame = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }; UiFrame.Center(frame); layer.AddChild(frame);
        var card = new PanelContainer { Position = new Vector2(400, 300) }; UiTheme.ApplyPaper(card); frame.AddChild(card);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 20); card.AddChild(row);
        row.AddChild(new MentorPortrait { CustomMinimumSize = new Vector2(176, 220), Expression = expression });
        var col = new VBoxContainer { CustomMinimumSize = new Vector2(560, 0) }; col.AddThemeConstantOverride("separation", 12); row.AddChild(col);
        col.AddChild(UiTheme.Label(Loc.T("mentor.name"), 30, UiTheme.Ink, display: true));
        var text = UiTheme.Label(TextOf(id, args), UiTheme.Body, UiTheme.Ink);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart; text.CustomMinimumSize = new Vector2(540, 0); col.AddChild(text);
        var ok = UiTheme.Primary(Loc.T("mentor.ok")); ok.CustomMinimumSize = new Vector2(180, 50);
        ok.Pressed += () => { if (pause) tree.Paused = was; layer.QueueFree(); onClose?.Invoke(); };
        col.AddChild(ok);
        tree.Root.AddChild(layer);
        ok.GrabFocus();
    }

    void Enqueue(Line line)
    {
        _queue.Enqueue(line);
        if (_showing is null && IsInsideTree()) Next();
    }

    public override void _Ready()
    {
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }; UiFrame.Center(_root); AddChild(_root);
        _bar = new PanelContainer { Position = new Vector2(250, 706), CustomMinimumSize = new Vector2(1100, 112), Visible = false };
        UiTheme.ApplyPaper(_bar);
        _bar.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) _elapsed = double.MaxValue; };      // a click closes the bar
        _root.AddChild(_bar);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 16); _bar.AddChild(row);
        _face = new MentorPortrait { CustomMinimumSize = new Vector2(96, 120), MouseFilter = Control.MouseFilterEnum.Ignore }; row.AddChild(_face);
        var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; row.AddChild(col);
        _name = UiTheme.Label(Loc.T("mentor.name"), UiTheme.Small, UiTheme.Graphite); col.AddChild(_name);
        _text = UiTheme.Label("", UiTheme.Body, UiTheme.Ink); _text.AutowrapMode = TextServer.AutowrapMode.WordSmart; col.AddChild(_text);
        if (_queue.Count > 0) Next();      // lines queued before the node entered the tree
    }

    void Next()
    {
        if (!_queue.TryDequeue(out var line)) { _showing = null; _bar.Visible = false; return; }
        _showing = line; _elapsed = 0;
        _text.Text = line.Text; _face.Expression = line.Expression; _bar.Visible = true;
    }

    public override void _Process(double delta)
    {
        if (_showing is null) return;
        _elapsed += delta;
        var mouth = MentorTiming.MouthAt(_showing.Text, _elapsed);
        if (mouth != _face.Mouth) { _face.Mouth = mouth; _face.QueueRedraw(); }
        if (_elapsed >= MentorTiming.Duration(_showing.Text)) Next();
    }
}
