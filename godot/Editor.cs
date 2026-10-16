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

    static string PhaseLabel(string phase) => Loc.Name(phase).ToUpperInvariant();

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
        _confirm = new ConfirmationDialog { Title = Loc.T("editor.unsaved_title"), DialogText = Loc.T("editor.unsaved_text") };
        _confirm.Theme = UiTheme.Create();
        _confirm.GetOkButton().Text = Loc.T("common.ok");
        _confirm.GetCancelButton().Text = Loc.T("common.cancel");
        AddChild(_confirm);
        BuildChrome();
        _returnScene = GameSession.ReturnScene ?? _returnScene; GameSession.ReturnScene = null;
        _sourcePath = GameSession.EditPath ?? Path.Combine(GameSession.TacticsDir(), "laag_blok_442.json");
        try { SetModel(TacticEditorModel.LoadFile(_sourcePath), Path.GetFileNameWithoutExtension(_sourcePath)); }
        catch (Exception e) { SetStatus(Loc.T("editor.load_failed") + Loc.Error(e), false); }
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

        var panel = new PanelContainer { Position = new Vector2(1148, 8), Size = new Vector2(364, 764), ClipContents = true };
        UiTheme.Apply(panel);
        AddChild(panel);
        var col = new VBoxContainer(); col.AddThemeConstantOverride("separation", 8); panel.AddChild(col);

        col.AddChild(UiTheme.Label(Loc.T("editor.title"), UiTheme.Heading + 4, UiTheme.Accent, display: true));
        var files = new HBoxContainer(); col.AddChild(files);
        var newMenu = new MenuButton { Text = Loc.T("editor.new"), CustomMinimumSize = new Vector2(0, 38) };
        var newPopup = newMenu.GetPopup();
        var formations = TacticTemplates.List;
        for (int i = 0; i < formations.Count; i++) newPopup.AddItem(Loc.T("editor.new_formation") + formations[i].Label, i);
        newPopup.IdPressed += id => Guard(() => NewTactic(formations[(int)id].Key, formations[(int)id].Label));
        files.AddChild(newMenu);
        var openMenu = new MenuButton { Text = Loc.T("editor.open"), CustomMinimumSize = new Vector2(0, 38) };
        _openPopup = openMenu.GetPopup();
        FillOpenMenu();
        _openPopup.AboutToPopup += FillOpenMenu;               // a tactic saved a moment ago must be in the list
        _openPopup.IdPressed += id =>
        {
            if (id == OpenCopyId) CopyToClipboard();
            else if (id == OpenPasteId) Guard(PasteFromClipboard);
            else if (id == OpenImportId) ShowTacticImport();
            else if (id == OpenExportId) ShowTacticExport();
            else if (id >= 0 && id < _openFiles.Length) { var file = _openFiles[(int)id]; Guard(() => OpenFile(file)); }
        };
        files.AddChild(openMenu);
        var back = UiTheme.Secondary(Loc.T("editor.menu")); back.CustomMinimumSize = new Vector2(0, 38);
        back.Pressed += () => Guard(() => GetTree().ChangeSceneToFile(_returnScene)); files.AddChild(back);

        var nameRow = new HBoxContainer(); col.AddChild(nameRow);
        _name = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, PlaceholderText = Loc.T("editor.filename") }; nameRow.AddChild(_name);
        var save = UiTheme.Primary(Loc.T("editor.save")); save.CustomMinimumSize = new Vector2(130, 44); save.Pressed += Save; nameRow.AddChild(save);

        _status = UiTheme.Label("", UiTheme.Small, UiTheme.Muted);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart; _status.CustomMinimumSize = new Vector2(0, 40);
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
        _openPopup.AddSeparator(Loc.T("editor.share"));
        _openPopup.AddItem(Loc.T("editor.copy_clipboard"), OpenCopyId);
        _openPopup.AddItem(Loc.T("editor.paste_clipboard"), OpenPasteId);
        _openPopup.AddItem(Loc.T("editor.import_file"), OpenImportId);
        _openPopup.AddItem(Loc.T("editor.export_file"), OpenExportId);
    }

    static VBoxContainer Tab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        tabs.AddChild(scroll);
        tabs.SetTabTitle(tabs.GetTabCount() - 1, title);
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
        var o = new OptionButton { FitToLongestItem = false, CustomMinimumSize = new Vector2(0, 40), ClipText = true };
        foreach (var i in items) { o.AddItem(text?.Invoke(i) ?? Loc.Name(i)); o.SetItemMetadata(o.ItemCount - 1, i); }
        return o;
    }

    static string Meta(OptionButton o) => o.Selected < 0 ? "" : (string)o.GetItemMetadata(o.Selected);
    static string Nice(string s) => Loc.Name(s);

    void BuildPlayerTab()
    {
        var t = Tab(_tabs, Loc.T("editor.player_tab"));
        _playerTitle = UiTheme.Label("", 28, UiTheme.Accent, display: true); t.AddChild(_playerTitle);
        t.AddChild(Small(Loc.T("editor.role")));
        _role = Options(TacticEditorModel.Roles, Nice); t.AddChild(_role);
        _role.ItemSelected += _ => Try(() => { if (_selected != null) _model.SetRole(_selected, Meta(_role)); });

        _markBox = new CheckBox { Text = Loc.T("editor.mark") }; t.AddChild(_markBox);
        _markTarget = Options(OpponentIds); t.AddChild(_markTarget);
        _markBox.Toggled += _ => ApplyMarking();
        _markTarget.ItemSelected += _ => { if (_markBox.ButtonPressed) ApplyMarking(); };

        t.AddChild(Small(Loc.T("editor.follow_ball")));
        (_followX, _followXLabel) = Follow(t, Loc.T("editor.follow_x"));
        (_followY, _followYLabel) = Follow(t, Loc.T("editor.follow_y"));
        t.AddChild(Small(Loc.T("editor.instructions")));
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
        t.AddChild(UiTheme.Label(Loc.T("editor.path"), 24, UiTheme.Accent, display: true));
        t.AddChild(Small(Loc.T("editor.path_help")));
        _pathToggle = new Button { Text = Loc.T("editor.draw_path"), ToggleMode = true, ThemeTypeVariation = "Chip", CustomMinimumSize = new Vector2(0, 40) };
        _pathToggle.Toggled += on =>
        {
            _drawPath = on;
            SetStatus(on ? Loc.T("editor.draw_help") : Loc.T("editor.draw_stopped"), true);
            QueueRedraw();
        };
        t.AddChild(_pathToggle);
        _pathInfo = Small(""); t.AddChild(_pathInfo);
        t.AddChild(Small(Loc.T("editor.speed")));
        _pathSpeed = Options(new[] { "wandelen", "joggen", "sprint" }); t.AddChild(_pathSpeed);
        _pathSpeed.ItemSelected += _ => RewritePath();
        t.AddChild(Small(Loc.T("editor.point_wait")));
        _pathWait = new SpinBox { MinValue = 0, MaxValue = 5, Step = 0.5 }; t.AddChild(_pathWait);
        _pathWait.ValueChanged += _ => RewritePath();
        var clear = UiTheme.Secondary(Loc.T("editor.clear_path")); t.AddChild(clear);
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
        if (points.Count >= 8) { SetStatus(Loc.T("editor.path_limit"), false); return; }
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
        var t = Tab(_tabs, Loc.T("editor.team_tab"));
        foreach (var (setting, title) in new[]
        {
            ("tempo", Loc.T("editor.tempo")), ("pressing_intensiteit", Loc.T("editor.pressing")), ("lijnhoogte", Loc.T("editor.line_height")), ("compactheid", Loc.T("editor.compactness"))
        })
        {
            var label = UiTheme.Label(title, UiTheme.Body); t.AddChild(label);
            var slider = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.01 }; t.AddChild(slider);
            slider.ValueChanged += v => Try(() => { _model.SetTeamSetting(setting, v); label.Text = Loc.T("common.label_value", title, v); }, refresh: false);
            _teamSliders[setting] = (slider, label, title);
        }
        t.AddChild(Small(Loc.T("editor.pressing_form")));
        _pressForm = Options(TacticEditorModel.PressingForms); t.AddChild(_pressForm);
        _pressForm.ItemSelected += _ => Try(() => _model.SetPressingForm(Meta(_pressForm)));
        t.AddChild(Small(Loc.T("editor.team_help")));
    }

    void BuildRulesTab()
    {
        var t = Tab(_tabs, Loc.T("editor.rules_tab"));
        t.AddChild(UiTheme.Label(Loc.T("editor.triggers"), 26, UiTheme.Accent, display: true));
        t.AddChild(Small(Loc.T("editor.triggers_help")));
        _triggerList = new VBoxContainer(); t.AddChild(_triggerList);
        _trigName = new LineEdit { PlaceholderText = Loc.T("editor.rule_name") }; t.AddChild(_trigName);
        t.AddChild(Small(Loc.T("editor.phase"))); _trigPhase = Options(TacticEditorModel.BasePhases, n => PhaseLabel(n)); t.AddChild(_trigPhase);
        t.AddChild(Small(Loc.T("editor.when"))); _trigCond = Options(TacticEditorModel.Conditions, Nice); t.AddChild(_trigCond);
        t.AddChild(Small(Loc.T("editor.zone"))); _trigZone = Options(new[] { "" }.Concat(TacticEditorModel.Zones), z => z == "" ? Loc.T("editor.everywhere") : Nice(z)); t.AddChild(_trigZone);
        t.AddChild(Small(Loc.T("editor.player"))); _trigPlayer = new OptionButton { FitToLongestItem = false, CustomMinimumSize = new Vector2(0, 40), ClipText = true }; t.AddChild(_trigPlayer);
        t.AddChild(Small(Loc.T("editor.instruction"))); _trigInstr = Options(TacticEditorModel.AllowedInstructions.Where(i => i != "dek_man"), Nice); t.AddChild(_trigInstr);
        var addTrig = UiTheme.Secondary(Loc.T("editor.add_rule")); t.AddChild(addTrig);
        addTrig.Pressed += () => Try(() =>
        {
            var zone = Meta(_trigZone);
            _model.AddTrigger(_trigName.Text.Length > 0 ? _trigName.Text : Loc.T("editor.rule_prefix") + (_model.Triggers.Count + 1),
                Meta(_trigPhase), Meta(_trigCond), zone.Length == 0 ? null : zone, new[] { (Meta(_trigPlayer), Meta(_trigInstr)) });
            _trigName.Text = "";
        });

        t.AddChild(new HSeparator());
        t.AddChild(UiTheme.Label(Loc.T("editor.block_switches"), 26, UiTheme.Accent, display: true));
        t.AddChild(Small(Loc.T("editor.block_help")));
        _switchList = new VBoxContainer(); t.AddChild(_switchList);
        _swName = new LineEdit { PlaceholderText = Loc.T("editor.switch_name") }; t.AddChild(_swName);
        t.AddChild(Small(Loc.T("editor.score"))); _swStand = Options(new[] { "" }.Concat(TacticEditorModel.Standings), s => s == "" ? Loc.T("editor.any_score") : Nice(s)); t.AddChild(_swStand);
        t.AddChild(Small(Loc.T("editor.from_minute"))); _swMinute = new SpinBox { MinValue = 0, MaxValue = 120, Value = 0 }; t.AddChild(_swMinute);
        t.AddChild(Small(Loc.T("editor.fatigue_above"))); _swFatigue = new SpinBox { MinValue = 0, MaxValue = 1, Step = 0.05, Value = 0 }; t.AddChild(_swFatigue);
        t.AddChild(Small(Loc.T("editor.to_block"))); _swTo = Options(TacticEditorModel.BlockHeights); t.AddChild(_swTo);
        var addSw = UiTheme.Secondary(Loc.T("editor.add_switch")); t.AddChild(addSw);
        addSw.Pressed += () => Try(() =>
        {
            var stand = Meta(_swStand);
            _model.AddBlockSwitch(_swName.Text.Length > 0 ? _swName.Text : Loc.T("editor.switch_prefix") + (_model.BlockSwitches.Count + 1),
                stand.Length == 0 ? null : stand, _swMinute.Value > 0 ? (int)_swMinute.Value : null,
                _swFatigue.Value > 0 ? _swFatigue.Value : null, Meta(_swTo));
            _swName.Text = "";
        });
    }

    void BuildPhaseTab()
    {
        var t = Tab(_tabs, Loc.T("editor.phase_tab"));
        t.AddChild(Small(Loc.T("editor.phase_help")));
        var mirror = UiTheme.Secondary(Loc.T("editor.mirror")); t.AddChild(mirror);
        mirror.Pressed += () => Try(() => _model.Mirror(_phase));

        t.AddChild(Small(Loc.T("editor.line_template_help")));
        _lineTemplate = Options(TacticTemplates.Lines.Keys, Nice); t.AddChild(_lineTemplate);
        var apply = UiTheme.Secondary(Loc.T("editor.apply_line")); t.AddChild(apply);
        apply.Pressed += () => Try(() => _model.ApplyLineTemplate(_phase, Meta(_lineTemplate)));

        t.AddChild(Small(Loc.T("editor.copy_phase_to")));
        _copyTarget = new OptionButton { FitToLongestItem = false, CustomMinimumSize = new Vector2(0, 40), ClipText = true }; t.AddChild(_copyTarget);
        var copy = UiTheme.Secondary(Loc.T("editor.copy")); t.AddChild(copy);
        copy.Pressed += () => Try(() => _model.CopyPhase(_phase, Meta(_copyTarget)));

        t.AddChild(Small(Loc.T("editor.alias_help")));
        _aliasTarget = Options(TacticEditorModel.BasePhases, n => PhaseLabel(n)); t.AddChild(_aliasTarget);
        var alias = UiTheme.Secondary(Loc.T("editor.alias")); alias.ClipText = true; t.AddChild(alias);
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
        SetStatus(Loc.T("editor.drag_help"), true);
    }

    void NewTactic(string key, string label)
    {
        var stem = "my_" + label.Replace("-", "");
        for (int n = 2; File.Exists(Path.Combine(GameSession.WritableTacticsDir(), stem + ".json")); n++) stem = $"my_{label.Replace("-", "")}_{n}";
        _sourcePath = null;
        SetModel(TacticTemplates.Create(key, stem), stem);
        SetStatus(Loc.T("editor.new_created", label), true);
    }

    // Sharing (async friends mode, roadmap 3b): a tactic is plain JSON, so the clipboard is enough to hand it to a friend.
    void CopyToClipboard()
    {
        if (_model is null) return;
        try
        {
            _model.Validate();
            DisplayServer.ClipboardSet(_model.ToJson());
            SetStatus(Loc.T("editor.copied"), true);
        }
        catch (Exception e) { SetStatus(Loc.T("editor.copy_failed") + Loc.Error(e), false); }
    }

    void PasteFromClipboard()
    {
        var text = DisplayServer.ClipboardGet();
        if (string.IsNullOrWhiteSpace(text)) { SetStatus(Loc.T("editor.clipboard_empty"), false); return; }
        try
        {
            var model = TacticEditorModel.Parse(text);           // validated by the real loader before anything is shown
            var stem = TacticFileNames.Sanitize(model.Name);          // no reserved Windows names, at most 60 characters
            if (stem.Length == 0) stem = "imported";
            for (int n = 2; File.Exists(Path.Combine(GameSession.WritableTacticsDir(), stem + ".json")); n++) stem = $"imported_{n}";
            _sourcePath = null;
            SetModel(model, stem);
            _dirty = true;
            SetStatus(Loc.T("editor.pasted", Loc.Name(model.Name)), true);
        }
        catch (Exception e) { SetStatus(Loc.T("editor.invalid_tactic") + Loc.Error(e), false); }
    }

    void OpenFile(string path)
    {
        try
        {
            _sourcePath = path;
            var stem = Path.GetFileNameWithoutExtension(path);
            SetModel(TacticEditorModel.LoadFile(path), stem.EndsWith("_edited") ? stem : stem + "_edited");
            SetStatus(Loc.T("editor.opened", stem), true);
        }
        catch (Exception e) { SetStatus(Loc.T("editor.open_failed") + Loc.Error(e), false); }
    }

    /// <summary>Runs a model edit; on failure the model has rolled back and the reason is shown.</summary>
    void Try(Action edit, bool refresh = true)
    {
        if (_model is null) return;
        try { edit(); _dirty = true; if (refresh) RefreshAll(); else QueueRedraw(); }
        catch (Exception e) { SetStatus(Loc.Error(e), false); RefreshAll(); }
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
        var alias = _model.IsAlias(_phase) ? Loc.T("editor.alias_hint", PhaseLabel(_model.AliasOf(_phase) ?? "")) : "";
        _phaseHint.Text = Loc.T("editor.attack_direction", alias);
        QueueRedraw();
    }

    void BuildChips()
    {
        foreach (var c in _chips.GetChildren()) c.QueueFree();
        var group = new ButtonGroup();
        foreach (var phase in _model.AllPhases.OrderBy(p => Array.IndexOf(PhaseOrder, p)))
        {
            var label = PhaseLabel(phase);
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
        _playerTitle.Text = has ? $"{_selected}  •  {Nice(_model.GetRole(_selected!))}" : Loc.T("editor.select_player");
        if (!has) return;
        SelectMeta(_role, _model.GetRole(_selected!));
        var marking = _model.GetMarking(_phase, _selected!);
        _markBox.SetPressedNoSignal(marking is not null);
        _markTarget.Disabled = marking is null;
        if (marking is not null) SelectMeta(_markTarget, marking);
        var follow = _model.GetFollow(_phase, _selected!);
        _followX.SetValueNoSignal(follow?.X ?? 0); _followY.SetValueNoSignal(follow?.Y ?? 0);
        _followXLabel.Text = Loc.T("editor.follow_x_value", follow?.X ?? 0);
        _followYLabel.Text = Loc.T("editor.follow_y_value", follow?.Y ?? 0);
        var instr = _model.GetInstructions(_phase, _selected!);
        foreach (var (name, box) in _instructionBoxes) box.SetPressedNoSignal(instr.Contains(name));
        var path = _model.GetPath(_phase, _selected!);
        _pathInfo.Text = path.Count == 0 ? Loc.T("editor.no_path") : Loc.T("editor.path_points", path.Count);
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
            label.Text = Loc.T("common.label_value", title, v);
        }
        SelectMeta(_pressForm, _model.GetPressingForm());
    }

    void RefreshRules()
    {
        // players and phases for the forms
        var prev = Meta(_trigPlayer);
        _trigPlayer.Clear();
        foreach (var id in _model.PlayerIds) { _trigPlayer.AddItem(Loc.T("common.player_role", id, Nice(_model.GetRole(id)))); _trigPlayer.SetItemMetadata(_trigPlayer.ItemCount - 1, id); }
        if (prev.Length > 0) SelectMeta(_trigPlayer, prev);

        Rebuild(_triggerList, _model.Triggers.Count == 0 ? new[] { (Text: Loc.T("editor.no_rules"), Index: -1) } :
            _model.Triggers.Select((t, i) => (Text: $"{(t.Name.Length > 0 ? Nice(t.Name) : Loc.T("editor.rule"))}\n{PhaseLabel(t.Phase)} • {Nice(t.Condition)}{(t.Zone is null ? "" : " • " + Nice(t.Zone))}\n→ " +
                string.Join(", ", t.Actions.Select(a => $"{a.Player}: {Nice(a.Instruction)}")), Index: i)),
            i => Try(() => _model.RemoveTrigger(i)));
        Rebuild(_switchList, _model.BlockSwitches.Count == 0 ? new[] { (Text: Loc.T("editor.no_switches"), Index: -1) } :
            _model.BlockSwitches.Select((b, i) => (Text: $"{(b.Name.Length > 0 ? Nice(b.Name) : Loc.T("editor.switch"))}\n" +
                string.Join(" • ", new[] { b.Stand is null ? null : Loc.T("editor.score_prefix") + Nice(b.Stand), b.FromMinute is null ? null : Loc.T("editor.minute_prefix") + b.FromMinute,
                    b.FatigueAbove is null ? null : Loc.T("editor.fatigue_rule", b.FatigueAbove) }.Where(s => s != null)) + Loc.T("editor.block_rule", Nice(b.To)), Index: i)),
            i => Try(() => _model.RemoveBlockSwitch(i)));
    }

    static void Rebuild(VBoxContainer list, IEnumerable<(string Text, int Index)> rows, Action<int> remove)
    {
        foreach (var c in list.GetChildren()) c.QueueFree();
        foreach (var (text, index) in rows)
        {
            var row = new HBoxContainer(); list.AddChild(row);
            var l = UiTheme.Label(text, UiTheme.Small, index < 0 ? UiTheme.Muted : UiTheme.Text);
            l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; l.AutowrapMode = TextServer.AutowrapMode.WordSmart; l.CustomMinimumSize = new Vector2(10, 0); row.AddChild(l);
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
        { _copyTarget.AddItem(PhaseLabel(p)); _copyTarget.SetItemMetadata(_copyTarget.ItemCount - 1, p); }
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
                    ? Loc.T("editor.reserved_name") : Loc.T("editor.invalid_filename"), false);
                return;
            }
            Directory.CreateDirectory(GameSession.WritableTacticsDir());
            var target = Path.Combine(GameSession.WritableTacticsDir(), safe + ".json");
            if (File.Exists(target) && safe != _savedAs)
            {
                SetStatus(Loc.T("editor.exists", safe), false);
                return;
            }
            var old = _model.Name;
            _model.Name = safe;
            try { _model.Save(target); }
            catch { _model.Name = old; throw; }
            if (!File.Exists(target)) { _model.Name = old; throw new IOException(Loc.T("editor.missing_save")); }
            _savedAs = safe; _dirty = false;
            _name.Text = safe;                                  // show the name as it was really saved (cleaned and cut)
            GameSession.LastSavedTactic = (OS.HasFeature("editor") ? "" : TacticCatalog.OwnPrefix) + safe + ".json";
            FillOpenMenu();
            SetStatus(Loc.T("editor.saved", safe), true);
        }
        catch (Exception e) { SetStatus(Loc.T("editor.save_failed") + Loc.Error(e), false); }
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
            if (_drawPath) { _drawPath = false; _pathToggle.SetPressedNoSignal(false); SetStatus(Loc.T("editor.draw_stopped"), true); QueueRedraw(); }
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
        DrawString(GameFonts.Semi, new Vector2(field.Position.X + 280, ay + 5), Loc.T("editor.attack"), HorizontalAlignment.Left, -1, 15, chalk);
        if (_model is null) return;
        foreach (var id in _model.PlayerIds)
        {
            var (x, y) = _model.GetPosition(_phase, id);
            var marking = _model.GetMarking(_phase, id);
            GameArt.DrawPlayer(this, ToScreen(x, y), UiTheme.Home, id, 0, 13, id == _selected);
            if (marking is not null)
                DrawString(GameFonts.Semi, ToScreen(x, y) + new Vector2(-18, 30), Loc.T("editor.marks") + marking, HorizontalAlignment.Left, -1, 13, UiTheme.Accent);
        }
        GameArt.DrawVignette(this, field.Grow(24), 0.4f);
        DrawPaths();
        if (_drawPath)
            DrawString(GameFonts.Display, new Vector2(field.Position.X + 20, field.Position.Y + 34), Loc.T("editor.drawing"), HorizontalAlignment.Left, -1, 30, UiTheme.Accent);
    }
}
