// Competition screen (v0, VOORLOPIG): table, schedule with results, next match (live or simulated), tactic choice, end of season.
// The career lives in user://zeus_carriere.json (a damaged file never breaks the screen). Simulation logic is in Zeus.Sim.Competition.
using System;
using System.IO;
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
        var env = OS.GetEnvironment("ZEUS_CAREER_FILE");       // visual checks must not touch the real career
        return env.Length > 0 ? env : DefaultFile;
    }

    public override void _Ready()
    {
        UiTheme.Apply(this);
        SetAnchorsPreset(LayoutPreset.FullRect);
        RenderingServer.SetDefaultClearColor(UiTheme.Bg);
        var bg = new ColorRect { Color = UiTheme.Bg }; bg.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(bg);
        _tacticsDir = GameSession.TacticsDir(); _squadsDir = GameSession.SquadsDir();
        GameSession.InCompetition = false;
        _state = Load(out _note);

        var margin = new MarginContainer(); margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right" }) margin.AddThemeConstantOverride("margin_" + side, 44);
        foreach (var side in new[] { "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 28);
        AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 14); margin.AddChild(root);

        var head = new HBoxContainer(); root.AddChild(head);
        head.AddChild(UiTheme.Label("COMPETITIE", 56, UiTheme.Accent, display: true));
        head.AddChild(UiTheme.Label("  voorlopig (v0)", UiTheme.Small, UiTheme.Muted));
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var back = UiTheme.Secondary("Terug naar menu  (Esc)"); back.Pressed += Back; head.AddChild(back);

        var body = new HBoxContainer(); body.AddThemeConstantOverride("separation", 24); body.SizeFlagsVertical = SizeFlags.ExpandFill; root.AddChild(body);

        // Left: the table (+ history below it).
        var left = Panel(); left.CustomMinimumSize = new Vector2(690, 0); left.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; body.AddChild(left);
        _table = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _table.AddThemeConstantOverride("separation", 6); left.AddChild(_table);

        // Right: actions on top, schedule below.
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; right.AddThemeConstantOverride("separation", 14); body.AddChild(right);
        var actionPanel = Panel(); right.AddChild(actionPanel);
        _actions = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _actions.AddThemeConstantOverride("separation", 9); actionPanel.AddChild(_actions);
        var schedulePanel = Panel(); schedulePanel.SizeFlagsVertical = SizeFlags.ExpandFill; right.AddChild(schedulePanel);
        var scheduleBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; scheduleBox.AddThemeConstantOverride("separation", 6); schedulePanel.AddChild(scheduleBox);
        scheduleBox.AddChild(UiTheme.Label("SPEELSCHEMA", 26, UiTheme.Accent, display: true));
        _scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; scheduleBox.AddChild(_scroll);
        _schedule = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill }; _schedule.AddThemeConstantOverride("h_separation", 14);
        _schedule.AddThemeConstantOverride("v_separation", 4); _scroll.AddChild(_schedule);

        Refresh();
        // Coming back from a live match: process the final score once (only when it belongs to the round that is up next).
        if (GameSession.LastResult is { } result)
        {
            GameSession.LastResult = null;
            if (GameSession.LastResultRound == _state.Round && !Competition.IsFinished(_state)) StartWork(1, result);
        }
        else if (OS.GetEnvironment("ZEUS_LIVE").Length > 0 && !Competition.IsFinished(_state)) { _waitForLive = true; Callable.From(PlayLive).CallDeferred(); }   // visual check: go straight into the live match
        else if (int.TryParse(OS.GetEnvironment("ZEUS_ROUNDS"), out var rounds) && rounds > 0 && !Competition.IsFinished(_state))
            StartWork(rounds >= Competition.Rounds ? int.MaxValue : rounds, null);        // visual check: play some rounds at once
    }

    static PanelContainer Panel()
    {
        var p = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        p.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(UiTheme.Panel, 0.95f), BorderColor = new Color(UiTheme.Accent, 0.55f), BorderWidthTop = 3,
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

    static SeasonState Load(out string note)
    {
        note = "";
        try
        {
            if (!Godot.FileAccess.FileExists(CareerFile())) return new SeasonState();
            using var f = Godot.FileAccess.Open(CareerFile(), Godot.FileAccess.ModeFlags.Read);
            return SeasonState.Parse(f.GetAsText());
        }
        catch (Exception e)
        {
            note = "Het opgeslagen seizoen was beschadigd en is genegeerd; er begint een nieuw seizoen. (" + e.Message + ")";
            return new SeasonState();
        }
    }

    void Save()
    {
        try
        {
            using var f = Godot.FileAccess.Open(CareerFile(), Godot.FileAccess.ModeFlags.Write);
            f.StoreString(_state.ToJson());
        }
        catch (Exception e) { GD.PushWarning("Carrière opslaan mislukt: " + e.Message); }
    }

    // ---- building the screen -------------------------------------------------------------------------------------

    string[] TacticFiles() => Directory.Exists(_tacticsDir)
        ? Directory.GetFiles(_tacticsDir, "*.json").Select(f => Path.GetFileName(f)!).OrderBy(f => f, StringComparer.Ordinal).ToArray()
        : Array.Empty<string>();

    static string Pretty(string file) => Path.GetFileNameWithoutExtension(file).Replace('_', ' ');

    void Refresh()
    {
        foreach (var c in _table.GetChildren()) c.QueueFree();
        foreach (var c in _actions.GetChildren()) c.QueueFree();
        foreach (var c in _schedule.GetChildren()) c.QueueFree();
        _currentHeader = null;
        BuildTable();
        BuildActions();
        BuildSchedule();
        _scrollFrames = 3;                                  // scroll to the next round once the grid has been laid out (see _Process)
    }

    void BuildTable()
    {
        var division = Competition.Divisions[_state.Division];
        _table.AddChild(UiTheme.Label(division.Name.ToUpperInvariant(), 40, UiTheme.Text, display: true));
        _table.AddChild(UiTheme.Label($"Seizoen {_state.Season}   •   niveau {division.Level}   •   {_state.Round} van {Competition.Rounds} speelrondes gespeeld", UiTheme.Small + 1, UiTheme.Muted));
        var grid = new GridContainer { Columns = 10 }; grid.AddThemeConstantOverride("h_separation", 6); grid.AddThemeConstantOverride("v_separation", 6);
        _table.AddChild(grid);
        string[] heads = { "", "CLUB", "GS", "W", "G", "V", "DV", "DT", "DS", "PTN" };
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
            grid.AddChild(Cell(r.Name, color, HorizontalAlignment.Left, 22, 210, true));
            int[] values = { r.Played, r.Won, r.Drawn, r.Lost, r.GoalsFor, r.GoalsAgainst, r.GoalDifference, r.Points };
            for (int k = 0; k < values.Length; k++)
                grid.AddChild(Cell(k == 6 && values[k] > 0 ? "+" + values[k] : values[k].ToString(), k == 7 ? color : (r.IsPlayer ? color : UiTheme.Muted), HorizontalAlignment.Right, k == 7 ? 24 : 20, k == 7 ? 52 : 40, k == 7));
        }
        _table.AddChild(Wrapped("▲ promotie (top 2)   ▼ degradatie (laatste)   •   3 punten per winst, 1 per gelijkspel", UiTheme.Muted, UiTheme.Small));
        if (_state.History.Count > 0)
        {
            _table.AddChild(new HSeparator());
            _table.AddChild(UiTheme.Label("EERDERE SEIZOENEN", 22, UiTheme.Accent, display: true));
            foreach (var h in _state.History.TakeLast(4))
                _table.AddChild(Wrapped($"Seizoen {h.Season}: {Competition.DivisionName(h.Division)}, plaats {h.Position}  ({Outcome(h.Outcome)})", UiTheme.Muted, UiTheme.Small + 1));
        }
        _table.AddChild(Wrapped("Voorlopige opzet (v0): clubs, niveaus en regels voor promotie en degradatie kunnen nog veranderen. Alle clubs zijn verzonnen.", UiTheme.Muted, UiTheme.Small));
    }

    static string Outcome(SeasonOutcome o) => o switch { SeasonOutcome.Promoted => "gepromoveerd", SeasonOutcome.Relegated => "gedegradeerd", _ => "gebleven" };

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
        _actions.AddChild(UiTheme.Label($"RONDE {_state.Round + 1} VAN {Competition.Rounds}", 26, UiTheme.Accent, display: true));
        var opp = clubs[live.PlayerIsHome ? live.Fixture.Away : live.Fixture.Home];
        _actions.AddChild(UiTheme.Label($"{clubs[live.Fixture.Home].Name}  –  {clubs[live.Fixture.Away].Name}", 30, UiTheme.Text, display: true));
        _actions.AddChild(Wrapped($"Jij speelt {(live.PlayerIsHome ? "thuis" : "uit")} tegen {opp.Name} (tactiek: {Pretty(opp.TacticFile ?? "")}, niveau {live.Level}).", UiTheme.Muted, UiTheme.Small + 1));

        var tacticRow = new HBoxContainer(); _actions.AddChild(tacticRow);
        tacticRow.AddChild(UiTheme.Label("JOUW TACTIEK", 18, UiTheme.Muted, display: true));
        _tactic = new OptionButton { CustomMinimumSize = new Vector2(0, 44), SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = HorizontalAlignment.Left };
        var files = TacticFiles();
        foreach (var f in files) { _tactic.AddItem(Pretty(f)); _tactic.SetItemMetadata(_tactic.ItemCount - 1, f); }
        int sel = Array.IndexOf(files, _state.PlayerTactic);
        if (sel < 0 && files.Length > 0) { sel = 0; _state.PlayerTactic = files[0]; Save(); }       // chosen file is gone: fall back to the first one
        if (sel >= 0) _tactic.Select(sel);
        _tactic.ItemSelected += i => { _state.PlayerTactic = (string)_tactic.GetItemMetadata((int)i); Save(); Refresh(); };
        tacticRow.AddChild(_tactic);
        var edit = UiTheme.Secondary("Tactiek aanpassen"); edit.Pressed += EditTactic; tacticRow.AddChild(edit);
        edit.Disabled = files.Length == 0;

        _live = UiTheme.Primary("SPEEL VOLGENDE WEDSTRIJD (LIVE)  ▶"); _live.Pressed += PlayLive; _actions.AddChild(_live);
        var sims = new HBoxContainer(); _actions.AddChild(sims);
        _simNext = UiTheme.Secondary("Simuleer volgende wedstrijd"); _simNext.SizeFlagsHorizontal = SizeFlags.ExpandFill; _simNext.Pressed += () => StartWork(1, null); sims.AddChild(_simNext);
        _simRest = UiTheme.Secondary("Simuleer de rest van het seizoen"); _simRest.SizeFlagsHorizontal = SizeFlags.ExpandFill; _simRest.Pressed += () => StartWork(int.MaxValue, null); sims.AddChild(_simRest);
        _progress = new ProgressBar { MinValue = 0, MaxValue = 1, CustomMinimumSize = new Vector2(0, 18), Visible = _work is not null, ShowPercentage = false };
        _actions.AddChild(_progress);
        _status = Wrapped("", UiTheme.Muted, UiTheme.Small + 1); _actions.AddChild(_status);
        SetBusy(_work is not null || files.Length == 0);
    }

    void BuildEndOfSeason()
    {
        var summary = Competition.Summarize(_state);
        var table = Competition.Standings(_state);
        _actions.AddChild(UiTheme.Label($"SEIZOEN {_state.Season} AFGELOPEN", 34, UiTheme.Accent, display: true));
        _actions.AddChild(Wrapped($"{table[0].Name} is kampioen van de {Competition.DivisionName(_state.Division)}. Jij eindigde op plaats {summary.Position} met {table.First(r => r.IsPlayer).Points} punten.",
            UiTheme.Text));
        string next = Competition.DivisionName(Math.Clamp(_state.Division + (summary.Outcome == SeasonOutcome.Promoted ? 1 : summary.Outcome == SeasonOutcome.Relegated ? -1 : 0), 0, Competition.Divisions.Count - 1));
        var (text, color) = summary.Outcome switch
        {
            SeasonOutcome.Promoted => ($"PROMOTIE naar de {next}", UiTheme.Good),
            SeasonOutcome.Relegated => ($"DEGRADATIE naar de {next}", UiTheme.Bad),
            _ when _state.Division == Competition.Divisions.Count - 1 && summary.Position <= Competition.Promoted => ($"Je blijft in de {next}: hoger kan niet", UiTheme.Good),
            _ => ($"Je blijft in de {next}", UiTheme.Muted),
        };
        _actions.AddChild(UiTheme.Label(text, 30, color, display: true));
        var start = UiTheme.Primary($"START SEIZOEN {_state.Season + 1}  ▶"); start.Pressed += NextSeason; _actions.AddChild(start);
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
            var header = UiTheme.Label($"RONDE {round.Key + 1}" + (current ? "  •  VOLGENDE" : ""), 20, current ? UiTheme.Accent : UiTheme.Muted, display: true);
            if (current) _currentHeader = header;
            _schedule.AddChild(header); _schedule.AddChild(new Control()); _schedule.AddChild(new Control());
            foreach (var f in round)
            {
                var result = played ? _state.Results.FirstOrDefault(r => r.Round == f.Round && r.Home == f.Home && r.Away == f.Away) : null;
                bool mine = f.Home == 0 || f.Away == 0;
                var color = mine ? UiTheme.Accent : (played ? UiTheme.Text : UiTheme.Muted);
                _schedule.AddChild(Cell(clubs[f.Home].Name, color, HorizontalAlignment.Right, 19, 0, mine));
                _schedule.AddChild(Cell(result is null ? "–" : $"{result.HomeGoals} - {result.AwayGoals}", color, HorizontalAlignment.Center, 19, 62, true));
                _schedule.AddChild(Cell(clubs[f.Away].Name, color, HorizontalAlignment.Left, 19, 0, mine));
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
            var home = Path.Combine(_tacticsDir, m.HomeTactic); var away = Path.Combine(_tacticsDir, m.AwayTactic);
            if (!File.Exists(home) || !File.Exists(away)) throw new FileNotFoundException("Tactiekbestand ontbreekt.");
            GameSession.HomePath = home; GameSession.AwayPath = away;
            GameSession.Seed = m.Seed; GameSession.HomeLevel = m.Level; GameSession.AwayLevel = m.Level;
            GameSession.InCompetition = true; GameSession.LastResult = null; GameSession.LastResultRound = _state.Round;
            Save();
            GetTree().ChangeSceneToFile("res://Main.tscn");
        }
        catch (Exception e) { _status.Text = "Wedstrijd kon niet starten: " + e.Message; _status.AddThemeColorOverride("font_color", UiTheme.Bad); }
    }

    void EditTactic()
    {
        if (_work is not null || _tactic.ItemCount == 0) return;
        GameSession.EditPath = Path.Combine(_tacticsDir, _state.PlayerTactic);
        GameSession.ReturnScene = "res://Season.tscn";
        Save();
        GetTree().ChangeSceneToFile("res://Editor.tscn");
    }

    void NextSeason()
    {
        if (!Competition.IsFinished(_state)) return;
        Competition.StartNextSeason(_state);
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
        if (_status is not null) { _status.AddThemeColorOverride("font_color", UiTheme.Muted); _status.Text = "Wedstrijden worden gespeeld..."; }
        var tactics = _tacticsDir; var squads = _squadsDir;
        _work = Task.Run(() =>
        {
            void Report(double matchesDone) => Volatile.Write(ref _permille, (int)(Math.Clamp((copy.Round - start + matchesDone / Competition.MatchesPerRound) / Math.Max(1, target - start), 0, 1) * 1000));
            // Round by round so that "n rounds" stops exactly after n; each round runs its matches with at most 3 at a time.
            while (copy.Round < target)
            {
                token.ThrowIfCancellationRequested();
                Competition.PlayRound(copy, tactics, squads, copy.Round == start ? playerResult : null, token, Report);
            }
            return copy;
        }, token);
    }

    void Back()
    {
        _cts?.Cancel();
        GetTree().ChangeSceneToFile("res://Menu.tscn");
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) Back();
    }

    public override void _Process(double delta)
    {
        _elapsed += delta; _frames++;
        if (_scrollFrames > 0 && --_scrollFrames == 0 && _currentHeader is { } header && IsInstanceValid(header))
            _scroll.ScrollVertical = Math.Max(0, (int)header.Position.Y - 4);
        var shot = OS.GetEnvironment("ZEUS_SHOT");
        if (shot.Length > 0 && !_waitForLive && ((_frames >= 12 && _work is null) || _elapsed > 240))
        {
            GetViewport().GetTexture().GetImage().SavePng(shot + "_season.png"); GetTree().Quit(); return;
        }
        if (_work is null) return;
        double fraction = Volatile.Read(ref _permille) / 1000.0;
        if (!_work.IsCompleted)
        {
            if (_progress is not null) _progress.Value = fraction;
            if (_status is not null) _status.Text = $"Wedstrijden worden gespeeld... {fraction * 100:0}%";
            return;
        }
        var finished = _work;
        _work = null;
        if (finished.IsCompletedSuccessfully)
        {
            _state = finished.Result; _note = "";
            Save();
            Refresh();
            if (_status is not null) _status.Text = "";
        }
        else
        {
            var msg = finished.IsCanceled ? "Geannuleerd." : "Mislukt: " + (finished.Exception?.GetBaseException().Message ?? "onbekende fout");
            Refresh();
            if (_status is not null) { _status.Text = msg; _status.AddThemeColorOverride("font_color", UiTheme.Bad); }
        }
    }
}
