using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Zeus.Sim;

// Selecting several players (rectangle on an empty spot, Shift+click), moving them together as one undo step, and the optional
// snap grid (G). One selected player is _selected; two or more are _group and then _selected stays null.
public partial class Editor
{
    const double GridMetres = 2.5;
    readonly HashSet<string> _group = new();
    Dictionary<string, (double X, double Y)> _dragFrom = new();
    Vector2? _marquee;                   // where the rectangle started, null when none is being dragged
    Vector2 _marqueeEnd;
    bool _snap = Loc.Setting("editor_grid", "off") == "on";
    Button _gridToggle = null!;

    List<string> Members() => _group.Count > 0 ? _group.ToList() : _selected is null ? new List<string>() : new List<string> { _selected };

    void SetSelection(IEnumerable<string> ids)
    {
        var set = ids.ToList();
        _group.Clear();
        _selected = set.Count == 1 ? set[0] : null;
        if (set.Count > 1) foreach (var id in set) _group.Add(id);
    }

    void AddGridToggle(HBoxContainer bar)
    {
        _gridToggle = new Button { ToggleMode = true, ButtonPressed = _snap, ThemeTypeVariation = "CompactButton", TooltipText = Loc.T("editor.grid_tip"), CustomMinimumSize = new Vector2(0, 36) };
        _gridToggle.Toggled += SetSnap;
        bar.AddChild(_gridToggle);
        SetSnap(_snap);
    }

    void SetSnap(bool on)
    {
        _snap = on;
        _gridToggle.SetPressedNoSignal(on);
        _gridToggle.Text = (on ? "☑ " : "☐ ") + Loc.T("editor.grid");
        Loc.SetSetting("editor_grid", on ? "on" : "off");
        QueueRedraw();
    }

    (double X, double Y) Snapped(double x, double y)
    {
        if (!_snap) return (x, y);
        return (Math.Round(x * L / GridMetres) * GridMetres / L, Math.Round(y * W / GridMetres) * GridMetres / W);
    }

    void PressPitch(string? hit, Vector2 at, bool shift)
    {
        _drag = hit; _marquee = null;
        if (hit is not null)
        {
            if (shift)
            {
                var set = Members();
                if (!set.Remove(hit)) set.Add(hit); else _drag = null;
                SetSelection(set);
            }
            else if (!_group.Contains(hit)) SetSelection(new[] { hit });
            if (_drag is not null) _dragFrom = Members().ToDictionary(id => id, id => _model.GetPosition(_phase, id));
        }
        else if (PitchRect().HasPoint(at))
        {
            if (!shift) SetSelection(Array.Empty<string>());      // an empty spot shows the runs of the whole team again
            _marquee = _marqueeEnd = at;
        }
    }

    void FinishMarquee()
    {
        if (_marquee is not { } start) return;
        _marquee = null;
        var box = new Rect2(start, Vector2.Zero).Expand(_marqueeEnd);
        if (box.Size.Length() >= 6)
            SetSelection(Members().Concat(_model.PlayerIds.Where(id => { var (x, y) = _model.GetPosition(_phase, id); return box.HasPoint(ToScreen(x, y)); })).Distinct().ToList());
        RefreshPlayer();
        QueueRedraw();
    }

    void DragTo(Vector2 mouse, bool first)
    {
        var (lx, ly) = _dragFrom[_drag!];
        var (x, y) = FromScreen(mouse);
        (x, y) = Snapped(x, y);
        var ids = _dragFrom.Keys.ToList();
        Try(() => MoveGroup(_dragFrom, x - lx, y - ly), refresh: first, undoKey: "drag:" + string.Join(",", ids) + _phase);      // a first move can detach an alias: rebuild the chips once
    }

    // The whole group moves by the same amount and stays on the pitch as one shape.
    void MoveGroup(IReadOnlyDictionary<string, (double X, double Y)> from, double dx, double dy)
    {
        dx = Math.Clamp(dx, -from.Values.Min(p => p.X), 1 - from.Values.Max(p => p.X));
        dy = Math.Clamp(dy, -from.Values.Min(p => p.Y), 1 - from.Values.Max(p => p.Y));
        foreach (var (id, p) in from) _model.SetPosition(_phase, id, p.X + dx, p.Y + dy);
    }

    // Visual check: ZEUS_SELECT_RECT="x0,y0,x1,y1" (screen pixels) selects like a finished rectangle and shows it; ZEUS_GRID=1 turns the grid on.
    void DebugSelection()
    {
        if (GameSession.DebugEnvironment("ZEUS_GRID").Length > 0) SetSnap(true);
        var v = GameSession.DebugEnvironment("ZEUS_SELECT_RECT").Split(',').Select(t => float.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : float.NaN).ToArray();
        if (v.Length != 4 || v.Any(float.IsNaN) || _model is null) return;
        _marquee = new Vector2(v[0], v[1]); _marqueeEnd = new Vector2(v[2], v[3]);
        var box = new Rect2(_marquee.Value, Vector2.Zero).Expand(_marqueeEnd);
        SetSelection(_model.PlayerIds.Where(id => { var (x, y) = _model.GetPosition(_phase, id); return box.HasPoint(ToScreen(x, y)); }));
        RefreshPlayer();
        if (GameSession.DebugEnvironment("ZEUS_SELECT_DONE").Length > 0) _marquee = null;
    }

    void DrawGrid(Rect2 field)
    {
        if (!_snap) return;
        var soft = new Color(1, 1, 1, 0.13f);
        for (double m = GridMetres; m < L; m += GridMetres) DrawLine(new Vector2(field.Position.X + (float)m * Px, field.Position.Y), new Vector2(field.Position.X + (float)m * Px, field.End.Y), soft, 1);
        for (double m = GridMetres; m < W; m += GridMetres) DrawLine(new Vector2(field.Position.X, field.Position.Y + (float)m * Px), new Vector2(field.End.X, field.Position.Y + (float)m * Px), soft, 1);
    }

    void DrawMarquee()
    {
        if (_marquee is not { } start) return;
        var box = new Rect2(start, Vector2.Zero).Expand(_marqueeEnd);
        DrawRect(box, new Color(UiTheme.Accent, 0.15f), true);
        DrawRect(box, UiTheme.Accent, false, 1.5f);
    }
}
