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
    static readonly (string Name, double Value)[] Intensities = [("Laag", 0.25), ("Middel", 0.5), ("Hoog", 1.0)];

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
        UiTheme.Apply(this);
        SetAnchorsPreset(LayoutPreset.FullRect);
        RenderingServer.SetDefaultClearColor(UiTheme.Bg);
        var bg = new ColorRect { Color = UiTheme.Bg }; bg.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(bg);
        _store = FamiliarityStore.Load();

        var margin = new MarginContainer(); margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right" }) margin.AddThemeConstantOverride("margin_" + side, 30);
        foreach (var side in new[] { "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 16);
        AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 8); margin.AddChild(root);

        var head = new HBoxContainer(); root.AddChild(head);
        head.AddChild(UiTheme.Label("SELECTIE", 46, UiTheme.Accent, display: true));
        head.AddChild(UiTheme.Label("  voorlopig (v0, werkhypothese, ongekalibreerd)", UiTheme.Small, UiTheme.Muted));
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var back = UiTheme.Secondary("Terug naar menu  (Esc)"); back.CustomMinimumSize = new Vector2(0, 40); back.Pressed += Back; head.AddChild(back);

        var pick = new HBoxContainer(); pick.AddThemeConstantOverride("separation", 10); root.AddChild(pick);
        pick.AddChild(UiTheme.Label("NIVEAU", 18, UiTheme.Muted, display: true));
        _level = new OptionButton { CustomMinimumSize = new Vector2(150, 36) };
        foreach (var l in Levels) _level.AddItem(char.ToUpperInvariant(l[0]) + l[1..]);
        _level.Select(Math.Max(0, Array.IndexOf(Levels, GameSession.HomeLevel)));
        pick.AddChild(_level);
        pick.AddChild(UiTheme.Label("TACTIEK", 18, UiTheme.Muted, display: true));
        _tactic = new OptionButton { CustomMinimumSize = new Vector2(300, 36) };
        pick.AddChild(_tactic);
        _message = UiTheme.Label("", UiTheme.Small, UiTheme.Bad);
        pick.AddChild(_message);
        var dir = GameSession.TacticsDir();
        _files = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.json").Select(p => Path.GetFileName(p)!).OrderBy(n => n).ToArray() : [];
        foreach (var f in _files) _tactic.AddItem(Path.GetFileNameWithoutExtension(f).Replace('_', ' '));
        int ti = GameSession.HomePath is { } hp ? Array.IndexOf(_files, Path.GetFileName(hp)) : 0;
        if (_files.Length > 0) _tactic.Select(Math.Max(0, ti));
        _level.ItemSelected += _ => Rebuild();
        _tactic.ItemSelected += _ => Rebuild();

        var body = new HBoxContainer(); body.AddThemeConstantOverride("separation", 18); body.SizeFlagsVertical = SizeFlags.ExpandFill; root.AddChild(body);
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(880, 0) }; left.AddThemeConstantOverride("separation", 4); body.AddChild(left);
        _table = new SquadTable { CustomMinimumSize = new Vector2(880, 448) };
        left.AddChild(_table);
        var legend = UiTheme.Label("Kenmerken 1..20: PAS passen, AAN aanname, AFW afwerken, DRI dribbelen, VER verdedigen, KOP koppen, SNE snelheid, ACC acceleratie, " +
            "KRA kracht, CON conditie, OVZ overzicht, ANT anticipatie, POS positiespel, BES beslissen, DIS discipline.", 13, UiTheme.Muted);
        legend.AutowrapMode = TextServer.AutowrapMode.WordSmart; legend.CustomMinimumSize = new Vector2(880, 0);
        left.AddChild(legend);

        var right = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        right.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(UiTheme.Panel, 0.9f), BorderColor = new Color(UiTheme.Accent, 0.55f), BorderWidthTop = 3,
            ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 10, ContentMarginBottom = 10,
        });
        body.AddChild(right);
        var rv = new VBoxContainer(); rv.AddThemeConstantOverride("separation", 6); right.AddChild(rv);
        rv.AddChild(UiTheme.Label("TACTISCH BEGRIP  (voorlopig)", 22, UiTheme.Text, display: true));
        var cols = new HBoxContainer(); cols.AddThemeConstantOverride("separation", 8); rv.AddChild(cols);
        var h0 = UiTheme.Label("concept", 14, UiTheme.Muted); h0.CustomMinimumSize = new Vector2(132, 0); cols.AddChild(h0);
        var eis = UiTheme.Label("eis van tactiek", 14, UiTheme.Accent); eis.CustomMinimumSize = new Vector2(112, 0); cols.AddChild(eis);
        var vert = UiTheme.Label("vertrouwdheid", 14, UiTheme.Good); vert.CustomMinimumSize = new Vector2(112, 0); cols.AddChild(vert);
        _concepts = new VBoxContainer(); _concepts.AddThemeConstantOverride("separation", 3); rv.AddChild(_concepts);

        var irow = new HBoxContainer(); irow.AddThemeConstantOverride("separation", 6); rv.AddChild(irow);
        irow.AddChild(UiTheme.Label("INTENSITEIT", 16, UiTheme.Muted, display: true));
        _intensityButtons = new Button[Intensities.Length];
        for (int i = 0; i < Intensities.Length; i++)
        {
            int idx = i;
            var b = new Button { Text = Intensities[i].Name, ToggleMode = true, ThemeTypeVariation = "Chip", ButtonPressed = i == _intensity };
            b.Pressed += () => { _intensity = idx; for (int j = 0; j < _intensityButtons.Length; j++) _intensityButtons[j].SetPressedNoSignal(j == idx); };
            _intensityButtons[i] = b; irow.AddChild(b);
        }
        _period = UiTheme.Label("", UiTheme.Small, UiTheme.Muted); irow.AddChild(_period);
        var next = UiTheme.Secondary("Nieuwe periode"); next.CustomMinimumSize = new Vector2(0, 30); next.AddThemeFontSizeOverride("font_size", 15); next.Pressed += NewPeriod; irow.AddChild(next);

        _limit = UiTheme.Label("", UiTheme.Small, UiTheme.Accent);
        _limit.AutowrapMode = TextServer.AutowrapMode.WordSmart; _limit.CustomMinimumSize = new Vector2(500, 20); rv.AddChild(_limit);
        rv.AddChild(new ColorRect { Color = new Color(1, 1, 1, 0.1f), CustomMinimumSize = new Vector2(0, 1) });
        rv.AddChild(UiTheme.Label("WAT IS TE ZWAAR?", 18, UiTheme.Muted, display: true));
        _hint = UiTheme.Label("", 14, UiTheme.Text);
        _hint.AutowrapMode = TextServer.AutowrapMode.WordSmart; _hint.CustomMinimumSize = new Vector2(500, 0); rv.AddChild(_hint);
        rv.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        _match = new CheckBox { Text = "Vertrouwdheid meenemen in wedstrijd (thuisteam, menu-wedstrijd)", ButtonPressed = GameSession.UseFamiliarity };
        _match.AddThemeFontSizeOverride("font_size", 15);
        _match.Toggled += on => GameSession.UseFamiliarity = on;
        rv.AddChild(_match);

        if (_files.Length == 0) _message.Text = "Geen tactieken gevonden in tactics/.";
        else Rebuild();
        if (_store.Note.Length > 0 && _message.Text.Length == 0) _message.Text = _store.Note;
        if (OS.GetEnvironment("ZEUS_SQUADDEMO").Length > 0 && _squad is not null)    // visual check: shows bar growth and the limit message
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
            var tactic = TacticLoader.LoadFile(Path.Combine(GameSession.TacticsDir(), _files[Math.Max(0, _tactic.Selected)]));
            _squad = SquadFactory.ForLevel(level, tactic, GameSession.SquadsDir());
            _demand = TacticDemand.From(tactic);
        }
        catch (Exception e) { _squad = null; _demand = null; _error = "Kon selectie niet opbouwen: " + e.Message; }
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
        _period.Text = "  periode " + _state.Period;
        var starters = _squad.Starters;
        var tooHard = new System.Collections.Generic.List<string>();
        foreach (var concept in TacticalConcepts.All)
        {
            double fam = Fam(concept), need = _demand[concept] * _demand.K;
            double understanding = starters.Average(p => TacticalConcepts.Understanding(p.Attributes, concept));
            double reachable = understanding * (0.5 + 0.5 * fam);
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); _concepts.AddChild(row);
            var name = UiTheme.Label(concept.Replace('_', ' '), 16, UiTheme.Text); name.CustomMinimumSize = new Vector2(132, 0); row.AddChild(name);
            row.AddChild(new BarCell(_demand[concept], UiTheme.Accent, $"{_demand[concept] * 100:0}%") { CustomMinimumSize = new Vector2(112, 20) });
            row.AddChild(new BarCell(fam, UiTheme.Good, $"{fam * 100:0.#}%") { CustomMinimumSize = new Vector2(112, 20) });
            var train = new Button { Text = "Train " + concept.Replace('_', ' '), CustomMinimumSize = new Vector2(0, 26), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            train.AddThemeFontSizeOverride("font_size", 15);
            train.Pressed += () => Train(concept);
            row.AddChild(train);
            if (_demand[concept] > 0.01 && need - reachable > 0.03)
                tooHard.Add($"{concept.Replace('_', ' ')}: eis {need:0.00}, haalbaar {reachable:0.00} (vertrouwdheid {fam * 100:0}%)");
        }
        double worst = starters.Max(p => _demand.Deficit(p.Attributes, _state.Players[p.Id].Concepts));
        _hint.Text = tooHard.Count == 0
            ? $"Geen eis is te zwaar voor de huidige vertrouwdheid (gemiddelde basisspeler). Grootste tekort: {worst:0.00}."
            : $"Te zwaar voor de huidige vertrouwdheid (gemiddelde basisspeler, factor {_demand.K:0.00}; grootste tekort {worst:0.00}). Train deze concepten of kies een eenvoudiger tactiek:\n- "
              + string.Join("\n- ", tooHard);
    }

    double Fam(string concept) => _squad!.Starters.Average(p => _state.Players[p.Id].Concepts.GetValueOrDefault(concept));

    void Train(string concept)
    {
        if (_squad is null) return;
        double before = Fam(concept);
        double room = _state.Players.Values.Min(p => FamiliarityState.PeriodLimit - p.Learned);
        try { FamiliarityState.Train(_state, concept, Intensities[_intensity].Value); }
        catch (Exception e) { _limit.Text = "Training mislukt: " + e.Message; return; }
        double after = Fam(concept);
        var err = _store.Put(_key, _state);
        string text = $"{concept.Replace('_', ' ')}: {before * 100:0.0}% -> {after * 100:0.0}%.";
        double used = _state.Players.Values.Max(p => p.Learned);
        if (after - before < 1e-9 || room < 1e-9)
            text = $"Leerlimiet van deze periode bereikt ({FamiliarityState.PeriodLimit:0.00} per speler). Start een nieuwe periode om verder te trainen.";
        else if (used >= FamiliarityState.PeriodLimit - 1e-6)
            text += " Leerlimiet van deze periode nu bereikt.";
        else text += $" Leerbudget gebruikt: {used / FamiliarityState.PeriodLimit * 100:0}%.";
        _limit.Text = text;
        if (err.Length > 0) _message.Text = err;
        RefreshConcepts();
    }

    void NewPeriod()
    {
        if (_squad is null) return;
        try { _state.BeginPeriod(_state.Period + 1); }
        catch (Exception e) { _limit.Text = "Nieuwe periode mislukt: " + e.Message; return; }
        var err = _store.Put(_key, _state);
        if (err.Length > 0) _message.Text = err;
        _limit.Text = "Nieuwe periode: het leerbudget is hersteld.";
        RefreshConcepts();
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) Back();
    }

    void Back()
    {
        if (_files.Length > 0 && _squad is not null)           // the menu's home team becomes the team chosen here
        {
            GameSession.HomeLevel = Levels[_level.Selected];
            GameSession.HomePath = Path.Combine(GameSession.TacticsDir(), _files[_tactic.Selected]);
        }
        GetTree().ChangeSceneToFile("res://Menu.tscn");
    }

    // ZEUS_SHOT=<prefix> saves a screenshot of this screen after a few frames and quits (visual check).
    public override void _Process(double delta)
    {
        var shot = OS.GetEnvironment("ZEUS_SHOT");
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
        DrawRect(r, new Color("0B121B"), true);
        DrawRect(new Rect2(r.Position, new Vector2(r.Size.X * (float)_value, r.Size.Y)), new Color(_color, 0.75f), true);
        DrawString(GameFonts.Semi, new Vector2(6, r.Size.Y - 5), _text, HorizontalAlignment.Left, -1, 15, Colors.White);
    }
}

