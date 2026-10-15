// Tactic editor v1: drag the 11 players per phase on the pitch, then save a validated tactic file.
using System;
using System.IO;
using System.Linq;
using Godot;
using Zeus.Sim;

public partial class Editor : Node2D
{
    const float Px = 10f;
    static readonly Vector2 Origin = new(600, 390);
    const float L = (float)Pitch.Length, W = (float)Pitch.Width;

    TacticEditorModel _model = null!;
    string _path = "";
    string _phase = "opbouw";
    string? _drag;
    string? _selected;
    Vector2 _pressPosition;
    bool _dragStarted;
    string? _savedAs;
    Label _statusLabel = null!, _phaseHint = null!;
    int _frames;
    OptionButton _phaseBox = null!;
    LineEdit _name = null!;
    Label _playerLabel = null!;
    readonly System.Collections.Generic.Dictionary<string, CheckBox> _instructionBoxes = new();

    public override void _Ready()
    {
        _path = GameSession.EditPath ?? Path.Combine(GameSession.TacticsDir(), "laag_blok_442.json");
        RenderingServer.SetDefaultClearColor(UiTheme.Bg);
        var panel = new PanelContainer { Position = new Vector2(1148, 20), Size = new Vector2(360, 740) };
        UiTheme.Apply(panel);
        AddChild(panel);
        var layout = new VBoxContainer(); panel.AddChild(layout);
        layout.AddChild(UiTheme.Label("Tactiekeditor", UiTheme.Heading + 10, null, true));
        var back = UiTheme.Secondary("Terug naar menu [Esc]");
        back.Pressed += () => GetTree().ChangeSceneToFile("res://Menu.tscn"); layout.AddChild(back);
        _statusLabel = UiTheme.Label("Selecteer een speler op het veld.", UiTheme.Small, UiTheme.Muted);
        _statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        layout.AddChild(_statusLabel);
        _phaseHint = UiTheme.Label("", UiTheme.Body); _phaseHint.Position = new Vector2(75, 16); AddChild(_phaseHint);
        try { _model = TacticEditorModel.LoadFile(_path); }
        catch (Exception e) { SetStatus("Laden mislukt: " + e.Message, false); return; }
        _phase = _model.Phases.Contains("opbouw") ? "opbouw" : _model.Phases[0];
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        layout.AddChild(scroll);
        var ui = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(ui);
        ui.AddChild(UiTheme.Label("Spelfase", UiTheme.Body, UiTheme.Muted));
        _phaseBox = new OptionButton(); ui.AddChild(_phaseBox);
        foreach (var p in _model.Phases) _phaseBox.AddItem(p);
        _phaseBox.Select(_model.Phases.ToList().IndexOf(_phase));
        _phaseBox.ItemSelected += i => { _phase = _model.Phases[(int)i]; RefreshInstructions(); QueueRedraw(); };
        ui.AddChild(UiTheme.Label("Bestandsnaam (zonder .json)", UiTheme.Small, UiTheme.Muted));
        var baseName = Path.GetFileNameWithoutExtension(_path).Replace("_aangepast", "");
        var suggestion = baseName + "_aangepast";
        for (int n = 2; File.Exists(Path.Combine(GameSession.TacticsDir(), suggestion + ".json")); n++) suggestion = $"{baseName}_aangepast{n}";
        _name = new LineEdit { Text = suggestion }; ui.AddChild(_name);
        var save = UiTheme.Primary("Opslaan"); save.Pressed += Save; ui.AddChild(save);
        ui.AddChild(UiTheme.Label("Klik: selecteer een speler.\nSlepen: verplaats de speler.\nEsc: terug naar menu.", UiTheme.Small, UiTheme.Muted));
        ui.AddChild(UiTheme.Label("Teaminstellingen", UiTheme.Heading - 6));
        foreach (var (setting, title) in new[]
        {
            ("tempo", "Tempo"), ("pressing_intensiteit", "Pressing-intensiteit"),
            ("lijnhoogte", "Lijnhoogte"), ("compactheid", "Compactheid")
        })
        {
            var value = _model.GetTeamSetting(setting);
            var label = UiTheme.Label($"{title}: {(value ?? 0.5):F2}", UiTheme.Small);
            ui.AddChild(label);
            var slider = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.01, Value = value ?? 0.5 };
            slider.ValueChanged += v =>
            {
                _model.SetTeamSetting(setting, v);
                label.Text = $"{title}: {_model.GetTeamSetting(setting):F2}";
            };
            ui.AddChild(slider);
        }
        _playerLabel = UiTheme.Label("", UiTheme.Body, UiTheme.Accent);
        _playerLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        ui.AddChild(_playerLabel);
        foreach (var instruction in TacticEditorModel.AllowedInstructions)
        {
            var box = new CheckBox { Text = instruction.Replace('_', ' ') };
            box.AddThemeFontSizeOverride("font_size", UiTheme.Small);
            box.Toggled += _ =>
            {
                if (_selected is null) return;
                _model.ToggleInstruction(_phase, _selected, instruction);
                RefreshInstructions();
                QueueRedraw();
            };
            _instructionBoxes.Add(instruction, box);
            ui.AddChild(box);
        }
        _selected = _model.PlayerIds.FirstOrDefault();
        RefreshInstructions();
    }

    void SetStatus(string text, bool good)
    {
        _statusLabel.Text = text;
        _statusLabel.AddThemeColorOverride("font_color", good ? UiTheme.Good : UiTheme.Bad);
    }

    public override void _Process(double delta)
    {
        var shot = OS.GetEnvironment("ZEUS_SHOT");
        if (shot.Length == 0 || ++_frames != 10) return;
        GetViewport().GetTexture().GetImage().SavePng(shot + "_editor.png");
        GetTree().Quit();
    }

    void RefreshInstructions()
    {
        _phaseHint.Text = $"Fase: {_phase}  |  Sleep spelers op het veld  |  Aanval naar rechts";
        _playerLabel.Text = _selected is null ? "Instructies: kies een speler" : $"Instructies: {_selected} / {_phase}";
        var instructions = _selected is null ? Array.Empty<string>() : _model.GetInstructions(_phase, _selected);
        foreach (var (instruction, box) in _instructionBoxes)
        {
            box.Disabled = _selected is null;
            box.SetPressedNoSignal(instructions.Contains(instruction));
        }
    }

    static Vector2 ToScreen(double x, double y) => Origin + new Vector2((float)((x - 0.5) * L), (float)((y - 0.5) * W)) * Px;
    static (double, double) FromScreen(Vector2 s) => ((s.X - Origin.X) / Px / L + 0.5, (s.Y - Origin.Y) / Px / W + 0.5);

    void Save()
    {
        try
        {
            var safe = new string(_name.Text.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray());
            if (safe.Length == 0) { SetStatus("Geef een geldige naam.", false); QueueRedraw(); return; }
            var target = Path.Combine(GameSession.TacticsDir(), safe + ".json");
            if (File.Exists(target) && safe != _savedAs)
            {
                SetStatus($"'{safe}.json' bestaat al; kies een andere naam.", false);
                QueueRedraw();
                return;
            }
            var old = _model.Name;
            _model.Name = safe;
            try { _model.Save(target); }
            catch { _model.Name = old; throw; }
            _savedAs = safe;
            SetStatus("Opgeslagen: " + safe + ".json", true);
        }
        catch (Exception e) { SetStatus("Opslaan mislukt: " + e.Message, false); }
        QueueRedraw();
    }

    // Release can be swallowed by a control under the cursor, so also stop dragging on any input once the button is up.
    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            _drag = null;
            QueueRedraw();
        }
        if (e is InputEventKey { Pressed: true, Keycode: Key.Escape }) GetTree().ChangeSceneToFile("res://Menu.tscn");
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_model is null) return;
        if (_drag is not null && !Input.IsMouseButtonPressed(MouseButton.Left)) _drag = null;
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
        {
            if (mb.Pressed)
            {
                _drag = _model.PlayerIds
                    .Select(id => (id, d: ToScreen(_model.GetPosition(_phase, id).X, _model.GetPosition(_phase, id).Y).DistanceTo(mb.Position)))
                    .Where(t => t.d < 12).OrderBy(t => t.d).Select(t => t.id).FirstOrDefault();
                _selected = _drag;
                _pressPosition = mb.Position;
                _dragStarted = false;
                RefreshInstructions();
                QueueRedraw();
            }
            else _drag = null;
        }
        else if (e is InputEventMouseMotion mm && _drag is not null)
        {
            if (!_dragStarted && mm.Position.DistanceTo(_pressPosition) < 4) return;
            _dragStarted = true;
            var (x, y) = FromScreen(mm.Position);
            _model.SetPosition(_phase, _drag, x, y);
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var field = new Rect2(Origin - new Vector2(L * Px / 2, W * Px / 2), new Vector2(L * Px, W * Px));
        DrawRect(new Rect2(0, 0, 1520, 780), UiTheme.Bg, true);
        GameArt.DrawPitch(this, field, 24);
        // Attack direction arrow in chalk along the lower touchline.
        var chalk = new Color(1, 1, 1, 0.55f);
        var ay = field.End.Y - 16;
        DrawLine(new Vector2(field.Position.X + 380, ay), new Vector2(field.Position.X + 660, ay), chalk, 2);
        DrawColoredPolygon(new[] { new Vector2(field.Position.X + 676, ay), new Vector2(field.Position.X + 660, ay - 7), new Vector2(field.Position.X + 660, ay + 7) }, chalk);
        DrawString(GameFonts.Semi, new Vector2(field.Position.X + 280, ay + 5), "AANVAL", HorizontalAlignment.Left, -1, 15, chalk);
        if (_model is null) return;
        foreach (var id in _model.PlayerIds)
        {
            var (x, y) = _model.GetPosition(_phase, id);
            GameArt.DrawPlayer(this, ToScreen(x, y), UiTheme.Home, id, 0, 13, id == _selected);
        }
        GameArt.DrawVignette(this, field.Grow(24), 0.4f);

    }
}
