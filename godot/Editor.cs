// Tactic maker: build a tactic from a formation template or an existing file. Drag players per phase, set roles,
// instructions, marking, team form, rules (triggers), block switches, and phase tools. Saves a validated tactic file.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Zeus.Sim;

public partial class Editor : Node2D
{
    const float Px = 10f;
    static readonly Vector2 Origin = new(600, 390);
    const float L = (float)Pitch.Length, W = (float)Pitch.Width;

    // Opponent ids are the same position ids as the own team; offer the union of all template ids.
    static readonly string[] OpponentIds = TacticTemplates.Formations.Values
        .SelectMany(f => f.Slots.Select(s => s.Id)).Distinct().OrderBy(s => s).ToArray();

    static readonly Dictionary<string, string> PhaseLabels = new()
    {
        ["opbouw"] = "OPBOUW", ["aanval"] = "AANVAL", ["balverlies"] = "BALVERLIES", ["verdedigen"] = "VERDEDIGEN",
        ["balwinst"] = "BALWINST", ["stilstaand"] = "STILSTAAND",
        ["verdedigen@hoog"] = "VERD. HOOG", ["verdedigen@midden"] = "VERD. MIDDEN", ["verdedigen@laag"] = "VERD. LAAG",
    };

    static readonly string[] PhaseOrder =
        { "opbouw", "aanval", "balverlies", "verdedigen", "verdedigen@hoog", "verdedigen@midden", "verdedigen@laag", "balwinst", "stilstaand" };

    TacticEditorModel _model = null!;
    string? _sourcePath;
    string _returnScene = "res://Menu.tscn";      // Season sets GameSession.ReturnScene so the editor comes back to the competition
    string _phase = "opbouw";
    string? _drag, _selected, _savedAs;
    Vector2 _pressPosition;
    bool _dragStarted, _dirty;
    int _frames;

    Label _status = null!, _phaseHint = null!, _playerTitle = null!;
    LineEdit _name = null!;
    HBoxContainer _chips = null!;
    ConfirmationDialog _confirm = null!;
    TabContainer _tabs = null!;

    // player tab
    OptionButton _role = null!, _markTarget = null!;
    CheckBox _markBox = null!;
    HSlider _followX = null!, _followY = null!;
    Label _followXLabel = null!, _followYLabel = null!;
    readonly Dictionary<string, CheckBox> _instructionBoxes = new();
    // path drawing (player tab)
    bool _drawPath;
    Button _pathToggle = null!;
    Label _pathInfo = null!;
    OptionButton _pathSpeed = null!;
    SpinBox _pathWait = null!;
    // team tab
    readonly Dictionary<string, (HSlider Slider, Label Label, string Title)> _teamSliders = new();
    OptionButton _pressForm = null!;
    // rules tab
    VBoxContainer _triggerList = null!, _switchList = null!;
    LineEdit _trigName = null!, _swName = null!;
    OptionButton _trigPhase = null!, _trigCond = null!, _trigZone = null!, _trigPlayer = null!, _trigInstr = null!;
    OptionButton _swStand = null!, _swTo = null!;
    SpinBox _swMinute = null!, _swFatigue = null!;
    // phase tab
    OptionButton _copyTarget = null!, _aliasTarget = null!, _lineTemplate = null!;

    public override void _Ready()
    {
        RenderingServer.SetDefaultClearColor(UiTheme.Bg);
        _confirm = new ConfirmationDialog { Title = "Niet-opgeslagen wijzigingen", DialogText = "Je hebt wijzigingen die nog niet zijn opgeslagen. Doorgaan en ze weggooien?" };
        _confirm.Theme = UiTheme.Create();
        AddChild(_confirm);
        BuildChrome();
        _returnScene = GameSession.ReturnScene ?? _returnScene; GameSession.ReturnScene = null;
        _sourcePath = GameSession.EditPath ?? Path.Combine(GameSession.TacticsDir(), "laag_blok_442.json");
        try { SetModel(TacticEditorModel.LoadFile(_sourcePath), Path.GetFileNameWithoutExtension(_sourcePath)); }
        catch (Exception e) { SetStatus("Laden mislukt: " + e.Message, false); }
        if (GameSession.DebugEnvironment("ZEUS_PATHDEMO").Length > 0 && _model is not null)      // visual check: a sample path for the right back
        {
            _selected = _model.PlayerIds.Contains("RB") ? "RB" : _model.PlayerIds[1];
            Try(() => _model.SetPath(_phase, _selected, new[] { (0.40, 0.92), (0.62, 0.94), (0.70, 0.74) }, "sprint", 0.5));
        }
        if (int.TryParse(GameSession.DebugEnvironment("ZEUS_TAB"), out var tab)) _tabs.CurrentTab = tab;      // visual check of a tab
        if (GameSession.DebugEnvironment("ZEUS_NEW") is { Length: > 0 } key && TacticTemplates.Formations.ContainsKey(key)) NewTactic(key, TacticTemplates.Formations[key].Label);
    }

