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
    OptionButton _tactic = null!;
    Button _start = null!;
    ProgressBar _progress = null!;
    Label _status = null!;
    Task<(ChallengeResult Result, List<ChallengeMatchResult> Matches, OpponentPlan? Plan, HistoryEntry Played)>? _work;
    CancellationTokenSource? _cts;
    int _permille;
    int _frames;
    double _elapsed;
    string _playerTactic = "";
    bool _shown;

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

        try { _specs = ChallengeEvaluator.LoadAll(GameSession.ChallengesDir()); }
        catch (Exception e) { _detail.AddChild(Wrapped("Uitdagingen konden niet worden geladen: " + e.Message, UiTheme.Bad)); return; }
        if (_specs.Count == 0) { _detail.AddChild(Wrapped("Geen uitdagingen gevonden in challenges/.", UiTheme.Muted)); return; }

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
        for (int i = 0; i < files.Length; i++)
            if (GameSession.HomePath is not null && Path.GetFileName(files[i]) == Path.GetFileName(GameSession.HomePath)) _tactic.Select(i);
        left.AddChild(_tactic);

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
        GameSession.HomePath = playerPath;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _permille = 0;
        _start.Disabled = true; _progress.Visible = true; _progress.Value = 0;
        _status.AddThemeColorOverride("font_color", UiTheme.Muted);
        _status.Text = "Wedstrijden worden gespeeld...";
        var history = PlayStyleHistory.Load();
        var tacticsDir = GameSession.TacticsDir();
        _work = Task.Run(() =>
        {
            var player = TacticLoader.LoadFile(playerPath);
            var opponent = TacticLoader.LoadFile(Path.Combine(tacticsDir, spec.Opponent));
            OpponentPlan? plan = null;
            if (spec.Adaptive)        // the opponent only reacts to what it can know: the style of your last attempts
            {
                var pool = Directory.GetFiles(tacticsDir, "*.json").OrderBy(f => f, StringComparer.Ordinal)
                    .Select(f => (Name: Path.GetFileNameWithoutExtension(f), Tactic: TacticLoader.LoadFile(f))).ToList();
                plan = OpponentPlanner.Plan(player, pool, Path.GetFileNameWithoutExtension(spec.Opponent), history, token);
                if (plan.Prepared) opponent = pool.First(c => c.Name == plan.OpponentName).Tactic;
            }
            var matches = ChallengeEvaluator.Play(spec, player, opponent, token, total => Volatile.Write(ref _permille, (int)(total * 1000)));
            return (ChallengeEvaluator.Evaluate(spec, matches), matches, plan, new HistoryEntry(player.Name, StyleProfile.From(player)));
        }, token);
    }

    void EditTactic()
    {
        if (_work is not null || _tactic.ItemCount == 0) return;
        GameSession.EditPath = SelectedTacticPath();
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
        if (_work.IsCompletedSuccessfully) Show(_work.Result.Result, _work.Result.Matches, _work.Result.Plan, _work.Result.Played);
        else
        {
            _status.Text = _work.IsCanceled ? "Geannuleerd." : "Mislukt: " + (_work.Exception?.GetBaseException().Message ?? "onbekende fout");
            _status.AddThemeColorOverride("font_color", UiTheme.Bad);
        }
        _work = null; _start.Disabled = false;
    }

    void Show(ChallengeResult result, List<ChallengeMatchResult> matches, OpponentPlan? plan, HistoryEntry played)
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
        _result.AddChild(Wrapped("Uitslagen: " + string.Join("   ", matches.Select(m => $"{m.GoalsFor}-{m.GoalsAgainst}")), UiTheme.Muted, UiTheme.Small + 1));
        var watch = UiTheme.Secondary("Bekijk wedstrijd 1 live"); _result.AddChild(watch);
        var spec = result.Spec;
        watch.Pressed += () =>
        {
            GameSession.HomePath = SelectedTacticPath();
            GameSession.AwayPath = Path.Combine(GameSession.TacticsDir(), spec.Opponent);
            GameSession.Seed = 1;
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

    public static Dictionary<string, Entry> Load()
    {
        var result = new Dictionary<string, Entry>();
        try
        {
            if (!Godot.FileAccess.FileExists(FilePath)) return result;
            using var f = Godot.FileAccess.Open(FilePath, Godot.FileAccess.ModeFlags.Read);
            var parsed = Json.ParseString(f.GetAsText());
            if (parsed.VariantType != Variant.Type.Dictionary) return result;
            var done = parsed.AsGodotDictionary().TryGetValue("gehaald", out var d) && d.VariantType == Variant.Type.Dictionary ? d.AsGodotDictionary() : null;
            if (done is null) return result;
            foreach (var key in done.Keys)
            {
                var e = done[key].AsGodotDictionary();
                result[key.AsString()] = new Entry(e.TryGetValue("tactiek", out var t) ? t.AsString() : "", e.TryGetValue("datum", out var dt) ? dt.AsString() : "");
            }
        }
        catch (Exception) { /* a damaged progress file must never break the screen */ }
        return result;
    }

    public static void MarkDone(string id, string tactic)
    {
        try
        {
            var all = Load();
            all[id] = new Entry(tactic, DateTime.Now.ToString("yyyy-MM-dd"));
            var done = new Godot.Collections.Dictionary();
            foreach (var (key, e) in all) done[key] = new Godot.Collections.Dictionary { ["tactiek"] = e.Tactic, ["datum"] = e.Date };
            using var f = Godot.FileAccess.Open(FilePath, Godot.FileAccess.ModeFlags.Write);
            f.StoreString(Json.Stringify(new Godot.Collections.Dictionary { ["gehaald"] = done }, "  "));
        }
        catch (Exception e) { GD.PushWarning("Voortgang opslaan mislukt: " + e.Message); }
    }
}

