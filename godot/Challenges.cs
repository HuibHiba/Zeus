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
    ScrollContainer _scroll = null!;
    OptionButton _tactic = null!, _level = null!;
    Button _start = null!;
    ProgressBar _progress = null!;
    Label _status = null!;
    Task<(ChallengeResult Result, List<ChallengeMatchResult> Matches, OpponentPlan? Plan, HistoryEntry Played, Played Setup)>? _work;
    CancellationTokenSource? _cts;
    int _permille;
    int _frames;
    double _elapsed;
    string _playerTactic = "";
    bool _shown;
    static string? _lastTactic, _lastLevel;      // what he chose here last (the menu's GameSession fields stay untouched)

    /// <summary>What was actually played: the player's tactic file and level, the opponent that was really chosen (adaptive challenges pick one) and its level.</summary>
    sealed record Played(string PlayerPath, string PlayerLevel, string OpponentPath, string OpponentLevel);

    public override void _Ready()
    {
        UiTheme.Apply(this);
        SetAnchorsPreset(LayoutPreset.FullRect);
        RenderingServer.SetDefaultClearColor(UiTheme.Bg);
        var bg = new ColorRect { Color = UiTheme.Bg }; bg.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(bg);

        var margin = new MarginContainer(); margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right" }) margin.AddThemeConstantOverride("margin_" + side, 50);
        foreach (var side in new[] { "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 36);
        AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 18); margin.AddChild(root);

        var head = new HBoxContainer(); root.AddChild(head);
        head.AddChild(UiTheme.Label("UITDAGINGEN", 58, UiTheme.Accent, display: true));
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var back = UiTheme.Secondary("Terug naar menu  (Esc)"); back.Pressed += Back; head.AddChild(back);

        var body = new HBoxContainer(); body.AddThemeConstantOverride("separation", 28); body.SizeFlagsVertical = SizeFlags.ExpandFill; root.AddChild(body);

        var listScroll = new ScrollContainer { CustomMinimumSize = new Vector2(430, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        body.AddChild(listScroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; list.AddThemeConstantOverride("separation", 8); listScroll.AddChild(list);

        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(UiTheme.Panel, 0.95f), BorderColor = new Color(UiTheme.Accent, 0.55f), BorderWidthTop = 3,
            ContentMarginLeft = 30, ContentMarginRight = 30, ContentMarginTop = 22, ContentMarginBottom = 22,
        });
        body.AddChild(panel);
        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; panel.AddChild(_scroll);
        _detail = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _detail.AddThemeConstantOverride("separation", 10); _scroll.AddChild(_detail);

        IReadOnlyList<string> loadErrors = Array.Empty<string>();
        try { _specs = ChallengeEvaluator.LoadAll(GameSession.ChallengesDir(), out loadErrors); }
        catch (Exception e) { _detail.AddChild(Wrapped("Uitdagingen konden niet worden geladen: " + e.Message, UiTheme.Bad)); return; }
        foreach (var problem in loadErrors) GD.PushWarning("Uitdaging overgeslagen: " + problem);
        if (_specs.Count == 0)
        {
            _detail.AddChild(Wrapped(loadErrors.Count > 0 ? "Geen enkele uitdaging kon worden geladen:\n" + string.Join("\n", loadErrors) : "Geen uitdagingen gevonden in challenges/.",
                loadErrors.Count > 0 ? UiTheme.Bad : UiTheme.Muted));
            return;
        }
        if (GameSession.LastSavedTactic is { } saved)       // back from the editor: the tactic he just saved is offered as his choice
        {
            GameSession.LastSavedTactic = null;
            if (File.Exists(Path.Combine(GameSession.TacticsDir(), saved))) _lastTactic = saved;
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
        var wanted = int.TryParse(OS.GetEnvironment("ZEUS_CHALLENGE"), out var n) ? Math.Clamp(n, 0, _specs.Count - 1) : 0;
        Select(_specs[wanted]);
        if (loadErrors.Count > 0)
            _status.Text = $"{loadErrors.Count} uitdaging(en) overgeslagen omdat het bestand niet klopt: " + string.Join("; ", loadErrors);
        if (OS.GetEnvironment("ZEUS_RUN").Length > 0) Run();
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
        if (_work is not null) return;                       // do not switch while a run is going
        _selected = spec;
        _buttons[spec.Id].SetPressedNoSignal(true);
        foreach (var c in _detail.GetChildren()) c.QueueFree();
        _shown = false;
        var cols = new HBoxContainer(); cols.AddThemeConstantOverride("separation", 36); _detail.AddChild(cols);
        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; left.AddThemeConstantOverride("separation", 10); cols.AddChild(left);
        string opp;
        try { opp = TacticLoader.LoadFile(Path.Combine(GameSession.TacticsDir(), spec.Opponent)).Name; } catch { opp = spec.Opponent; }

        left.AddChild(UiTheme.Label(spec.Name.ToUpperInvariant(), 46, UiTheme.Text, display: true));
        left.AddChild(Wrapped(spec.Description, UiTheme.Text));
        left.AddChild(Wrapped($"Tegenstander: {opp}   •   {spec.Matches} wedstrijden, jij speelt thuis", UiTheme.Muted, UiTheme.Small + 1));
        left.AddChild(UiTheme.Label("DOELEN", 26, UiTheme.Accent, display: true));
        var progressAll = ChallengeProgress.Load();
        foreach (var c in spec.Criteria)
            left.AddChild(Wrapped("•  " + Describe(c), UiTheme.Text));
        if (spec.Hint.Length > 0) left.AddChild(Wrapped("Tip: " + spec.Hint, UiTheme.Muted, UiTheme.Small + 1));
        if (spec.Adaptive)
            left.AddChild(Wrapped("Deze tegenstander leert van je: speel je steeds dezelfde stijl, dan bereidt hij zich voor en kiest hij een tactiek die daartegen werkt. Wissel je stijl om hem te verrassen.", UiTheme.Accent, UiTheme.Small + 1));
        if (progressAll.TryGetValue(spec.Id, out var done))
            left.AddChild(Wrapped($"✓ Gehaald met '{done.Tactic}' op {done.Date}", UiTheme.Good));

        left.AddChild(new HSeparator());
        left.AddChild(UiTheme.Label("JOUW TACTIEK", 26, UiTheme.Accent, display: true));
        _tactic = new OptionButton { CustomMinimumSize = new Vector2(0, 48), Alignment = HorizontalAlignment.Left };
        var files = Directory.Exists(GameSession.TacticsDir()) ? Directory.GetFiles(GameSession.TacticsDir(), "*.json").OrderBy(f => f).ToArray() : Array.Empty<string>();
        foreach (var f in files) { _tactic.AddItem(Path.GetFileNameWithoutExtension(f).Replace('_', ' ')); _tactic.SetItemMetadata(_tactic.ItemCount - 1, f); }
        var preferred = _lastTactic ?? (GameSession.HomePath is null ? null : Path.GetFileName(GameSession.HomePath));      // own memory first: GameSession is borrowed by Season
        for (int i = 0; i < files.Length; i++)
            if (preferred is not null && Path.GetFileName(files[i]) == preferred) _tactic.Select(i);
        left.AddChild(_tactic);
        var levelRow = new HBoxContainer(); left.AddChild(levelRow);
        levelRow.AddChild(UiTheme.Label("NIVEAU VAN JE TEAM", 18, UiTheme.Muted, display: true));
        _level = new OptionButton { CustomMinimumSize = new Vector2(0, 40), SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = HorizontalAlignment.Left };
        foreach (var l in SquadFactory.Levels) _level.AddItem(char.ToUpperInvariant(l[0]) + l[1..]);
        _level.Select(Math.Max(0, Array.IndexOf(SquadFactory.Levels.ToArray(), _lastLevel ?? GameSession.HomeLevel)));
        levelRow.AddChild(_level);
        if (spec.OpponentLevel != "neutraal")
            left.AddChild(Wrapped($"De tegenstander speelt op niveau {spec.OpponentLevel}.", UiTheme.Muted, UiTheme.Small + 1));

        var row = new HBoxContainer(); left.AddChild(row);
        _start = UiTheme.Primary("START UITDAGING  ▶"); _start.SizeFlagsHorizontal = SizeFlags.ExpandFill; _start.Pressed += Run; row.AddChild(_start);
        var edit = UiTheme.Secondary("Tactiek aanpassen"); edit.Pressed += EditTactic; row.AddChild(edit);
        _progress = new ProgressBar { MinValue = 0, MaxValue = spec.Matches, CustomMinimumSize = new Vector2(0, 22), Visible = false }; left.AddChild(_progress);
        _status = Wrapped("", UiTheme.Muted, UiTheme.Small + 1); left.AddChild(_status);
        _result = new VBoxContainer { CustomMinimumSize = new Vector2(430, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill }; _result.AddThemeConstantOverride("separation", 8); cols.AddChild(_result);
        _result.AddChild(Wrapped("Het resultaat verschijnt hier na de uitdaging.", UiTheme.Muted, UiTheme.Small + 1));
    }

    static string Describe(ChallengeCriterion c)
    {
        var (label, unit, _) = ChallengeEvaluator.Metrics[c.Metric];
        var value = c.Value % 1 == 0 ? c.Value.ToString("0") : c.Value.ToString("0.0");
        return $"{char.ToUpperInvariant(label[0])}{label[1..]} {(c.Operator == ">=" ? "minstens" : "hoogstens")} {value}{(unit.Length > 0 ? " " + unit : "")}";
    }

    string SelectedTacticPath() => (string)_tactic.GetItemMetadata(_tactic.Selected);

    void Run()
    {
        if (_selected is null || _work is not null || _tactic.ItemCount == 0) return;
        foreach (var c in _result.GetChildren()) c.QueueFree();
        var spec = _selected;
        var playerPath = SelectedTacticPath();
        _playerTactic = Path.GetFileNameWithoutExtension(playerPath).Replace('_', ' ');
        _lastTactic = Path.GetFileName(playerPath);
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _permille = 0;
        _start.Disabled = true; _progress.Visible = true; _progress.Value = 0;
        _status.AddThemeColorOverride("font_color", UiTheme.Muted);
        _status.Text = "Wedstrijden worden gespeeld...";
        var history = PlayStyleHistory.Load();
        var tacticsDir = GameSession.TacticsDir();
        var playerLevel = SquadFactory.Levels[_level.Selected];
        _lastLevel = playerLevel;
        var squadsDir = GameSession.SquadsDir();
        _work = Task.Run(() =>
        {
            var player = TacticLoader.LoadFile(playerPath);
            var opponentPath = Path.Combine(tacticsDir, spec.Opponent);
            var opponent = TacticLoader.LoadFile(opponentPath);
            OpponentPlan? plan = null;
            if (spec.Adaptive)        // the opponent only reacts to what it can know: the style of your last attempts
            {
                var pool = Directory.GetFiles(tacticsDir, "*.json").OrderBy(f => f, StringComparer.Ordinal)
                    .Select(f => (Name: Path.GetFileNameWithoutExtension(f), Tactic: TacticLoader.LoadFile(f))).ToList();
                plan = OpponentPlanner.Plan(player, pool, Path.GetFileNameWithoutExtension(spec.Opponent), history, token);
                if (plan.Prepared)
                {
                    var chosen = pool.First(c => c.Name == plan.OpponentName);
                    opponent = chosen.Tactic;
                    opponentPath = Path.Combine(tacticsDir, chosen.Name + ".json");
                }
            }
            var matches = ChallengeEvaluator.Play(spec, player, opponent, token, total => Volatile.Write(ref _permille, (int)(total * 1000)),
                playerSquad: SquadFactory.ForLevel(playerLevel, player, squadsDir), opponentSquad: SquadFactory.ForLevel(spec.OpponentLevel, opponent, squadsDir));
            return (ChallengeEvaluator.Evaluate(spec, matches), matches, plan, new HistoryEntry(player.Name, StyleProfile.From(player)),
                new Played(playerPath, playerLevel, opponentPath, spec.OpponentLevel));
        }, token);
    }

    void EditTactic()
    {
        if (_work is not null || _tactic.ItemCount == 0) return;
        GameSession.EditPath = SelectedTacticPath();
        GameSession.ReturnScene = "res://Challenges.tscn";      // come back here, where a saved tactic is offered as the chosen one
        GameSession.LastSavedTactic = null;
        _lastTactic = Path.GetFileName(SelectedTacticPath());
        GetTree().ChangeSceneToFile("res://Editor.tscn");
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
        var shot = OS.GetEnvironment("ZEUS_SHOT");
        bool run = OS.GetEnvironment("ZEUS_RUN").Length > 0;
        if (shot.Length > 0 && ((_frames >= 12 && (!run || _shown)) || _elapsed > 240))
        {
            GetViewport().GetTexture().GetImage().SavePng(shot + "_challenges.png"); GetTree().Quit(); return;
        }
        if (_work is null) return;
        double total = Volatile.Read(ref _permille) / 1000.0;
        _progress.Value = total;
        if (!_work.IsCompleted) { _status.Text = $"{Math.Floor(total):0} van {_selected!.Matches} wedstrijden gespeeld..."; return; }
        if (_work.IsCompletedSuccessfully) Show(_work.Result.Result, _work.Result.Matches, _work.Result.Plan, _work.Result.Played, _work.Result.Setup);
        else
        {
            _status.Text = _work.IsCanceled ? "Geannuleerd." : "Mislukt: " + (_work.Exception?.GetBaseException().Message ?? "onbekende fout");
            _status.AddThemeColorOverride("font_color", UiTheme.Bad);
        }
        _work = null; _start.Disabled = false;
    }

    void Show(ChallengeResult result, List<ChallengeMatchResult> matches, OpponentPlan? plan, HistoryEntry played, Played setup)
    {
        _progress.Value = _progress.MaxValue;
        _status.Text = "";
        PlayStyleHistory.Add(played);
        var banner = UiTheme.Label(result.Passed ? "GESLAAGD" : "NOG NIET GEHAALD", 48, result.Passed ? UiTheme.Good : UiTheme.Bad, display: true);
        _result.AddChild(banner);
        _result.AddChild(Wrapped($"Met '{_playerTactic}':  {result.Wins} gewonnen, {result.Draws} gelijk, {result.Losses} verloren", UiTheme.Muted));
        if (plan is not null)
        {
            _result.AddChild(UiTheme.Label(plan.Prepared ? "DE TEGENSTANDER HEEFT ZICH VOORBEREID" : "DE TEGENSTANDER KENT JE NOG NIET", 22,
                plan.Prepared ? UiTheme.Accent : UiTheme.Muted, display: true));
            _result.AddChild(Wrapped(plan.Reason, plan.Prepared ? UiTheme.Text : UiTheme.Muted, UiTheme.Small + 1));
        }
        foreach (var c in result.Criteria)
            _result.AddChild(Wrapped($"{(c.Passed ? "✓" : "✗")}  {c.Text}", c.Passed ? UiTheme.Good : UiTheme.Bad));
        _result.AddChild(Wrapped("Uitslagen: " + string.Join("   ", matches.Select(m => $"{m.GoalsFor}-{m.GoalsAgainst}{(m.Abandoned ? " (gestaakt)" : "")}")), UiTheme.Muted, UiTheme.Small + 1));
        var watch = UiTheme.Secondary("Bekijk wedstrijd 1 live"); _result.AddChild(watch);
        var spec = result.Spec;
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
        if (result.Passed)
        {
            ChallengeProgress.MarkDone(spec.Id, _playerTactic);
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