    // ---- layout ----------------------------------------------------------------------------------------------------

    void BuildChrome()
    {
        _phaseHint = UiTheme.Label("", UiTheme.Body, UiTheme.Muted); _phaseHint.Position = new Vector2(75, 746); AddChild(_phaseHint);
        var chipScroll = new ScrollContainer { Position = new Vector2(70, 4), Size = new Vector2(1070, 36), VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        UiTheme.Apply(chipScroll);
        AddChild(chipScroll);
        _chips = new HBoxContainer(); _chips.AddThemeConstantOverride("separation", 6); chipScroll.AddChild(_chips);

        var panel = new PanelContainer { Position = new Vector2(1148, 14), Size = new Vector2(360, 752), ClipContents = true };
        UiTheme.Apply(panel);
        AddChild(panel);
        var col = new VBoxContainer(); panel.AddChild(col);

        col.AddChild(UiTheme.Label("TACTIEKMAKER", UiTheme.Heading + 10, UiTheme.Accent, display: true));
        var files = new HBoxContainer(); col.AddChild(files);
        var newMenu = new MenuButton { Text = "Nieuw ▾", CustomMinimumSize = new Vector2(0, 42) };
        var newPopup = newMenu.GetPopup();
        var formations = TacticTemplates.List;
        for (int i = 0; i < formations.Count; i++) newPopup.AddItem("Nieuw: " + formations[i].Label, i);
        newPopup.IdPressed += id => Guard(() => NewTactic(formations[(int)id].Key, formations[(int)id].Label));
        files.AddChild(newMenu);
        var openMenu = new MenuButton { Text = "Open ▾", CustomMinimumSize = new Vector2(0, 42) };
        _openPopup = openMenu.GetPopup();
        FillOpenMenu();
        _openPopup.AboutToPopup += FillOpenMenu;               // a tactic saved a moment ago must be in the list
        _openPopup.IdPressed += id =>
        {
            if (id == OpenCopyId) CopyToClipboard();
            else if (id == OpenPasteId) Guard(PasteFromClipboard);
            else if (id >= 0 && id < _openFiles.Length) { var file = _openFiles[(int)id]; Guard(() => OpenFile(file)); }
        };
        files.AddChild(openMenu);
        var back = UiTheme.Secondary("Menu"); back.CustomMinimumSize = new Vector2(0, 42);
        back.Pressed += () => Guard(() => GetTree().ChangeSceneToFile(_returnScene)); files.AddChild(back);

        var nameRow = new HBoxContainer(); col.AddChild(nameRow);
        _name = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, PlaceholderText = "bestandsnaam" }; nameRow.AddChild(_name);
        var save = UiTheme.Primary("OPSLAAN"); save.CustomMinimumSize = new Vector2(130, 46); save.Pressed += Save; nameRow.AddChild(save);

        _status = UiTheme.Label("", UiTheme.Small, UiTheme.Muted);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart; _status.CustomMinimumSize = new Vector2(0, 38);
        col.AddChild(_status);

        _tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; col.AddChild(_tabs);
        BuildPlayerTab(); BuildTeamTab(); BuildRulesTab(); BuildPhaseTab();
    }

    // The Open menu is rebuilt each time it opens (and after a save), so new files show up.
    PopupMenu _openPopup = null!;
    string[] _openFiles = Array.Empty<string>();
    const int OpenCopyId = 100000, OpenPasteId = 100001;

    void FillOpenMenu()
    {
        _openFiles = GameSession.Tactics().Select(t => t.Path).ToArray();
        _openPopup.Clear();
        for (int i = 0; i < _openFiles.Length; i++) _openPopup.AddItem(GameSession.TacticLabel(_openFiles[i]), i);
        _openPopup.AddSeparator("Delen met een vriend");
        _openPopup.AddItem("Kopieer tactiek naar klembord", OpenCopyId);
        _openPopup.AddItem("Plak tactiek van klembord", OpenPasteId);
    }

    static VBoxContainer Tab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer { Name = title, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        tabs.AddChild(scroll);
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; scroll.AddChild(box);
        return box;
    }

    // Long labels must wrap, otherwise the widest tab pushes the whole side panel wider than the screen.
    static Label Small(string text)
    {
        var l = UiTheme.Label(text, UiTheme.Small, UiTheme.Muted);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        l.CustomMinimumSize = new Vector2(10, 0);
        return l;
    }

    static OptionButton Options(IEnumerable<string> items, Func<string, string>? text = null)
    {
        var o = new OptionButton { CustomMinimumSize = new Vector2(0, 40) };
        foreach (var i in items) { o.AddItem(text?.Invoke(i) ?? i); o.SetItemMetadata(o.ItemCount - 1, i); }
        return o;
    }

    static string Meta(OptionButton o) => o.Selected < 0 ? "" : (string)o.GetItemMetadata(o.Selected);
    static string Nice(string s) => s.Replace('_', ' ');

