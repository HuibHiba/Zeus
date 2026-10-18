// Competition screen (v0, VOORLOPIG): table, schedule with results, next match (live or simulated), tactic choice, end of season.
// The career lives in user://zeus_carriere.json (a damaged file never breaks the screen). Simulation logic is in Zeus.Sim.Competition.
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Zeus.Sim;

public partial class Season : Control
{
    const string DefaultFile = "user://zeus_carriere.json";

    SeasonState _state = new();
    string _note = "";                       // message about loading (e.g. damaged file ignored)
    VBoxContainer _table = null!, _actions = null!;
    VBoxContainer _familiarityPanel = null!;
    GridContainer _schedule = null!;
    ScrollContainer _scroll = null!;
    OptionButton _tactic = null!;
    Button _live = null!, _simNext = null!, _simRest = null!;
    ProgressBar _progress = null!;
    Label _status = null!;
    Task<SeasonState>? _work;
    CancellationTokenSource? _cts;
    int _permille;
    int _frames;
    double _elapsed;
    string _tacticsDir = "", _squadsDir = "";
    Control? _currentHeader;
    int _scrollFrames;
    bool _waitForLive;

    static string CareerFile()
    {
        var env = GameSession.DebugEnvironment("ZEUS_CAREER_FILE");       // visual checks must not touch the real career
        return env.Length > 0 ? env : DefaultFile;
    }

    public override void _Ready()
    {
        UiTheme.ApplyPaper(this);
        SetAnchorsPreset(LayoutPreset.FullRect);
        RenderingServer.SetDefaultClearColor(UiTheme.Bg);
        var bg = new UiPaper(); bg.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(bg);
        _tacticsDir = GameSession.TacticsDir(); _squadsDir = GameSession.SquadsDir();
        GameSession.InCompetition = false;
        _state = Load(out _note);

        var margin = new MarginContainer(); margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right" }) margin.AddThemeConstantOverride("margin_" + side, side == "right" ? 72 : 44);   // right: room for the Esc button of Hotkeys (16 px gap)
        foreach (var side in new[] { "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 12);
        AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 8); margin.AddChild(root);

        var head = new HBoxContainer(); root.AddChild(head);
        head.AddChild(UiTheme.Label(Loc.T("season.title"), 56, UiTheme.Accent, display: true));
        var provisional = UiTheme.Label(Loc.T("season.provisional"), UiTheme.Small, UiTheme.Muted);      // the longer explanation is in About and in this tooltip
        provisional.TooltipText = Loc.T("season.provisional_help"); provisional.MouseFilter = MouseFilterEnum.Pass; head.AddChild(provisional);
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        Welcome.Install(this, "season", head);
        var back = UiTheme.Secondary(Loc.T("season.back")); back.Pressed += Back; head.AddChild(back);

        var familiarityPanel = Panel();
        var familiarityStyle = (StyleBoxFlat)familiarityPanel.GetThemeStylebox("panel").Duplicate();
        familiarityStyle.ContentMarginTop = familiarityStyle.ContentMarginBottom = 6;
        familiarityPanel.AddThemeStyleboxOverride("panel", familiarityStyle);
        familiarityPanel.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        root.AddChild(familiarityPanel);
        _familiarityPanel = new VBoxContainer(); familiarityPanel.AddChild(_familiarityPanel);

        var body = new HBoxContainer(); body.AddThemeConstantOverride("separation", 24); body.SizeFlagsVertical = SizeFlags.ExpandFill; root.AddChild(body);

        // Left: the table (+ history below it).
        var left = Panel(); left.CustomMinimumSize = new Vector2(690, 0); left.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; body.AddChild(left);
        // The table scrolls when the history makes it taller than the panel, so the left side never pushes the screen past the window.
        var tableScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; left.AddChild(tableScroll);
        _table = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _table.AddThemeConstantOverride("separation", 4); tableScroll.AddChild(_table);

        // Right: actions on top, schedule below.
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; right.AddThemeConstantOverride("separation", 8); body.AddChild(right);
        var actionPanel = Panel();
        var actionStyle = (StyleBoxFlat)actionPanel.GetThemeStylebox("panel").Duplicate();
        actionStyle.ContentMarginTop = actionStyle.ContentMarginBottom = 5;
        actionPanel.AddThemeStyleboxOverride("panel", actionStyle); actionPanel.SizeFlagsVertical = SizeFlags.ShrinkBegin; right.AddChild(actionPanel);
        _actions = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _actions.AddThemeConstantOverride("separation", 5); actionPanel.AddChild(_actions);
        var schedulePanel = Panel();
        var scheduleStyle = (StyleBoxFlat)schedulePanel.GetThemeStylebox("panel").Duplicate();
        scheduleStyle.ContentMarginTop = scheduleStyle.ContentMarginBottom = 10;
        schedulePanel.AddThemeStyleboxOverride("panel", scheduleStyle); schedulePanel.SizeFlagsVertical = SizeFlags.ExpandFill; schedulePanel.ClipContents = true; right.AddChild(schedulePanel);
        var scheduleBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; scheduleBox.AddThemeConstantOverride("separation", 6); schedulePanel.AddChild(scheduleBox);
        scheduleBox.AddChild(UiTheme.Label(Loc.T("season.schedule"), 26, UiTheme.Accent, display: true));
        _scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 130), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; scheduleBox.AddChild(_scroll);
        _schedule = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill }; _schedule.AddThemeConstantOverride("h_separation", 14);
        _schedule.AddThemeConstantOverride("v_separation", 4); _scroll.AddChild(_schedule);

