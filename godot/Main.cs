// Display only: reads Zeus.Sim snapshots, no game logic. Sim runs on a fixed step via an accumulator.
using System;
using System.IO;
using Godot;
using Zeus.Sim;
using Side = Zeus.Sim.Side;

public partial class Main : Node2D
{
    const float Px = 10f;                       // pixels per metre
    static readonly Vector2 Origin = new(600, 390);

    Match _match = null!;
    Snapshot _prev = null!, _curr = null!;
    double _stepSeconds;
    double _acc;
    double _speed = 1.0;
    bool _paused;
    int _shots;

    static string Env(string name, string fallback)
    {
        var v = OS.GetEnvironment(name);
        return v.Length > 0 ? v : fallback;
    }

    public override void _Ready()
    {
        var defaultDir = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "tactics"));
        var dir = Env("ZEUS_TACTICS_DIR", defaultDir);
        var home = TacticLoader.LoadFile(Path.Combine(dir, Env("ZEUS_HOME", "laag_blok_442.json")));
        var away = TacticLoader.LoadFile(Path.Combine(dir, Env("ZEUS_AWAY", "feyenoord_433_voorlopig.json")));
        var config = new MatchConfig();
        _stepSeconds = config.StepSeconds;
        _match = Match.Create(config, home, away, 1UL);
        _prev = _curr = _match.GetSnapshot();
        if (OS.GetEnvironment("ZEUS_SHOT").Length > 0) _speed = 4.0;
        GD.Print($"Loaded '{home.Name}' (home) vs '{away.Name}' (away), step {_stepSeconds}s");
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        switch (k.Keycode)
        {
            case Key.Space: _paused = !_paused; break;
            case Key.Key1: _speed = 1.0; break;
            case Key.Key4: _speed = 4.0; break;
        }
    }

    public override void _Process(double delta)
    {
        if (!_paused)
        {
            _acc += Math.Min(delta, 0.25) * _speed;
            while (_acc >= _stepSeconds)
            {
                _acc -= _stepSeconds;
                _match.Step();
                _prev = _curr;
                _curr = _match.GetSnapshot();
            }
        }
        QueueRedraw();

        var shot = OS.GetEnvironment("ZEUS_SHOT");
        if (shot.Length == 0) return;
        double t = _curr.TimeSeconds;
        if ((_shots == 0 && t > 6) || (_shots == 1 && t > 14))
        {
            GetViewport().GetTexture().GetImage().SavePng($"{shot}_{_shots + 1}.png");
            GD.Print($"Screenshot {_shots + 1} at sim time {t:F1} s");
            _shots++;
        }
        if (_shots >= 2) GetTree().Quit();
    }

    static Vector2 ToScreen(Vec2 p) => Origin + new Vector2((float)p.X, (float)p.Y) * Px;

    public override void _Draw()
    {
        DrawPitch();
        float alpha = _paused ? 1f : (float)Math.Clamp(_acc / _stepSeconds, 0, 1);
        for (int i = 0; i < _curr.Players.Count; i++)
        {
            var c = _curr.Players[i];
            var p0 = i < _prev.Players.Count ? _prev.Players[i].Pos : c.Pos;
            var pos = ToScreen(new Vec2(p0.X + (c.Pos.X - p0.X) * alpha, p0.Y + (c.Pos.Y - p0.Y) * alpha));
            var col = c.Team == Side.Home ? new Color(0.9f, 0.1f, 0.1f) : new Color(0.15f, 0.35f, 0.95f);
            DrawCircle(pos, 7, col);
            DrawArc(pos, 7, 0, Mathf.Tau, 24, Colors.White, 1.5f);
            var dir = new Vector2((float)Math.Cos(c.Heading), (float)Math.Sin(c.Heading));
            DrawLine(pos, pos + dir * 14, Colors.White, 2);
        }
        if (_curr.Ball is { } ball)
        {
            var previous = _prev.Ball?.Pos ?? ball.Pos;
            var pos = ToScreen(new Vec2(previous.X + (ball.Pos.X - previous.X) * alpha,
                previous.Y + (ball.Pos.Y - previous.Y) * alpha));
            DrawCircle(pos, 5, Colors.Black);
            DrawCircle(pos, 3.5f, Colors.White);
        }
        int m = (int)(_curr.TimeSeconds / 60), s = (int)(_curr.TimeSeconds % 60);
        DrawString(ThemeDB.FallbackFont, new Vector2(12, 24),
            $"{m:D2}:{s:D2}  tick {_curr.Tick}  {_speed:0}x{(_paused ? "  PAUZE" : "")}   rood = thuis, blauw = uit   [spatie] pauze  [1] 1x  [4] 4x",
            HorizontalAlignment.Left, -1, 16, Colors.White);
        DrawString(ThemeDB.FallbackFont, new Vector2(12, 755),
            $"Balbezit: {(_match.Possession == Side.Home ? "thuis" : "uit")}  speler: {_match.CarrierId ?? "vrije bal"}   Fases: {_match.PhaseOf(Side.Home)} / {_match.PhaseOf(Side.Away)}",
            HorizontalAlignment.Left, -1, 16, Colors.White);
    }

    void DrawPitch()
    {
        const float L = (float)Pitch.Length, W = (float)Pitch.Width;
        var field = new Rect2(Origin - new Vector2(L * Px / 2, W * Px / 2), new Vector2(L * Px, W * Px));
        DrawRect(field, new Color(0.18f, 0.5f, 0.2f));
        var line = new Color(1, 1, 1, 0.8f);
        DrawRect(field, line, false, 2);
        DrawLine(Origin + new Vector2(0, -W * Px / 2), Origin + new Vector2(0, W * Px / 2), line, 2);
        DrawArc(Origin, 9.15f * Px, 0, Mathf.Tau, 64, line, 2);
        DrawCircle(Origin, 2, line);
        foreach (var sign in new[] { -1, 1 })
        {
            float boxW = 16.5f * Px, boxH = 40.3f * Px;
            float x = Origin.X + sign * (L * Px / 2 - boxW / 2);
            DrawRect(new Rect2(x - boxW / 2, Origin.Y - boxH / 2, boxW, boxH), line, false, 2);
        }
    }
}