    void BuildPlayerTab()
    {
        var t = Tab(_tabs, "SPELER");
        _playerTitle = UiTheme.Label("", 28, UiTheme.Accent, display: true); t.AddChild(_playerTitle);
        t.AddChild(Small("Rol"));
        _role = Options(TacticEditorModel.Roles, Nice); t.AddChild(_role);
        _role.ItemSelected += _ => Try(() => { if (_selected != null) _model.SetRole(_selected, Meta(_role)); });

        _markBox = new CheckBox { Text = "man dekken" }; t.AddChild(_markBox);
        _markTarget = Options(OpponentIds); t.AddChild(_markTarget);
        _markBox.Toggled += _ => ApplyMarking();
        _markTarget.ItemSelected += _ => { if (_markBox.ButtonPressed) ApplyMarking(); };

        t.AddChild(Small("Positie volgt de bal"));
        (_followX, _followXLabel) = Follow(t, "links-rechts als de bal beweegt (x)");
        (_followY, _followYLabel) = Follow(t, "breedte als de bal beweegt (y)");
        t.AddChild(Small("Instructies"));
        foreach (var instruction in TacticEditorModel.AllowedInstructions.Where(i => i != "dek_man"))
        {
            var box = new CheckBox { Text = Nice(instruction) };
            box.Toggled += _ => Try(() =>
            {
                if (_selected is null) return;
                _model.ToggleInstruction(_phase, _selected, instruction);
            });
            _instructionBoxes.Add(instruction, box);
            t.AddChild(box);
        }

        t.AddChild(new HSeparator());
        t.AddChild(UiTheme.Label("LOOPPAD", 24, UiTheme.Accent, display: true));
        t.AddChild(Small("Teken tot 8 punten: de speler loopt dit pad bij het begin van de fase en keert daarna terug naar zijn positie."));
        _pathToggle = new Button { Text = "Teken pad (klik op het veld)", ToggleMode = true, ThemeTypeVariation = "Chip", CustomMinimumSize = new Vector2(0, 40) };
        _pathToggle.Toggled += on =>
        {
            _drawPath = on;
            SetStatus(on ? "Klik op het veld om punten toe te voegen. Rechtsklik wist het laatste punt, Esc stopt." : "Tekenen gestopt.", true);
            QueueRedraw();
        };
        t.AddChild(_pathToggle);
        _pathInfo = Small(""); t.AddChild(_pathInfo);
        t.AddChild(Small("Snelheid"));
        _pathSpeed = Options(new[] { "wandelen", "joggen", "sprint" }); t.AddChild(_pathSpeed);
        _pathSpeed.ItemSelected += _ => RewritePath();
        t.AddChild(Small("Wachttijd bij elk punt (seconden)"));
        _pathWait = new SpinBox { MinValue = 0, MaxValue = 5, Step = 0.5 }; t.AddChild(_pathWait);
        _pathWait.ValueChanged += _ => RewritePath();
        var clear = UiTheme.Secondary("Wis pad"); t.AddChild(clear);
        clear.Pressed += () => Try(() => { if (_selected != null) _model.ClearPath(_phase, _selected); });
    }

    void RewritePath()
    {
        if (_selected is null || _model is null) return;
        var points = _model.GetPath(_phase, _selected);
        if (points.Count > 0) Try(() => _model.SetPath(_phase, _selected, points, Meta(_pathSpeed), _pathWait.Value), refresh: false);
    }

    void AddPathPoint(Vector2 screen)
    {
        if (_selected is null) return;
        var (x, y) = FromScreen(screen);
        var points = _model.GetPath(_phase, _selected).ToList();
        if (points.Count >= 8) { SetStatus("Een pad heeft maximaal 8 punten.", false); return; }
        points.Add((Math.Round(Math.Clamp(x, 0, 1), 3), Math.Round(Math.Clamp(y, 0, 1), 3)));
        var id = _selected;
        Try(() => _model.SetPath(_phase, id, points, Meta(_pathSpeed), _pathWait.Value));
    }

    void RemoveLastPathPoint()
    {
        if (_selected is null) return;
        var points = _model.GetPath(_phase, _selected).ToList();
        if (points.Count == 0) return;
        var id = _selected;
        if (points.Count == 1) Try(() => _model.ClearPath(_phase, id));
        else { points.RemoveAt(points.Count - 1); Try(() => _model.SetPath(_phase, id, points, Meta(_pathSpeed), _pathWait.Value)); }
    }

