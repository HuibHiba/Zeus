// Display only: reads Zeus.Sim snapshots, no game logic. Sim runs on a fixed step via an accumulator.
using System;
using System.IO;
using System.Linq;
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
    bool _debug;
    int _shots;
    int _countedEvents;
    int _passCount;
    ReplayFile? _replay;
    bool _replayVerified;

    static string Env(string name, string fallback)
    {
        var v = OS.GetEnvironment(name);
        return v.Length > 0 ? v : fallback;
    }

    public override void _Ready()
    {
        try { InitializeMatch(); }
        catch (Exception error)
        {
            GD.PushError($"Zeus kon niet starten: {error.Message}");
            SetProcess(false);
            GetTree().Quit(1);
        }
    }

    void InitializeMatch()
    {
        var replayPath = OS.GetEnvironment("ZEUS_REPLAY");
        Tactic home, away;
        MatchConfig config;
        ulong seed = 1;
        if (replayPath.Length > 0)
        {
            _replay = ReplayFile.Load(replayPath);
            home = TacticLoader.Parse(_replay.HomeTacticJson);
            away = TacticLoader.Parse(_replay.AwayTacticJson);
            config = _replay.Config;
            seed = _replay.Seed;
        }
        else
        {
            var defaultDir = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "tactics"));
            var dir = Env("ZEUS_TACTICS_DIR", defaultDir);
            home = TacticLoader.LoadFile(Path.Combine(dir, Env("ZEUS_HOME", "laag_blok_442.json")));
            away = TacticLoader.LoadFile(Path.Combine(dir, Env("ZEUS_AWAY", "feyenoord_433_voorlopig.json")));
            config = new MatchConfig();
        }
        _stepSeconds = config.StepSeconds;
        _match = Match.Create(config, home, away, seed);
        _prev = _curr = _match.GetSnapshot();
        if (OS.GetEnvironment("ZEUS_SHOT").Length > 0) _speed = 4.0;
        _debug = OS.GetEnvironment("ZEUS_DEBUG").Length > 0;
        GD.Print($"Loaded '{home.Name}' (home) vs '{away.Name}' (away), step {_stepSeconds}s");
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        switch (k.Keycode)
        {
            case Key.Space: if (!_replayVerified) _paused = !_paused; break;
            case Key.Key1: _speed = 1.0; break;
            case Key.Key4: _speed = 4.0; break;
            case Key.D: _debug = !_debug; break;
        }
    }

    public override void _Process(double delta)
    {
        CheckReplayEnd();
        if (!_paused)
        {
            _acc += Math.Min(delta, 0.25) * _speed;
            while (_acc >= _stepSeconds)
            {
                _acc -= _stepSeconds;
                _match.Step();
                _prev = _curr;
                _curr = _match.GetSnapshot();
                CheckReplayEnd();
                if (_replayVerified) break;
            }
        }
        // Count each event once; the full log grows during long simulations.
        var events = _match.Events.Events;
        while (_countedEvents < events.Count)
            if (events[_countedEvents++].Type == EventType.Pass) _passCount++;
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

    void CheckReplayEnd()
    {
        if (_replay is null || _replayVerified || _curr.Tick < _replay.Steps) return;
        _paused = true;
        _acc = 0;
        try { _replay.Verify(_match); }
        catch (InvalidOperationException error)
        {
            GD.PushError($"Replaycontrole mislukt: {error.Message}");
            SetProcess(false);
            GetTree().Quit(1);
            return;
        }
        _replayVerified = true;
        GD.Print($"Replay verified after {_curr.Tick} steps: {_match.EventHash()}");
    }

    public override void _Draw()
    {
        if (_curr is null) return;
        DrawPitch();
        float alpha = _paused ? 1f : (float)Math.Clamp(_acc / _stepSeconds, 0, 1);
        for (int i = 0; i < _curr.Players.Count; i++)
        {
            var c = _curr.Players[i];
            if (c.SentOff) continue;
            var p0 = i < _prev.Players.Count ? _prev.Players[i].Pos : c.Pos;
            var pos = ToScreen(new Vec2(p0.X + (c.Pos.X - p0.X) * alpha, p0.Y + (c.Pos.Y - p0.Y) * alpha));
            var col = c.Team == Side.Home ? new Color(0.9f, 0.1f, 0.1f) : new Color(0.15f, 0.35f, 0.95f);
            if (_debug) DrawPlayerDebug(c, pos, col);
            DrawCircle(pos, 7, col);
            DrawArc(pos, 7, 0, Mathf.Tau, 24, Colors.White, 1.5f);
            var dir = new Vector2((float)Math.Cos(c.Heading), (float)Math.Sin(c.Heading));
            DrawLine(pos, pos + dir * 14, Colors.White, 2);
            if (c.YellowCards > 0)
                DrawRect(new Rect2(pos + new Vector2(-3, -15), new Vector2(6, 9)), Colors.Yellow);
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
            $"{m:D2}:{s:D2}  tick {_curr.Tick}  {_speed:0}x{(_paused ? "  PAUZE" : "")}   rood = thuis, blauw = uit   [spatie] pauze  [1] 1x  [4] 4x  [D] debug {(_debug ? "aan" : "uit")}",
            HorizontalAlignment.Left, -1, 16, Colors.White);
        DrawString(ThemeDB.FallbackFont, new Vector2(12, 755),
            $"Balbezit: {(_match.Possession == Side.Home ? "thuis" : "uit")}  speler: {_match.CarrierId ?? "vrije bal"}   Fases: {_match.PhaseOf(Side.Home)} / {_match.PhaseOf(Side.Away)}   Passes: {_passCount}",
            HorizontalAlignment.Left, -1, 16, Colors.White);
        DrawPanel();
    }

    void DrawPlayerDebug(PlayerSnapshot player, Vector2 pos, Color color)
    {
        if (_match.TargetOf(player.Team, player.Id) is { } target)
        {
            var end = ToScreen(target);
            DrawLine(pos, end, new Color(color.R, color.G, color.B, 0.5f), 1);
            DrawLine(end - new Vector2(4, 4), end + new Vector2(4, 4), Colors.White, 1);
            DrawLine(end - new Vector2(4, -4), end + new Vector2(4, -4), Colors.White, 1);
        }
        if (_match.IntentOf(player.Team, player.Id) is { Kind: IntentKind.Pass } pass)
        {
            var end = ToScreen(pass.Target);
            DrawLine(pos, end, Colors.Yellow, 2);
            DrawArc(end, 6, 0, Mathf.Tau, 16, Colors.Yellow, 2);
        }
        DrawString(ThemeDB.FallbackFont, pos + new Vector2(9, -9), player.Id,
            HorizontalAlignment.Left, -1, 12, Colors.White);
    }

    static string Short(string text, int max = 39) => text.Length <= max ? text : text[..(max - 1)] + "…";

    void PanelLine(string text, float y, Color? color = null, int size = 15) =>
        DrawString(ThemeDB.FallbackFont, new Vector2(1162, y), Short(text),
            HorizontalAlignment.Left, -1, size, color ?? Colors.White);

    void DrawPanel()
    {
        DrawRect(new Rect2(1148, 44, 360, 688), new Color(0.08f, 0.11f, 0.15f));
        PanelLine("ZEUS / WEDSTRIJD", 70, size: 18);
        PanelLine($"Status: {_curr.Status}", 99);
        PanelLine($"Passes: {_passCount}", 122);
        int yellow = 0, dismissed = 0;
        foreach (var player in _curr.Players)
        {
            yellow += player.YellowCards;
            if (player.SentOff) dismissed++;
        }
        PanelLine($"Geel: {yellow}   Uitgesloten: {dismissed}", 145);
        if (_replay is not null)
            PanelLine(_replayVerified ? "Replay klaar / hash gecontroleerd" : $"Replay: {_curr.Tick}/{_replay.Steps}", 164,
                Colors.LightGreen, 13);
        PanelLine("LAATSTE EVENTS", 183, size: 17);
        var events = _match.Events.Events;
        int row = 0;
        for (int i = Math.Max(0, events.Count - 6); i < events.Count; i++, row++)
        {
            var e = events[i];
            float y = 209 + row * 53;
            PanelLine($"{e.Seconds:0.0}s  {e.Type}", y);
            PanelLine($"{e.Team} {e.PlayerId} {e.Outcome}", y + 20, new Color(0.75f, 0.8f, 0.85f), 13);
        }
        PanelLine(_debug ? "DEBUG AAN" : "[D] toont doelen en spelers-id's", 555, size: 16);
        if (_debug)
        {
            PanelLine("Wit kruis: loopdoel", 578);
            PanelLine("Geel rondje/lijn: passintentie", 601);
            var carrier = _curr.Players.FirstOrDefault(p => p.Team == _match.Possession && p.Id == _match.CarrierId);
            if (carrier is not null && _match.PerceptionOf(carrier.Team, carrier.Id) is { } view)
            {
                PanelLine($"Balhouder ziet {view.VisiblePlayers.Count} spelers", 627);
                PanelLine($"Intentie: {_match.IntentOf(carrier.Team, carrier.Id)?.Kind}", 650);
            }
        }
        float cardY = 675;
        foreach (var player in _curr.Players.Where(p => p.SentOff).Take(2))
        {
            PanelLine($"ROOD: {player.Team} {player.Id}", cardY, Colors.OrangeRed, 13);
            cardY += 20;
        }
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
