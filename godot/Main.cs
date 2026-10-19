// Display only: reads Zeus.Sim snapshots, no game logic. The match itself runs in MatchDriver (sim, fixed step via an accumulator,
// skip, replay check, highlight re-simulation, event reading); Main draws the picture and the HUD and passes the keys on.
using System;
using System.Linq;
using Godot;
using Zeus.Sim;
using Side = Zeus.Sim.Side;

public partial class Main : Node2D
{
    const float Px = 11f;                       // pixels per metre
    static readonly Vector2 Origin = new(630, 446);     // pitch 52..1208 x 72..820 in the 1600x900 viewport
    public const float PanelX = 1230;           // right-hand panel x 1230..1590

    MatchDriver _driver = null!;
    CrowdModel _crowd = new();                  // home crowd: intensity for the AudioManager beds plus one-shot reactions
    int _crowdSeen;                             // events already handed to the crowd
    bool _debug;
    bool _phases, _triggers, _passes, _offside, _events;
    readonly DebugViewState _view = new();
    int _frames, _backFrames;
    Control _ui = null!;
    MatchPopups? _popups;                       // goal/card/... popups above the picture (see MatchPopups.cs)
    PanelContainer? _endCard;
    PauseMenu? _pauseMenu;
    int _hlDebugFrames;
    Button? _hlSkip;
    PlayerCard? _card;                          // clicked player (card in the right panel + marker and walking target on the pitch)