    void DrawPaths()
    {
        foreach (var id in _model.PlayerIds)
        {
            var points = _model.GetPath(_phase, id);
            if (points.Count == 0) continue;
            bool mine = id == _selected;
            var color = mine ? UiTheme.Accent : new Color(1, 1, 1, 0.55f);
            var (px, py) = _model.GetPosition(_phase, id);
            var prev = ToScreen(px, py);
            for (int i = 0; i < points.Count; i++)
            {
                var next = ToScreen(points[i].X, points[i].Y);
                DrawDashedLine(prev, next, color, mine ? 3 : 2, 8);
                var dir = (next - prev).Normalized();
                if (next.DistanceTo(prev) > 20)
                {
                    var n = new Vector2(-dir.Y, dir.X);
                    DrawColoredPolygon(new[] { next - dir * 9, next - dir * 22 + n * 6, next - dir * 22 - n * 6 }, color);
                }
                DrawCircle(next, 10, new Color(color, 0.95f));
                DrawString(GameFonts.Display, next + new Vector2(-4, 5), (i + 1).ToString(), HorizontalAlignment.Left, -1, 14, UiTheme.Bg);
                prev = next;
            }
            DrawDashedLine(prev, ToScreen(px, py), new Color(color, 0.35f), 1.5f, 6);      // way back to the phase position
        }
    }

