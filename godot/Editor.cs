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
    string? _savedAs;
    string _status = "";
    OptionButton _phaseBox = null!;
    LineEdit _name = null!;

    public override void _Ready()
    {
        _path = GameSession.EditPath ?? Path.Combine(GameSession.TacticsDir(), "laag_blok_442.json");
        var early = new Button { Text = "Terug naar menu (Esc)", Position = new Vector2(1148, 44) };
        early.Pressed += () => GetTree().ChangeSceneToFile("res://Menu.tscn");
        AddChild(early);
        try { _model = TacticEditorModel.LoadFile(_path); }
        catch (Exception e) { _status = "Laden mislukt: " + e.Message; return; }
        early.QueueFree();
        _phase = _model.Phases.Contains("opbouw") ? "opbouw" : _model.Phases[0];

        var ui = new VBoxContainer { Position = new Vector2(1148, 44), CustomMinimumSize = new Vector2(340, 0) };
        AddChild(ui);
        ui.AddChild(new Label { Text = "TACTIEKEDITOR" });
        ui.AddChild(new Label { Text = "Spelfase" });
        _phaseBox = new OptionButton(); ui.AddChild(_phaseBox);
        foreach (var p in _model.Phases) _phaseBox.AddItem(p);
        _phaseBox.Select(_model.Phases.ToList().IndexOf(_phase));
        _phaseBox.ItemSelected += i => { _phase = _model.Phases[(int)i]; QueueRedraw(); };
        ui.AddChild(new Label { Text = "Naam van het bestand (zonder .json)" });
        var baseName = Path.GetFileNameWithoutExtension(_path).Replace("_aangepast", "");
        var suggestion = baseName + "_aangepast";
        for (int n = 2; File.Exists(Path.Combine(GameSession.TacticsDir(), suggestion + ".json")); n++) suggestion = $"{baseName}_aangepast{n}";
        _name = new LineEdit { Text = suggestion }; ui.AddChild(_name);
        var save = new Button { Text = "Opslaan in tactics/" }; save.Pressed += Save; ui.AddChild(save);
        var back = new Button { Text = "Terug naar menu" };
        back.Pressed += () => GetTree().ChangeSceneToFile("res://Menu.tscn"); ui.AddChild(back);
        ui.AddChild(new Label { Text = "Sleep een speler met de muis.\nEen fase die 'gelijk aan' een andere is\nwordt zelfstandig zodra je hem aanpast." });
    }

    static Vector2 ToScreen(double x, double y) => Origin + new Vector2((float)((x - 0.5) * L), (float)((y - 0.5) * W)) * Px;
    static (double, double) FromScreen(Vector2 s) => ((s.X - Origin.X) / Px / L + 0.5, (s.Y - Origin.Y) / Px / W + 0.5);

    void Save()
    {
        try
        {
            var safe = new string(_name.Text.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray());
            if (safe.Length == 0) { _status = "Geef een geldige naam."; QueueRedraw(); return; }
            var target = Path.Combine(GameSession.TacticsDir(), safe + ".json");
            if (File.Exists(target) && safe != _savedAs)
            {
                _status = $"'{safe}.json' bestaat al; kies een andere naam (overschrijven van bestaande tactieken is uit).";
                QueueRedraw();
                return;
            }
            var old = _model.Name;
            _model.Name = safe;
            try { _model.Save(target); }
            catch { _model.Name = old; throw; }
            _savedAs = safe;
            _status = "Opgeslagen: " + target;
        }
        catch (Exception e) { _status = "Opslaan mislukt: " + e.Message; }
        QueueRedraw();
    }

    // Release can be swallowed by a control under the cursor, so also stop dragging on any input once the button is up.
    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }) _drag = null;
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
            }
            else _drag = null;
        }
        else if (e is InputEventMouseMotion mm && _drag is not null)
        {
            var (x, y) = FromScreen(mm.Position);
            _model.SetPosition(_phase, _drag, x, y);
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var field = new Rect2(Origin - new Vector2(L * Px / 2, W * Px / 2), new Vector2(L * Px, W * Px));
        DrawRect(field, new Color(0.18f, 0.5f, 0.2f));
        var line = new Color(1, 1, 1, 0.8f);
        DrawRect(field, line, false, 2);
        DrawLine(Origin + new Vector2(0, -W * Px / 2), Origin + new Vector2(0, W * Px / 2), line, 2);
        DrawArc(Origin, 9.15f * Px, 0, Mathf.Tau, 64, line, 2);
        if (_model is null)
        {
            DrawString(ThemeDB.FallbackFont, new Vector2(20, 30), _status, HorizontalAlignment.Left, -1, 16, Colors.OrangeRed);
            return;
        }
        foreach (var id in _model.PlayerIds)
        {
            var (x, y) = _model.GetPosition(_phase, id);
            var p = ToScreen(x, y);
            DrawCircle(p, 9, id == _drag ? Colors.Yellow : new Color(0.9f, 0.1f, 0.1f));
            DrawArc(p, 9, 0, Mathf.Tau, 24, Colors.White, 1.5f);
            DrawString(ThemeDB.FallbackFont, p + new Vector2(11, -9), id, HorizontalAlignment.Left, -1, 12, Colors.White);
        }
        string alias = _model.IsAlias(_phase) ? "  (gelijk aan een andere fase)" : "";
        DrawString(ThemeDB.FallbackFont, new Vector2(12, 24), $"Fase: {_phase}{alias}   aanvalsrichting: naar rechts",
            HorizontalAlignment.Left, -1, 16, Colors.White);
        DrawString(ThemeDB.FallbackFont, new Vector2(12, 755), _status, HorizontalAlignment.Left, -1, 14, Colors.LightGreen);
    }
}
