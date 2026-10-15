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
    int _frames, _backFrames;
    string _homeTactic = "", _awayTactic = "";
    readonly Heatmap _heatHome = new(), _heatAway = new();   // one sample per simulated second, for the end screen
    readonly int[] _live = new int[8];          // live counters (home, away): shots, passes, ball controls, fouls
    Control _ui = null!;
    PanelContainer? _endCard;
    PauseMenu? _pauseMenu;
    bool _pauseRequested;                       // B pressed: open the pause menu at the next rest moment
    Squad? _homeSquad;                          // names and roles for the pause menu (the match itself uses the same neutral squad)
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
        RenderingServer.SetDefaultClearColor(UiTheme.Bg);
        try { InitializeMatch(); BuildHud(); if (OS.GetEnvironment("ZEUS_PAUSEMENU").Length > 0) RequestPause(); }
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
        Squad? homeSquad = null, awaySquad = null;
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
            homeSquad = SquadFactory.ForLevel(Env("ZEUS_HOMELEVEL", GameSession.HomeLevel), home, GameSession.SquadsDir());
            awaySquad = SquadFactory.ForLevel(Env("ZEUS_AWAYLEVEL", GameSession.AwayLevel), away, GameSession.SquadsDir());
        }
        _stepSeconds = config.StepSeconds;
        _homeTactic = home.Name; _awayTactic = away.Name;
        _match = Match.Create(config, home, away, seed, homeSquad: homeSquad, awaySquad: awaySquad);
        _homeSquad = homeSquad ?? Squad.NeutralFor(home);
        _prev = _curr = _match.GetSnapshot();
        if (OS.GetEnvironment("ZEUS_FINISH").Length > 0)         // visual check of the end screen: play the whole match instantly
        {
            for (int n = 0; n < 150000 && _match.GetSnapshot().Status != MatchStatus.Finished; n++)
            {
                _match.Step();
                if (n % 10 == 0) SampleHeat(_match.GetSnapshot());
            }
            _prev = _curr = _match.GetSnapshot();
        }
        if (OS.GetEnvironment("ZEUS_SHOT").Length > 0) _speed = 4.0;
        _debug = OS.GetEnvironment("ZEUS_DEBUG").Length > 0;
        SetDebugLayers(_debug);
        ReadEvents();
        GD.Print($"Loaded '{home.Name}' (home) vs '{away.Name}' (away), step {_stepSeconds}s");
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (_pauseMenu is not null)                     // the panel owns the keyboard: Enter/Esc resume
        {
            if (k.Keycode is Key.Enter or Key.KpEnter or Key.Escape) ClosePauseMenu();
            return;
        }
        switch (k.Keycode)
        {
            case Key.B: RequestPause(); break;
            case Key.Enter:
            case Key.KpEnter:
                if (_curr.Status == MatchStatus.Finished)
                {
                    if (GameSession.InCompetition) GetTree().ChangeSceneToFile("res://Season.tscn");
                    else GetTree().ReloadCurrentScene();
                }
                break;
            case Key.Space: if (!_replayVerified) _paused = !_paused; break;
            case Key.Key1: _speed = 1.0; break;
            case Key.Key4: _speed = 4.0; break;
            case Key.Key8: _speed = 16.0; break;
            case Key.End: SkipToEnd(); break;
            case Key.D: _debug = !_debug; SetDebugLayers(_debug); break;
            case Key.F: _phases = !_phases; break;
            case Key.T: _triggers = !_triggers; break;
            case Key.P: _passes = !_passes; break;
            case Key.O: _offside = !_offside; break;
            case Key.E: _events = !_events; break;
            case Key.Escape: GetTree().ChangeSceneToFile(GameSession.InCompetition ? "res://Season.tscn" : "res://Menu.tscn"); break;
        }
    }

    // Counts per team for the end screen, straight from the event stream.
    int[]? _endStats;   // computed once when the match finishes: shots, passes, controls, fouls, offsides (home, away each)

    int Count(EventType type, Side team) => _match.Events.Events.Count(e => e.Type == type && e.Team == team);

    int Stat(int i, Side team) => _endStats![i * 2 + (team == Side.Home ? 0 : 1)];

    void BuildHud()
    {
        _ui = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }; UiTheme.Apply(_ui); AddChild(_ui);
        var keys = UiTheme.Label("SPATIE pauze   •   1 / 4 / 8 snelheid   •   B wissels en tactiek   •   END eindstand   •   D debug   •   ESC menu", UiTheme.Small, UiTheme.Muted);
        keys.Position = new Vector2(75, 748); _ui.AddChild(keys);
    }

    // Pause and steer (PRD): substitutions and tactic changes are only accepted at a rest moment, so B waits for the next one.
    void RequestPause()
    {
        if (_replay is not null || _homeSquad is null || _curr.Status == MatchStatus.Finished) return;
        _pauseRequested = true;
        if (_match.IsRestMoment) OpenPauseMenu();
    }

    void OpenPauseMenu()
    {
        if (_pauseMenu is not null) return;
        _pauseRequested = false;
        _paused = true;
        _pauseMenu = new PauseMenu(_match, _homeSquad!, ClosePauseMenu);
        _ui.AddChild(_pauseMenu);
    }

    void ClosePauseMenu()
    {
        _pauseMenu?.QueueFree();
        _pauseMenu = null;
        _paused = false;
        _acc = 0;
    }

    // Fast result with analysis (PRD): play the rest of the match at once, then the end screen shows the stats and heatmaps.
    void SkipToEnd()
    {
        if (_replay is not null || _curr.Status == MatchStatus.Finished) return;
        for (int n = 0; n < 150000 && _match.GetSnapshot().Status != MatchStatus.Finished; n++)
        {
            _match.Step();
            if (n % 10 == 0) SampleHeat(_match.GetSnapshot());
        }
        _prev = _curr = _match.GetSnapshot();
        _acc = 0;
        ReadEvents();
    }

    void SampleHeat(Snapshot snapshot)
    {
        _heatHome.AddSnapshot(snapshot, Side.Home);
        _heatAway.AddSnapshot(snapshot, Side.Away);
    }

    void ShowEndScreen()
    {
        if (_endCard is not null) return;
        var types = new[] { EventType.Shot, EventType.Pass, EventType.BallControl, EventType.Foul, EventType.Offside };
        _endStats = types.SelectMany(t => new[] { Count(t, Side.Home), Count(t, Side.Away) }).ToArray();
        _endCard = new PanelContainer { Position = new Vector2(150, 60), CustomMinimumSize = new Vector2(980, 0) }; _ui.AddChild(_endCard);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 36); _endCard.AddChild(row);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(480, 0) }; row.AddChild(box);
        var heats = new VBoxContainer(); row.AddChild(heats);
        heats.AddChild(UiTheme.Label("WAAR SPEELDEN ZE", 24, UiTheme.Accent, display: true));
        foreach (var (heat, color, title) in new[] { (_heatHome, UiTheme.Home, "Thuis"), (_heatAway, UiTheme.Away, "Uit") })
        {
            var view = new HeatmapView(); heats.AddChild(view); view.Show(heat, color, title);
        }
        box.AddChild(UiTheme.Label("Eindstand", UiTheme.Heading + 10, null, true));
        var scoreRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; box.AddChild(scoreRow);
        scoreRow.AddThemeConstantOverride("separation", 26);
        scoreRow.AddChild(UiTheme.Label("THUIS", 34, UiTheme.Home, display: true));
        scoreRow.AddChild(UiTheme.Label($"{_curr.HomeScore} - {_curr.AwayScore}", 96, UiTheme.Accent, display: true));
        scoreRow.AddChild(UiTheme.Label("UIT", 34, UiTheme.Away, display: true));
        int hc = Stat(2, Side.Home), ac = Stat(2, Side.Away);
        double homePoss = hc + ac == 0 ? 50 : 100.0 * hc / (hc + ac);
        var grid = new GridContainer { Columns = 3 }; grid.AddThemeConstantOverride("h_separation", 40); box.AddChild(grid);
        string[] labels = { "Schoten", "Passes", "Balbezit (%)", "Overtredingen", "Buitenspel" };
        grid.AddChild(UiTheme.Label("Statistieken", UiTheme.Small, UiTheme.Muted));
        grid.AddChild(UiTheme.Label("THUIS", UiTheme.Small, UiTheme.Home)); grid.AddChild(UiTheme.Label("UIT", UiTheme.Small, UiTheme.Away));
        for (int i = 0; i < labels.Length; i++)
        {
            grid.AddChild(UiTheme.Label(labels[i]));
            grid.AddChild(UiTheme.Label(i == 2 ? $"{homePoss:0}" : Stat(i, Side.Home).ToString(), UiTheme.Body, UiTheme.Home));
            grid.AddChild(UiTheme.Label(i == 2 ? $"{100 - homePoss:0}" : Stat(i, Side.Away).ToString(), UiTheme.Body, UiTheme.Away));
        }
        if (GameSession.InCompetition)          // competition match: the final score goes back to the season screen
        {
            GameSession.LastResult = (_curr.HomeScore, _curr.AwayScore);
            var back = UiTheme.Primary("[Enter] terug naar de competitie"); back.Pressed += () => GetTree().ChangeSceneToFile("res://Season.tscn"); box.AddChild(back);
            return;
        }
        var actions = new HBoxContainer(); box.AddChild(actions);
        var again = UiTheme.Primary("[Enter] opnieuw"); again.Pressed += () => GetTree().ReloadCurrentScene(); actions.AddChild(again);
        var menu = UiTheme.Secondary("[Esc] menu"); menu.Pressed += () => GetTree().ChangeSceneToFile("res://Menu.tscn"); actions.AddChild(menu);
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
                _replay?.ApplyDue(_match);
                _match.Step();
                _prev = _curr;
                _curr = _match.GetSnapshot();
                if (_curr.Tick % 10 == 0) SampleHeat(_curr);
                if (_pauseRequested && _match.IsRestMoment) { OpenPauseMenu(); break; }
                ReadEvents();
                CheckReplayEnd();
                if (_replayVerified) break;
            }
        }
        _view.Advance(_curr.TimeSeconds);
        QueueRedraw();

        if (_curr.Status == MatchStatus.Finished) ShowEndScreen();
        // Visual/functional check of the competition loop: with ZEUS_AUTOBACK the finished match returns to Season by itself.
        if (_curr.Status == MatchStatus.Finished && GameSession.InCompetition && OS.GetEnvironment("ZEUS_AUTOBACK").Length > 0 && ++_backFrames == 6)
        {
            if (OS.GetEnvironment("ZEUS_SHOT") is { Length: > 0 } prefix) GetViewport().GetTexture().GetImage().SavePng(prefix + "_matchend.png");
            GetTree().ChangeSceneToFile("res://Season.tscn");
            return;
        }
        var shot = OS.GetEnvironment("ZEUS_SHOT");
        if (shot.Length == 0 || ++_frames < 10 || (_curr.TimeSeconds < 6.2 && _pauseMenu is null)) return;
        GetViewport().GetTexture().GetImage().SavePng(shot + "_match.png");
        GetTree().Quit();
    }

    static Vector2 ToScreen(Vec2 p) => Origin + new Vector2((float)p.X, (float)p.Y) * Px;

    void SetDebugLayers(bool enabled) => _phases = _triggers = _passes = _offside = _events = enabled;

    void ReadEvents()
    {
        var events = _match.Events.Events;
        while (_countedEvents < events.Count)
        {
            var e = events[_countedEvents++];
            _view.Read(e, _curr);
            Tally(e);
        }
        _view.Advance(_curr.TimeSeconds);
    }

    void Tally(Zeus.Sim.Event e)
    {
        int slot = e.Type switch { EventType.Shot => 0, EventType.Pass => 2, EventType.BallControl => 4, EventType.Foul => 6, _ => -1 };
        if (slot >= 0) _live[slot + (e.Team == Side.Home ? 0 : 1)]++;
    }

    void CheckReplayEnd()
    {
        if (_replay is null || _replayVerified || _curr.Tick < _replay.Steps) return;
        _paused = true;
        _acc = 0;
        try { _replay.ApplyDue(_match); _replay.Verify(_match); }
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
        DrawRect(new Rect2(0, 0, 1520, 780), UiTheme.Bg, true);
        const float L = (float)Pitch.Length, W = (float)Pitch.Width;
        var field = new Rect2(Origin - new Vector2(L * Px / 2, W * Px / 2), new Vector2(L * Px, W * Px));
        GameArt.DrawPitch(this, field, 24);
        if (_offside) DrawOffside();
        if (_passes) DrawLastPass();
        float alpha = _paused ? 1f : (float)Math.Clamp(_acc / _stepSeconds, 0, 1);
        for (int i = 0; i < _curr.Players.Count; i++)
        {
            var c = _curr.Players[i];
            if (c.SentOff) continue;
            var p0 = i < _prev.Players.Count ? _prev.Players[i].Pos : c.Pos;
            var pos = ToScreen(new Vec2(p0.X + (c.Pos.X - p0.X) * alpha, p0.Y + (c.Pos.Y - p0.Y) * alpha));
            var col = c.Team == Side.Home ? UiTheme.Home : UiTheme.Away;
            GameArt.DrawPlayer(this, pos, col, c.Id, c.Heading, 11);
            if (c.YellowCards > 0)
                DrawRect(new Rect2(pos + new Vector2(-4, -26), new Vector2(8, 11)), new Color("FFD400"));
            if (_phases)
                DrawString(GameFonts.Semi, pos + new Vector2(14, -12),
                    DebugViewState.PhaseLabel(_view.PhaseOf(c.Team)), HorizontalAlignment.Left, -1, 13, Colors.White);
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
            GameArt.DrawBall(this, pos);
        }
        GameArt.DrawVignette(this, field.Grow(24), 0.5f);
        if (_debug)
            DrawString(GameFonts.Body, new Vector2(75, 772),
                $"F fases {On(_phases)}   T triggers {On(_triggers)}   P pass {On(_passes)}   O buitenspel {On(_offside)}   E events {On(_events)}",
                HorizontalAlignment.Left, -1, UiTheme.Small, UiTheme.Muted);
        int minutes = (int)(_curr.TimeSeconds / 60), seconds = (int)(_curr.TimeSeconds % 60);
        GameArt.DrawScoreBug(this, new Vector2(75, 1), "THUIS", "UIT", _curr.HomeScore, _curr.AwayScore, $"{minutes:D2}:{seconds:D2}",
            $"HELFT {_curr.Period}   •   {_speed:0}x{(_paused ? "   •   PAUZE" : "")}{(_pauseRequested ? "   •   WISSELMENU AANGEVRAAGD: WACHT OP EEN RUSTMOMENT" : "")}");
        DrawPanel();
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
            var color = team == Side.Home ? UiTheme.Home : UiTheme.Away;
            DrawLine(ToScreen(new Vec2(line.X, -Pitch.Width / 2)),
                ToScreen(new Vec2(line.X, Pitch.Width / 2)), color, 2);
            DrawString(ThemeDB.FallbackFont, ToScreen(new Vec2(line.X, -Pitch.Width / 2)) + new Vector2(4, 15),
                team == Side.Home ? "lijn thuis" : "lijn uit", HorizontalAlignment.Left, -1, 12, color);
            foreach (var player in _curr.Players.Where(p => p.Team == team && line.Candidates.Contains(p.Id)))
                DrawArc(ToScreen(player.Pos), 16, 0, Mathf.Tau, 24, color, 3);
        }
    }

    static string Short(string text, int max = 39) => text.Length <= max ? text : text[..(max - 1)] + "…";

    void PanelLine(string text, float y, Color? color = null, int size = 15, Font? font = null) =>
        DrawString(font ?? GameFonts.Body, new Vector2(1170, y), Short(text, size <= 13 ? 48 : 39),
            HorizontalAlignment.Left, -1, size, color ?? UiTheme.Text);

    // One mirrored stat row: home value, label, away value, and a split bar (home share in red, away share in blue).
    void StatRow(string label, float y, double home, double away, bool percent = false)
    {
        const float x = 1170, w = 316;
        double total = home + away;
        double share = total <= 0 ? 0.5 : home / total;
        string h = percent ? $"{share * 100:0}%" : $"{home:0}", a = percent ? $"{(1 - share) * 100:0}%" : $"{away:0}";
        DrawString(GameFonts.Display, new Vector2(x, y + 24), h, HorizontalAlignment.Left, -1, 30, UiTheme.Home);
        DrawString(GameFonts.Display, new Vector2(x + w - 90, y + 24), a, HorizontalAlignment.Right, 90, 30, UiTheme.Away);
        DrawString(GameFonts.Semi, new Vector2(x, y + 22), label, HorizontalAlignment.Center, w, 16, UiTheme.Muted);
        DrawRect(new Rect2(x, y + 32, w, 7), new Color(1, 1, 1, 0.08f), true);
        DrawRect(new Rect2(x, y + 32, (float)(w * share), 7), UiTheme.Home, true);
        DrawRect(new Rect2(x + (float)(w * share), y + 32, (float)(w * (1 - share)), 7), UiTheme.Away, true);
    }

    void DrawPanel()
    {
        DrawStyleBox(new StyleBoxFlat { BgColor = new Color(UiTheme.Panel, 0.92f), BorderColor = new Color(UiTheme.Accent, 0.6f), BorderWidthTop = 3 },
            new Rect2(1148, 50, 360, 680));
        PanelLine("LIVE", 100, UiTheme.Accent, 38, GameFonts.Display);
        PanelLine(Short(_homeTactic, 40), 128, UiTheme.Home, 15, GameFonts.Semi);
        PanelLine(Short(_awayTactic, 40), 148, UiTheme.Away, 15, GameFonts.Semi);

        StatRow("SCHOTEN", 175, _live[0], _live[1]);
        StatRow("PASSES", 230, _live[2], _live[3]);
        StatRow("BALBEZIT", 285, _live[4], _live[5], percent: true);
        StatRow("OVERTREDINGEN", 340, _live[6], _live[7]);

        int yellowH = 0, yellowA = 0, redH = 0, redA = 0;
        foreach (var player in _curr.Players)
        {
            bool home = player.Team == Side.Home;
            if (home) yellowH += player.YellowCards; else yellowA += player.YellowCards;
            if (player.SentOff) { if (home) redH++; else redA++; }
        }
        PanelLine("KAARTEN", 412, UiTheme.Muted, 16, GameFonts.Semi);
        PanelLine($"geel {yellowH}   rood {redH}", 434, UiTheme.Home, 18, GameFonts.Semi);
        PanelLine($"geel {yellowA}   rood {redA}", 458, UiTheme.Away, 18, GameFonts.Semi);
        if (_replay is not null)
            PanelLine(_replayVerified ? "Replay klaar / hash gecontroleerd" : $"Replay: {_curr.Tick}/{_replay.Steps}", 490, UiTheme.Good, 14);
        if (_phases)
        {
            PanelLine("Thuis: " + DebugViewState.PhaseLabel(_view.PhaseOf(Side.Home)), 518, UiTheme.Home);
            PanelLine("Uit: " + DebugViewState.PhaseLabel(_view.PhaseOf(Side.Away)), 540, UiTheme.Away);
        }
        if (!_debug)
        {
            PanelLine("D = debuglagen voor de ontwikkelaar", 700, UiTheme.Muted, 14);
            return;
        }
        PanelLine(_events ? "LAATSTE EVENTS" : "E = events tonen", 566, UiTheme.Text, 16, GameFonts.Semi);
        if (_events)
        {
            int row = 0;
            foreach (var e in _view.RecentEvents.TakeLast(3))
            {
                float y = 590 + row++ * 36;
                PanelLine($"{e.Seconds:0}s {e.Type} {e.Team} {e.PlayerId}", y, UiTheme.Text, 13);
                PanelLine(Short(e.Outcome, 44), y + 15, UiTheme.Muted, 12);
            }
        }
        if (_passes && _view.LastPass is { } pass)
            PanelLine($"PASS {pass.Event.Team} {pass.Event.PlayerId} -> {pass.ReceiverId ?? "?"}", 700, UiTheme.Accent, 13);
        if (_triggers && _view.Triggers.Count > 0)
            PanelLine("TRIGGER " + _view.Triggers.Last().PlayerId + ": " + _view.Triggers.Last().Outcome, 718, Colors.Orange, 12);
    }
}
