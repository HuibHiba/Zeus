// Selection screen (v0, VOORLOPIG): the 18-player squad of a level fitted to a tactic, the tactic's demand per tactical concept,
// the team's familiarity per concept (user://zeus_vertrouwdheid.json) and training per concept. The class is not called Squad
// because that would hide Zeus.Sim.Squad; the scene is Squad.tscn.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Zeus.Sim;

public partial class SquadScreen : Control
{
    static readonly string[] Levels = ["amateur", "halfprof", "prof"];
    static readonly (string Name, double Value)[] Intensities = [("squad.low", 0.25), ("squad.medium", 0.5), ("squad.high", 1.0)];

    OptionButton _level = null!, _tactic = null!;
    SquadTable _table = null!;
    VBoxContainer _concepts = null!;
    Label _hint = null!, _message = null!, _limit = null!, _period = null!;
    CheckBox _match = null!;
    Button[] _intensityButtons = [];
    int _intensity = 1;
    int _frames;

    FamiliarityStore _store = null!;
    FamiliarityState _state = null!;
    Squad? _squad;
    TacticDemand? _demand;
    string _key = "", _error = "";
    string[] _files = [];

    public override void _Ready()
    {
        UiTheme.ApplyPaper(this);
        SetAnchorsPreset(LayoutPreset.FullRect);
        RenderingServer.SetDefaultClearColor(UiTheme.Bg);
        var bg = new UiPaper(); bg.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(bg);
        _store = FamiliarityStore.Load();

        var margin = new MarginContainer(); margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right" }) margin.AddThemeConstantOverride("margin_" + side, 30);
        foreach (var side in new[] { "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 16);
        AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 8); margin.AddChild(root);

        var head = new HBoxContainer(); root.AddChild(head);
        head.AddChild(UiTheme.Label(Loc.T("squad.title"), 46, UiTheme.Accent, display: true));
        head.AddChild(UiTheme.Label(Loc.T("squad.provisional"), UiTheme.Small, UiTheme.Muted));
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        Welcome.Install(this, "squad", head);
        var back = UiTheme.Secondary(Loc.T("squad.back")); back.CustomMinimumSize = new Vector2(0, 40); back.Pressed += Back; head.AddChild(back);

        var pick = new HBoxContainer(); pick.AddThemeConstantOverride("separation", 10); root.AddChild(pick);
        pick.AddChild(UiTheme.Label(Loc.T("squad.level"), 18, UiTheme.Muted, display: true));
        _level = new OptionButton { FitToLongestItem = false, CustomMinimumSize = new Vector2(150, 36) };
        foreach (var l in Levels) _level.AddItem(Loc.Name(l));
        _level.Select(Math.Max(0, Array.IndexOf(Levels, GameSession.HomeLevel)));
        pick.AddChild(_level);
        pick.AddChild(UiTheme.Label(Loc.T("squad.tactic"), 18, UiTheme.Muted, display: true));
        _tactic = new OptionButton { FitToLongestItem = false, CustomMinimumSize = new Vector2(300, 36) };
        pick.AddChild(_tactic);
        _message = UiTheme.Label("", UiTheme.Small, UiTheme.Bad);
        pick.AddChild(_message);
        _files = GameSession.Tactics().Select(t => t.Path).ToArray();
        foreach (var f in _files) _tactic.AddItem(GameSession.TacticLabel(f));
        int ti = GameSession.HomePath is { } hp ? Array.IndexOf(_files, hp) : 0;
        if (_files.Length > 0) _tactic.Select(Math.Max(0, ti));
        _level.ItemSelected += _ => Rebuild();
        _tactic.ItemSelected += _ => Rebuild();

        var body = new HBoxContainer(); body.AddThemeConstantOverride("separation", 12); body.SizeFlagsVertical = SizeFlags.ExpandFill; root.AddChild(body);
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(880, 0) }; left.AddThemeConstantOverride("separation", 4); body.AddChild(left);
        _table = new SquadTable { CustomMinimumSize = new Vector2(880, 448) };
        left.AddChild(_table);
        var legend = UiTheme.Label(Loc.T("squad.legend_start") +
            Loc.T("squad.legend_end"), UiTheme.Small, UiTheme.Muted);
        legend.AutowrapMode = TextServer.AutowrapMode.WordSmart; legend.CustomMinimumSize = new Vector2(880, 0);
        left.AddChild(legend);

        var right = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        right.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = UiTheme.Paper, BorderColor = UiTheme.PaperShade, BorderWidthTop = 3,
            ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 10, ContentMarginBottom = 10,
        });
        body.AddChild(right);
        var rv = new VBoxContainer(); rv.AddThemeConstantOverride("separation", 6); right.AddChild(rv);
        rv.AddChild(UiTheme.Label(Loc.T("squad.understanding"), 22, UiTheme.Text, display: true));
        var cols = new HBoxContainer(); cols.AddThemeConstantOverride("separation", 8); rv.AddChild(cols);
        var h0 = UiTheme.Label(Loc.T("squad.concept"), UiTheme.Small, UiTheme.Muted); h0.CustomMinimumSize = new Vector2(132, 0); cols.AddChild(h0);
        var eis = UiTheme.Label(Loc.T("squad.demand"), UiTheme.Small, UiTheme.Accent); eis.CustomMinimumSize = new Vector2(112, 0); cols.AddChild(eis);
        var vert = UiTheme.Label(Loc.T("squad.familiarity"), UiTheme.Small, UiTheme.Good); vert.CustomMinimumSize = new Vector2(112, 0); cols.AddChild(vert);
        _concepts = new VBoxContainer(); _concepts.AddThemeConstantOverride("separation", 3); rv.AddChild(_concepts);

        var irow = new HBoxContainer(); irow.AddThemeConstantOverride("separation", 6); rv.AddChild(irow);
        irow.AddChild(UiTheme.Label(Loc.T("squad.intensity"), 16, UiTheme.Muted, display: true));
        _intensityButtons = new Button[Intensities.Length];
        for (int i = 0; i < Intensities.Length; i++)
        {
            int idx = i;
            var b = new Button { Text = Loc.T(Intensities[i].Name), ToggleMode = true, ThemeTypeVariation = "Chip", ButtonPressed = i == _intensity };
            b.Pressed += () => { _intensity = idx; for (int j = 0; j < _intensityButtons.Length; j++) _intensityButtons[j].SetPressedNoSignal(j == idx); };
            _intensityButtons[i] = b; irow.AddChild(b);
        }
        _period = UiTheme.Label("", UiTheme.Small, UiTheme.Muted); irow.AddChild(_period);
        var next = UiTheme.Secondary(Loc.T("squad.new_period")); next.ThemeTypeVariation = "CompactButton"; next.CustomMinimumSize = new Vector2(0, 30); next.Pressed += NewPeriod; irow.AddChild(next);

        _limit = UiTheme.Label("", UiTheme.Small, UiTheme.Accent);
        _limit.AutowrapMode = TextServer.AutowrapMode.WordSmart; _limit.CustomMinimumSize = new Vector2(500, 20); rv.AddChild(_limit);
        rv.AddChild(new ColorRect { Color = new Color(1, 1, 1, 0.1f), CustomMinimumSize = new Vector2(0, 1) });
        rv.AddChild(UiTheme.Label(Loc.T("squad.too_hard"), 18, UiTheme.Muted, display: true));
        _hint = UiTheme.Label("", UiTheme.Small, UiTheme.Text);
        _hint.AutowrapMode = TextServer.AutowrapMode.WordSmart; _hint.CustomMinimumSize = new Vector2(500, 0); rv.AddChild(_hint);
        rv.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        _match = new CheckBox { Text = Loc.T("squad.use_familiarity"), ButtonPressed = GameSession.UseFamiliarity };
        _match.AddThemeFontSizeOverride("font_size", 15);
        _match.Toggled += on => GameSession.UseFamiliarity = on;
        rv.AddChild(_match);

        if (_files.Length == 0) _message.Text = Loc.T("squad.no_tactics");
        else Rebuild();
        if (_store.Note.Length > 0 && _message.Text.Length == 0) _message.Text = _store.Note;
        if (GameSession.DebugEnvironment("ZEUS_SQUADDEMO").Length > 0 && _squad is not null)    // visual check: shows bar growth and the limit message
        {
            _intensity = 2; Train("hoge_pressing"); Train("hoge_pressing"); Train("positiespel");
        }
    }

    // Rebuilds squad, demand and state for the chosen level + tactic. Any failure shows a message; the screen stays usable.
    void Rebuild()
    {
        _error = "";
        _squad = null; _demand = null;
        if (_files.Length == 0) return;
        var level = Levels[Math.Max(0, _level.Selected)];
        try
        {
            var tactic = TacticLoader.LoadFile(_files[Math.Max(0, _tactic.Selected)]);
            _squad = SquadFactory.ForLevel(level, tactic, GameSession.SquadsDir());
            _demand = TacticDemand.From(tactic);
        }
        catch (Exception e) { _squad = null; _demand = null; _error = Loc.T("squad.build_failed") + Loc.Error(e); }
        if (_squad is not null)
        {
            _key = FamiliarityStore.Key(level, _squad.Name);
            _state = _store.Get(_key, _key, _squad.Starters.Concat(_squad.Bench).Select(p => p.Id));
        }
        _message.Text = _error.Length > 0 ? _error : _store.Note;
        _limit.Text = "";
        _table.Set(_squad);
        RefreshConcepts();
    }

    void RefreshConcepts()
    {
        foreach (var c in _concepts.GetChildren()) { _concepts.RemoveChild(c); c.QueueFree(); }
        if (_squad is null || _demand is null) { _hint.Text = _error; _period.Text = ""; return; }
        _period.Text = Loc.T("squad.period_prefix") + _state.Period;
        var starters = _squad.Starters;
        var tooHard = new System.Collections.Generic.List<string>();
        foreach (var concept in TacticalConcepts.All)
        {
            double fam = Fam(concept), need = _demand[concept] * _demand.K;
            double understanding = starters.Average(p => TacticalConcepts.Understanding(p.Attributes, concept));
            double reachable = understanding * (0.5 + 0.5 * fam);
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); _concepts.AddChild(row);
            var name = UiTheme.Label(Loc.Name(concept), 16, UiTheme.Text); name.CustomMinimumSize = new Vector2(132, 0); row.AddChild(name);
            row.AddChild(new BarCell(_demand[concept], UiTheme.Accent, $"{_demand[concept] * 100:0}%") { CustomMinimumSize = new Vector2(112, 20) });
            row.AddChild(new BarCell(fam, UiTheme.Good, $"{fam * 100:0.#}%") { CustomMinimumSize = new Vector2(112, 20) });
            var train = new Button { Text = Loc.T("squad.train_prefix") + Loc.Name(concept), ThemeTypeVariation = "CompactButton", CustomMinimumSize = new Vector2(0, 26), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            train.Pressed += () => Train(concept);
            row.AddChild(train);
            if (_demand[concept] > 0.01 && need - reachable > 0.03)
                tooHard.Add(Loc.T("squad.deficit", Loc.Name(concept), need, reachable, fam * 100));
        }
        double worst = starters.Max(p => _demand.Deficit(p.Attributes, _state.Players[p.Id].Concepts));
        _hint.Text = tooHard.Count == 0
            ? Loc.T("squad.no_deficit", worst)
            : Loc.T("squad.deficit_help", _demand.K, worst)
              + string.Join("\n- ", tooHard);
    }

    double Fam(string concept) => _squad!.Starters.Average(p => _state.Players[p.Id].Concepts.GetValueOrDefault(concept));

    void Train(string concept)
    {
        if (_squad is null) return;
        double before = Fam(concept);
        double room = _state.Players.Values.Min(p => FamiliarityState.PeriodLimit - p.Learned);
        try { FamiliarityState.Train(_state, concept, Intensities[_intensity].Value); }
        catch (Exception e) { _limit.Text = Loc.T("squad.training_failed") + Loc.Error(e); return; }
        double after = Fam(concept);
        var err = _store.Put(_key, _state);
        string text = $"{Loc.Name(concept)}: {before * 100:0.0}% -> {after * 100:0.0}%.";
        double used = _state.Players.Values.Max(p => p.Learned);
        if (after - before < 1e-9 || room < 1e-9)
            text = Loc.T("squad.learning_limit", FamiliarityState.PeriodLimit);
        else if (used >= FamiliarityState.PeriodLimit - 1e-6)
            text += Loc.T("squad.limit_reached");
        else text += Loc.T("squad.budget_used", used / FamiliarityState.PeriodLimit * 100);
        _limit.Text = text;
        if (err.Length > 0) _message.Text = err;
        RefreshConcepts();
    }

    void NewPeriod()
    {
        if (_squad is null) return;
        try { _state.BeginPeriod(_state.Period + 1); }
        catch (Exception e) { _limit.Text = Loc.T("squad.period_failed") + Loc.Error(e); return; }
        var err = _store.Put(_key, _state);
        if (err.Length > 0) _message.Text = err;
        _limit.Text = Loc.T("squad.period_reset");
        RefreshConcepts();
    }

    public void RequestHome() => Back();

    void Back()
    {
        if (_files.Length > 0 && _squad is not null)           // the menu's home team becomes the team chosen here
        {
            GameSession.HomeLevel = Levels[_level.Selected];
            GameSession.HomePath = _files[_tactic.Selected];
        }
        GetTree().ChangeSceneToFile("res://Menu.tscn");
    }

    // ZEUS_SHOT=<prefix> saves a screenshot of this screen after a few frames and quits (visual check).
    public override void _Process(double delta)
    {
        var shot = GameSession.DebugEnvironment("ZEUS_SHOT");
        if (shot.Length == 0 || ++_frames != 15) return;
        GetViewport().GetTexture().GetImage().SavePng(shot + "_squad.png");
        GetTree().Quit();
    }
}

