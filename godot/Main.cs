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
    bool _phases, _triggers, _passes, _offside, _events;
    readonly DebugViewState _view = new();
    int _shots;
    int _countedEvents;
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
            if (GameSession.HomePath is not null)
            {
                GameSession.LastError = "Wedstrijd kon niet starten: " + error.Message;
                GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://Menu.tscn");
            }
            else GetTree().Quit(1);
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
            var dir = Env("ZEUS_TACTICS_DIR", GameSession.TacticsDir());
            home = TacticLoader.LoadFile(GameSession.HomePath ?? Path.Combine(dir, Env("ZEUS_HOME", "laag_blok_442.json")));
            away = TacticLoader.LoadFile(GameSession.AwayPath ?? Path.Combine(dir, Env("ZEUS_AWAY", "testcase_a_433.json")));
            config = new MatchConfig();
            seed = GameSession.Seed;
        }
        _stepSeconds = config.StepSeconds;
        _match = Match.Create(config, home, away, seed);
        _prev = _curr = _match.GetSnapshot();
        if (OS.GetEnvironment("ZEUS_SHOT").Length > 0) _speed = 4.0;
        _debug = OS.GetEnvironment("ZEUS_DEBUG").Length > 0;
        SetDebugLayers(_debug);
        ReadEvents();
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
            case Key.D: _debug = !_debug; SetDebugLayers(_debug); break;
            case Key.F: _phases = !_phases; break;
            case Key.T: _triggers = !_triggers; break;
            case Key.P: _passes = !_passes; break;
            case Key.O: _offside = !_offside; break;
            case Key.E: _events = !_events; break;
            case Key.Escape: GetTree().ChangeSceneToFile("res://Menu.tscn"); break;
        }
    }

    // Counts per team for the end screen, straight from the event stream.
    int[]? _endStats;   // computed once when the match finishes: shots, passes, controls, fouls, offsides (home, away each)

    int Count(EventType type, Side team) => _match.Events.Events.Count(e => e.Type == type && e.Team == team);

    int Stat(int i, Side team) => _endStats![i * 2 + (team == Side.Home ? 0 : 1)];

    void DrawEndScreen()
    {
        if (_endStats is null)
        {
            var types = new[] { EventType.Shot, EventType.Pass, EventType.BallControl, EventType.Foul, EventType.Offside };
            _endStats = types.SelectMany(t => new[] { Count(t, Side.Home), Count(t, Side.Away) }).ToArray();
        }
        DrawRect(new Rect2(300, 200, 600, 360), new Color(0.05f, 0.07f, 0.1f, 0.94f));
        var f = ThemeDB.FallbackFont;
        DrawString(f, new Vector2(330, 245), "EINDSTAND", HorizontalAlignment.Left, -1, 26, Colors.White);
        DrawString(f, new Vector2(330, 290), $"{_curr.HomeScore} - {_curr.AwayScore}", HorizontalAlignment.Left, -1, 40, Colors.Yellow);
        int hc = Stat(2, Side.Home), ac = Stat(2, Side.Away);
        double homePoss = hc + ac == 0 ? 50 : 100.0 * hc / (hc + ac);
        string[] rows =
        {
            $"Schoten            {Stat(0, Side.Home),4}  {Stat(0, Side.Away),4}",
            $"Passes             {Stat(1, Side.Home),4}  {Stat(1, Side.Away),4}",
            $"Balbezit (%)       {homePoss,4:0}  {100 - homePoss,4:0}",
            $"Overtredingen      {Stat(3, Side.Home),4}  {Stat(3, Side.Away),4}",
            $"Buitenspel         {Stat(4, Side.Home),4}  {Stat(4, Side.Away),4}",
        };
        DrawString(f, new Vector2(330, 335), "                   thuis  uit", HorizontalAlignment.Left, -1, 16, Colors.Gray);
        for (int i = 0; i < rows.Length; i++)
            DrawString(f, new Vector2(330, 365 + i * 28), rows[i], HorizontalAlignment.Left, -1, 18, Colors.White);
        DrawString(f, new Vector2(330, 535), "[Esc] terug naar het menu", HorizontalAlignment.Left, -1, 18, Colors.LightGreen);
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
                ReadEvents();
                CheckReplayEnd();
                if (_replayVerified) break;
            }
        }
        _view.Advance(_curr.TimeSeconds);
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

    void SetDebugLayers(bool enabled) => _phases = _triggers = _passes = _offside = _events = enabled;

    void ReadEvents()
    {
        var events = _match.Events.Events;
        while (_countedEvents < events.Count) _view.Read(events[_countedEvents++], _curr);
        _view.Advance(_curr.TimeSeconds);
    }

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
        if (_offside) DrawOffside();
        if (_passes) DrawLastPass();
        float alpha = _paused ? 1f : (float)Math.Clamp(_acc / _stepSeconds, 0, 1);
        for (int i = 0; i < _curr.Players.Count; i++)
        {
            var c = _curr.Players[i];
            if (c.SentOff) continue;
            var p0 = i < _prev.Players.Count ? _prev.Players[i].Pos : c.Pos;
            var pos = ToScreen(new Vec2(p0.X + (c.Pos.X - p0.X) * alpha, p0.Y + (c.Pos.Y - p0.Y) * alpha));
            var col = c.Team == Side.Home ? new Color(0.9f, 0.1f, 0.1f) : new Color(0.15f, 0.35f, 0.95f);
            DrawCircle(pos, 7, col);
            DrawArc(pos, 7, 0, Mathf.Tau, 24, Colors.White, 1.5f);
            var dir = new Vector2((float)Math.Cos(c.Heading), (float)Math.Sin(c.Heading));
            DrawLine(pos, pos + dir * 14, Colors.White, 2);
            if (c.YellowCards > 0)
                DrawRect(new Rect2(pos + new Vector2(-3, -15), new Vector2(6, 9)), Colors.Yellow);
            if (_phases || _triggers || _passes || _offside)
                DrawString(ThemeDB.FallbackFont, pos + new Vector2(9, -9),
                    c.Id + (_phases ? " / " + DebugViewState.PhaseLabel(_view.PhaseOf(c.Team)) : ""),
                    HorizontalAlignment.Left, -1, 12, Colors.White);
            if (_triggers)
            {
                var fired = _view.Triggers.Where(e => e.Team == c.Team && e.PlayerId == c.Id).ToArray();
                for (int j = 0; j < fired.Length; j++)
                {
                    DrawArc(pos, 12, 0, Mathf.Tau, 24, Colors.Orange, 2);
                    DrawString(ThemeDB.FallbackFont, pos + new Vector2(9, -25 - j * 15),
                        Short(fired[j].Outcome, 48), HorizontalAlignment.Left, -1, 12, Colors.Orange);
                }
            }
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
            $"{m:D2}:{s:D2}  tick {_curr.Tick}  {_speed:0}x{(_paused ? "  PAUZE" : "")}   rood = thuis, blauw = uit   [spatie] pauze  [1] 1x  [4] 4x  [D] alle debug-lagen",
            HorizontalAlignment.Left, -1, 16, Colors.White);
        DrawString(ThemeDB.FallbackFont, new Vector2(12, 755),
            $"[F] fases {On(_phases)}   [T] triggers {On(_triggers)}   [P] laatste pass {On(_passes)}   [O] buitenspel {On(_offside)}   [E] events {On(_events)}",
            HorizontalAlignment.Left, -1, 16, Colors.White);
        DrawPanel();
        if (_curr.Status.ToString() == "Finished") DrawEndScreen();
    }

    static string On(bool enabled) => enabled ? "aan" : "uit";

    void DrawLastPass()
    {
        if (_view.LastPass is not { ReceiverPosition: { } receiver } pass) return;
        var start = ToScreen(pass.Event.Location);
        var end = ToScreen(receiver);
        DrawLine(start, end, Colors.Yellow, 2);
        DrawArc(end, 10, 0, Mathf.Tau, 24, Colors.Yellow, 2);
        if (start.DistanceTo(end) > 1)
        {
            var direction = (end - start).Normalized();
            var normal = new Vector2(-direction.Y, direction.X);
            DrawLine(end, end - direction * 12 + normal * 5, Colors.Yellow, 2);
            DrawLine(end, end - direction * 12 - normal * 5, Colors.Yellow, 2);
        }
        DrawString(ThemeDB.FallbackFont, end + new Vector2(12, 17),
            $"{pass.Event.Team} {pass.Event.PlayerId} -> {pass.ReceiverId}",
            HorizontalAlignment.Left, -1, 12, Colors.Yellow);
    }

    void DrawOffside()
    {
        foreach (var team in new[] { Side.Home, Side.Away })
        {
            if (DebugViewState.OffsideLine(_curr, team) is not { } line) continue;
            var color = team == Side.Home ? Colors.Salmon : Colors.LightSkyBlue;
            DrawLine(ToScreen(new Vec2(line.X, -Pitch.Width / 2)),
                ToScreen(new Vec2(line.X, Pitch.Width / 2)), color, 2);
            DrawString(ThemeDB.FallbackFont, ToScreen(new Vec2(line.X, -Pitch.Width / 2)) + new Vector2(4, 15),
                team == Side.Home ? "lijn thuis" : "lijn uit", HorizontalAlignment.Left, -1, 12, color);
            foreach (var player in _curr.Players.Where(p => p.Team == team && line.Candidates.Contains(p.Id)))
                DrawArc(ToScreen(player.Pos), 16, 0, Mathf.Tau, 24, color, 3);
        }
    }

    static string Short(string text, int max = 39) => text.Length <= max ? text : text[..(max - 1)] + "…";

    void PanelLine(string text, float y, Color? color = null, int size = 15) =>
        DrawString(ThemeDB.FallbackFont, new Vector2(1162, y), Short(text, size <= 13 ? 48 : 39),
            HorizontalAlignment.Left, -1, size, color ?? Colors.White);

    void DrawPanel()
    {
        DrawRect(new Rect2(1148, 44, 360, 688), new Color(0.08f, 0.11f, 0.15f));
        PanelLine("ZEUS / WEDSTRIJD", 70, size: 18);
        PanelLine($"{_curr.HomeScore} - {_curr.AwayScore}   helft {_curr.Period}   {_curr.Status}", 99);
        PanelLine($"Passes: {_view.PassCount}", 122);
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
        if (_phases)
        {
            PanelLine("Thuis: " + DebugViewState.PhaseLabel(_view.PhaseOf(Side.Home)), 187, Colors.Salmon);
            PanelLine("Uit: " + DebugViewState.PhaseLabel(_view.PhaseOf(Side.Away)), 208, Colors.LightSkyBlue);
        }
        PanelLine(_events ? "LAATSTE EVENTS" : "[D] alle lagen  /  [E] events", 239, size: 17);
        if (_events)
        {
            int row = 0;
            foreach (var e in _view.RecentEvents)
            {
                float y = 263 + row++ * 51;
                PanelLine($"{e.Seconds:0.0}s {e.Type} {e.Team} {e.PlayerId}", y, size: 13);
                var outcome = Short(e.Outcome, 86);
                PanelLine(outcome.Length > 43 ? outcome[..43] : outcome, y + 15, Colors.LightGray, 12);
                if (outcome.Length > 43) PanelLine(outcome[43..], y + 30, Colors.LightGray, 12);
            }
        }
        if (_passes && _view.LastPass is { } pass)
        {
            PanelLine($"PASS {pass.Event.Seconds:0.0}s {pass.Event.Team} {pass.Event.PlayerId}", 581, Colors.Yellow, 13);
            PanelLine(pass.ReceiverId is null ? "Geen ontvanger: " + pass.Event.Outcome
                : "Naar " + pass.ReceiverId + " (positie bij event)", 599, Colors.Yellow, 13);
        }
        if (_triggers)
        {
            int row = 0;
            foreach (var e in _view.Triggers.TakeLast(2))
            {
                PanelLine($"TRIGGER {e.Team} {e.PlayerId}", 624 + row * 31, Colors.Orange, 12);
                PanelLine(e.Outcome, 639 + row++ * 31, Colors.Orange, 12);
            }
        }
        float cardY = 696;
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
