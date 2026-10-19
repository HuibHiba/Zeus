using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Zeus.Sim;

// Opponent scouting in the TEST tab (the chosen opponent's build-up or attack spots, a line about his style, optional runs)
// and the "how it works" texts of the starter tactics.
public partial class Editor
{
    ScoutView _scout = null!;
    Label _scoutStyle = null!, _scoutAdapt = null!;
    AcceptDialog? _explainDialog;

    void BuildScout(VBoxContainer t)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6); t.AddChild(row);
        var group = new ButtonGroup();
        foreach (var phase in new[] { "opbouw", "aanval" })
        {
            var b = new Button { Text = PhaseLabel(phase), ToggleMode = true, ButtonGroup = group, ButtonPressed = phase == "opbouw", ThemeTypeVariation = "CompactButton", CustomMinimumSize = new Vector2(0, 28) };
            var shown = phase;
            b.Pressed += () => { _scout.Phase = shown; };
            row.AddChild(b);
        }
        var runs = new Button { Text = "☑ " + Loc.T("editor.runs"), ToggleMode = true, ButtonPressed = true, ThemeTypeVariation = "CompactButton", CustomMinimumSize = new Vector2(0, 28), TooltipText = Loc.T("editor.runs_tip") };
        runs.Toggled += on => { runs.Text = (on ? "☑ " : "☐ ") + Loc.T("editor.runs"); _scout.ShowRuns = on; };
        row.AddChild(runs);
        _scout = new ScoutView(); t.AddChild(_scout);
        _scoutStyle = Small(""); t.AddChild(_scoutStyle);
        _scoutAdapt = Small(""); _scoutAdapt.AddThemeColorOverride("font_color", UiTheme.Accent); t.AddChild(_scoutAdapt);
        _testOpponent.ItemSelected += _ => RefreshScout();
        RefreshScout();
    }

    // Career opponents adapt to your style (smart opponent v1): say so here, with the reason. Never breaks the scouting.
    string AdaptNote()
    {
        try
        {
            if (!GameSession.AdaptiveOpponent || SafeFile.Read(Season.CareerFile(), out var text) != SafeFile.ReadStatus.Ok) return "";
            var state = SeasonState.Parse(text);
            // Only when the tactic picked here is the one the next career opponent will really play.
            if (Competition.Adaptation(state) is not { } a || Competition.NextLiveMatch(state, true) is not { } next) return "";
            var nextFile = next.PlayerIsHome ? next.AwayTactic : next.HomeTactic;
            return System.IO.Path.GetFullPath(GameSession.TacticPath(nextFile)) == System.IO.Path.GetFullPath(Meta(_testOpponent)) ? Loc.T(a.ReasonKey) : "";
        }
        catch (Exception e) when (e is FormatException or System.IO.IOException) { return ""; }
    }

    void RefreshScout()
    {
        if (_testOpponent.ItemCount == 0) return;
        _scoutAdapt.Text = AdaptNote();
        try
        {
            var m = TacticEditorModel.LoadFile(Meta(_testOpponent));
            _scout.Show(m);
            string Level(string setting) { var v = m.GetTeamSetting(setting) ?? 0.5; return Loc.T(v < 0.34 ? "editor.scout_low" : v < 0.67 ? "editor.scout_mid" : "editor.scout_high"); }
            _scoutStyle.Text = Loc.T("editor.scout_style", Loc.Name(m.DefendingBlock), Level("pressing_intensiteit"), Level("tempo"));
        }
        catch (Exception e) { _scout.Show(null); _scoutStyle.Text = Loc.T("editor.scout_failed") + Loc.Error(e); }
    }

    // A submenu of the Open menu: the panel is too narrow for a fourth button.
    PopupMenu? _guidePopup;
    void AddGuideSubmenu()
    {
        if (_guidePopup is null)
        {
            _guidePopup = new PopupMenu { Name = "Guide" };
            var starters = GameSession.Tactics().Where(e => !e.IsOwn && GameSession.TacticExplanation(e.Path).Length > 0).ToArray();
            for (int i = 0; i < starters.Length; i++) _guidePopup.AddItem(starters[i].Label, i);
            _guidePopup.IdPressed += id => ShowExplanation(starters[(int)id].Path);
            _openPopup.AddChild(_guidePopup);
            if (GameSession.DebugEnvironment("ZEUS_EXPLAIN") is { Length: > 0 } debug) Callable.From(() => ShowExplanation(GameSession.TacticPath(debug))).CallDeferred();      // visual check
        }
        _openPopup.AddSubmenuNodeItem(Loc.T("editor.explain_menu"), _guidePopup);
    }

    void ShowExplanation(string path)
    {
        _explainDialog ??= new AcceptDialog { MinSize = new Vector2I(520, 0) };
        if (_explainDialog.GetParent() is null) AddChild(_explainDialog);
        _explainDialog.Title = Loc.T("editor.explain_title", GameSession.TacticLabel(path));
        _explainDialog.DialogText = GameSession.TacticExplanation(path);
        _explainDialog.DialogAutowrap = true;
        _explainDialog.GetOkButton().Text = Loc.T("common.ok");
        _explainDialog.PopupCentered();
    }
}

// A small pitch with the dots of one phase of a tactic, and the runs to the attack spots and from instructions.
public partial class ScoutView : Control
{
    const float ScoutWidth = 210;
    TacticEditorModel? _model;
    string _phase = "opbouw";
    bool _runs = true;

    public string Phase { set { _phase = value; QueueRedraw(); } }
    public bool ShowRuns { set { _runs = value; QueueRedraw(); } }

    public ScoutView() => CustomMinimumSize = new Vector2(ScoutWidth, ScoutWidth * 68 / 105f);

    public void Show(TacticEditorModel? model) { _model = model; QueueRedraw(); }

    public override void _Draw()
    {
        var size = new Vector2(ScoutWidth, ScoutWidth * 68 / 105f);
        GameArt.DrawPitch(this, new Rect2(Vector2.Zero, size), 0);
        if (_model is null) return;
        Vector2 At((double X, double Y) p) => new((float)p.X * size.X, (float)p.Y * size.Y);
        foreach (var id in _model.PlayerIds)
        {
            var from = At(_model.GetPosition(_phase, id));
            if (_runs)
            {
                var to = At(_model.GetPosition("aanval", id));
                if (_phase == "opbouw" && from.DistanceTo(to) > 14) Editor.DrawArrow(this, from, to, new Color(UiTheme.Good, 0.7f), 1.5f, false, 6);
                foreach (var instruction in _model.GetInstructions(_phase, id))
                    if (Editor.RunTarget(instruction, _model.GetPosition(_phase, id)) is { } target)
                        Editor.DrawArrow(this, from, At(target), new Color(UiTheme.Accent, 0.9f), 1.5f, true, 6);
            }
        }
        foreach (var id in _model.PlayerIds) DrawCircle(At(_model.GetPosition(_phase, id)), 5, UiTheme.Away);
    }
}