/// <summary>Squad list: 11 starters (slot id, role, name) and 7 reserves with the 15 attributes as numbered bars.</summary>
public partial class SquadTable : Control
{
    static readonly string[] Heads = ["PAS", "AAN", "AFW", "DRI", "VER", "KOP", "SNE", "ACC", "KRA", "CON", "OVZ", "ANT", "POS", "BES", "DIS"];
    Squad? _squad;
    public void Set(Squad? squad) { _squad = squad; QueueRedraw(); }

    static double[] Values(PlayerAttributes a) => [a.Passing, a.FirstTouch, a.Finishing, a.Dribbling, a.Defending, a.Heading, a.Speed,
        a.Acceleration, a.Strength, a.Endurance, a.Vision, a.Anticipation, a.Positioning, a.Decisions, a.Discipline];

    public override void _Draw()
    {
        var font = GameFonts.Semi;
        const float rowH = 22, xRole = 50, xName = 190, xAttr = 360, cell = 34;
        DrawString(font, new Vector2(0, 16), "SLOT", HorizontalAlignment.Left, -1, 14, UiTheme.Muted);
        DrawString(font, new Vector2(xRole, 16), "ROL", HorizontalAlignment.Left, -1, 14, UiTheme.Muted);
        DrawString(font, new Vector2(xName, 16), "NAAM", HorizontalAlignment.Left, -1, 14, UiTheme.Muted);
        for (int i = 0; i < Heads.Length; i++)
            DrawString(font, new Vector2(xAttr + i * cell + 6, 16), Heads[i], HorizontalAlignment.Left, -1, 13, UiTheme.Muted);
        if (_squad is null) { DrawString(font, new Vector2(0, 60), "Geen selectie beschikbaar.", HorizontalAlignment.Left, -1, 18, UiTheme.Bad); return; }
        float y = 24;
        int index = 0;
        void Row(SquadPlayer p, bool bench)
        {
            if (index++ % 2 == 0) DrawRect(new Rect2(0, y, Size.X, rowH), new Color(1, 1, 1, 0.04f), true);
            var baseline = y + 16;
            DrawString(font, new Vector2(4, baseline), p.Id, HorizontalAlignment.Left, -1, 15, bench ? UiTheme.Muted : UiTheme.Accent);
            DrawString(font, new Vector2(xRole, baseline), p.Role.Replace('_', ' '), HorizontalAlignment.Left, 136, 15, UiTheme.Muted);
            DrawString(GameFonts.Body, new Vector2(xName, baseline), p.Name, HorizontalAlignment.Left, 165, 14, UiTheme.Text);
            var v = Values(p.Attributes);
            for (int i = 0; i < v.Length; i++)
            {
                var rect = new Rect2(xAttr + i * cell, y + 3, cell - 4, rowH - 6);
                int n = (int)Math.Round(v[i] * 19 + 1);
                DrawRect(rect, new Color("0B121B"), true);
                DrawRect(new Rect2(rect.Position, new Vector2(rect.Size.X * (float)v[i], rect.Size.Y)), Tone(v[i]), true);
                DrawString(font, new Vector2(rect.Position.X + 8, rect.Position.Y + rect.Size.Y - 2), n.ToString(), HorizontalAlignment.Left, -1, 13, Colors.White);
            }
            y += rowH;
        }
        foreach (var p in _squad.Starters) Row(p, false);
        y += 4;
        DrawString(font, new Vector2(0, y + 14), "BANK", HorizontalAlignment.Left, -1, 14, UiTheme.Muted);
        y += 18; index = 0;
        foreach (var p in _squad.Bench) Row(p, true);
    }

    static Color Tone(double v) => v < 0.4 ? new Color("8C4A4A") : v < 0.65 ? new Color("5C6F86") : new Color("3A8F68");
}
