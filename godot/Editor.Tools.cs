using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Zeus.Sim;

// Line helpers for the phase tab (move the defence or the whole team, make it narrower, wider, shorter or longer) and the
// warnings under the pitch. Every helper is one undo step; the keeper never moves.
public partial class Editor
{
    static readonly HashSet<string> DefenceRoles = new() { "centrale_verdediger", "back" };
    static readonly HashSet<string> MidfieldRoles = new() { "controlerende_middenvelder", "box_to_box", "aanvallende_middenvelder" };
    const double MaxLineGap = 25, MaxTeamLength = 55;      // metres; beyond this the opponent gets a lot of room between the lines

    void AddLineTools(VBoxContainer t)
    {
        t.AddChild(Small(Loc.T("editor.line_tools_help")));
        var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; t.AddChild(grid);
        foreach (var (row, less, lessAction, more, moreAction) in new (string, string, Action, string, Action)[]
        {
            ("editor.line_defence", "editor.higher", () => Shift(DefenceRoles, 2), "editor.deeper", () => Shift(DefenceRoles, -2)),
            ("editor.line_team", "editor.higher", () => Shift(null, 3), "editor.deeper", () => Shift(null, -3)),
            ("editor.line_width", "editor.narrower", () => Stretch(false, 0.9), "editor.wider", () => Stretch(false, 1.1)),
            ("editor.line_length", "editor.shorter", () => Stretch(true, 0.9), "editor.longer", () => Stretch(true, 1.1)),
        })
        {
            var label = UiTheme.Label(Loc.T(row), UiTheme.Small, UiTheme.Muted); label.CustomMinimumSize = new Vector2(92, 0); grid.AddChild(label);
            foreach (var (key, action) in new[] { (less, lessAction), (more, moreAction) })
            {
                var b = UiTheme.Secondary(Loc.T(key)); b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; b.ClipText = true;
                b.TooltipText = Loc.T(row) + ": " + Loc.T(key);
                b.Pressed += () => Try(action);
                grid.AddChild(b);
            }
        }
    }

    IEnumerable<string> Outfield(HashSet<string>? roles) =>
        _model.PlayerIds.Where(id => _model.GetRole(id) != "keeper" && (roles is null || roles.Contains(_model.GetRole(id)))).ToList();

    void Shift(HashSet<string>? roles, double metres)
    {
        foreach (var id in Outfield(roles))
        {
            var (x, y) = _model.GetPosition(_phase, id);
            _model.SetPosition(_phase, id, x + metres / L, y);
        }
    }

    // Lengthways around the middle of the outfield players, sideways around the middle of the pitch.
    void Stretch(bool lengthways, double factor)
    {
        var ids = Outfield(null).ToList();
        if (ids.Count == 0) return;
        double centre = lengthways ? ids.Average(id => _model.GetPosition(_phase, id).X) : 0.5;
        foreach (var id in ids)
        {
            var (x, y) = _model.GetPosition(_phase, id);
            if (lengthways) x = centre + (x - centre) * factor; else y = centre + (y - centre) * factor;
            _model.SetPosition(_phase, id, x, y);
        }
    }

    /// <summary>The most important problem of this phase in plain words, or null.</summary>
    string? PhaseWarning()
    {
        int keepers = _model.PlayerIds.Count(id => _model.GetRole(id) == "keeper");
        if (keepers == 0) return Loc.T("editor.warn_no_keeper");
        if (keepers > 1) return Loc.T("editor.warn_keepers", keepers);
        double Depth(HashSet<string>? roles) { var ids = Outfield(roles).ToList(); return ids.Count == 0 ? double.NaN : ids.Average(id => _model.GetPosition(_phase, id).X) * L; }
        double gap = Depth(MidfieldRoles) - Depth(DefenceRoles);
        if (gap > MaxLineGap) return Loc.T("editor.warn_gap", gap);
        var xs = Outfield(null).Select(id => _model.GetPosition(_phase, id).X * L).ToList();
        if (xs.Count > 1 && xs.Max() - xs.Min() > MaxTeamLength) return Loc.T("editor.warn_length", xs.Max() - xs.Min());
        return null;
    }
}