    public override void _Ready()
    {
        RenderingServer.SetDefaultClearColor(UiTheme.Bg);
        UiFrame.Follow(this, () => { Position = UiFrame.Offset(GetViewport()); QueueRedraw(); });      // 1600x900 layout centred on wider/taller screens
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

    // The driver is a child node so it can reach the tree (replay check quits the game); it does not process by itself, _Process drives it.
    void InitializeMatch()
    {
        _driver = new MatchDriver { Name = "MatchDriver" };
        AddChild(_driver);
        _driver.EventRead += e => _view.Read(e, _driver.Current);
        _driver.EventsRead += OnEventsRead;
        _driver.RestMomentReached += OpenPauseMenu;
        _driver.HighlightStarted += OnHighlightStarted;
        _driver.HighlightEnded += OnHighlightEnded;
        _debug = GameSession.DebugEnvironment("ZEUS_DEBUG").Length > 0;
        SetDebugLayers(_debug);
        _driver.Initialize();
        _crowd = new CrowdModel(Side.Home, _driver.TotalSeconds);
    }

    public override void _ExitTree() => AudioManager.CrowdOff();

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } || _driver.Highlighting || _pauseMenu is not null || _endCard is not null || _driver.Skipping) return;
        var click = ((InputEventMouseButton)MakeInputLocal(e)).Position;      // the event's own position, not the OS cursor (injected test input)
        if (click.X >= PanelX) return;                    // the panel itself is not the pitch
        _card = PlayerCard.At(_driver.Current, ToScreen, click);
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (_driver.Highlighting)                       // highlight reel: any of these keys skips it, the rest is ignored
        {
            if (k.Keycode is Key.Space or Key.Enter or Key.KpEnter or Key.Escape) _driver.EndHighlights();
            return;
        }
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
            case Key.Space: _driver.TogglePause(); break;
            case Key.End: _driver.SkipToEnd(); break;
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

    bool MatchOver => _driver.MatchOver;

    int Count(EventType type, Side team) => _driver.Match.Events.Events.Count(e => e.Type == type && e.Team == team);

    int Stat(int i, Side team) => _endStats![i * 2 + (team == Side.Home ? 0 : 1)];

    void BuildHud()
    {
        Welcome.Install(this, "match");
        _ui = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }; UiTheme.Apply(_ui); AddChild(_ui);
        var keys = UiTheme.Label(Loc.T(GameSession.DebugHints ? "match.keys" : "match.keys_release"), UiTheme.Small, UiTheme.Muted);
        keys.AddThemeColorOverride("font_color", UiTheme.Muted);
        keys.Position = new Vector2(53, 846); _ui.AddChild(keys);
        _popups = new MatchPopups(Loc.T("popup.home"), Loc.T("popup.away"), PopupNames.From(_driver.Match, _driver.HomeSquad!, _driver.AwaySquad!));
        AddChild(_popups);
        var curr = _driver.Current;
        if (int.TryParse(GameSession.DebugEnvironment("ZEUS_INSPECT"), out var inspect) && inspect >= 0 && inspect < curr.Players.Count)      // visual check of the player card: player index (0-10 home, 11-21 away)
            _card = PlayerCard.At(curr, ToScreen, ToScreen(curr.Players[inspect].Pos));
        AudioManager.Sfx("whistle_start");
        _popups.Feed(_driver.Match.Events.Events, _driver.Speed, suppress: true);          // events from before the HUD existed (ZEUS_FINISH): only the final result
        if (GameSession.DebugEnvironment("ZEUS_POPUPDEMO").Length > 0) _popups.StartDemo();
        if (CanCoach && Tour.ShouldShow("match")) Callable.From(() => Tour.Start(this, "match", ("match.coach", keys))).CallDeferred();
    }

    public void RequestCoaching() => RequestPause();

    /// <summary>False at the end screen, while skipping, in a replay and while the pause menu is already open: Settings then hides its coaching button.</summary>
    public bool CanCoach => _driver.CanSteer && _pauseMenu is null;

    /// <summary>Esc closes an open pause menu first (Hotkeys calls this before it opens Settings).</summary>
    public bool TryClosePauseMenu()
    {
        if (_driver.Highlighting) { _driver.EndHighlights(); return true; }          // Esc also skips the highlight reel
        if (_pauseMenu is null) { if (_card is null) return false; _card = null; return true; }
        ClosePauseMenu();
        return true;
    }

    // Free menu match only (challenges and replays keep their fixed seeds): a new seed, otherwise "play again" is the same match.
    void PlayAgain()
    {
        if (_driver.Replay is null) GameSession.Seed = GameSession.Seed % 999999 + 1;
        GetTree().ReloadCurrentScene();
    }

    // Pause and steer (PRD): substitutions and tactic changes are only accepted at a rest moment, so B waits for the next one.
    void RequestPause()
    {
        if (_pauseMenu is not null) return;
        _driver.RequestPause();
    }

    // The driver paused at a rest moment (RestMomentReached): show the menu.
    void OpenPauseMenu()
    {
        if (_pauseMenu is not null) return;
        _pauseMenu = new PauseMenu(_driver.Match, _driver.PlayerSquad!, _driver.PlayerSide, ClosePauseMenu);
        _ui.AddChild(_pauseMenu);
    }

    void ClosePauseMenu()
    {
        _pauseMenu?.QueueFree();
        _pauseMenu = null;
        _driver.Resume();
    }

    // Marks the player's own side in the competition, so it is clear who is who when he plays away.
    string You(Side side, bool lower = false) => GameSession.InCompetition && side == _driver.PlayerSide ? (lower ? Loc.T("match.you_lower") : Loc.T("match.you")) : "";

    // In the competition the scoreboard and end card name the clubs (the player's own club name); elsewhere HOME / AWAY.
    string SideName(Side side) => !GameSession.InCompetition ? Loc.T(side == Side.Home ? "match.home_caps" : "match.away_caps")
        : side == _driver.PlayerSide ? ClubIdentity.Name : GameSession.CompetitionOpponent;       // at most 24 characters: fits the scoreboard

    Control SideLabel(Side side)
    {
        var label = UiTheme.Label(SideName(side), GameSession.InCompetition ? 24 : 34, side == Side.Home ? UiTheme.Home : UiTheme.Away, display: true);
        if (!GameSession.InCompetition || side != _driver.PlayerSide) return label;
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; row.AddThemeConstantOverride("separation", 8);
        row.AddChild(CrestView.For(36)); row.AddChild(label);
        return row;
    }

    void ShowEndScreen()
    {
        if (_endCard is not null) return;
        var match = _driver.Match;
        var curr = _driver.Current;
        var result = MatchStatistics.FromMatch(match).AwardedGoals();
        FeedbackLog.MatchFinished(_driver.LogSeed, _driver.HomeTactic, _driver.AwayTactic, MatchDriver.Env("ZEUS_HOMELEVEL", GameSession.HomeLevel), MatchDriver.Env("ZEUS_AWAYLEVEL", GameSession.AwayLevel),
            _driver.LogMode, result.Home, result.Away, curr.Tick, curr.Status.ToString(), match.EventHash());
        var types = new[] { EventType.Shot, EventType.Pass, EventType.BallControl, EventType.Foul, EventType.Offside };
        _endStats = types.SelectMany(t => new[] { Count(t, Side.Home), Count(t, Side.Away) }).ToArray();
        _endCard = new PanelContainer { Position = new Vector2(140, 96), CustomMinimumSize = new Vector2(980, 0) }; _ui.AddChild(_endCard); UiTheme.ApplyPaper(_endCard);
        var card = _endCard;
        if (Tour.ShouldShow("end")) Callable.From(() => Tour.Start(this, "end", ("match.endcard", card))).CallDeferred();
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 36); _endCard.AddChild(row);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(480, 0) }; row.AddChild(box);
        var heats = new VBoxContainer(); row.AddChild(heats);
        heats.AddChild(UiTheme.Label(Loc.T("match.heatmaps"), 24, UiTheme.Accent, display: true));
        foreach (var (heat, color, title) in new[] { (_driver.HeatHome, UiTheme.Home, Loc.T("match.home") + You(Side.Home, true)), (_driver.HeatAway, UiTheme.Away, Loc.T("match.away") + You(Side.Away, true)) })
        {
            var view = new HeatmapView { Scale = 1.2f }; heats.AddChild(view); view.Show(heat, color, title);
        }
        box.AddChild(UiTheme.Label(Loc.T(curr.Status == MatchStatus.Abandoned ? "popup.abandoned" : "match.result"), UiTheme.Heading + 10, null, true));
        var scoreRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; box.AddChild(scoreRow);
        scoreRow.AddThemeConstantOverride("separation", 26);
        scoreRow.AddChild(SideLabel(Side.Home));
        scoreRow.AddChild(UiTheme.Label($"{result.Home} - {result.Away}", 96, UiTheme.Accent, display: true));
        scoreRow.AddChild(SideLabel(Side.Away));
        if (GameSession.InCompetition)
            box.AddChild(UiTheme.Label(Loc.T("match.your_side", _driver.PlayerSide == Side.Home ? Loc.T("common.home_red") : Loc.T("common.away_blue")), UiTheme.Small, UiTheme.Muted));
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
        var why = MatchWhy.Causes(MatchStatistics.FromMatch(match), _driver.PlayerSide);      // the "why": up to three causes for your side
        if (why.Count > 0)
        {
            box.AddChild(UiTheme.Label(Loc.T("why.title"), UiTheme.Small, UiTheme.Muted));
            foreach (var cause in why)
            {
                var line = UiTheme.Label("• " + Loc.T("why." + cause.Key, cause.Value), UiTheme.Small, cause.Good ? UiTheme.Good : UiTheme.Bad);
                line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                box.AddChild(line);
            }
        }
        if (match.Status == MatchStatus.Abandoned)
        {
            var notice = UiTheme.Label(Loc.T("challenge.live.abandoned"), UiTheme.Small, UiTheme.Bad);
            notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            box.AddChild(notice);
        }
        if (_driver.PickHighlights() is not null)
        {
            var watch = UiTheme.Secondary(Loc.T("highlight.watch")); watch.Pressed += StartHighlights; box.AddChild(watch);
        }
        if (GameSession.Challenge is { } run)           // live challenge: remember this match, Challenges shows how it stands
        {
            run.Record(ChallengeEvaluator.ToResult(MatchStatistics.FromMatch(match)));
            var next = UiTheme.Primary(Loc.T(run.Done ? "challenge.live.show_result" : "challenge.live.next_match", run.Results.Count + 1, run.Spec.Matches));
            next.Pressed += () => GetTree().ChangeSceneToFile("res://Challenges.tscn"); box.AddChild(next);
            return;
        }
        if (GameSession.InCompetition)          // competition match: the final score goes back to the season screen
        {
            GameSession.CompetitionDemand = match.FamiliarityDemand(_driver.PlayerSide);
            GameSession.LastRedCards = _driver.PlayerSquad is { } own ? Competition.RedCardedNames(match, _driver.PlayerSide, own).ToList() : new();      // suspensions next round
            GameSession.LastResult = result;
            var back = UiTheme.Primary(Loc.T("match.back_season")); back.Pressed += () => GetTree().ChangeSceneToFile("res://Season.tscn"); box.AddChild(back);
            return;
        }
        var actions = new HBoxContainer(); box.AddChild(actions);
        var again = UiTheme.Primary(Loc.T("match.again")); again.Pressed += PlayAgain; actions.AddChild(again);
        var menu = UiTheme.Secondary(Loc.T("match.menu")); menu.Pressed += () => GetTree().ChangeSceneToFile("res://Menu.tscn"); actions.AddChild(menu);
    }

    void StartHighlights()
    {
        if (_endCard is null) return;
        _driver.StartHighlights();
    }

    void OnHighlightStarted()
    {
        if (_endCard is not null) _endCard.Visible = false;
        _hlSkip = UiTheme.Secondary(Loc.T("highlight.skip")); _hlSkip.Position = new Vector2(Origin.X - 110, 176);      // under the highlight banner (y 92..164)
        _hlSkip.Pressed += _driver.EndHighlights; _ui.AddChild(_hlSkip);
    }

    void OnHighlightEnded()
    {
        if (_endCard is not null) _endCard.Visible = true;
        _hlSkip?.QueueFree(); _hlSkip = null;
    }

    public override void _Process(double delta)
    {
        bool reel = _driver.Highlighting;
        _driver.Advance(delta);
        if (reel)
        {
            QueueRedraw();
            if (_driver.Highlighting && _driver.HighlightPlayed > 3 && GameSession.DebugEnvironment("ZEUS_HIGHLIGHT").Length > 0 && GameSession.DebugEnvironment("ZEUS_SHOT") is { Length: > 0 } clipShot)
            {
                GetViewport().GetTexture().GetImage().SavePng(clipShot + "_highlight.png");
                GetTree().Quit();
            }
            return;
        }
        var curr = _driver.Current;
        _view.Advance(curr.TimeSeconds);
        if (_popups is not null) { _popups.Visible = _pauseMenu is null && _endCard is null; _popups.Advance(delta, _driver.Paused || _driver.Skipping); }
        QueueRedraw();

        if (MatchOver) ShowEndScreen();
        // Visual check of the highlight reel: ZEUS_HIGHLIGHT=1 (with ZEUS_SHOT) saves the end card, then plays the first clip.
        if (_endCard is not null && _driver.Moments is not null && GameSession.DebugEnvironment("ZEUS_HIGHLIGHT").Length > 0 && GameSession.DebugEnvironment("ZEUS_SHOT") is { Length: > 0 } cardShot && ++_hlDebugFrames == 10)
        {
            GetViewport().GetTexture().GetImage().SavePng(cardShot + "_match.png");
            StartHighlights();
            return;
        }
        // Visual/functional check of the competition loop: with ZEUS_AUTOBACK the finished match returns to Season by itself.
        if (MatchOver && (GameSession.InCompetition || GameSession.Challenge is not null) && GameSession.DebugEnvironment("ZEUS_AUTOBACK").Length > 0 && ++_backFrames == 6)
        {
            if (GameSession.DebugEnvironment("ZEUS_SHOT") is { Length: > 0 } prefix) GetViewport().GetTexture().GetImage().SavePng(prefix + "_matchend.png");
            GetTree().ChangeSceneToFile(GameSession.Challenge is not null ? "res://Challenges.tscn" : "res://Season.tscn");
            return;
        }
        var shot = GameSession.DebugEnvironment("ZEUS_SHOT");
        if (shot.Length == 0 || ++_frames < 10 || (curr.TimeSeconds < 6.2 && _pauseMenu is null) || _popups?.DemoRunning == true) return;
        GetViewport().GetTexture().GetImage().SavePng(shot + "_match.png");
        GetTree().Quit();
    }

    static Vector2 ToScreen(Vec2 p) => Origin + new Vector2((float)p.X, (float)p.Y) * Px;

    void SetDebugLayers(bool enabled) => _phases = _triggers = _passes = _offside = _events = enabled;

    // After the driver read a batch of events: popups (skipping suppresses all popups except the final result) and the debug view.
    void OnEventsRead()
    {
        var events = _driver.Match.Events.Events;
        int first = _crowdSeen; _crowdSeen = events.Count;
        if (!_driver.Skipping)         // the crowd follows sim time, so it needs every tick, also without events
        {
            var reactions = _crowd.Update(_driver.Current, first < events.Count ? events.Skip(first).ToList() : Array.Empty<Zeus.Sim.Event>());
            AudioManager.Crowd(_crowd.Intensity);
            foreach (var reaction in reactions) AudioManager.CrowdReaction(reaction, _driver.Speed);
        }
        _popups?.Feed(events, _driver.Speed, suppress: _driver.Skipping);
        _view.Advance(_driver.Current.TimeSeconds);
    }

    public override void _Draw()
    {
        var curr = _driver?.Current;
        if (curr is null) return;
        var prev = _driver!.Previous;
        DrawRect(new Rect2(-Position, GetViewportRect().Size), UiTheme.Bg, true);
        const float L = (float)Pitch.Length, W = (float)Pitch.Width;
        var field = new Rect2(Origin - new Vector2(L * Px / 2, W * Px / 2), new Vector2(L * Px, W * Px));
        GameArt.DrawPitch(this, field, 24);
        if (_offside) DrawOffside();
        if (_passes) DrawLastPass();
        float alpha = _driver.Alpha;
        for (int i = 0; i < curr.Players.Count; i++)
        {
            var c = curr.Players[i];
            if (c.SentOff) continue;
            var p0 = i < prev.Players.Count ? prev.Players[i].Pos : c.Pos;
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
        if (curr.Ball is { } ball)
        {
            var previous = prev.Ball?.Pos ?? ball.Pos;
            var pos = ToScreen(new Vec2(previous.X + (ball.Pos.X - previous.X) * alpha,
                previous.Y + (ball.Pos.Y - previous.Y) * alpha));
            GameArt.DrawBall(this, pos);
        }
        GameArt.DrawVignette(this, field.Grow(24), 0.5f);
        if (!_driver.Highlighting) _card?.DrawOnField(this, _driver.Match, curr, _driver.PlayerSide, ToScreen);
        if (_debug)
            DrawString(GameFonts.Body, new Vector2(53, 892),
                Loc.T("match.debug_layers", On(_phases), On(_triggers), On(_passes), On(_offside), On(_events)),
                HorizontalAlignment.Left, -1, UiTheme.Small, UiTheme.Muted);
        int minutes = (int)(curr.TimeSeconds / 60), seconds = (int)(curr.TimeSeconds % 60);
        GameArt.DrawScoreBug(this, new Vector2(53, 1), SideName(Side.Home), SideName(Side.Away), curr.HomeScore, curr.AwayScore, $"{minutes:D2}:{seconds:D2}",
            Loc.T("match.period", curr.Period, _driver.Speed, _driver.Paused ? Loc.T("match.paused") : "", _driver.PauseRequested ? Loc.T("match.pause_requested") : ""),
            GameSession.InCompetition ? _driver.PlayerSide : null);
        DrawPanel();
        if (_driver.Highlighting)
        {
            var moments = _driver.Moments!;
            var banner = new Rect2(Origin.X - 260, 92, 520, 72);
            DrawStyleBox(new StyleBoxFlat { BgColor = new Color(UiTheme.Panel, 0.92f), BorderColor = new Color(UiTheme.Accent, 0.7f), BorderWidthTop = 3 }, banner);
            DrawString(GameFonts.Display, banner.Position + new Vector2(0, 38), moments[Math.Min(_driver.HighlightIndex, moments.Count - 1)].Label, HorizontalAlignment.Center, banner.Size.X, 34, UiTheme.Accent);
            DrawString(GameFonts.Body, banner.Position + new Vector2(0, 62), Loc.T("highlight.skip_help", Math.Min(_driver.HighlightIndex + 1, moments.Count), moments.Count), HorizontalAlignment.Center, banner.Size.X, UiTheme.Small, UiTheme.Muted);
        }
        if (_driver.Skipping)
        {
            var banner = new Rect2(Origin.X - 260, Origin.Y - 40, 520, 80);
            DrawStyleBox(new StyleBoxFlat { BgColor = new Color(UiTheme.Panel, 0.92f), BorderColor = new Color(UiTheme.Accent, 0.7f), BorderWidthTop = 3 }, banner);
            DrawString(GameFonts.Display, banner.Position + new Vector2(0, 38), Loc.T("match.skipping", Math.Clamp(curr.TimeSeconds / _driver.TotalSeconds * 100, 0, 100)),
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
        var curr = _driver.Current;
        foreach (var team in new[] { Side.Home, Side.Away })
        {
            if (DebugViewState.OffsideLine(curr, team) is not { } line) continue;
            var color = team == Side.Home ? UiTheme.Home : UiTheme.Away;
            DrawLine(ToScreen(new Vec2(line.X, -Pitch.Width / 2)),
                ToScreen(new Vec2(line.X, Pitch.Width / 2)), color, 2);
            DrawString(ThemeDB.FallbackFont, ToScreen(new Vec2(line.X, -Pitch.Width / 2)) + new Vector2(4, 15),
                team == Side.Home ? Loc.T("match.home_line") : Loc.T("match.away_line"), HorizontalAlignment.Left, -1, 12, color);
            foreach (var player in curr.Players.Where(p => p.Team == team && line.Candidates.Contains(p.Id)))
                DrawArc(ToScreen(player.Pos), 16, 0, Mathf.Tau, 24, color, 3);
        }
    }

    static string Short(string text, int max = 39) => text.Length <= max ? text : text[..(max - 1)] + "…";

    void PanelLine(string text, float y, Color? color = null, int size = 15, Font? font = null) =>
        DrawString(font ?? GameFonts.Body, new Vector2(PanelX + 22, y), Short(text, size <= 13 ? 48 : 39),
            HorizontalAlignment.Left, -1, Math.Max(size, 14), UiTheme.PaperColor(color ?? UiTheme.Text));    // 14 floor: the window scales to ~0.84-0.89 on 1280x720 / 1366x768

    // One mirrored stat row: home value, label, away value, and a split bar (home share in red, away share in blue).
    void StatRow(string label, float y, double home, double away, bool percent = false)
    {
        const float x = PanelX + 22, w = 316;
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
        var curr = _driver.Current;
        var live = _driver.Live;
        DrawStyleBox(new StyleBoxFlat { BgColor = UiTheme.Paper, BorderColor = UiTheme.PaperShade, BorderWidthTop = 3 },
            new Rect2(PanelX, 50, 360, 800));
        UiPaper.DrawPage(this, new Rect2(PanelX, 50, 360, 800));
        PanelLine(Loc.T("match.live"), 100, UiTheme.Accent, 38, GameFonts.Display);
        PanelLine(Short(_driver.HomeTactic + You(Side.Home), 40), 128, UiTheme.Home, 15, GameFonts.Semi);
        PanelLine(Short(_driver.AwayTactic + You(Side.Away), 40), 148, UiTheme.Away, 15, GameFonts.Semi);

        StatRow(Loc.T("match.shots_caps"), 180, live[0], live[1]);
        StatRow(Loc.T("match.passes_caps"), 250, live[2], live[3]);
        StatRow(Loc.T("match.possession"), 320, live[4], live[5], percent: true);
        StatRow(Loc.T("match.fouls_caps"), 390, live[6], live[7]);

        int yellowH = 0, yellowA = 0, redH = 0, redA = 0;
        foreach (var player in curr.Players)
        {
            bool home = player.Team == Side.Home;
            if (home) yellowH += player.YellowCards; else yellowA += player.YellowCards;
            if (player.SentOff) { if (home) redH++; else redA++; }
        }
        PanelLine(Loc.T("match.cards"), 476, UiTheme.Muted, 16, GameFonts.Semi);
        PanelLine(Loc.T("match.card_counts", yellowH, redH), 498, UiTheme.Home, 18, GameFonts.Semi);
        PanelLine(Loc.T("match.card_counts", yellowA, redA), 522, UiTheme.Away, 18, GameFonts.Semi);
        if (_driver.Replay is { } replay)
            PanelLine(_driver.ReplayVerified ? Loc.T("match.replay_checked") : Loc.T("match.replay", curr.Tick, replay.Steps), 554, UiTheme.Good, 14);
        if (_phases)
        {
            PanelLine(Loc.T("match.home_prefix") + Loc.Name(DebugViewState.PhaseLabel(_view.PhaseOf(Side.Home))), 582, UiTheme.Home);
            PanelLine(Loc.T("match.away_prefix") + Loc.Name(DebugViewState.PhaseLabel(_view.PhaseOf(Side.Away))), 604, UiTheme.Away);
        }
        if (_card is not null && !_driver.Highlighting && _pauseMenu is null) { _card.DrawCard(this, _driver.Match, curr, _driver.PlayerSide, PopupNames.From(_driver.Match, _driver.HomeSquad!, _driver.AwaySquad!)); return; }
        if (!_debug)
        {
            if (GameSession.DebugHints) PanelLine(Loc.T("match.debug_help"), 820, UiTheme.Muted, 14);
            return;
        }
        PanelLine(_events ? Loc.T("match.recent_events") : Loc.T("match.show_events"), 646, UiTheme.Text, 16, GameFonts.Semi);
        if (_events)
        {
            int row = 0;
            foreach (var e in _view.RecentEvents.TakeLast(3))
            {
                float y = 670 + row++ * 36;
                PanelLine($"{e.Seconds:0}s {e.Type} {e.Team} {e.PlayerId}", y, UiTheme.Text, 13);
                PanelLine(Short(e.Outcome, 44), y + 17, UiTheme.Muted, 12);
            }
        }
        if (_passes && _view.LastPass is { } pass)
            PanelLine($"PASS {pass.Event.Team} {pass.Event.PlayerId} -> {pass.ReceiverId ?? "?"}", 800, UiTheme.Accent, 13);
        if (_triggers && _view.Triggers.Count > 0)
            PanelLine("TRIGGER " + _view.Triggers.Last().PlayerId + ": " + _view.Triggers.Last().Outcome, 820, Colors.Orange, 12);
    }
}