/// <summary>The style of your recent attempts, kept locally in user://zeus_history.json (max 20). The smart opponent reads only this.</summary>
public static class PlayStyleHistory
{
    const string FilePath = "user://zeus_history.json";

    public static List<HistoryEntry> Load()
    {
        var result = new List<HistoryEntry>();
        try
        {
            if (!Godot.FileAccess.FileExists(FilePath)) return result;
            using var f = Godot.FileAccess.Open(FilePath, Godot.FileAccess.ModeFlags.Read);
            var parsed = Json.ParseString(f.GetAsText());
            if (parsed.VariantType != Variant.Type.Dictionary) return result;
            var dict = parsed.AsGodotDictionary();
            if (!dict.TryGetValue("geschiedenis", out var list) || list.VariantType != Variant.Type.Array) return result;
            foreach (var item in list.AsGodotArray())
            {
                var e = item.AsGodotDictionary();
                result.Add(new HistoryEntry(e["tactiek"].AsString(),
                    new StyleProfile(e["blok"].AsString(), e["druk"].AsDouble(), e["tempo"].AsDouble(), e["lijn"].AsDouble(), e["dekkers"].AsInt32())));
            }
        }
        catch (Exception) { result.Clear(); /* a damaged history file must never break the screen */ }
        return result;
    }

    public static void Add(HistoryEntry entry)
    {
        try
        {
            var all = Load();
            all.Add(entry);
            var list = new Godot.Collections.Array();
            foreach (var h in all.TakeLast(20))
                list.Add(new Godot.Collections.Dictionary { ["tactiek"] = h.TacticName, ["blok"] = h.Style.Block, ["druk"] = h.Style.Pressing,
                    ["tempo"] = h.Style.Tempo, ["lijn"] = h.Style.LineHeight, ["dekkers"] = h.Style.Markers });
            using var f = Godot.FileAccess.Open(FilePath, Godot.FileAccess.ModeFlags.Write);
            f.StoreString(Json.Stringify(new Godot.Collections.Dictionary { ["geschiedenis"] = list }, "  "));
        }
        catch (Exception e) { GD.PushWarning("Geschiedenis opslaan mislukt: " + e.Message); }
    }
}

