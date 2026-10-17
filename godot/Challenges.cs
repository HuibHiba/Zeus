// Challenges: pick a goal, choose your own tactic, play the matches in the background and see which goals you reached.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Zeus.Sim;

public partial class Challenges : Control
{
    IReadOnlyList<ChallengeSpec> _specs = Array.Empty<ChallengeSpec>();
    ChallengeSpec? _selected;
    readonly Dictionary<string, Button> _buttons = new();
    VBoxContainer _detail = null!, _result = null!;
    OptionButton _tactic = null!, _level = null!;
    Button _live = null!, _start = null!, _edit = null!, _cancel = null!;      // _start = "Simuleer alles"; _live = play the matches yourself (primary)
    Task<ChallengeRun>? _prep;                                // picking the opponent for a live run (the adaptive planner simulates)
    bool _confirmAbort;
    ProgressBar _progress = null!;
    Label _status = null!;
    Task<(ChallengeResult Result, List<ChallengeMatchResult> Matches, OpponentPlan? Plan, HistoryEntry Played, Played Setup)>? _work;
    CancellationTokenSource? _cts;
    int _permille;
    int _preparing;
    int _frames;
    double _elapsed;
    string _playerTactic = "";
    bool _shown;
    static string? _lastTactic, _lastLevel;      // what he chose here last (the menu's GameSession fields stay untouched)

    /// <summary>What was actually played: the player's tactic file and level, the opponent that was really chosen (adaptive challenges pick one) and its level.</summary>
    sealed record Played(string PlayerPath, string PlayerLevel, string OpponentPath, string OpponentLevel);