    (HSlider, Label) Follow(VBoxContainer parent, string text)
    {
        var label = Small(text); parent.AddChild(label);
        var s = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05 }; parent.AddChild(s);
        s.ValueChanged += _ => Try(() => { if (_selected != null) _model.SetFollow(_phase, _selected, _followX.Value, _followY.Value); }, refresh: false);
        return (s, label);
    }

    void ApplyMarking() => Try(() =>
    {
        if (_selected is null) return;
        _model.SetMarking(_phase, _selected, _markBox.ButtonPressed ? Meta(_markTarget) : null);
    });

    void BuildTeamTab()
    {
        var t = Tab(_tabs, "TEAM");
        foreach (var (setting, title) in new[]
        {
            ("tempo", "Tempo"), ("pressing_intensiteit", "Druk zetten"), ("lijnhoogte", "Lijnhoogte"), ("compactheid", "Compactheid")
        })
        {
            var label = UiTheme.Label(title, UiTheme.Body); t.AddChild(label);
            var slider = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.01 }; t.AddChild(slider);
            slider.ValueChanged += v => Try(() => { _model.SetTeamSetting(setting, v); label.Text = $"{title}: {v:F2}"; }, refresh: false);
            _teamSliders[setting] = (slider, label, title);
        }
        t.AddChild(Small("Pressingvorm"));
        _pressForm = Options(TacticEditorModel.PressingForms); t.AddChild(_pressForm);
        _pressForm.ItemSelected += _ => Try(() => _model.SetPressingForm(Meta(_pressForm)));
        t.AddChild(Small("Tempo en druk bepalen hoe snel en hoe vaak het team naar voren gaat; lijnhoogte en compactheid hoe ver het team van achter naar voren staat en hoe dicht de lijnen op elkaar zitten."));
    }

    void BuildRulesTab()
    {
        var t = Tab(_tabs, "REGELS");
        t.AddChild(UiTheme.Label("Triggers", 26, UiTheme.Accent, display: true));
        t.AddChild(Small("Als dit gebeurt in een fase, krijgt een speler een instructie."));
        _triggerList = new VBoxContainer(); t.AddChild(_triggerList);
        _trigName = new LineEdit { PlaceholderText = "naam van de regel" }; t.AddChild(_trigName);
        t.AddChild(Small("Fase")); _trigPhase = Options(TacticEditorModel.BasePhases, n => PhaseLabels[n]); t.AddChild(_trigPhase);
        t.AddChild(Small("Wanneer")); _trigCond = Options(TacticEditorModel.Conditions, Nice); t.AddChild(_trigCond);
        t.AddChild(Small("Zone (derde-baan)")); _trigZone = Options(new[] { "" }.Concat(TacticEditorModel.Zones), z => z == "" ? "overal" : z); t.AddChild(_trigZone);
        t.AddChild(Small("Speler")); _trigPlayer = new OptionButton { CustomMinimumSize = new Vector2(0, 40) }; t.AddChild(_trigPlayer);
        t.AddChild(Small("Instructie")); _trigInstr = Options(TacticEditorModel.AllowedInstructions.Where(i => i != "dek_man"), Nice); t.AddChild(_trigInstr);
        var addTrig = UiTheme.Secondary("+ Regel toevoegen"); t.AddChild(addTrig);
        addTrig.Pressed += () => Try(() =>
        {
            var zone = Meta(_trigZone);
            _model.AddTrigger(_trigName.Text.Length > 0 ? _trigName.Text : "Regel " + (_model.Triggers.Count + 1),
                Meta(_trigPhase), Meta(_trigCond), zone.Length == 0 ? null : zone, new[] { (Meta(_trigPlayer), Meta(_trigInstr)) });
            _trigName.Text = "";
        });

        t.AddChild(new HSeparator());
        t.AddChild(UiTheme.Label("Blokwissels", 26, UiTheme.Accent, display: true));
        t.AddChild(Small("Wissel tijdens de wedstrijd van verdedigend blok (hoog, midden, laag)."));
        _switchList = new VBoxContainer(); t.AddChild(_switchList);
        _swName = new LineEdit { PlaceholderText = "naam van de wissel" }; t.AddChild(_swName);
        t.AddChild(Small("Stand")); _swStand = Options(new[] { "" }.Concat(TacticEditorModel.Standings), s => s == "" ? "maakt niet uit" : s); t.AddChild(_swStand);
        t.AddChild(Small("Vanaf minuut (0 = niet gebruiken)")); _swMinute = new SpinBox { MinValue = 0, MaxValue = 120, Value = 0 }; t.AddChild(_swMinute);
        t.AddChild(Small("Vermoeidheid boven (0 = niet gebruiken)")); _swFatigue = new SpinBox { MinValue = 0, MaxValue = 1, Step = 0.05, Value = 0 }; t.AddChild(_swFatigue);
        t.AddChild(Small("Naar blok")); _swTo = Options(TacticEditorModel.BlockHeights); t.AddChild(_swTo);
        var addSw = UiTheme.Secondary("+ Blokwissel toevoegen"); t.AddChild(addSw);
        addSw.Pressed += () => Try(() =>
        {
            var stand = Meta(_swStand);
            _model.AddBlockSwitch(_swName.Text.Length > 0 ? _swName.Text : "Wissel " + (_model.BlockSwitches.Count + 1),
                stand.Length == 0 ? null : stand, _swMinute.Value > 0 ? (int)_swMinute.Value : null,
                _swFatigue.Value > 0 ? _swFatigue.Value : null, Meta(_swTo));
            _swName.Text = "";
        });
    }

    void BuildPhaseTab()
    {
        var t = Tab(_tabs, "FASE");
        t.AddChild(Small("Acties op de gekozen fase (chips boven het veld)."));
        var mirror = UiTheme.Secondary("Spiegel links en rechts"); t.AddChild(mirror);
        mirror.Pressed += () => Try(() => _model.Mirror(_phase));

        t.AddChild(Small("Zet de lijnhoogte van alle spelers naar een sjabloon (gemeten waarden)"));
        _lineTemplate = Options(TacticTemplates.Lines.Keys, k => k); t.AddChild(_lineTemplate);
        var apply = UiTheme.Secondary("Zet lijnhoogte"); t.AddChild(apply);
        apply.Pressed += () => Try(() => _model.ApplyLineTemplate(_phase, Meta(_lineTemplate)));

        t.AddChild(Small("Kopieer deze fase naar"));
        _copyTarget = new OptionButton { CustomMinimumSize = new Vector2(0, 40) }; t.AddChild(_copyTarget);
        var copy = UiTheme.Secondary("Kopieer"); t.AddChild(copy);
        copy.Pressed += () => Try(() => _model.CopyPhase(_phase, Meta(_copyTarget)));

        t.AddChild(Small("Maak een fase gelijk aan een andere (bespaart dubbel werk)"));
        _aliasTarget = Options(TacticEditorModel.BasePhases, n => PhaseLabels[n]); t.AddChild(_aliasTarget);
        var alias = UiTheme.Secondary("Maak deze fase gelijk aan de gekozen fase"); t.AddChild(alias);
        alias.Pressed += () => Try(() => _model.SetAlias(_phase, Meta(_aliasTarget)));
    }

    // ---- state -----------------------------------------------------------------------------------------------------

    void Guard(Action proceed)
    {
        if (!_dirty) { proceed(); return; }
        foreach (var c in _confirm.GetSignalConnectionList(AcceptDialog.SignalName.Confirmed)) _confirm.Disconnect(AcceptDialog.SignalName.Confirmed, c["callable"].AsCallable());
        _confirm.Confirmed += () => { _dirty = false; proceed(); };
        _confirm.PopupCentered();
    }

    void SetModel(TacticEditorModel model, string fileStem)
    {
        _model = model;
        _name.Text = fileStem;
        _savedAs = null;
        _dirty = false;
        _phase = _model.Phases.Contains("opbouw") ? "opbouw" : _model.Phases[0];
        _selected = _model.PlayerIds.FirstOrDefault();
        RefreshAll();
        SetStatus("Sleep spelers op het veld; kies een fase boven het veld.", true);
    }

    void NewTactic(string key, string label)
    {
        var stem = "mijn_" + label.Replace("-", "");
        for (int n = 2; File.Exists(Path.Combine(GameSession.WritableTacticsDir(), stem + ".json")); n++) stem = $"mijn_{label.Replace("-", "")}_{n}";
        _sourcePath = null;
        SetModel(TacticTemplates.Create(key, stem), stem);
        SetStatus($"Nieuwe tactiek vanuit {label}. Pas aan en sla op.", true);
    }

    // Sharing (async friends mode, roadmap 3b): a tactic is plain JSON, so the clipboard is enough to hand it to a friend.
    void CopyToClipboard()
    {
        if (_model is null) return;
        try
        {
            _model.Validate();
            DisplayServer.ClipboardSet(_model.ToJson());
            SetStatus("Tactiek gekopieerd. Stuur de tekst naar een vriend; die kiest Open > Plak tactiek.", true);
        }
        catch (Exception e) { SetStatus("Kopiëren mislukt: " + e.Message, false); }
    }

    void PasteFromClipboard()
    {
        var text = DisplayServer.ClipboardGet();
        if (string.IsNullOrWhiteSpace(text)) { SetStatus("Het klembord is leeg.", false); return; }
        try
        {
            var model = TacticEditorModel.Parse(text);           // validated by the real loader before anything is shown
            var stem = TacticFileNames.Sanitize(model.Name);          // no reserved Windows names, at most 60 characters
            if (stem.Length == 0) stem = "geimporteerd";
            for (int n = 2; File.Exists(Path.Combine(GameSession.WritableTacticsDir(), stem + ".json")); n++) stem = $"geimporteerd_{n}";
            _sourcePath = null;
            SetModel(model, stem);
            _dirty = true;
            SetStatus($"Tactiek '{model.Name}' geplakt. Controleer hem en sla op.", true);
        }
        catch (Exception e) { SetStatus("Dat is geen geldige tactiek: " + e.Message, false); }
    }

    void OpenFile(string path)
    {
        try
        {
            _sourcePath = path;
            var stem = Path.GetFileNameWithoutExtension(path);
            SetModel(TacticEditorModel.LoadFile(path), stem.EndsWith("_aangepast") ? stem : stem + "_aangepast");
            SetStatus($"'{stem}' geopend. Opslaan maakt een nieuw bestand, de oorspronkelijke blijft staan.", true);
        }
        catch (Exception e) { SetStatus("Openen mislukt: " + e.Message, false); }
    }

    /// <summary>Runs a model edit; on failure the model has rolled back and the reason is shown.</summary>
    void Try(Action edit, bool refresh = true)
    {
        if (_model is null) return;
        try { edit(); _dirty = true; if (refresh) RefreshAll(); else QueueRedraw(); }
        catch (Exception e) { SetStatus(e.Message, false); RefreshAll(); }
    }

    void SetStatus(string text, bool good)
    {
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", good ? UiTheme.Good : UiTheme.Bad);
    }

    void RefreshAll()
    {
        if (_model is null) return;
        if (!_model.AllPhases.Contains(_phase)) _phase = "opbouw";
        BuildChips();
        RefreshPlayer();
        RefreshTeam();
        RefreshRules();
        RefreshPhaseTools();
        var alias = _model.IsAlias(_phase) ? $"   •   gelijk aan {PhaseLabels.GetValueOrDefault(_model.AliasOf(_phase) ?? "", "?")} (bewerken maakt de fase los)" : "";
        _phaseHint.Text = $"Aanval naar rechts{alias}";
        QueueRedraw();
    }

    void BuildChips()
    {
        foreach (var c in _chips.GetChildren()) c.QueueFree();
        var group = new ButtonGroup();
        foreach (var phase in _model.AllPhases.OrderBy(p => Array.IndexOf(PhaseOrder, p)))
        {
            var label = PhaseLabels.GetValueOrDefault(phase, phase.ToUpperInvariant());
            if (_model.IsAlias(phase)) label += " =";
            var chip = new Button { Text = label, ToggleMode = true, ButtonGroup = group, ThemeTypeVariation = "Chip", CustomMinimumSize = new Vector2(0, 32) };
            chip.SetPressedNoSignal(phase == _phase);
            var p = phase;
            chip.Pressed += () => { _phase = p; RefreshAll(); };
            _chips.AddChild(chip);
        }
        UiTheme.Apply(_chips);
    }

    void RefreshPlayer()
    {
        bool has = _selected is not null;
        _playerTitle.Text = has ? $"{_selected}  •  {Nice(_model.GetRole(_selected!))}" : "KIES EEN SPELER";
        if (!has) return;
        SelectMeta(_role, _model.GetRole(_selected!));
        var marking = _model.GetMarking(_phase, _selected!);
        _markBox.SetPressedNoSignal(marking is not null);
        _markTarget.Disabled = marking is null;
        if (marking is not null) SelectMeta(_markTarget, marking);
        var follow = _model.GetFollow(_phase, _selected!);
        _followX.SetValueNoSignal(follow?.X ?? 0); _followY.SetValueNoSignal(follow?.Y ?? 0);
        _followXLabel.Text = $"links-rechts als de bal beweegt: {(follow?.X ?? 0):F2}";
        _followYLabel.Text = $"breedte als de bal beweegt: {(follow?.Y ?? 0):F2}";
        var instr = _model.GetInstructions(_phase, _selected!);
        foreach (var (name, box) in _instructionBoxes) box.SetPressedNoSignal(instr.Contains(name));
        var path = _model.GetPath(_phase, _selected!);
        _pathInfo.Text = path.Count == 0 ? "Geen pad voor deze speler in deze fase." : $"{path.Count} punt(en); loopt eenmaal bij fasebegin.";
        SelectMeta(_pathSpeed, _model.GetPathSpeed(_phase, _selected!));
        _pathWait.SetValueNoSignal(_model.GetPathWait(_phase, _selected!));
        _pathToggle.SetPressedNoSignal(_drawPath);
    }

    static void SelectMeta(OptionButton o, string value)
    {
        for (int i = 0; i < o.ItemCount; i++) if ((string)o.GetItemMetadata(i) == value) { o.Select(i); return; }
    }

    void RefreshTeam()
    {
        foreach (var (setting, (slider, label, title)) in _teamSliders)
        {
            var v = _model.GetTeamSetting(setting) ?? 0.5;
            slider.SetValueNoSignal(v);
            label.Text = $"{title}: {v:F2}";
        }
        SelectMeta(_pressForm, _model.GetPressingForm());
    }

    void RefreshRules()
    {
        // players and phases for the forms
        var prev = Meta(_trigPlayer);
        _trigPlayer.Clear();
        foreach (var id in _model.PlayerIds) { _trigPlayer.AddItem($"{id}  ({Nice(_model.GetRole(id))})"); _trigPlayer.SetItemMetadata(_trigPlayer.ItemCount - 1, id); }
        if (prev.Length > 0) SelectMeta(_trigPlayer, prev);

        Rebuild(_triggerList, _model.Triggers.Count == 0 ? new[] { (Text: "Nog geen regels.", Index: -1) } :
            _model.Triggers.Select((t, i) => (Text: $"{(t.Name.Length > 0 ? t.Name : "regel")}\n{PhaseLabels[t.Phase]} • {Nice(t.Condition)}{(t.Zone is null ? "" : " • " + t.Zone)}\n→ " +
                string.Join(", ", t.Actions.Select(a => $"{a.Player}: {Nice(a.Instruction)}")), Index: i)),
            i => Try(() => _model.RemoveTrigger(i)));
        Rebuild(_switchList, _model.BlockSwitches.Count == 0 ? new[] { (Text: "Nog geen blokwissels.", Index: -1) } :
            _model.BlockSwitches.Select((b, i) => (Text: $"{(b.Name.Length > 0 ? b.Name : "wissel")}\n" +
                string.Join(" • ", new[] { b.Stand is null ? null : "stand " + b.Stand, b.FromMinute is null ? null : "vanaf min. " + b.FromMinute,
                    b.FatigueAbove is null ? null : $"vermoeid > {b.FatigueAbove:F2}" }.Where(s => s != null)) + $"\n→ blok {b.To}", Index: i)),
            i => Try(() => _model.RemoveBlockSwitch(i)));
    }

    static void Rebuild(VBoxContainer list, IEnumerable<(string Text, int Index)> rows, Action<int> remove)
    {
        foreach (var c in list.GetChildren()) c.QueueFree();
        foreach (var (text, index) in rows)
        {
            var row = new HBoxContainer(); list.AddChild(row);
            var l = UiTheme.Label(text, UiTheme.Small, index < 0 ? UiTheme.Muted : UiTheme.Text);
            l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; l.AutowrapMode = TextServer.AutowrapMode.WordSmart; row.AddChild(l);
            if (index < 0) continue;
            var x = new Button { Text = "✕", CustomMinimumSize = new Vector2(40, 40) };
            var i = index;
            x.Pressed += () => remove(i);
            row.AddChild(x);
        }
    }

    void RefreshPhaseTools()
    {
        _copyTarget.Clear();
        foreach (var p in _model.AllPhases.Where(p => p != _phase))
        { _copyTarget.AddItem(PhaseLabels.GetValueOrDefault(p, p)); _copyTarget.SetItemMetadata(_copyTarget.ItemCount - 1, p); }
    }

    // ---- save / input / draw ---------------------------------------------------------------------------------------

    void Save()
    {
        try
        {
            var safe = TacticFileNames.Sanitize(_name.Text);
            if (safe.Length == 0)
            {
                SetStatus(TacticFileNames.IsReserved(new string(_name.Text.Where(char.IsLetterOrDigit).ToArray()))
                    ? "Die naam is gereserveerd door Windows (bijv. CON, NUL, COM1); kies een andere naam." : "Geef een geldige bestandsnaam.", false);
                return;
            }
            Directory.CreateDirectory(GameSession.WritableTacticsDir());
            var target = Path.Combine(GameSession.WritableTacticsDir(), safe + ".json");
            if (File.Exists(target) && safe != _savedAs)
            {
                SetStatus($"'{safe}.json' bestaat al; kies een andere naam (bestaande tactieken worden niet overschreven).", false);
                return;
            }
            var old = _model.Name;
            _model.Name = safe;
            try { _model.Save(target); }
            catch { _model.Name = old; throw; }
            if (!File.Exists(target)) { _model.Name = old; throw new IOException("het bestand staat na het opslaan niet op schijf"); }
            _savedAs = safe; _dirty = false;
            _name.Text = safe;                                  // show the name as it was really saved (cleaned and cut)
            GameSession.LastSavedTactic = (OS.HasFeature("editor") ? "" : TacticCatalog.OwnPrefix) + safe + ".json";
            FillOpenMenu();
            SetStatus($"Opgeslagen als {safe}.json; hij staat nu in het menu.", true);
        }
        catch (Exception e) { SetStatus("Opslaan mislukt: " + e.Message, false); }
    }

    public override void _Process(double delta)
    {
        var shot = GameSession.DebugEnvironment("ZEUS_SHOT");
        if (shot.Length == 0 || ++_frames != 12) return;
        GetViewport().GetTexture().GetImage().SavePng(shot + "_editor.png");
        GetTree().Quit();
    }

    static Vector2 ToScreen(double x, double y) => Origin + new Vector2((float)((x - 0.5) * L), (float)((y - 0.5) * W)) * Px;
    static (double, double) FromScreen(Vector2 s) => ((s.X - Origin.X) / Px / L + 0.5, (s.Y - Origin.Y) / Px / W + 0.5);

    // Release can be swallowed by a control under the cursor, so also stop dragging on any input once the button is up.
    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }) { _drag = null; QueueRedraw(); }
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && GetViewport().GuiGetFocusOwner() is not LineEdit)
        {
            if (_drawPath) { _drawPath = false; _pathToggle.SetPressedNoSignal(false); SetStatus("Tekenen gestopt.", true); QueueRedraw(); }
            else Guard(() => GetTree().ChangeSceneToFile(_returnScene));
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_model is null) return;
        if (_drag is not null && !Input.IsMouseButtonPressed(MouseButton.Left)) _drag = null;
        if (_drawPath && e is InputEventMouseButton { Pressed: true } click)
        {
            var field = new Rect2(Origin - new Vector2(L * Px / 2, W * Px / 2), new Vector2(L * Px, W * Px)).Grow(8);
            if (click.ButtonIndex == MouseButton.Left && field.HasPoint(click.Position)) AddPathPoint(click.Position);
            else if (click.ButtonIndex == MouseButton.Right) RemoveLastPathPoint();
            return;
        }
        if (_drawPath) return;                       // no selecting or dragging while drawing a path
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
        {
            if (mb.Pressed)
            {
                _drag = _model.PlayerIds
                    .Select(id => (id, d: ToScreen(_model.GetPosition(_phase, id).X, _model.GetPosition(_phase, id).Y).DistanceTo(mb.Position)))
                    .Where(t => t.d < 16).OrderBy(t => t.d).Select(t => t.id).FirstOrDefault();
                if (_drag is not null) _selected = _drag;
                _pressPosition = mb.Position;
                _dragStarted = false;
                RefreshPlayer();
                QueueRedraw();
            }
            else _drag = null;
        }
        else if (e is InputEventMouseMotion mm && _drag is not null)
        {
            if (!_dragStarted && mm.Position.DistanceTo(_pressPosition) < 4) return;
            bool first = !_dragStarted;
            _dragStarted = true;
            var (x, y) = FromScreen(mm.Position);
            var id = _drag;
            Try(() => _model.SetPosition(_phase, id, x, y), refresh: first);      // a first move can detach an alias: rebuild the chips once
        }
    }

    public override void _Draw()
    {
        var field = new Rect2(Origin - new Vector2(L * Px / 2, W * Px / 2), new Vector2(L * Px, W * Px));
        DrawRect(new Rect2(0, 0, 1520, 780), UiTheme.Bg, true);
        GameArt.DrawPitch(this, field, 24);
        var chalk = new Color(1, 1, 1, 0.55f);
        var ay = field.End.Y - 16;
        DrawLine(new Vector2(field.Position.X + 380, ay), new Vector2(field.Position.X + 660, ay), chalk, 2);
        DrawColoredPolygon(new[] { new Vector2(field.Position.X + 676, ay), new Vector2(field.Position.X + 660, ay - 7), new Vector2(field.Position.X + 660, ay + 7) }, chalk);
        DrawString(GameFonts.Semi, new Vector2(field.Position.X + 280, ay + 5), "AANVAL", HorizontalAlignment.Left, -1, 15, chalk);
        if (_model is null) return;
        foreach (var id in _model.PlayerIds)
        {
            var (x, y) = _model.GetPosition(_phase, id);
            var marking = _model.GetMarking(_phase, id);
            GameArt.DrawPlayer(this, ToScreen(x, y), UiTheme.Home, id, 0, 13, id == _selected);
            if (marking is not null)
                DrawString(GameFonts.Semi, ToScreen(x, y) + new Vector2(-18, 30), "dekt " + marking, HorizontalAlignment.Left, -1, 13, UiTheme.Accent);
        }
        GameArt.DrawVignette(this, field.Grow(24), 0.4f);
        DrawPaths();
        if (_drawPath)
            DrawString(GameFonts.Display, new Vector2(field.Position.X + 20, field.Position.Y + 34), "PAD TEKENEN", HorizontalAlignment.Left, -1, 30, UiTheme.Accent);
    }
}
