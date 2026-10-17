using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Zeus.Sim;

// Runs on the pitch, like the tactic boards of FIFA and Football Manager: solid arrows from each player's spot in this phase
// to the spot in a chosen next phase, dashed arrows for instructions that make a player run, and a short animation of the
// switch. With a player selected only his arrows are shown; click an empty spot of the pitch to see the whole team again.
public partial class Editor
{
    const double AnimSeconds = 2.6, AnimHold = 0.4, AnimMove = 1.7;
    bool _showRuns = true;
    double _animT = -1;                  // seconds into the animation, -1 when it is not playing
    string _runFrom = "";                // the phase _runTo was chosen for; another phase picks its natural next phase again
    OptionButton _runTo = null!;
    Button _runsToggle = null!;

    // The phase a team usually goes to from here; block variants behave like 'verdedigen'.
    static string NaturalNext(string phase) => (phase.StartsWith("verdedigen", StringComparison.Ordinal) ? "verdedigen" : phase) switch
    {
        "opbouw" => "aanval", "aanval" => "balverlies", "balverlies" => "verdedigen",
        "verdedigen" => "balwinst", "balwinst" => "aanval", _ => "opbouw"
    };

    void BuildRunBar()
    {
        // The hint wraps into two small lines on the left, so the bar with undo and the run controls fits under the pitch.
        _phaseHint.AddThemeFontSizeOverride("font_size", UiTheme.Small);
        _phaseHint.AutowrapMode = TextServer.AutowrapMode.WordSmart; _phaseHint.Position = new Vector2(75, 736); _phaseHint.Size = new Vector2(330, 42);
        var bar = new HBoxContainer { Position = new Vector2(410, 736), Size = new Vector2(715, 36), Alignment = BoxContainer.AlignmentMode.End };
        bar.AddThemeConstantOverride("separation", 6);
        AddChild(bar);
        foreach (var (text, tip, action) in new (string, string, Action)[] { ("↶", Loc.T("editor.undo"), Undo), ("↷", Loc.T("editor.redo"), Redo) })
        {
            var b = new Button { Text = text, TooltipText = tip, ThemeTypeVariation = "CompactButton", CustomMinimumSize = new Vector2(40, 36) };
            b.Pressed += action; bar.AddChild(b);
        }
        bar.AddChild(new Control { CustomMinimumSize = new Vector2(10, 0) });
        _runsToggle = new Button { Text = Loc.T("editor.runs"), ToggleMode = true, ButtonPressed = _showRuns, ThemeTypeVariation = "CompactButton",
            TooltipText = Loc.T("editor.runs_tip"), CustomMinimumSize = new Vector2(0, 36) };
        _runsToggle.Toggled += on => { _showRuns = on; _runsToggle.Text = (on ? "☑ " : "☐ ") + Loc.T("editor.runs"); QueueRedraw(); };
        _runsToggle.Text = "☑ " + Loc.T("editor.runs");
        bar.AddChild(_runsToggle);
        AddGridToggle(bar);
        _runTo = new OptionButton { FitToLongestItem = false, ClipText = true, ThemeTypeVariation = "CompactButton", CustomMinimumSize = new Vector2(200, 36), TooltipText = Loc.T("editor.runs_to_tip") };
        _runTo.ItemSelected += _ => { _runFrom = _phase; QueueRedraw(); };
        bar.AddChild(_runTo);
        var play = new Button { Text = Loc.T("editor.play_runs"), TooltipText = Loc.T("editor.play_runs_tip"), ThemeTypeVariation = "CompactButton", CustomMinimumSize = new Vector2(0, 36) };
        play.Pressed += PlayRuns;
        bar.AddChild(play);
        UiTheme.Apply(bar);
        if (double.TryParse(GameSession.DebugEnvironment("ZEUS_ANIM"), out var t)) _animT = t;      // visual check: freeze the animation at t seconds
    }

    void RefreshRunTargets()
    {
        var keep = _runFrom == _phase ? Meta(_runTo) : NaturalNext(_phase);
        // A linked phase has the same spots and so no arrows: follow the chain to the first phase where the team moves.
        for (int i = 0; i < 6 && _runFrom != _phase && _model.AllPhases.Contains(keep)
             && _model.PlayerIds.All(id => _model.GetPosition(keep, id) == _model.GetPosition(_phase, id)); i++) keep = NaturalNext(keep);
        _runTo.Clear();
        foreach (var p in _model.AllPhases.Where(p => p != _phase).OrderBy(p => Array.IndexOf(PhaseOrder, p)))
        { _runTo.AddItem(Loc.T("editor.runs_to", PhaseLabel(p))); _runTo.SetItemMetadata(_runTo.ItemCount - 1, p); }
        SelectMeta(_runTo, keep);
        if (Meta(_runTo) != keep && _runTo.ItemCount > 0) _runTo.Select(0);
    }