        Refresh();
        // Coming back from the tactic editor: offer the tactic he just saved as his chosen one.
        if (GameSession.LastSavedTactic is { } saved)
        {
            GameSession.LastSavedTactic = null;
            if (File.Exists(GameSession.TacticPath(saved)) && saved != _state.PlayerTactic)
            {
                _state.PlayerTactic = saved; Save(); Refresh();
                _status.Text = Loc.T("season.saved_tactic", Pretty(saved));
            }
        }
        // Coming back from a live match: store the final score in the state at once (as "pending") and only clear it after a
        // successful save of the processed round, so pressing Esc while the round is being processed cannot lose it.
        if (GameSession.LastResult is { } result)
        {
            GameSession.LastResult = null;
            if (GameSession.LastResultRound == _state.Round && !Competition.IsFinished(_state))
            {
                OnboardingProgress.Mark("season");
                _state.PendingDemand = GameSession.CompetitionDemand;
                _state.Pending = new PendingResult(_state.Round, result.Home, result.Away);
                Save();
            }
        }
        // The club sets a goal at the start of each season; a brand-new career first gets the intro card (club choice + goal),
        // except in the visual checks that play at once (ZEUS_LIVE, ZEUS_ROUNDS).
        if (_state.Goal is null && _state.Round == 0 && _state.Pending is null)
        {
            bool debugRun = GameSession.DebugEnvironment("ZEUS_LIVE").Length > 0 || GameSession.DebugEnvironment("ZEUS_ROUNDS").Length > 0;
            if (_state.Season == 1 && _state.History.Count == 0 && !debugRun) ShowIntro();
            else { SetGoal(); Save(); Refresh(); }
        }
        if (GetNodeOrNull("career_intro") is null) FocusAction();      // with the intro card open, its own start button has the focus
        if (_state.Pending is { } pending && pending.Round == _state.Round && !Competition.IsFinished(_state)) StartWork(1, null);      // also after a restart
        else if (GameSession.DebugEnvironment("ZEUS_LIVE").Length > 0 && !Competition.IsFinished(_state)) { _waitForLive = true; Callable.From(PlayLive).CallDeferred(); }   // visual check: go straight into the live match
        else if (int.TryParse(GameSession.DebugEnvironment("ZEUS_ROUNDS"), out var rounds) && rounds > 0 && !Competition.IsFinished(_state))
            StartWork(rounds >= Competition.Rounds ? int.MaxValue : rounds, null);        // visual check: play some rounds at once
    }

    SeasonGoal? ExpectedGoal()
    {
        try { return Competition.ExpectedGoal(_state, _tacticsDir, _squadsDir, GameSession.OwnTacticsDir()); }
        catch (Exception e) { GD.PushWarning("Seizoensdoel niet bepaald: " + e.Message); return null; }
    }

    void SetGoal() => _state.Goal = ExpectedGoal();

    string GoalText(SeasonGoal goal) => goal switch
    {
        SeasonGoal.Promotion when _state.Division == Competition.Divisions.Count - 1 => Loc.T("season.goal.top_two"),
        SeasonGoal.Promotion => Loc.T("season.goal.promotion"),
        SeasonGoal.TopHalf => Loc.T("season.goal.top_half"),
        _ => Loc.T("season.goal.survival"),
    };

    // The player's club is shown with his own name; the other clubs keep their (translated) built-in names.
    static string Show(string club) => club == Competition.PlayerClubName ? ClubIdentity.Name : Loc.Name(club);

    // Intro card of a new career in notebook style: the club (name, kit, crest; changeable here) and the goal the board sets.
    void ShowIntro()
    {
        var goal = ExpectedGoal();
        var overlay = new Control { Name = "career_intro" }; overlay.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(overlay);
        var shade = new ColorRect { Color = new Color(0, 0, 0, .55f) }; shade.SetAnchorsPreset(LayoutPreset.FullRect); overlay.AddChild(shade);
        var center = new CenterContainer(); center.SetAnchorsPreset(LayoutPreset.FullRect); overlay.AddChild(center);
        var card = Panel(); card.CustomMinimumSize = new Vector2(860, 0); card.SizeFlagsVertical = SizeFlags.ShrinkCenter; center.AddChild(card);
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 8); card.AddChild(box);
        box.AddChild(UiTheme.Label(Loc.T("season.intro_title"), 40, UiTheme.Accent, display: true));
        var club = new HBoxContainer(); club.AddThemeConstantOverride("separation", 16); box.AddChild(club);
        var crest = CrestView.For(84); club.AddChild(crest);
        var lines = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; club.AddChild(lines);
        var name = UiTheme.Label(ClubIdentity.Name, 34, UiTheme.Text, display: true); lines.AddChild(name);
        lines.AddChild(UiTheme.Label(Loc.T("season.intro_division", Loc.Name(Competition.DivisionName(_state.Division)), Loc.Name(Competition.Level(_state.Division))), UiTheme.Small + 1, UiTheme.Muted));
        if (goal is { } g)
        {
            lines.AddChild(UiTheme.Label(Loc.T("season.intro_goal", GoalText(g)), 26, UiTheme.Accent, display: true));
            lines.AddChild(Wrapped(Loc.T("season.intro_goal_help"), UiTheme.Muted, UiTheme.Small));
        }
        box.AddChild(new HSeparator());
        box.AddChild(UiTheme.Label(Loc.T("season.intro_club"), 22, UiTheme.Accent, display: true));
        var picker = new ClubPicker(); box.AddChild(picker);
        picker.Changed += () => { name.Text = ClubIdentity.Name; crest.QueueRedraw(); };
        var start = UiTheme.Primary(Loc.T("season.intro_start")); start.Name = "intro_start"; box.AddChild(start);
        start.Pressed += () => { _state.Goal = goal; Save(); overlay.QueueFree(); Refresh(); FocusAction(); };
        start.GrabFocus();
    }

    // Keyboard: the enabled primary action (play live, or start the next season) gets the focus when the screen opens.
    void FocusAction()
    {
        foreach (var c in _actions.GetChildren())
            if (c is Button { Disabled: false } b && b.ThemeTypeVariation == "PrimaryButton" && !b.IsQueuedForDeletion()) { b.GrabFocus(); return; }
    }

    static PanelContainer Panel()
    {
        var p = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        p.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = UiTheme.Paper, BorderColor = UiTheme.PaperShade, BorderWidthTop = 3,
            ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 16, ContentMarginBottom = 16,
        });
        return p;
    }

    static Label Wrapped(string text, Color color, int size = UiTheme.Body)
    {
        var l = UiTheme.Label(text, size, color);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart; l.SizeFlagsHorizontal = SizeFlags.ExpandFill; l.CustomMinimumSize = new Vector2(10, 0);
        return l;
    }

    // ---- persistence ---------------------------------------------------------------------------------------------

    // A file that could not be read (temporary failure) is not "damaged": nothing is written then, so the career is never overwritten.
    // A really damaged file is moved to zeus_carriere.bad before a new season starts.
    bool _saveBlocked;

    SeasonState Load(out string note)
    {
        note = "";
        var path = CareerFile();
        var status = SafeFile.Read(path, out var text);
        if (status == SafeFile.ReadStatus.Missing) return new SeasonState();
        if (status == SafeFile.ReadStatus.Unreadable)
        {
            _saveBlocked = true;
            note = Loc.T("season.unreadable");
            return new SeasonState();
        }
        try { return SeasonState.Parse(text); }
        catch (Exception e)
        {
            bool aside = SafeFile.SetAside(path);
            _saveBlocked = !aside;                       // could not be moved away: do not overwrite it either
            note = aside ? Loc.T("season.damaged_recovered") + Loc.Error(e) + ")"
                : Loc.T("season.damaged_blocked") + Loc.Error(e) + ")";
            return new SeasonState();
        }
    }

    void Save()
    {
        if (_saveBlocked) return;
        if (!SafeFile.Write(CareerFile(), _state.ToJson(), out var error)) {
            GD.PushWarning("Carrière opslaan mislukt: " + error);
            if (_status is not null) { _status.Text = Loc.T("settings.save_failed"); _status.AddThemeColorOverride("font_color", UiTheme.PaperColor(UiTheme.Bad)); }
        }
    }

    // ---- building the screen -------------------------------------------------------------------------------------

    string[] TacticFiles() => GameSession.Tactics().Select(t => t.Id).ToArray();

    static string Pretty(string file) => GameSession.TacticLabel(GameSession.TacticPath(file));

    void Refresh()
    {
        foreach (var c in _table.GetChildren()) c.QueueFree();
        foreach (var c in _actions.GetChildren()) c.QueueFree();
        foreach (var c in _schedule.GetChildren()) c.QueueFree();
        _currentHeader = null;
        BuildTable();
        BuildActions();
        BuildSchedule();
        BuildFamiliarity();
        _scrollFrames = 3;                                  // scroll to the next round once the grid has been laid out (see _Process)
    }

    void BuildFamiliarity()
    {
        foreach (var c in _familiarityPanel.GetChildren()) { _familiarityPanel.RemoveChild(c); c.QueueFree(); }
        try
        {
            var tactic = TacticLoader.LoadFile(GameSession.TacticPath(_state.PlayerTactic));
            var squad = SquadFactory.ForLevel(Competition.Level(_state.Division), tactic, _squadsDir)!;
            var state = Competition.EnsureFamiliarity(_state, squad);
            var demand = TacticDemand.From(tactic);
            double deficit = squad.Starters.Max(p => demand.Deficit(p.Attributes, state.Players[p.Id].Concepts));
            var heading = new HBoxContainer(); _familiarityPanel.AddChild(heading);
            heading.AddChild(UiTheme.Label(Loc.T("season.familiarity"), 24, UiTheme.Accent, display: true));
            // Fresh start: the squad has not trained yet, so the gap is expected; explain the bars instead of warning.
            bool fresh = _state.Round == 0 && _state.History.Count == 0;
            bool warn = deficit > .03 && !fresh;
            var hint = Wrapped(Loc.T(warn ? "season.familiarity_warning" : "season.familiarity_help"), warn ? UiTheme.Bad : UiTheme.Muted, 18);
            hint.SizeFlagsHorizontal = SizeFlags.ExpandFill; heading.AddChild(hint);
            var columns = new HBoxContainer(); columns.AddThemeConstantOverride("separation", 12); _familiarityPanel.AddChild(columns);
            foreach (var concept in TacticalConcepts.All)
            {
                var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; columns.AddChild(col);
                col.AddChild(UiTheme.Label(Loc.Name(concept), 16, UiTheme.Text));
                double current = squad.Starters.Average(p => state.Players[p.Id].Concepts.GetValueOrDefault(concept));
                col.AddChild(UiTheme.Label(Loc.T("season.concept_values", demand[concept] * demand.K * 100, current * 100), 14, UiTheme.Muted));
                col.AddChild(new ProgressBar { MaxValue = 1, Step = .001, Value = current, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 10) });
            }
            if (!Competition.IsFinished(_state))
            {
                var row = new HBoxContainer(); _familiarityPanel.AddChild(row);
                var conceptPick = new OptionButton { ThemeTypeVariation = "CompactButton", FitToLongestItem = false, CustomMinimumSize = new Vector2(200, 30) };
                foreach (var concept in TacticalConcepts.All) conceptPick.AddItem(Loc.Name(concept));
                // Default: the concept the chosen tactic needs most beyond what the starters already know.
                double Gap(string c) => demand[c] * demand.K - squad.Starters.Average(p => state.Players[p.Id].Concepts.GetValueOrDefault(c));
                conceptPick.Select(TacticalConcepts.All.Select((c, i) => (Gap: Gap(c), i)).MaxBy(x => x.Gap).i);
                row.AddChild(conceptPick);
                var intensity = new OptionButton { ThemeTypeVariation = "CompactButton", CustomMinimumSize = new Vector2(120, 30) };
                intensity.AddItem(Loc.T("squad.low")); intensity.AddItem(Loc.T("squad.medium")); intensity.AddItem(Loc.T("squad.high")); intensity.Select(1); row.AddChild(intensity);
                var train = UiTheme.Secondary(Loc.T("season.train")); train.ThemeTypeVariation = "CompactButton"; train.CustomMinimumSize = new Vector2(0, 30);
                train.Disabled = _work is not null || _state.Pending is not null || _state.TrainedThisRound;
                conceptPick.Disabled = intensity.Disabled = train.Disabled;
                train.Pressed += () => {
                    Competition.Train(_state, TacticalConcepts.All[conceptPick.Selected], new[] { .25, .5, 1.0 }[intensity.Selected]);
                    Save(); BuildFamiliarity();
                };
                row.AddChild(train);
                row.AddChild(UiTheme.Label(Loc.T(_state.TrainedThisRound ? "season.trained" : "season.training_available"), 17, UiTheme.Muted));
            }
        }
        catch (Exception e) { _familiarityPanel.AddChild(Wrapped(Loc.T("squad.build_failed") + Loc.Error(e), UiTheme.Bad, 18)); }
    }

    void BuildTable()
    {
        var division = Competition.Divisions[_state.Division];
        _table.AddChild(UiTheme.Label(Loc.Name(division.Name).ToUpperInvariant(), 34, UiTheme.Text, display: true));
        _table.AddChild(UiTheme.Label(Loc.T("season.summary", _state.Season, Loc.Name(division.Level), _state.Round, Competition.Rounds), UiTheme.Small + 1, UiTheme.Muted));
        var grid = new GridContainer { Columns = 10 }; grid.AddThemeConstantOverride("h_separation", 6); grid.AddThemeConstantOverride("v_separation", 3);
        _table.AddChild(grid);
        string[] heads = { "", Loc.T("season.club"), Loc.T("season.played"), Loc.T("season.won"), Loc.T("season.drawn"), Loc.T("season.lost"), Loc.T("season.goals_for"), Loc.T("season.goals_against"), Loc.T("season.goal_difference"), Loc.T("season.points") };
        for (int i = 0; i < heads.Length; i++) grid.AddChild(Cell(heads[i], UiTheme.Muted, i == 1 ? HorizontalAlignment.Left : HorizontalAlignment.Right, 17, i == 1 ? 210 : i == 0 ? 46 : i == 9 ? 52 : 40));
        var rows = Competition.Standings(_state);
        bool canPromote = _state.Division < Competition.Divisions.Count - 1, canRelegate = _state.Division > 0;
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var color = r.IsPlayer ? UiTheme.Accent : UiTheme.Text;
            string mark = i < Competition.Promoted && canPromote ? "▲" : i >= rows.Count - Competition.Relegated && canRelegate ? "▼" : "";
            var markColor = mark == "▲" ? UiTheme.Good : UiTheme.Bad;
            grid.AddChild(Cell($"{i + 1} {mark}", mark.Length > 0 ? markColor : UiTheme.Muted, HorizontalAlignment.Left, 20, 46));
            var name = Cell(Show(r.Name), color, HorizontalAlignment.Left, 22, r.IsPlayer ? 182 : 210, true);
            if (r.IsPlayer)
            {
                var own = new HBoxContainer(); own.AddThemeConstantOverride("separation", 4);
                own.AddChild(CrestView.For(24)); own.AddChild(name); grid.AddChild(own);
            }
            else grid.AddChild(name);
            int[] values = { r.Played, r.Won, r.Drawn, r.Lost, r.GoalsFor, r.GoalsAgainst, r.GoalDifference, r.Points };
            for (int k = 0; k < values.Length; k++)
                grid.AddChild(Cell(k == 6 && values[k] > 0 ? "+" + values[k] : values[k].ToString(), k == 7 ? color : (r.IsPlayer ? color : UiTheme.Muted), HorizontalAlignment.Right, k == 7 ? 24 : 20, k == 7 ? 52 : 40, k == 7));
        }
        _table.AddChild(Wrapped(Loc.T("season.table_help"), UiTheme.Muted, UiTheme.Small));
        if (_state.History.Count > 0)
        {
            _table.AddChild(new HSeparator());
            _table.AddChild(UiTheme.Label(Loc.T("season.history"), 22, UiTheme.Accent, display: true));
            foreach (var h in _state.History.TakeLast(4))
                _table.AddChild(Wrapped(Loc.T("season.previous", h.Season, Loc.Name(Competition.DivisionName(h.Division)), h.Position, Outcome(h.Outcome)), UiTheme.Muted, UiTheme.Small + 1));
        }
    }

    static string Outcome(SeasonOutcome o) => o switch { SeasonOutcome.Promoted => Loc.T("season.promoted"), SeasonOutcome.Relegated => Loc.T("season.relegated"), _ => Loc.T("season.stayed") };

    static Label Cell(string text, Color color, HorizontalAlignment align, int size, float minWidth = 0, bool display = false)
    {
        var l = UiTheme.Label(text, size, color, display);
        l.HorizontalAlignment = align;
        if (minWidth > 0) l.CustomMinimumSize = new Vector2(minWidth, 0);
        l.ClipText = true;
        return l;
    }

    void BuildActions()
    {
        if (_note.Length > 0) _actions.AddChild(Wrapped(_note, UiTheme.Bad, UiTheme.Small + 1));
        if (Competition.IsFinished(_state)) { BuildEndOfSeason(); return; }

        var live = Competition.NextLiveMatch(_state)!;
        var clubs = Competition.Clubs(_state.Division);
        _actions.AddChild(UiTheme.Label(Loc.T("season.round_of", _state.Round + 1, Competition.Rounds), 26, UiTheme.Accent, display: true));
        var opp = clubs[live.PlayerIsHome ? live.Fixture.Away : live.Fixture.Home];
        _actions.AddChild(UiTheme.Label($"{Show(clubs[live.Fixture.Home].Name)}  –  {Show(clubs[live.Fixture.Away].Name)}", 30, UiTheme.Text, display: true));
        _actions.AddChild(Wrapped(Loc.T("season.opponent", live.PlayerIsHome ? Loc.T("season.home") : Loc.T("season.away"), Loc.Name(opp.Name), Pretty(opp.TacticFile ?? ""), Loc.Name(live.Level)), UiTheme.Muted, UiTheme.Small + 1));
        if (_state.Goal is { } goal) _actions.AddChild(Wrapped(Loc.T("season.goal_line", GoalText(goal)), UiTheme.Text, UiTheme.Small + 1));

        var tacticRow = new HBoxContainer(); _actions.AddChild(tacticRow);
        tacticRow.AddChild(UiTheme.Label(Loc.T("season.your_tactic"), 18, UiTheme.Muted, display: true));
        _tactic = new OptionButton { ThemeTypeVariation = "CompactButton", FitToLongestItem = false, ClipText = true, CustomMinimumSize = new Vector2(0, 32), SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = HorizontalAlignment.Left };
        var files = TacticFiles();
        foreach (var f in files) { _tactic.AddItem(Pretty(f)); _tactic.SetItemMetadata(_tactic.ItemCount - 1, f); }
        int sel = Array.IndexOf(files, _state.PlayerTactic);
        if (sel < 0 && files.Length > 0) { sel = 0; _state.PlayerTactic = files[0]; Save(); }       // chosen file is gone: fall back to the first one
        if (sel >= 0) _tactic.Select(sel);
        _tactic.ItemSelected += i => { _state.PlayerTactic = (string)_tactic.GetItemMetadata((int)i); Save(); Refresh(); };
        tacticRow.AddChild(_tactic);
        var edit = UiTheme.Secondary(Loc.T("season.edit")); edit.ThemeTypeVariation = "CompactButton"; edit.CustomMinimumSize = new Vector2(0, 32); edit.Pressed += EditTactic; tacticRow.AddChild(edit);
        edit.Disabled = files.Length == 0;

        _live = UiTheme.Primary(Loc.T("season.play_live"));
        _live.CustomMinimumSize = new Vector2(0, 44); _live.AddThemeFontSizeOverride("font_size", 26);
        _actions.AddChild(_live);
        foreach (var styleName in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        {
            var style = (StyleBoxFlat)_live.GetThemeStylebox(styleName).Duplicate();
            style.ContentMarginTop = style.ContentMarginBottom = 4;
            _live.AddThemeStyleboxOverride(styleName, style);
        }
        _live.Pressed += PlayLive;
        var sims = new HBoxContainer(); _actions.AddChild(sims);
        _simNext = UiTheme.Secondary(Loc.T("season.simulate_next")); _simNext.ThemeTypeVariation = "CompactButton"; _simNext.CustomMinimumSize = new Vector2(0, 32); _simNext.SizeFlagsHorizontal = SizeFlags.ExpandFill; _simNext.Pressed += () => StartWork(1, null); sims.AddChild(_simNext);
        _simRest = UiTheme.Secondary(Loc.T("season.simulate_rest")); _simRest.ThemeTypeVariation = "CompactButton"; _simRest.CustomMinimumSize = new Vector2(0, 32); _simRest.SizeFlagsHorizontal = SizeFlags.ExpandFill; _simRest.Pressed += () => StartWork(int.MaxValue, null); sims.AddChild(_simRest);
        _progress = new ProgressBar { MinValue = 0, MaxValue = 1, CustomMinimumSize = new Vector2(0, 18), Visible = _work is not null, ShowPercentage = false };
        _actions.AddChild(_progress);
        _status = Wrapped("", UiTheme.Muted, UiTheme.Small + 1); _actions.AddChild(_status);
        SetBusy(_work is not null || files.Length == 0);
    }

    void BuildEndOfSeason()
    {
        var summary = Competition.Summarize(_state);
        var table = Competition.Standings(_state);
        _actions.AddChild(UiTheme.Label(Loc.T("season.finished", _state.Season), 34, UiTheme.Accent, display: true));
        _actions.AddChild(Wrapped(Loc.T("season.champion", Show(table[0].Name), Loc.Name(Competition.DivisionName(_state.Division)), summary.Position, table.First(r => r.IsPlayer).Points),
            UiTheme.Text));
        string next = Competition.DivisionName(Math.Clamp(_state.Division + (summary.Outcome == SeasonOutcome.Promoted ? 1 : summary.Outcome == SeasonOutcome.Relegated ? -1 : 0), 0, Competition.Divisions.Count - 1));
        var (text, color) = summary.Outcome switch
        {
            SeasonOutcome.Promoted => (Loc.T("season.promotion", Loc.Name(next)), UiTheme.Good),
            SeasonOutcome.Relegated => (Loc.T("season.relegation", Loc.Name(next)), UiTheme.Bad),
            _ when _state.Division == Competition.Divisions.Count - 1 && summary.Position <= Competition.Promoted => (Loc.T("season.stay_top", Loc.Name(next)), UiTheme.Good),
            _ => (Loc.T("season.stay", Loc.Name(next)), UiTheme.Muted),
        };
        _actions.AddChild(UiTheme.Label(text, 30, color, display: true));
        if (_state.Goal is { } goal)
        {
            bool met = Competition.GoalMet(goal, summary.Position);
            _actions.AddChild(UiTheme.Label(Loc.T(met ? "season.goal_met" : "season.goal_missed", GoalText(goal)), 24, met ? UiTheme.Good : UiTheme.Bad, display: true));
        }
        var start = UiTheme.Primary(Loc.T("season.start_next", _state.Season + 1)); start.Pressed += NextSeason; _actions.AddChild(start);
        _status = Wrapped("", UiTheme.Muted, UiTheme.Small + 1); _actions.AddChild(_status);
        _progress = new ProgressBar { Visible = false };           // not used at the end of the season, but keeps the handlers safe
        _actions.AddChild(_progress);
    }

    void BuildSchedule()
    {
        var clubs = Competition.Clubs(_state.Division);
        var fixtures = Competition.Schedule();
        foreach (var round in fixtures.GroupBy(f => f.Round))
        {
            bool current = round.Key == _state.Round && !Competition.IsFinished(_state);
            bool played = round.Key < _state.Round;
            var header = UiTheme.Label(Loc.T("season.round", round.Key + 1) + (current ? Loc.T("season.next") : ""), 20, current ? UiTheme.Accent : UiTheme.Muted, display: true);
            if (current) _currentHeader = header;
            _schedule.AddChild(header); _schedule.AddChild(new Control()); _schedule.AddChild(new Control());
            foreach (var f in round)
            {
                var result = played ? _state.Results.FirstOrDefault(r => r.Round == f.Round && r.Home == f.Home && r.Away == f.Away) : null;
                bool mine = f.Home == 0 || f.Away == 0;
                var color = mine ? UiTheme.Accent : (played ? UiTheme.Text : UiTheme.Muted);
                _schedule.AddChild(Cell(Show(clubs[f.Home].Name), color, HorizontalAlignment.Right, 19, 0, mine));
                _schedule.AddChild(Cell(result is null ? "–" : $"{result.HomeGoals} - {result.AwayGoals}", color, HorizontalAlignment.Center, 19, 62, true));
                _schedule.AddChild(Cell(Show(clubs[f.Away].Name), color, HorizontalAlignment.Left, 19, 0, mine));
            }
        }
        // Make the name columns share the width equally.
        foreach (var c in _schedule.GetChildren()) if (c is Label l && l.CustomMinimumSize.X == 0) l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    // ---- actions -------------------------------------------------------------------------------------------------

    void SetBusy(bool busy)
    {
        if (_live is null) return;
        _live.Disabled = busy; _simNext.Disabled = busy; _simRest.Disabled = busy; _tactic.Disabled = busy;
    }

    void PlayLive()
    {
        if (_work is not null || Competition.NextLiveMatch(_state) is not { } m) return;
        try
        {
            var home = GameSession.TacticPath(m.HomeTactic); var away = GameSession.TacticPath(m.AwayTactic);
            if (!File.Exists(home) || !File.Exists(away)) throw new FileNotFoundException("Tactiekbestand ontbreekt.");
            GameSession.StashMenuChoices();                  // the menu gets its own choices back when it opens
            GameSession.HomePath = home; GameSession.AwayPath = away;
            GameSession.Seed = m.Seed; GameSession.HomeLevel = m.Level; GameSession.AwayLevel = m.Level;
            GameSession.PlayerSide = m.PlayerIsHome ? Zeus.Sim.Side.Home : Zeus.Sim.Side.Away;      // home path = the fixture's home club, so away play means the pause menu steers Away
            GameSession.CompetitionFamiliarity = _state.Familiarity;
            GameSession.CompetitionDemand = TacticDemand.From(TacticLoader.LoadFile(GameSession.TacticPath(_state.PlayerTactic)));
            GameSession.CompetitionOpponent = Show(Competition.Clubs(_state.Division)[m.PlayerIsHome ? m.Fixture.Away : m.Fixture.Home].Name);
            GameSession.InCompetition = true; GameSession.LastResult = null; GameSession.LastResultRound = _state.Round;
            Save();
            GetTree().ChangeSceneToFile("res://Main.tscn");
        }
        catch (Exception e) { _status.Text = Loc.T("match.start_failed") + Loc.Error(e); _status.AddThemeColorOverride("font_color", UiTheme.PaperColor(UiTheme.Bad)); }
    }

    void EditTactic()
    {
        if (_work is not null || _tactic.ItemCount == 0) return;
        GameSession.EditPath = GameSession.TacticPath(_state.PlayerTactic);
        GameSession.ReturnScene = "res://Season.tscn";
        GameSession.LastSavedTactic = null;
        Save();
        GetTree().ChangeSceneToFile("res://Editor.tscn");
    }

    void NextSeason()
    {
        if (!Competition.IsFinished(_state)) return;
        Competition.StartNextSeason(_state);
        SetGoal();
        _note = "";
        Save();
        Refresh();
    }

    /// <summary>Plays one round (<paramref name="rounds"/> = 1, optionally with the live result) or everything that is left, on a background task.</summary>
    void StartWork(int rounds, (int Home, int Away)? playerResult)
    {
        if (_work is not null || Competition.IsFinished(_state)) return;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var copy = SeasonState.Parse(_state.ToJson());        // work on a copy: the screen keeps showing the old state until the task is done
        int target = rounds == int.MaxValue ? Competition.Rounds : Math.Min(Competition.Rounds, copy.Round + rounds);
        int start = copy.Round;
        _permille = 0;
        if (_progress is not null) { _progress.Visible = true; _progress.Value = 0; }
        SetBusy(true);
        if (_status is not null) { _status.AddThemeColorOverride("font_color", UiTheme.PaperColor(UiTheme.Muted)); _status.Text = Loc.T("season.playing"); }
        var tactics = _tacticsDir; var squads = _squadsDir; var ownTactics = GameSession.OwnTacticsDir();
        _work = Task.Run(() =>
        {
            void Report(double matchesDone) => Volatile.Write(ref _permille, (int)(Math.Clamp((copy.Round - start + matchesDone / Competition.MatchesPerRound) / Math.Max(1, target - start), 0, 1) * 1000));
            // Round by round so that "n rounds" stops exactly after n; each round runs its matches with at most 3 at a time.
            while (copy.Round < target)
            {
                token.ThrowIfCancellationRequested();
                Competition.PlayRound(copy, tactics, squads, copy.Round == start ? playerResult : null, token, Report, ownTacticsDirectory: ownTactics);
            }
            return copy;
        }, token);
        BuildFamiliarity();
    }

    void Back()
    {
        _cts?.Cancel();
        GetTree().ChangeSceneToFile("res://Menu.tscn");
    }

    public override void _Process(double delta)
    {
        _elapsed += delta; _frames++;
        if (_scrollFrames > 0 && --_scrollFrames == 0 && _currentHeader is { } header && IsInstanceValid(header))
            _scroll.ScrollVertical = Math.Max(0, (int)header.Position.Y - 4);
        var shot = GameSession.DebugEnvironment("ZEUS_SHOT");
        if (shot.Length > 0 && !_waitForLive && ((_frames >= 12 && _work is null) || _elapsed > 240))
        {
            GetViewport().GetTexture().GetImage().SavePng(shot + "_season.png"); GetTree().Quit(); return;
        }
        if (_work is null) return;
        double fraction = Volatile.Read(ref _permille) / 1000.0;
        if (!_work.IsCompleted)
        {
            if (_progress is not null) _progress.Value = fraction;
            if (_status is not null) _status.Text = Loc.T("season.progress", fraction * 100);
            return;
        }
        var finished = _work;
        _work = null;
        if (finished.IsCompletedSuccessfully)
        {
            _state = finished.Result; _note = "";
            OnboardingProgress.Mark("season");
            Save();
            Refresh();
            if (_status is not null) _status.Text = "";
        }
        else
        {
            var msg = finished.IsCanceled ? Loc.T("season.cancelled") : Loc.T("season.failed") + (finished.Exception is { } failure ? Loc.Error(failure.GetBaseException()) : Loc.T("common.error.invalid"));
            Refresh();
            if (_status is not null) { _status.Text = msg; _status.AddThemeColorOverride("font_color", UiTheme.PaperColor(UiTheme.Bad)); }
        }
    }
}