    public override void _Ready()
    {
        UiTheme.ApplyPaper(this);
        SetAnchorsPreset(LayoutPreset.FullRect);
        RenderingServer.SetDefaultClearColor(UiTheme.Bg);
        var bg = new UiPaper(); bg.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(bg);

        var margin = new MarginContainer(); margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right" }) margin.AddThemeConstantOverride("margin_" + side, side == "right" ? 56 : 40);   // right: room for the Esc button of Hotkeys
        foreach (var side in new[] { "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 22);
        AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 10); margin.AddChild(root);

        var head = new HBoxContainer(); root.AddChild(head);
        head.AddChild(UiTheme.Label(Loc.T("challenge.title"), 46, UiTheme.Accent, display: true));
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        Welcome.Install(this, "challenges", head);
        var back = UiTheme.Secondary(Loc.T("challenge.back")); back.Pressed += Back; head.AddChild(back);

        var body = new HBoxContainer(); body.AddThemeConstantOverride("separation", 28); body.SizeFlagsVertical = SizeFlags.ExpandFill; root.AddChild(body);

        var listScroll = new ScrollContainer { CustomMinimumSize = new Vector2(360, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        body.AddChild(listScroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; list.AddThemeConstantOverride("separation", 8); listScroll.AddChild(list);

        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = UiTheme.Paper, BorderColor = UiTheme.PaperShade, BorderWidthTop = 3,
            ContentMarginLeft = 26, ContentMarginRight = 26, ContentMarginTop = 16, ContentMarginBottom = 16,
        });
        body.AddChild(panel);
        _detail = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; _detail.AddThemeConstantOverride("separation", 8); panel.AddChild(_detail);

        IReadOnlyList<string> loadErrors = Array.Empty<string>();
        try { _specs = ChallengeEvaluator.LoadAll(GameSession.ChallengesDir(), out loadErrors, Loc.Language); }
        catch (Exception e) { _detail.AddChild(Wrapped(Loc.T("challenge.load_failed") + Loc.Error(e), UiTheme.Bad)); return; }
        foreach (var problem in loadErrors) GD.PushWarning("Uitdaging overgeslagen: " + problem);
        if (_specs.Count == 0)
        {
            _detail.AddChild(Wrapped(loadErrors.Count > 0 ? Loc.T("challenge.none_loaded") : Loc.T("challenge.none_found"),
                loadErrors.Count > 0 ? UiTheme.Bad : UiTheme.Muted));
            return;
        }
        if (GameSession.LastSavedTactic is { } saved)       // back from the editor: the tactic he just saved is offered as his choice
        {
            GameSession.LastSavedTactic = null;
            if (File.Exists(GameSession.TacticPath(saved))) _lastTactic = GameSession.TacticPath(saved);
        }

        var group = new ButtonGroup();
        foreach (var spec in _specs)
        {
            var b = new Button { ToggleMode = true, ButtonGroup = group, ThemeTypeVariation = "Chip", Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(0, 58), ClipText = true };
            var s = spec;
            b.Pressed += () => Select(s);
            _buttons[spec.Id] = b;
            list.AddChild(b);
        }
        RefreshList();
        var wanted = int.TryParse(GameSession.DebugEnvironment("ZEUS_CHALLENGE"), out var n) ? Math.Clamp(n, 0, _specs.Count - 1) : 0;
        var resume = GameSession.Challenge;                // back from a live match: carry on with that challenge
        var resumeSpec = resume is null ? null : _specs.FirstOrDefault(s => s.Id == resume.Spec.Id);
        if (resume is not null && resumeSpec is null) GameSession.Challenge = null;
        Select(resumeSpec ?? _specs[wanted]);
        if (resumeSpec is not null) ShowRun(resume!);
        else if (GameSession.DebugEnvironment("ZEUS_LIVEFAKE") is { Length: > 0 } fake) ShowRun(FakeRun(fake));
        if (loadErrors.Count > 0)
            _status.Text = Loc.T("challenge.skipped", loadErrors.Count);
        if (GameSession.DebugEnvironment("ZEUS_RUN").Length > 0) Run();
        else if (GameSession.DebugEnvironment("ZEUS_LIVE").Length > 0 && resume is null) StartLive();
    }

    void RefreshList()
    {
        var progress = ChallengeProgress.Load();
        foreach (var spec in _specs)
            _buttons[spec.Id].Text = $"{(progress.ContainsKey(spec.Id) ? "✓  " : "     ")}{spec.Name}";
    }

    static Label Wrapped(string text, Color color, int size = UiTheme.Body)
    {
        var l = UiTheme.Label(text, size, color);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart; l.SizeFlagsHorizontal = SizeFlags.ExpandFill; l.CustomMinimumSize = new Vector2(10, 0);
        return l;
    }

    void Select(ChallengeSpec spec)
    {
        if (_work is not null || _prep is not null || GameSession.Challenge is not null && _selected is not null && _selected != spec)       // do not switch while a run is going
        {
            if (_selected is not null) _buttons[_selected.Id].SetPressedNoSignal(true);
            return;
        }
        _selected = spec;
        _confirmAbort = false;
        _buttons[spec.Id].SetPressedNoSignal(true);
        foreach (var c in _detail.GetChildren()) c.QueueFree();
        _shown = false;
        var cols = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; cols.AddThemeConstantOverride("separation", 30); _detail.AddChild(cols);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; column.AddThemeConstantOverride("separation", 8); cols.AddChild(column);
        // Info on top may scroll if a challenge has a lot of text; the choice and the start button below it always stay in view.
        var infoScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; column.AddChild(infoScroll);
        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; left.AddThemeConstantOverride("separation", 8); infoScroll.AddChild(left);
        var notes = new VBoxContainer(); notes.AddThemeConstantOverride("separation", 8);      // tip and explanations go to the right column, so the left one never needs to scroll
        string opp;
        try { opp = TacticLoader.LoadFile(Path.Combine(GameSession.TacticsDir(), spec.Opponent)).Name; } catch { opp = spec.Opponent; }

        left.AddChild(UiTheme.Label(spec.Name.ToUpperInvariant(), 38, UiTheme.Text, display: true));
        left.AddChild(Wrapped(spec.Description, UiTheme.Text));
        left.AddChild(Wrapped(Loc.T("challenge.opponent", Loc.Name(opp), spec.Matches), UiTheme.Muted, UiTheme.Small + 1));
        left.AddChild(UiTheme.Label(Loc.T("challenge.goals"), 26, UiTheme.Accent, display: true));
        var progressAll = ChallengeProgress.Load();
        foreach (var c in spec.Criteria)
            left.AddChild(Wrapped("•  " + Describe(c), UiTheme.Text));
        if (spec.Adaptive)
            notes.AddChild(Wrapped(Loc.T("challenge.adaptive_help"), UiTheme.Accent, UiTheme.Small + 1));
        if (progressAll.TryGetValue(spec.Id, out var done))
            notes.AddChild(Wrapped(Loc.T("challenge.completed_with", Loc.Name(done.Tactic), done.Date), UiTheme.Good));

        var act = new VBoxContainer(); act.AddThemeConstantOverride("separation", 8); column.AddChild(act);
        act.AddChild(new HSeparator());
        act.AddChild(UiTheme.Label(Loc.T("challenge.your_tactic"), 26, UiTheme.Accent, display: true));
        _tactic = new OptionButton { FitToLongestItem = false, CustomMinimumSize = new Vector2(0, 44), Alignment = HorizontalAlignment.Left };
        var files = GameSession.Tactics().Select(t => t.Path).ToArray();
        foreach (var f in files) { _tactic.AddItem(GameSession.TacticLabel(f)); _tactic.SetItemMetadata(_tactic.ItemCount - 1, f); }
        var preferred = _lastTactic ?? GameSession.HomePath;      // own memory first: GameSession is borrowed by Season
        for (int i = 0; i < files.Length; i++)
            if (preferred is not null && files[i] == preferred) _tactic.Select(i);
        act.AddChild(_tactic);
        var levelRow = new HBoxContainer(); act.AddChild(levelRow);
        levelRow.AddChild(UiTheme.Label(Loc.T("challenge.team_level"), 18, UiTheme.Muted, display: true));
        _level = new OptionButton { FitToLongestItem = false, CustomMinimumSize = new Vector2(0, 40), SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = HorizontalAlignment.Left };
        foreach (var l in SquadFactory.Levels) _level.AddItem(Loc.Name(l));
        _level.Select(Math.Max(0, Array.IndexOf(SquadFactory.Levels.ToArray(), _lastLevel ?? GameSession.HomeLevel)));
        levelRow.AddChild(_level);
        if (spec.OpponentLevel != "neutraal")
            act.AddChild(Wrapped(Loc.T("challenge.opponent_level", Loc.Name(spec.OpponentLevel)), UiTheme.Muted, UiTheme.Small + 1));

        _live = UiTheme.Primary(Loc.T("challenge.play_live")); _live.SizeFlagsHorizontal = SizeFlags.ExpandFill; _live.Pressed += StartLive; act.AddChild(_live);
        notes.AddChild(Wrapped(Loc.T("challenge.play_live_hint", spec.Matches), UiTheme.Muted, UiTheme.Small + 1));
        var row = new HBoxContainer(); act.AddChild(row);
        _start = UiTheme.Secondary(Loc.T("challenge.simulate")); _start.SizeFlagsHorizontal = SizeFlags.ExpandFill; _start.Pressed += Run; row.AddChild(_start);
        _edit = UiTheme.Secondary(Loc.T("challenge.edit")); _edit.Pressed += EditTactic; row.AddChild(_edit);
        var progressRow = new HBoxContainer(); act.AddChild(progressRow);
        _progress = new ProgressBar { MinValue = 0, MaxValue = spec.Matches, CustomMinimumSize = new Vector2(0, 22), Visible = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill }; progressRow.AddChild(_progress);
        _cancel = UiTheme.Secondary(Loc.T("batch.cancel")); _cancel.Visible = false;
        _cancel.Pressed += CancelWork; progressRow.AddChild(_cancel);
        _status = Wrapped("", UiTheme.Muted, UiTheme.Small + 1); act.AddChild(_status);
        var resultScroll = new ScrollContainer { CustomMinimumSize = new Vector2(380, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; cols.AddChild(resultScroll);
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; right.AddThemeConstantOverride("separation", 12); resultScroll.AddChild(right);
        if (TipBox(spec) is { } tipBox) right.AddChild(tipBox);      // prominent: top of the column next to the play buttons, not hidden in the scrolling info
        right.AddChild(notes);
        _result = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _result.AddThemeConstantOverride("separation", 8); right.AddChild(_result);
        _result.AddChild(Wrapped(Loc.T("challenge.result_hint"), UiTheme.Muted, UiTheme.Small + 1));
    }

    /// <summary>Highlighted tip plus the recommended starting tactic of a challenge; null when it has neither.</summary>
    static PanelContainer? TipBox(ChallengeSpec spec)
    {
        if (spec.Hint.Length == 0 && spec.StartTactic.Length == 0) return null;
        var box = new PanelContainer(); box.AddThemeStyleboxOverride("panel", UiTheme.PanelBox());
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4); box.AddChild(v);
        v.AddChild(UiTheme.Label(Loc.T("challenge.tip_title"), 22, UiTheme.Accent, display: true));
        if (spec.Hint.Length > 0) v.AddChild(Wrapped(spec.Hint, UiTheme.Text));
        if (spec.StartTactic.Length > 0)
        {
            string name;
            try { name = TacticLoader.LoadFile(Path.Combine(GameSession.TacticsDir(), spec.StartTactic)).Name; } catch { name = spec.StartTactic; }
            v.AddChild(Wrapped(Loc.T("challenge.start", Loc.Name(name)) + (spec.StartNote.Length > 0 ? " " + spec.StartNote : ""), UiTheme.Muted, UiTheme.Small + 1));
        }
        return box;
    }

    static string Describe(ChallengeCriterion c)
    {
        return Loc.Criterion(c);
    }

    string SelectedTacticPath() => (string)_tactic.GetItemMetadata(_tactic.Selected);

    void Run()
    {
        if (_selected is null || _work is not null || _prep is not null || GameSession.Challenge is not null || _tactic.ItemCount == 0) return;
        foreach (var c in _result.GetChildren()) c.QueueFree();
        var spec = _selected;
        var playerPath = SelectedTacticPath();
        _playerTactic = Path.GetFileNameWithoutExtension(playerPath).Replace('_', ' ');
        _lastTactic = playerPath;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _permille = 0;
        Volatile.Write(ref _preparing, spec.Adaptive ? 1 : 0);
        SetControlsEnabled(false); _cancel.Visible = true; _cancel.Disabled = false;
        _progress.Visible = true; _progress.Value = 0;
        _status.AddThemeColorOverride("font_color", UiTheme.PaperColor(UiTheme.Muted));
        _status.Text = Loc.T("challenge.playing");
        var history = PlayStyleHistory.Load();
        var tacticsDir = GameSession.TacticsDir();
        var playerLevel = SquadFactory.Levels[_level.Selected];
        _lastLevel = playerLevel;
        var squadsDir = GameSession.SquadsDir();
        _work = SimulationBatch.RunAsync(t =>
        {
            var (player, opponent, opponentPath, plan) = ChooseOpponent(spec, playerPath, tacticsDir, history, t,
                fraction => Volatile.Write(ref _permille, (int)(fraction * 1000)));
            Volatile.Write(ref _permille, 0);
            Volatile.Write(ref _preparing, 0);
            var matches = ChallengeEvaluator.Play(spec, player, opponent, token, total => Volatile.Write(ref _permille, (int)(total * 1000)),
                playerSquad: SquadFactory.ForLevel(playerLevel, player, squadsDir), opponentSquad: SquadFactory.ForLevel(spec.OpponentLevel, opponent, squadsDir));
            return (ChallengeEvaluator.Evaluate(spec, matches), matches, plan, new HistoryEntry(player.Name, StyleProfile.From(player)),
                new Played(playerPath, playerLevel, opponentPath, spec.OpponentLevel));
        }, token);
    }

    /// <summary>The player's tactic and the opponent for this attempt: the challenge's own, or (adaptive) the one the planner prepares against the player's recent style.</summary>
    static (Tactic Player, Tactic Opponent, string OpponentPath, OpponentPlan? Plan) ChooseOpponent(ChallengeSpec spec, string playerPath, string tacticsDir,
        List<HistoryEntry> history, CancellationToken token, Action<double>? progress = null)
    {
        var player = TacticLoader.LoadFile(playerPath);
        var opponentPath = Path.Combine(tacticsDir, spec.Opponent);
        var opponent = TacticLoader.LoadFile(opponentPath);
        OpponentPlan? plan = null;
        if (spec.Adaptive)        // the opponent only reacts to what it can know: the style of your last attempts
        {
            var pool = Directory.GetFiles(tacticsDir, "*.json").OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => (Name: Path.GetFileNameWithoutExtension(f), Tactic: TacticLoader.LoadFile(f))).ToList();
            plan = OpponentPlanner.Plan(player, pool, Path.GetFileNameWithoutExtension(spec.Opponent), history, token, progress: progress);
            if (plan.Prepared)
            {
                var chosen = pool.First(c => c.Name == plan.OpponentName);
                opponent = chosen.Tactic;
                opponentPath = Path.Combine(tacticsDir, chosen.Name + ".json");
            }
        }
        return (player, opponent, opponentPath, plan);
    }

    // ---- live: the player plays the challenge's matches himself, one by one (Main plays and records each one) ----

    void SetControlsEnabled(bool enabled)
    {
        _live.Disabled = _start.Disabled = _edit.Disabled = _tactic.Disabled = _level.Disabled = !enabled;
        foreach (var button in _buttons.Values) button.Disabled = !enabled;
    }

    void CancelWork()
    {
        if (_cts is null || _work is null && _prep is null) return;
        _cts.Cancel();
        _cancel.Disabled = true;
        _status.Text = Loc.T("batch.cancelling");
    }

    void FinishWork()
    {
        _cts?.Dispose(); _cts = null;
        _cancel.Visible = false;
        SetControlsEnabled(true);
    }

    void StartLive()
    {
        if (_selected is null || _work is not null || _prep is not null || _tactic.ItemCount == 0 || GameSession.Challenge is not null) return;
        foreach (var c in _result.GetChildren()) c.QueueFree();
        var spec = _selected;
        var playerPath = SelectedTacticPath();
        var playerName = Path.GetFileNameWithoutExtension(playerPath).Replace('_', ' ');
        _playerTactic = playerName;
        _lastTactic = playerPath;
        var playerLevel = SquadFactory.Levels[_level.Selected];
        _lastLevel = playerLevel;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var history = PlayStyleHistory.Load();
        var tacticsDir = GameSession.TacticsDir();
        SetControlsEnabled(false);
        _permille = 0;
        _progress.Visible = true; _progress.Value = 0;
        _cancel.Visible = true; _cancel.Disabled = false;
        _status.AddThemeColorOverride("font_color", UiTheme.PaperColor(UiTheme.Muted));
        _status.Text = Loc.T("challenge.live.preparing");
        _prep = SimulationBatch.RunAsync(t =>
        {
            var (player, _, opponentPath, plan) = ChooseOpponent(spec, playerPath, tacticsDir, history, t,
                fraction => Volatile.Write(ref _permille, (int)(fraction * 1000)));
            return new ChallengeRun(spec, playerPath, playerLevel, opponentPath, playerName, plan, new HistoryEntry(player.Name, StyleProfile.From(player)));
        }, token);
    }

    void PlayNext(ChallengeRun run)
    {
        run.Prepare();
        GetTree().ChangeSceneToFile("res://Main.tscn");
    }

    void ClearResult() { foreach (var c in _result.GetChildren()) c.QueueFree(); }

    /// <summary>What a run in progress shows: the tussenstand after a match, the question after a match that was left early, or the final result.</summary>
    void ShowRun(ChallengeRun run)
    {
        ClearResult();
        if (run.Results.Count > 0 && GameSession.DebugEnvironment("ZEUS_LIVEFAKE").Length == 0) OnboardingProgress.Mark("challenge");
        _playerTactic = run.PlayerName;
        for (int i = 0; i < _tactic.ItemCount; i++) if ((string)_tactic.GetItemMetadata(i) == run.PlayerPath) _tactic.Select(i);
        _level.Select(Math.Max(0, Array.IndexOf(SquadFactory.Levels.ToArray(), run.PlayerLevel)));
        SetControlsEnabled(false);
        int total = run.Spec.Matches;
        if (run.Done)
        {
            GameSession.Challenge = null;
            SetControlsEnabled(true);
            Show(ChallengeEvaluator.Evaluate(run.Spec, run.Results), run.Results, run.Plan, run.Played, new Played(run.PlayerPath, run.PlayerLevel, run.OpponentPath, run.Spec.OpponentLevel), live: true);
            return;
        }
        if (_confirmAbort || run.InMatch)          // left a match early, or Esc between matches: ask before throwing the run away
        {
            _result.AddChild(UiTheme.Label(Loc.T(run.InMatch ? "challenge.live.unfinished" : "challenge.live.abort_title", run.Results.Count + 1), 30, UiTheme.Bad, display: true));
            _result.AddChild(Wrapped(Loc.T(run.InMatch ? "challenge.live.unfinished_text" : "challenge.live.abort_text"), UiTheme.Text));
            var again = UiTheme.Primary(Loc.T(run.InMatch ? "challenge.live.replay_match" : "challenge.live.continue", run.Results.Count + 1, total)); again.Pressed += () => ContinueRun(run); _result.AddChild(again);
            var abort = UiTheme.Secondary(Loc.T("challenge.live.abort_yes")); abort.Pressed += () => AbortRun(run); _result.AddChild(abort);
            return;
        }
        int played = run.Results.Count;
        var standing = ChallengeEvaluator.Standing(run.Spec, run.Results);
        _result.AddChild(UiTheme.Label(Loc.T("challenge.live.after", played, total), 34, UiTheme.Accent, display: true));
        var next = UiTheme.Primary(Loc.T("challenge.live.next_match", played + 1, total)); next.Pressed += () => ContinueRun(run); _result.AddChild(next);
        var last = run.Results[^1];
        _result.AddChild(Wrapped(Loc.T("challenge.live.score", last.GoalsFor, last.GoalsAgainst) + (last.Abandoned ? " " + Loc.T("challenge.live.abandoned_suffix") : ""), UiTheme.Text));
        foreach (var g in standing.Goals)
        {
            var (mark, key, color) = !g.Possible ? ("✗", "challenge.live.unreachable", UiTheme.Bad) : g.OnTrack ? ("✓", "challenge.live.on_track", UiTheme.Good) : ("~", "challenge.live.reachable", UiTheme.Accent);
            _result.AddChild(Wrapped($"{mark}  {Loc.Criterion(g.Current.Criterion, g.Current.Actual)}  —  {Loc.T(key)}", color));
        }
        _result.AddChild(Wrapped(Loc.T(standing.AllPossible ? "challenge.live.verdict_ok" : "challenge.live.verdict_lost"), standing.AllPossible ? UiTheme.Muted : UiTheme.Bad, UiTheme.Small + 1));
        if (!standing.AllPossible && TipBox(run.Spec) is { } lostTip) _result.AddChild(lostTip);       // a goal slipped away: repeat the tip
        _result.AddChild(Wrapped(Loc.T("challenge.live.scores", string.Join("   ", run.Results.Select(m => $"{m.GoalsFor}-{m.GoalsAgainst}{(m.Abandoned ? Loc.T("challenge.abandoned") : "")}"))), UiTheme.Muted, UiTheme.Small + 1));
        var stop = UiTheme.Secondary(Loc.T("challenge.live.abort")); stop.Pressed += () => { _confirmAbort = true; ShowRun(run); }; _result.AddChild(stop);
        if (GameSession.DebugEnvironment("ZEUS_LIVE").Length > 0) Callable.From(() => ContinueRun(run)).CallDeferred();       // visual check: carry on by itself
    }

    void ContinueRun(ChallengeRun run)
    {
        if (run.Done) return;
        PlayNext(run);
    }

    void AbortRun(ChallengeRun run)
    {
        GameSession.Challenge = null;
        _confirmAbort = false;
        SetControlsEnabled(true);
        ClearResult();
        _result.AddChild(Wrapped(Loc.T("challenge.live.aborted"), UiTheme.Muted));
    }

    // Debug only (ZEUS_LIVEFAKE="2-0,1-1"): a run with invented scores, to look at the tussenstand without playing.
    ChallengeRun FakeRun(string scores)
    {
        var run = new ChallengeRun(_selected!, GameSession.Tactics().First().Path, SquadFactory.Levels[0], Path.Combine(GameSession.TacticsDir(), _selected!.Opponent), "voorbeeld", null, new HistoryEntry("voorbeeld", new StyleProfile("mid", 0.5, 0.5, 0.5, 0)));
        GameSession.Challenge = run;
        foreach (var part in scores.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var g = part.Split('-');
            run.Prepare(); run.Record(new ChallengeMatchResult(int.Parse(g[0]), int.Parse(g[1]), 9 + int.Parse(g[0]), 8, 110, 90, 9, 1, 0));
        }
        return run;
    }

    void EditTactic()
    {
        if (_work is not null || _prep is not null || _tactic.ItemCount == 0) return;
        GameSession.EditPath = SelectedTacticPath();
        GameSession.ReturnScene = "res://Challenges.tscn";      // come back here, where a saved tactic is offered as the chosen one
        GameSession.LastSavedTactic = null;
        _lastTactic = SelectedTacticPath();
        GetTree().ChangeSceneToFile("res://Editor.tscn");
    }

    void Back()
    {
        GameSession.Challenge = null;
        GetTree().ChangeSceneToFile("res://Menu.tscn");
    }

    public override void _ExitTree()
    {
        if (_cts is not { } source) return;
        _cts = null;
        SimulationBatch.CancelAndDispose(source, (Task?)_work ?? _prep);
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        // Escape opens Settings (Hotkeys); the abort buttons keep the challenge flow. Only Enter is handled here.
        if (GameSession.Challenge is { } run && _prep is null)       // live run: Enter plays on
        {
            if (k.Keycode is Key.Enter or Key.KpEnter)
            {
                if (_confirmAbort) { _confirmAbort = false; ShowRun(run); }
                else ContinueRun(run);
            }
            return;
        }
    }

    public override void _Process(double delta)
    {
        _elapsed += delta; _frames++;
        var shot = GameSession.DebugEnvironment("ZEUS_SHOT");
        bool run = GameSession.DebugEnvironment("ZEUS_RUN").Length > 0 || GameSession.DebugEnvironment("ZEUS_LIVE").Length > 0;
        if (shot.Length > 0 && ((_frames >= 12 && (!run || _shown)) || _elapsed > 240))
        {
            GetViewport().GetTexture().GetImage().SavePng(shot + "_challenges.png"); GetTree().Quit(); return;
        }
        if (_prep is { IsCompleted: true } prep)
        {
            _prep = null;
            _ = prep.Exception; // Observe faults even when cancellation won the UI race.
            bool cancelled = _cts?.IsCancellationRequested == true || prep.IsCanceled;
            FinishWork();
            if (prep.IsCompletedSuccessfully && !cancelled) { PlayNext(prep.Result); return; }
            _status.Text = cancelled ? Loc.T("challenge.cancelled") : Loc.T("challenge.failed") + (prep.Exception is { } failure ? Loc.Error(failure.GetBaseException()) : Loc.T("common.error.invalid"));
            _status.AddThemeColorOverride("font_color", UiTheme.PaperColor(UiTheme.Bad));
            SetControlsEnabled(true);
            return;
        }
        if (_prep is not null)
        {
            double fraction = Volatile.Read(ref _permille) / 1000.0;
            _progress.Value = fraction * _progress.MaxValue;
            _status.Text = _cts?.IsCancellationRequested == true ? Loc.T("batch.cancelling") : Loc.T("batch.preparing", fraction * 100);
            return;
        }
        if (_work is null) return;
        double total = Volatile.Read(ref _permille) / 1000.0;
        bool preparing = Volatile.Read(ref _preparing) != 0;
        _progress.Value = preparing ? total * _progress.MaxValue : total;
        if (!_work.IsCompleted)
        {
            _status.Text = _cts?.IsCancellationRequested == true ? Loc.T("batch.cancelling")
                : preparing ? Loc.T("batch.preparing", total * 100) : Loc.T("challenge.progress", Math.Floor(total), _selected!.Matches);
            return;
        }
        _ = _work.Exception;
        if (_work.IsCompletedSuccessfully && _cts?.IsCancellationRequested != true) Show(_work.Result.Result, _work.Result.Matches, _work.Result.Plan, _work.Result.Played, _work.Result.Setup);
        else
        {
            _status.Text = _work.IsCanceled || _cts?.IsCancellationRequested == true ? Loc.T("challenge.cancelled") : Loc.T("challenge.failed") + (_work.Exception is { } failure ? Loc.Error(failure.GetBaseException()) : Loc.T("common.error.invalid"));
            _status.AddThemeColorOverride("font_color", UiTheme.PaperColor(UiTheme.Bad));
        }
        _work = null; FinishWork();
    }

    void Show(ChallengeResult result, List<ChallengeMatchResult> matches, OpponentPlan? plan, HistoryEntry played, Played setup, bool live = false)
    {
        _progress.Value = _progress.MaxValue;
        _status.Text = "";
        bool save = GameSession.DebugEnvironment("ZEUS_NOSAVE").Length == 0;        // visual checks must not touch the real progress
        if (save) PlayStyleHistory.Add(played);
        var banner = UiTheme.Label(result.Passed ? Loc.T("challenge.passed") : Loc.T("challenge.not_passed"), 48, result.Passed ? UiTheme.Good : UiTheme.Bad, display: true);
        _result.AddChild(banner);
        _result.AddChild(Wrapped(Loc.T("challenge.result_record", Loc.Name(_playerTactic), result.Wins, result.Draws, result.Losses), UiTheme.Muted));
        if (plan is not null)
        {
            _result.AddChild(UiTheme.Label(plan.Prepared ? Loc.T("challenge.prepared") : Loc.T("challenge.unprepared"), 22,
                plan.Prepared ? UiTheme.Accent : UiTheme.Muted, display: true));
            _result.AddChild(Wrapped(Loc.T(plan.Prepared ? "challenge.plan.prepared" : "challenge.plan.unprepared", Loc.Name(plan.OpponentName)), plan.Prepared ? UiTheme.Text : UiTheme.Muted, UiTheme.Small + 1));
        }
        foreach (var c in result.Criteria)
            _result.AddChild(Wrapped($"{(c.Passed ? "✓" : "✗")}  {Loc.Criterion(c.Criterion, c.Actual)}", c.Passed ? UiTheme.Good : UiTheme.Bad));
        _result.AddChild(Wrapped(Loc.T("challenge.scores") + string.Join("   ", matches.Select(m => $"{m.GoalsFor}-{m.GoalsAgainst}{(m.Abandoned ? Loc.T("challenge.abandoned") : "")}")), UiTheme.Muted, UiTheme.Small + 1));
        var spec = result.Spec;
        if (!live)
        {
            var watch = UiTheme.Secondary(Loc.T("challenge.watch")); _result.AddChild(watch);
            watch.Pressed += () =>
            {
                // Exactly what was judged: the tactic and level that were played and the opponent the planner really chose.
                GameSession.StashMenuChoices();
                GameSession.HomePath = setup.PlayerPath; GameSession.HomeLevel = setup.PlayerLevel;
                GameSession.AwayPath = setup.OpponentPath; GameSession.AwayLevel = setup.OpponentLevel;
                GameSession.Seed = 1;
                GameSession.InCompetition = false;
                GameSession.PlayerSide = Zeus.Sim.Side.Home;
                GetTree().ChangeSceneToFile("res://Main.tscn");
            };
        }
        if (result.Passed)
        {
            if (save) ChallengeProgress.MarkDone(spec.Id, _playerTactic);
            RefreshList();
        }
        _shown = true;
    }
}

/// <summary>Which challenges are done, stored locally in user://zeus_progress.json.</summary>
public static class ChallengeProgress
{
    public sealed record Entry(string Tactic, string Date);
    const string FilePath = "user://zeus_progress.json";

    public static Dictionary<string, Entry> Load() => LoadCore(out _);

    // safeToWrite is false when the file could not be read or set aside: then nothing may be written over it.
    static Dictionary<string, Entry> LoadCore(out bool safeToWrite)
    {
        var result = new Dictionary<string, Entry>();
        safeToWrite = true;
        var status = SafeFile.Read(FilePath, out var text);
        if (status == SafeFile.ReadStatus.Missing) return result;
        if (status == SafeFile.ReadStatus.Unreadable) { safeToWrite = false; return result; }
        try
        {
            var parsed = Json.ParseString(text);
            if (parsed.VariantType != Variant.Type.Dictionary) throw new FormatException("geen woordenboek");
            var done = parsed.AsGodotDictionary().TryGetValue("gehaald", out var d) && d.VariantType == Variant.Type.Dictionary ? d.AsGodotDictionary() : null;
            if (done is null) throw new FormatException("'gehaald' ontbreekt");
            foreach (var key in done.Keys)
            {
                try         // one broken entry is skipped; the rest stays
                {
                    var e = done[key].AsGodotDictionary();
                    result[key.AsString()] = new Entry(e.TryGetValue("tactiek", out var t) ? t.AsString() : "", e.TryGetValue("datum", out var dt) ? dt.AsString() : "");
                }
                catch (Exception) { /* skip this entry */ }
            }
        }
        catch (Exception)
        {
            result.Clear();
            safeToWrite = SafeFile.SetAside(FilePath);          // a damaged progress file never breaks the screen, and is kept as .bad
        }
        return result;
    }

    public static void MarkDone(string id, string tactic)
    {
        try
        {
            var all = LoadCore(out var safe);
            if (!safe) { GD.PushWarning("Voortgang niet opgeslagen: het bestand kon niet veilig worden gelezen."); return; }
            all[id] = new Entry(tactic, DateTime.Now.ToString("yyyy-MM-dd"));
            var done = new Godot.Collections.Dictionary();
            foreach (var (key, e) in all) done[key] = new Godot.Collections.Dictionary { ["tactiek"] = e.Tactic, ["datum"] = e.Date };
            if (!SafeFile.Write(FilePath, Json.Stringify(new Godot.Collections.Dictionary { ["gehaald"] = done }, "  "), out var error))
                GD.PushWarning("Voortgang opslaan mislukt: " + error);
        }
        catch (Exception e) { GD.PushWarning("Voortgang opslaan mislukt: " + e.Message); }
    }
}

/// <summary>The style of your recent attempts, kept locally in user://zeus_history.json (max 20). The smart opponent reads only this.</summary>
public static class PlayStyleHistory
{
    const string FilePath = "user://zeus_history.json";

    public static List<HistoryEntry> Load() => LoadCore(out _);

    static List<HistoryEntry> LoadCore(out bool safeToWrite)
    {
        var result = new List<HistoryEntry>();
        safeToWrite = true;
        var status = SafeFile.Read(FilePath, out var text);
        if (status == SafeFile.ReadStatus.Missing) return result;
        if (status == SafeFile.ReadStatus.Unreadable) { safeToWrite = false; return result; }
        try
        {
            var parsed = Json.ParseString(text);
            if (parsed.VariantType != Variant.Type.Dictionary) throw new FormatException("geen woordenboek");
            var dict = parsed.AsGodotDictionary();
            if (!dict.TryGetValue("geschiedenis", out var list) || list.VariantType != Variant.Type.Array) throw new FormatException("'geschiedenis' ontbreekt");
            foreach (var item in list.AsGodotArray())
            {
                try         // one broken entry is skipped; the rest stays
                {
                    var e = item.AsGodotDictionary();
                    result.Add(new HistoryEntry(e["tactiek"].AsString(),
                        new StyleProfile(e["blok"].AsString(), e["druk"].AsDouble(), e["tempo"].AsDouble(), e["lijn"].AsDouble(), e["dekkers"].AsInt32())));
                }
                catch (Exception) { /* skip this entry */ }
            }
        }
        catch (Exception)
        {
            result.Clear();
            safeToWrite = SafeFile.SetAside(FilePath);          // a damaged history file never breaks the screen, and is kept as .bad
        }
        return result;
    }

    public static void Add(HistoryEntry entry)
    {
        try
        {
            var all = LoadCore(out var safe);
            if (!safe) { GD.PushWarning("Geschiedenis niet opgeslagen: het bestand kon niet veilig worden gelezen."); return; }
            all.Add(entry);
            var list = new Godot.Collections.Array();
            foreach (var h in all.TakeLast(20))
                list.Add(new Godot.Collections.Dictionary { ["tactiek"] = h.TacticName, ["blok"] = h.Style.Block, ["druk"] = h.Style.Pressing,
                    ["tempo"] = h.Style.Tempo, ["lijn"] = h.Style.LineHeight, ["dekkers"] = h.Style.Markers });
            if (!SafeFile.Write(FilePath, Json.Stringify(new Godot.Collections.Dictionary { ["geschiedenis"] = list }, "  "), out var error))
                GD.PushWarning("Geschiedenis opslaan mislukt: " + error);
        }
        catch (Exception e) { GD.PushWarning("Geschiedenis opslaan mislukt: " + e.Message); }
    }
}