    void PlayRuns()
    {
        if (_model is null || _runTo.ItemCount == 0) return;
        _animT = 0;
        QueueRedraw();
    }

    void StepAnimation(double delta)
    {
        if (_animT < 0 || GameSession.DebugEnvironment("ZEUS_ANIM").Length > 0) return;
        _animT += delta;
        if (_animT > AnimSeconds) _animT = -1;
        QueueRedraw();
    }

    bool RunKey(InputEventKey key)
    {
        if (!key.Pressed || key.Echo || key.CtrlPressed || key.AltPressed) return false;
        if (key.Keycode == Key.L) { _runsToggle.ButtonPressed = !_runsToggle.ButtonPressed; return true; }
        if (key.Keycode == Key.P) { PlayRuns(); return true; }
        return false;
    }

    /// <summary>Where the dot is drawn: its spot in this phase, or on its way to the target phase while the animation plays.</summary>
    Vector2 DotPosition(string id)
    {
        var (x, y) = _model.GetPosition(_phase, id);
        var here = ToScreen(x, y);
        if (_animT < 0 || Meta(_runTo) is not { Length: > 0 } to) return here;
        var (tx, ty) = _model.GetPosition(to, id);
        double t = Math.Clamp((_animT - AnimHold) / AnimMove, 0, 1);
        t = t * t * (3 - 2 * t);            // ease in and out
        return here.Lerp(ToScreen(tx, ty), (float)t);
    }

    IEnumerable<string> RunPlayers() => Members() is { Count: > 0 } picked ? picked : _model.PlayerIds;

    static Color PhaseColor(string phase) => phase switch
    {
        "opbouw" or "aanval" or "balwinst" => UiTheme.Good,
        "stilstaand" => UiTheme.Muted,
        _ => UiTheme.Away
    };

    void DrawRuns()
    {
        if (!_showRuns && _animT < 0) return;
        var to = Meta(_runTo);
        bool one = Members().Count > 0;
        foreach (var id in RunPlayers())
        {
            var (x, y) = _model.GetPosition(_phase, id);
            var from = ToScreen(x, y);
            if (to.Length > 0)
            {
                var (tx, ty) = _model.GetPosition(to, id);
                var end = ToScreen(tx, ty);
                var color = new Color(PhaseColor(to), one ? 0.95f : 0.6f);
                DrawCircle(end, 7, new Color(color, 0.18f));
                DrawArc(end, 7, 0, Mathf.Tau, 20, color, 1.5f);
                if (from.DistanceTo(end) > 16) Arrow(from, end - (end - from).Normalized() * 7, color, one ? 3 : 2, dashed: false, gap: 14);
            }
            foreach (var instruction in _model.GetInstructions(_phase, id))
                if (InstructionRun(instruction, x, y) is { } target)
                    Arrow(from, ToScreen(target.X, target.Y), new Color(UiTheme.Accent, one ? 0.95f : 0.7f), one ? 2.5f : 2, dashed: true, gap: 14);
        }
    }

    // Instructions that make a player run; the arrow shows the direction and a typical length, not an exact path.
    static (double X, double Y)? InstructionRun(string instruction, double x, double y)
    {
        double M(double metres) => metres / L;
        double toCentre = 0.5 - y;
        return instruction switch
        {
            "loop_diep" => (x + M(16), y),
            "kom_op" => (x + M(10), y),
            "start_pressing" => (x + M(7), y),
            "laat_zakken" => (x - M(10), y),
            "dek_ruimte_achter_lijn" => (x - M(7), y),
            "blijf_breed" => (x, y < 0.5 ? 0.05 : 0.95),
            "sluit_naar_binnen" => (x, y + toCentre * 0.6),
            "dribbel_in" => (x + M(8), y + toCentre * 0.5),
            "loop_zestien_in" => (Math.Max(x, 0.9), Math.Clamp(y, 0.38, 0.62)),
            _ => null
        };
    }

    void Arrow(Vector2 from, Vector2 to, Color color, float width, bool dashed, float gap)
    {
        var dir = (to - from).Normalized();
        var start = from + dir * gap;          // leave the dot itself free
        if (start.DistanceTo(to) < 12) return;
        var tip = to - dir * 2;
        if (dashed) DrawDashedLine(start, tip - dir * 8, color, width, 6);
        else DrawLine(start, tip - dir * 8, color, width, true);
        var n = new Vector2(-dir.Y, dir.X);
        DrawColoredPolygon(new[] { tip, tip - dir * 11 + n * 5, tip - dir * 11 - n * 5 }, color);
    }
}