/// <summary>A small horizontal bar (0..1) with a value text on it.</summary>
public partial class BarCell : Control
{
    readonly double _value; readonly Color _color; readonly string _text;
    public BarCell(double value, Color color, string text) { _value = Math.Clamp(value, 0, 1); _color = color; _text = text; }
    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        DrawRect(r, UiTheme.PaperShade, true);
        DrawRect(new Rect2(r.Position, new Vector2(r.Size.X * (float)_value, r.Size.Y)), new Color(_color, 0.75f), true);
        DrawString(GameFonts.Semi, new Vector2(6, r.Size.Y - 5), _text, HorizontalAlignment.Left, -1, 15, UiTheme.Ink);
    }
}

/// <summary>Squad list: 11 starters (slot id, role, name) and 7 reserves with the 15 attributes as numbered bars.</summary>
public partial class SquadTable : Control
{
    Squad? _squad;
    public void Set(Squad? squad) { _squad = squad; QueueRedraw(); }

    static double[] Values(PlayerAttributes a) => [a.Passing, a.FirstTouch, a.Finishing, a.Dribbling, a.Defending, a.Heading, a.Speed,
        a.Acceleration, a.Strength, a.Endurance, a.Vision, a.Anticipation, a.Positioning, a.Decisions, a.Discipline];

