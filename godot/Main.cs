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
    double _speed = 20;
    bool _paused;
    bool _debug;
    bool _phases, _triggers, _passes, _offside, _events;
    readonly DebugViewState _view = new();
    int _frames, _backFrames;
    string _homeTactic = "", _awayTactic = "";
    readonly Heatmap _heatHome = new(), _heatAway = new();   // one sample per simulated second, for the end screen
    readonly int[] _live = new int[8];          // live counters (home, away): shots, passes, ball controls, fouls
    Control _ui = null!;
    MatchPopups? _popups;                       // goal/card/... popups above the picture (see MatchPopups.cs)
    PanelContainer? _endCard;
    PauseMenu? _pauseMenu;
    bool _pauseRequested;                       // B pressed: open the pause menu at the next rest moment
    Squad? _homeSquad, _awaySquad;              // names and roles for the pause menu (the match itself uses the same neutral squad)
    Side _playerSide = Side.Home;               // the side the player controls: Home, except when he plays away in the competition
    bool _skipping;                             // END pressed: the rest of the match is played in chunks per frame
    const int SkipStepsPerFrame = 3000, SkipStepLimit = 150000;
    int _skipSteps;
    double _totalSeconds = 5400;
    int _countedEvents;
    ReplayFile? _replay;
    OpponentSubstitutionPolicy? _opponentSubstitutions;
    bool _replayVerified;
    ulong _logSeed;
    string _logMode = "menu";

    static string Env(string name, string fallback)
    {
        var v = GameSession.DebugEnvironment(name);
        return v.Length > 0 ? v : fallback;
    }

    public override void _Ready()
    {
        RenderingServer.SetDefaultClearColor(UiTheme.Bg);
        try { InitializeMatch(); BuildHud(); if (GameSession.DebugEnvironment("ZEUS_PAUSEMENU").Length > 0) RequestPause(); }
        catch (Exception error)
        {
            GD.PushError($"Zeus kon niet starten: {error.Message}");
            SetProcess(false);
            if (GameSession.HomePath is not null)
            {
                GameSession.LastError = Loc.T("match.start_failed") + Loc.Error(error);
                GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://Menu.tscn");
            }
            else GetTree().Quit(1);
        }
    }

    void InitializeMatch()
    {
        var replayPath = GameSession.DebugEnvironment("ZEUS_REPLAY");
        Tactic home, away;
        Squad? homeSquad = null, awaySquad = null;
        MatchConfig config;
        ulong seed = 1;
        FamiliarityState? homeFamiliarity = null, awayFamiliarity = null;
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
            if (GameSession.InCompetition && GameSession.CompetitionFamiliarity is { } careerFamiliarity)
            {
                config = config with { EnableFamiliarity = true };
                if (GameSession.PlayerSide == Side.Home) homeFamiliarity = careerFamiliarity;
                else awayFamiliarity = careerFamiliarity;
            }
            if (GameSession.UseFamiliarity && !GameSession.InCompetition && GameSession.Challenge is null && homeSquad is not null)     // selection screen checkbox (v0, voorlopig)
            {
                var key = FamiliarityStore.Key(Env("ZEUS_HOMELEVEL", GameSession.HomeLevel), homeSquad.Name);
                homeFamiliarity = FamiliarityStore.Load().Get(key, key, homeSquad.Starters.Concat(homeSquad.Bench).Select(p => p.Id));
                config = config with { EnableFamiliarity = true };
            }
        }
        _stepSeconds = config.StepSeconds;
        _homeTactic = Loc.Name(home.Name); _awayTactic = Loc.Name(away.Name);
        _match = Match.Create(config, home, away, seed, homeSquad: homeSquad, awaySquad: awaySquad, homeFamiliarity: homeFamiliarity, awayFamiliarity: awayFamiliarity);
        _logSeed = seed;
        _logMode = _replay is not null ? "replay" : GameSession.Challenge is not null ? "challenge" : GameSession.InCompetition ? "competition" : "menu";
        FeedbackLog.MatchStarted(seed, home.Name, away.Name, Env("ZEUS_HOMELEVEL", GameSession.HomeLevel), Env("ZEUS_AWAYLEVEL", GameSession.AwayLevel), _logMode);
        _homeSquad = homeSquad ?? Squad.NeutralFor(home);
        _awaySquad = awaySquad ?? Squad.NeutralFor(away);
        _playerSide = GameSession.InCompetition ? GameSession.PlayerSide : Side.Home;
        if (GameSession.DebugEnvironment("ZEUS_PLAYERSIDE") == "away") _playerSide = Side.Away;       // visual check of the pause menu for the away side
        _opponentSubstitutions = _replay is null && GameSession.OpponentSubstitutions ? new OpponentSubstitutionPolicy() : null;
        _totalSeconds = config.HalfSeconds * 2;
        _prev = _curr = _match.GetSnapshot();
        if (GameSession.DebugEnvironment("ZEUS_FINISH").Length > 0)         // visual check of the end screen: play the whole match instantly
        {
            for (int n = 0; n < 150000 && _match.GetSnapshot().Status != MatchStatus.Finished; n++)
            {
                StepMatch();
                if (n % 10 == 0) SampleHeat(_match.GetSnapshot());
            }
            _prev = _curr = _match.GetSnapshot();
        }
        if (GameSession.DebugEnvironment("ZEUS_SHOT").Length > 0) _speed = 4.0;
        _debug = GameSession.DebugEnvironment("ZEUS_DEBUG").Length > 0;
        SetDebugLayers(_debug);
        ReadEvents();
        GD.Print($"Loaded '{home.Name}' (home) vs '{away.Name}' (away), step {_stepSeconds}s");
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (_pauseMenu is not null)                     // the panel owns the keyboard: Enter resumes (Esc is handled in Hotkeys)
        {
            if (k.Keycode is Key.Enter or Key.KpEnter) ClosePauseMenu();
            return;
        }
        switch (k.Keycode)
        {
            case Key.B: RequestPause(); break;
            case Key.Enter:
            case Key.KpEnter:
                if (MatchOver)
                {
                    if (GameSession.Challenge is not null) GetTree().ChangeSceneToFile("res://Challenges.tscn");
                    else if (GameSession.InCompetition) GetTree().ChangeSceneToFile("res://Season.tscn");
                    else PlayAgain();
                }
                break;
            case Key.Space: if (!_replayVerified) _paused = !_paused; break;
            case Key.End: SkipToEnd(); break;
            case Key.D when GameSession.DebugEnabled: _debug = !_debug; SetDebugLayers(_debug); break;
            case Key.F when GameSession.DebugEnabled: _phases = !_phases; break;
            case Key.T when GameSession.DebugEnabled: _triggers = !_triggers; break;
            case Key.P when GameSession.DebugEnabled: _passes = !_passes; break;
            case Key.O when GameSession.DebugEnabled: _offside = !_offside; break;
            case Key.E when GameSession.DebugEnabled: _events = !_events; break;
        }
    }

    // Counts per team for the end screen, straight from the event stream.
    int[]? _endStats;   // computed once when the match finishes: shots, passes, controls, fouls, offsides (home, away each)

    // A live challenge match also ends when the referee abandons it (the result then counts as awarded, like in the simulation).
    // Menu and league matches need the same terminal screen so a referee's stoppage cannot strand the player.
    bool MatchOver => _curr.Status is MatchStatus.Finished or MatchStatus.Abandoned;

    int Count(EventType type, Side team) => _match.Events.Events.Count(e => e.Type == type && e.Team == team);

    int Stat(int i, Side team) => _endStats![i * 2 + (team == Side.Home ? 0 : 1)];

    void BuildHud()
    {
        Welcome.Install(this, "match");
        _ui = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }; UiTheme.Apply(_ui); AddChild(_ui);
        var keys = UiTheme.Label(Loc.T(GameSession.DebugEnabled ? "match.keys" : "match.keys_release"), UiTheme.Small, UiTheme.Muted);
        keys.AddThemeColorOverride("font_color", UiTheme.Muted);
        keys.Position = new Vector2(75, 748); _ui.AddChild(keys);
        _popups = new MatchPopups(Loc.T("popup.home"), Loc.T("popup.away"), PopupNames.From(_match, _homeSquad!, _awaySquad!));
        AddChild(_popups);
        _popups.Feed(_match.Events.Events, _speed, suppress: true);          // events from before the HUD existed (ZEUS_FINISH): only the final result
        if (GameSession.DebugEnvironment("ZEUS_POPUPDEMO").Length > 0) _popups.StartDemo();
    }

    public void RequestCoaching() => RequestPause();

    /// <summary>False at the end screen, while skipping, in a replay and while the pause menu is already open: Settings then hides its coaching button.</summary>
    public bool CanCoach => _replay is null && PlayerSquad is not null && !_skipping && !MatchOver && _pauseMenu is null;

    /// <summary>Esc closes an open pause menu first (Hotkeys calls this before it opens Settings).</summary>
    public bool TryClosePauseMenu()
    {
        if (_pauseMenu is null) return false;
        ClosePauseMenu();
        return true;
    }

    // Free menu match only (challenges and replays keep their fixed seeds): a new seed, otherwise "play again" is the same match.
    void PlayAgain()
    {
        if (_replay is null) GameSession.Seed = GameSession.Seed % 999999 + 1;
        GetTree().ReloadCurrentScene();
    }

    Squad? PlayerSquad => _playerSide == Side.Home ? _homeSquad : _awaySquad;

    // Pause and steer (PRD): substitutions and tactic changes are only accepted at a rest moment, so B waits for the next one.
    void RequestPause()
    {
        if (_replay is not null || PlayerSquad is null || _skipping || MatchOver || _pauseMenu is not null) return;
        _pauseRequested = true;
        if (_match.IsRestMoment) OpenPauseMenu();
    }

    void OpenPauseMenu()
    {
        if (_pauseMenu is not null) return;
        _pauseRequested = false;
        _paused = true;
        _pauseMenu = new PauseMenu(_match, PlayerSquad!, _playerSide, ClosePauseMenu);
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
    // Played in chunks of SkipStepsPerFrame per frame (see SkipChunk) so the window keeps responding; the heat samples are kept.
    void SkipToEnd()
    {
        if (_replay is not null || MatchOver || _skipping) return;
        _skipping = true;
        _skipSteps = 0;
        _pauseRequested = false;
        _acc = 0;
    }

    void StepMatch()
    {
        _replay?.ApplyDue(_match);
        if (_match.IsRestMoment)
            _opponentSubstitutions?.TrySubstitute(_match, _playerSide == Side.Home ? Side.Away : Side.Home);
        _match.Step();
    }

    void SkipChunk()
    {
        for (int n = 0; n < SkipStepsPerFrame && _skipSteps < SkipStepLimit && _match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned); n++, _skipSteps++)
        {
            StepMatch();
            if (_skipSteps % 10 == 0) SampleHeat(_match.GetSnapshot());
        }
        _prev = _curr = _match.GetSnapshot();
        _popups?.Feed(_match.Events.Events, _speed, suppress: true);     // skipping suppresses all popups except the final result
        ReadEvents();
        if (_match.Status is MatchStatus.Finished or MatchStatus.Abandoned || _skipSteps >= SkipStepLimit)
        {
            _skipping = false;
        }
    }

    void SampleHeat(Snapshot snapshot)
    {
        _heatHome.AddSnapshot(snapshot, Side.Home);
        _heatAway.AddSnapshot(snapshot, Side.Away);
    }

    // Marks the player's own side in the competition, so it is clear who is who when he plays away.
    string You(Side side, bool lower = false) => GameSession.InCompetition && side == _playerSide ? (lower ? Loc.T("match.you_lower") : Loc.T("match.you")) : "";

    void ShowEndScreen()
    {
        if (_endCard is not null) return;
        var result = MatchStatistics.FromMatch(_match).AwardedGoals();
        FeedbackLog.MatchFinished(_logSeed, _homeTactic, _awayTactic, Env("ZEUS_HOMELEVEL", GameSession.HomeLevel), Env("ZEUS_AWAYLEVEL", GameSession.AwayLevel),
            _logMode, result.Home, result.Away, _curr.Tick, _curr.Status.ToString(), _match.EventHash());
        var types = new[] { EventType.Shot, EventType.Pass, EventType.BallControl, EventType.Foul, EventType.Offside };
        _endStats = types.SelectMany(t => new[] { Count(t, Side.Home), Count(t, Side.Away) }).ToArray();
        _endCard = new PanelContainer { Position = new Vector2(150, 60), CustomMinimumSize = new Vector2(980, 0) }; _ui.AddChild(_endCard); UiTheme.ApplyPaper(_endCard);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 36); _endCard.AddChild(row);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(480, 0) }; row.AddChild(box);
        var heats = new VBoxContainer(); row.AddChild(heats);
        heats.AddChild(UiTheme.Label(Loc.T("match.heatmaps"), 24, UiTheme.Accent, display: true));
        foreach (var (heat, color, title) in new[] { (_heatHome, UiTheme.Home, Loc.T("match.home") + You(Side.Home, true)), (_heatAway, UiTheme.Away, Loc.T("match.away") + You(Side.Away, true)) })
        {
            var view = new HeatmapView(); heats.AddChild(view); view.Show(heat, color, title);
        }
        box.AddChild(UiTheme.Label(Loc.T(_curr.Status == MatchStatus.Abandoned ? "popup.abandoned" : "match.result"), UiTheme.Heading + 10, null, true));
        var scoreRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; box.AddChild(scoreRow);
        scoreRow.AddThemeConstantOverride("separation", 26);
        scoreRow.AddChild(UiTheme.Label(Loc.T("match.home_caps"), 34, UiTheme.Home, display: true));
        scoreRow.AddChild(UiTheme.Label($"{result.Home} - {result.Away}", 96, UiTheme.Accent, display: true));
        scoreRow.AddChild(UiTheme.Label(Loc.T("match.away_caps"), 34, UiTheme.Away, display: true));
        if (GameSession.InCompetition)
            box.AddChild(UiTheme.Label(Loc.T("match.your_side", _playerSide == Side.Home ? Loc.T("common.home_red") : Loc.T("common.away_blue")), UiTheme.Small, UiTheme.Muted));
        int hc = Stat(2, Side.Home), ac = Stat(2, Side.Away);
        double homePoss = hc + ac == 0 ? 50 : 100.0 * hc / (hc + ac);
        var grid = new GridContainer { Columns = 3 }; grid.AddThemeConstantOverride("h_separation", 40); box.AddChild(grid);
        string[] labels = { Loc.T("match.shots"), Loc.T("match.passes"), Loc.T("match.possession_pct"), Loc.T("match.fouls"), Loc.T("match.offside") };
        grid.AddChild(UiTheme.Label(Loc.T("match.statistics"), UiTheme.Small, UiTheme.Muted));
        grid.AddChild(UiTheme.Label(Loc.T("match.home_caps"), UiTheme.Small, UiTheme.Home)); grid.AddChild(UiTheme.Label(Loc.T("match.away_caps"), UiTheme.Small, UiTheme.Away));
        for (int i = 0; i < labels.Length; i++)
        {
            grid.AddChild(UiTheme.Label(labels[i]));
            grid.AddChild(UiTheme.Label(i == 2 ? $"{homePoss:0}" : Stat(i, Side.Home).ToString(), UiTheme.Body, UiTheme.Home));
            grid.AddChild(UiTheme.Label(i == 2 ? $"{100 - homePoss:0}" : Stat(i, Side.Away).ToString(), UiTheme.Body, UiTheme.Away));
        }
        if (_match.Status == MatchStatus.Abandoned)
        {
            var notice = UiTheme.Label(Loc.T("challenge.live.abandoned"), UiTheme.Small, UiTheme.Bad);
            notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            box.AddChild(notice);
        }
        if (GameSession.Challenge is { } run)           // live challenge: remember this match, Challenges shows how it stands
        {
            run.Record(ChallengeEvaluator.ToResult(MatchStatistics.FromMatch(_match)));
            var next = UiTheme.Primary(Loc.T(run.Done ? "challenge.live.show_result" : "challenge.live.next_match", run.Results.Count + 1, run.Spec.Matches));
            next.Pressed += () => GetTree().ChangeSceneToFile("res://Challenges.tscn"); box.AddChild(next);
            return;
        }
        if (GameSession.InCompetition)          // competition match: the final score goes back to the season screen
        {
            GameSession.CompetitionDemand = _match.FamiliarityDemand(_playerSide);
            GameSession.LastResult = result;
            var back = UiTheme.Primary(Loc.T("match.back_season")); back.Pressed += () => GetTree().ChangeSceneToFile("res://Season.tscn"); box.AddChild(back);
            return;
        }
        var actions = new HBoxContainer(); box.AddChild(actions);
        var again = UiTheme.Primary(Loc.T("match.again")); again.Pressed += PlayAgain; actions.AddChild(again);
        var menu = UiTheme.Secondary(Loc.T("match.menu")); menu.Pressed += () => GetTree().ChangeSceneToFile("res://Menu.tscn"); actions.AddChild(menu);
    }

    public override void _Process(double delta)
    {
        _speed = SettingsMenu.MatchSpeed;
        CheckReplayEnd();
        if (_skipping) SkipChunk();
        else if (!_paused)
        {
            _acc += Math.Min(delta, 0.25) * _speed;
            while (_acc >= _stepSeconds)
            {
                _acc -= _stepSeconds;
                StepMatch();
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
        if (_popups is not null) { _popups.Visible = _pauseMenu is null; _popups.Advance(delta, _paused || _skipping); }
        QueueRedraw();

        if (MatchOver) ShowEndScreen();
        // Visual/functional check of the competition loop: with ZEUS_AUTOBACK the finished match returns to Season by itself.
        if (MatchOver && (GameSession.InCompetition || GameSession.Challenge is not null) && GameSession.DebugEnvironment("ZEUS_AUTOBACK").Length > 0 && ++_backFrames == 6)
        {
            if (GameSession.DebugEnvironment("ZEUS_SHOT") is { Length: > 0 } prefix) GetViewport().GetTexture().GetImage().SavePng(prefix + "_matchend.png");
            GetTree().ChangeSceneToFile(GameSession.Challenge is not null ? "res://Challenges.tscn" : "res://Season.tscn");
            return;
        }
        var shot = GameSession.DebugEnvironment("ZEUS_SHOT");
        if (shot.Length == 0 || ++_frames < 10 || (_curr.TimeSeconds < 6.2 && _pauseMenu is null) || _popups?.DemoRunning == true) return;
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
        _popups?.Feed(events, _speed);
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
                    Loc.Name(DebugViewState.PhaseLabel(_view.PhaseOf(c.Team))), HorizontalAlignment.Left, -1, 13, Colors.White);
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
                Loc.T("match.debug_layers", On(_phases), On(_triggers), On(_passes), On(_offside), On(_events)),
                HorizontalAlignment.Left, -1, UiTheme.Small, UiTheme.Muted);
        int minutes = (int)(_curr.TimeSeconds / 60), seconds = (int)(_curr.TimeSeconds % 60);
        GameArt.DrawScoreBug(this, new Vector2(75, 1), Loc.T("match.home_caps"), Loc.T("match.away_caps"), _curr.HomeScore, _curr.AwayScore, $"{minutes:D2}:{seconds:D2}",
            Loc.T("match.period", _curr.Period, _speed, _paused ? Loc.T("match.paused") : "", _pauseRequested ? Loc.T("match.pause_requested") : ""));
        DrawPanel();
        if (_skipping)
        {
            var banner = new Rect2(Origin.X - 260, Origin.Y - 40, 520, 80);
            DrawStyleBox(new StyleBoxFlat { BgColor = new Color(UiTheme.Panel, 0.92f), BorderColor = new Color(UiTheme.Accent, 0.7f), BorderWidthTop = 3 }, banner);
            DrawString(GameFonts.Display, banner.Position + new Vector2(0, 38), Loc.T("match.skipping", Math.Clamp(_curr.TimeSeconds / _totalSeconds * 100, 0, 100)),
                HorizontalAlignment.Center, banner.Size.X, 34, UiTheme.Accent);
            DrawString(GameFonts.Body, banner.Position + new Vector2(0, 66), Loc.T("match.skip_help"), HorizontalAlignment.Center, banner.Size.X, UiTheme.Small, UiTheme.Muted);
        }
    }

    static string On(bool enabled) => enabled ? Loc.T("match.on") : Loc.T("match.off");

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
                team == Side.Home ? Loc.T("match.home_line") : Loc.T("match.away_line"), HorizontalAlignment.Left, -1, 12, color);
            foreach (var player in _curr.Players.Where(p => p.Team == team && line.Candidates.Contains(p.Id)))
                DrawArc(ToScreen(player.Pos), 16, 0, Mathf.Tau, 24, color, 3);
        }
    }

    static string Short(string text, int max = 39) => text.Length <= max ? text : text[..(max - 1)] + "…";

    void PanelLine(string text, float y, Color? color = null, int size = 15, Font? font = null) =>
        DrawString(font ?? GameFonts.Body, new Vector2(1170, y), Short(text, size <= 13 ? 48 : 39),
            HorizontalAlignment.Left, -1, size, UiTheme.PaperColor(color ?? UiTheme.Text));

    // One mirrored stat row: home value, label, away value, and a split bar (home share in red, away share in blue).
    void StatRow(string label, float y, double home, double away, bool percent = false)
    {
        const float x = 1170, w = 316;
        double total = home + away;
        double share = total <= 0 ? 0.5 : home / total;
        string h = percent ? $"{share * 100:0}%" : $"{home:0}", a = percent ? $"{(1 - share) * 100:0}%" : $"{away:0}";
        DrawString(GameFonts.Display, new Vector2(x, y + 24), h, HorizontalAlignment.Left, -1, 30, UiTheme.HomeInk);
        DrawString(GameFonts.Display, new Vector2(x + w - 90, y + 24), a, HorizontalAlignment.Right, 90, 30, UiTheme.AwayInk);
        DrawString(GameFonts.Semi, new Vector2(x, y + 22), label, HorizontalAlignment.Center, w, 16, UiTheme.Graphite);
        DrawRect(new Rect2(x, y + 32, w, 7), new Color(1, 1, 1, 0.08f), true);
        DrawRect(new Rect2(x, y + 32, (float)(w * share), 7), UiTheme.Home, true);
        DrawRect(new Rect2(x + (float)(w * share), y + 32, (float)(w * (1 - share)), 7), UiTheme.Away, true);
    }

    void DrawPanel()
    {
        DrawStyleBox(new StyleBoxFlat { BgColor = UiTheme.Paper, BorderColor = UiTheme.PaperShade, BorderWidthTop = 3 },
            new Rect2(1148, 50, 360, 680));
        UiPaper.DrawPage(this, new Rect2(1148, 50, 360, 680));
        PanelLine(Loc.T("match.live"), 100, UiTheme.Accent, 38, GameFonts.Display);
        PanelLine(Short(_homeTactic + You(Side.Home), 40), 128, UiTheme.Home, 15, GameFonts.Semi);
        PanelLine(Short(_awayTactic + You(Side.Away), 40), 148, UiTheme.Away, 15, GameFonts.Semi);

        StatRow(Loc.T("match.shots_caps"), 175, _live[0], _live[1]);
        StatRow(Loc.T("match.passes_caps"), 230, _live[2], _live[3]);
        StatRow(Loc.T("match.possession"), 285, _live[4], _live[5], percent: true);
        StatRow(Loc.T("match.fouls_caps"), 340, _live[6], _live[7]);

        int yellowH = 0, yellowA = 0, redH = 0, redA = 0;
        foreach (var player in _curr.Players)
        {
            bool home = player.Team == Side.Home;
            if (home) yellowH += player.YellowCards; else yellowA += player.YellowCards;
            if (player.SentOff) { if (home) redH++; else redA++; }
        }
        PanelLine(Loc.T("match.cards"), 412, UiTheme.Muted, 16, GameFonts.Semi);
        PanelLine(Loc.T("match.card_counts", yellowH, redH), 434, UiTheme.Home, 18, GameFonts.Semi);
        PanelLine(Loc.T("match.card_counts", yellowA, redA), 458, UiTheme.Away, 18, GameFonts.Semi);
        if (_replay is not null)
            PanelLine(_replayVerified ? Loc.T("match.replay_checked") : Loc.T("match.replay", _curr.Tick, _replay.Steps), 490, UiTheme.Good, 14);
        if (_phases)
        {
            PanelLine(Loc.T("match.home_prefix") + Loc.Name(DebugViewState.PhaseLabel(_view.PhaseOf(Side.Home))), 518, UiTheme.Home);
            PanelLine(Loc.T("match.away_prefix") + Loc.Name(DebugViewState.PhaseLabel(_view.PhaseOf(Side.Away))), 540, UiTheme.Away);
        }
        if (!_debug)
        {
            if (GameSession.DebugEnabled) PanelLine(Loc.T("match.debug_help"), 700, UiTheme.Muted, 14);
            return;
        }
        PanelLine(_events ? Loc.T("match.recent_events") : Loc.T("match.show_events"), 566, UiTheme.Text, 16, GameFonts.Semi);
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