    public override void _Draw()
    {
        var font = GameFonts.Semi;
        const float rowH = 22, xRole = 50, xName = 190, xAttr = 360, cell = 34;
        DrawString(font, new Vector2(0, 16), Loc.T("squad.slot"), HorizontalAlignment.Left, -1, 15, UiTheme.Graphite);
        DrawString(font, new Vector2(xRole, 16), Loc.T("squad.role"), HorizontalAlignment.Left, -1, 15, UiTheme.Graphite);
        DrawString(font, new Vector2(xName, 16), Loc.T("squad.name"), HorizontalAlignment.Left, -1, 15, UiTheme.Graphite);
        for (int i = 0; i < SquadLoader.AttributeNames.Length; i++)
            DrawString(font, new Vector2(xAttr + i * cell + 6, 16), Loc.T("squad.attribute." + i), HorizontalAlignment.Left, -1, 15, UiTheme.Graphite);
        if (_squad is null) { DrawString(font, new Vector2(0, 60), Loc.T("squad.no_squad"), HorizontalAlignment.Left, -1, 18, new Color("8E1F14")); return; }
        float y = 24;
        int index = 0;
        void Row(SquadPlayer p, bool bench)
        {
            if (index++ % 2 == 0) DrawRect(new Rect2(0, y, Size.X, rowH), UiTheme.PaperShade, true);
            var baseline = y + 16;
            DrawString(font, new Vector2(4, baseline), p.Id, HorizontalAlignment.Left, -1, 15, bench ? UiTheme.Graphite : UiTheme.Ink);
            DrawString(GameFonts.Display, new Vector2(xRole, baseline), Loc.Name(p.Role), HorizontalAlignment.Left, 136, 17, UiTheme.Graphite);
            DrawString(GameFonts.Body, new Vector2(xName, baseline), Loc.Name(p.Name), HorizontalAlignment.Left, 165, 15, UiTheme.Ink);
            var v = Values(p.Attributes);
            for (int i = 0; i < v.Length; i++)
            {
                var rect = new Rect2(xAttr + i * cell, y + 3, cell - 4, rowH - 6);
                int n = (int)Math.Round(v[i] * 19 + 1);
                DrawRect(rect, UiTheme.PaperShade, true);
                DrawRect(new Rect2(rect.Position, new Vector2(rect.Size.X * (float)v[i], rect.Size.Y)), Tone(v[i]), true);
                DrawString(font, new Vector2(rect.Position.X + 8, rect.Position.Y + rect.Size.Y - 2), n.ToString(), HorizontalAlignment.Left, -1, 15, UiTheme.Ink);
            }
            y += rowH;
        }
        foreach (var p in _squad.Starters) Row(p, false);
        y += 4;
        DrawString(font, new Vector2(0, y + 14), Loc.T("squad.bench"), HorizontalAlignment.Left, -1, 15, UiTheme.Graphite);
        y += 18; index = 0;
        foreach (var p in _squad.Bench) Row(p, true);
    }

    static Color Tone(double v) => v < 0.4 ? new Color("8C4A4A") : v < 0.65 ? new Color("5C6F86") : new Color("3A8F68");
}
