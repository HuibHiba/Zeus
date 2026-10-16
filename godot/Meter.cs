using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Zeus.Sim;
using Side = Zeus.Sim.Side;

// Runs simulations without scene objects; only the main thread reads/writes the UI.
public partial class Meter : Control
{
    const int SeedCount = 10;
    const int MatchCount = SeedCount * 2;
    const int StepLimit = 150000;
    readonly CancellationTokenSource _cancellation = new();
    record Report(string A, string B, int Wins, int Draws, int Losses, string[] Labels, double[] AvgA, double[] AvgB, double Possession, string[] Differences, Heatmap HeatA, Heatmap HeatB, int[] DiffIndices);
    Task<Report>? _work;
    OptionButton _pickA = null!, _pickB = null!;
    Button _compare = null!;
    Label _status = null!;
    ProgressBar _progress = null!;
    VBoxContainer _report = null!;
    ScrollContainer _scroll = null!;
    int _frames, _readyFrames;
    double _elapsed;
    bool _finished;
    int _completed;
    int _matchPermille;

    public override void _Ready()
    {
        UiTheme.Apply(this);
        var bg = new ColorRect { Color = UiTheme.Bg }; bg.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(bg);
        var margin = new MarginContainer(); AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var edge in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 32);
        var columns = new HBoxContainer(); margin.AddChild(columns);
        var side = new PanelContainer { CustomMinimumSize = new Vector2(350, 0) }; columns.AddChild(side);
        var box = new VBoxContainer(); side.AddChild(box);
        box.AddChild(UiTheme.Label("Tactiek-meter", UiTheme.Heading + 10, null, true));
        box.AddChild(Wrapped("10 seeds, elk met A thuis en A uit.\n20 wedstrijden in totaal.", UiTheme.Muted));
        var entries = GameSession.Tactics();
        OptionButton Pick(string title, string? preferred, int fallback)
        {
            box.AddChild(UiTheme.Label(title, UiTheme.Small, UiTheme.Muted));
            var pick = new OptionButton { ClipText = true };
            foreach (var entry in entries) { pick.AddItem(entry.Label); pick.SetItemMetadata(pick.ItemCount - 1, entry.Path); }
            int selected = Array.FindIndex(entries, t => t.Path == preferred);
            if (entries.Length > 0) pick.Select(selected >= 0 ? selected : Math.Min(fallback, entries.Length - 1));
            box.AddChild(pick);
            return pick;
        }
        _pickA = Pick("A", GameSession.HomePath, 0); _pickB = Pick("B", GameSession.AwayPath, 1);
        _compare = UiTheme.Secondary("Vergelijk gekozen tactieken");
        _compare.Pressed += () =>
        {
            if (_work is not null || entries.Length == 0) return;
            GameSession.HomePath = (string)_pickA.GetItemMetadata(_pickA.Selected);
            GameSession.AwayPath = (string)_pickB.GetItemMetadata(_pickB.Selected);
            GetTree().ReloadCurrentScene();
        };
        box.AddChild(_compare);
        _compare.Disabled = _pickA.Disabled = _pickB.Disabled = true;
        _status = Wrapped("Tactieken laden...", UiTheme.Muted); box.AddChild(_status);
        _progress = new ProgressBar { MaxValue = MatchCount, ShowPercentage = true, CustomMinimumSize = new Vector2(0, 28) }; box.AddChild(_progress);
        var track = new StyleBoxFlat { BgColor = UiTheme.PanelLight }; var fill = new StyleBoxFlat { BgColor = UiTheme.Accent };
        _progress.AddThemeStyleboxOverride("background", track); _progress.AddThemeStyleboxOverride("fill", fill);
        box.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        box.AddChild(Wrapped("Balbezit = aandeel balcontacten. Dit meet verschillen in de simulatie; geen bewijs van voetbalrealisme.", UiTheme.Muted));
        var back = UiTheme.Secondary("Terug naar menu [Esc]"); back.Pressed += Back; box.AddChild(back);
        var reportPanel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; columns.AddChild(reportPanel);
        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; reportPanel.AddChild(_scroll);
        _report = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _scroll.AddChild(_report);
        // Capture paths on the main thread, including for a directly opened scene.
        if (entries.Length == 0) { _status.Text = "Geen tactieken gevonden."; return; }
        var homePath = (string)_pickA.GetItemMetadata(_pickA.Selected);
        var awayPath = (string)_pickB.GetItemMetadata(_pickB.Selected);
        _report.AddChild(UiTheme.Label("Tactieken vergelijken", UiTheme.Heading + 10, null, true));
        _report.AddChild(Wrapped($"A = {GameSession.TacticLabel(homePath)}\nB = {GameSession.TacticLabel(awayPath)}", UiTheme.Muted));
        var token = _cancellation.Token;
        _work = Task.Run(() => Compare(homePath, awayPath, token), token);
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        _frames++;
        var shot = GameSession.DebugEnvironment("ZEUS_SHOT");
        if (shot.Length > 0 && ((_finished && ++_readyFrames >= 10 && _frames >= 10) || _elapsed >= 300))
        {
            if (!_finished) { GD.PushError("Screenshotlimiet: rapport niet klaar binnen 300 seconden."); GetTree().Quit(1); return; }
            if (int.TryParse(GameSession.DebugEnvironment("ZEUS_SCROLL"), out var scrollTo) && _readyFrames < 20) { _scroll.ScrollVertical = scrollTo; return; }   // visual check of the lower part
            GetViewport().GetTexture().GetImage().SavePng(shot + "_meter.png"); GetTree().Quit();
        }
        if (_work is null) return;
        int completed = Volatile.Read(ref _completed);
        _progress.Value = completed + Volatile.Read(ref _matchPermille) / 1000.0;
        _status.Text = $"{completed}/{MatchCount} wedstrijden klaar - seed {Math.Min(SeedCount, completed / 2 + 1)}, A {(completed % 2 == 0 ? "thuis" : "uit")}";
        if (!_work.IsCompleted) return;
        if (_work.IsCompletedSuccessfully)
        {
            ShowReport(_work.Result);
            _status.AddThemeColorOverride("font_color", UiTheme.Good);
            _progress.Value = MatchCount;
            _status.Text = $"Klaar: {MatchCount} wedstrijden, {SeedCount} seeds, thuis/uit gewisseld.";
        }
        else
        {
            _status.Text = _work.IsCanceled ? "Meting geannuleerd." : "Meting mislukt; geen volledig resultaat.";
            _status.AddThemeColorOverride("font_color", UiTheme.Bad);
            if (_work.Exception is { } error) _report.AddChild(Wrapped(error.GetBaseException().Message, UiTheme.Bad));
        }
        _finished = true;
        _work = null;
        _compare.Disabled = _pickA.Disabled = _pickB.Disabled = false;
    }

    Report Compare(string homePath, string awayPath, CancellationToken token)
    {
        var a = TacticLoader.LoadFile(homePath);
        var b = TacticLoader.LoadFile(awayPath);
        var totalsA = new double[8];
        var totalsB = new double[8];
        int wins = 0, draws = 0, losses = 0;
        var heatA = new Heatmap();
        var heatB = new Heatmap();
        for (ulong seed = 1; seed <= SeedCount; seed++)
        for (int leg = 0; leg < 2; leg++)
        {
            token.ThrowIfCancellationRequested();
            var match = Match.Create(new MatchConfig(), leg == 0 ? a : b, leg == 0 ? b : a, seed);
            int steps = 0;
            while (match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned) && steps < StepLimit)
            {
                match.Step();
                steps++;
                if (steps % 10 == 0)         // one sample per simulated second
                {
                    var snapshot = match.GetSnapshot();
                    heatA.AddSnapshot(snapshot, leg == 0 ? Side.Home : Side.Away);
                    heatB.AddSnapshot(snapshot, leg == 0 ? Side.Away : Side.Home);
                }
                if (steps % 256 != 0) continue;
                token.ThrowIfCancellationRequested();
                // Added time has no fixed duration: keep the current match below 100% until finished.
                Volatile.Write(ref _matchPermille, (int)(Math.Min(.99, match.TimeSeconds / 5400) * 1000));
            }
            if (match.Status != MatchStatus.Finished)
                throw new InvalidOperationException($"Seed {seed}, A {(leg == 0 ? "thuis" : "uit")}: wedstrijd {(match.Status == MatchStatus.Abandoned ? "gestaakt" : "niet afgerond binnen de stappenlimiet")}. Meting afgebroken.");
            var stats = MatchStatistics.FromMatch(match);
            var statsA = leg == 0 ? stats.Home : stats.Away;
            var statsB = leg == 0 ? stats.Away : stats.Home;
            Add(totalsA, statsA);
            Add(totalsB, statsB);
            if (statsA.Goals > statsB.Goals) wins++;
            else if (statsA.Goals < statsB.Goals) losses++;
            else draws++;
            Volatile.Write(ref _matchPermille, 0);
            Interlocked.Increment(ref _completed);
        }
        token.ThrowIfCancellationRequested();
        string[] labels = { "Doelpunten", "Schoten", "Passes", "Balcontacten", "Onderscheppingen", "Overtredingen", "Gele kaarten", "Rode kaarten" };
        var avgA = totalsA.Select(v => v / MatchCount).ToArray();
        var avgB = totalsB.Select(v => v / MatchCount).ToArray();
        double controls = Math.Max(1, totalsA[3] + totalsB[3]);
        // Same candidates and normalization as scripts/tactic_meter.py (excluding goals/red cards).
        var differences = Enumerable.Range(1, 6)
            .Select(i => new { Index = i, Relative = Math.Abs(avgA[i] - avgB[i]) / Math.Max(1e-9, (avgA[i] + avgB[i]) / 2) })
            .OrderByDescending(d => d.Relative).ThenBy(d => labels[d.Index], StringComparer.Ordinal).Take(3);
        var reasons = differences.Select(diff =>
        {
            int i = diff.Index;
            return diff.Relative == 0
                ? $"{labels[i]}: geen verschil ({avgA[i]:F1} / {avgB[i]:F1})."
                : $"{labels[i]}: {(avgA[i] > avgB[i] ? "A" : "B")} heeft meer ({avgA[i]:F1} / {avgB[i]:F1}); verschil {100 * diff.Relative:F0}% van het gemiddelde.";
        }).ToArray();
        return new Report(a.Name, b.Name, wins, draws, losses, labels, avgA, avgB, 100 * totalsA[3] / controls, reasons, heatA, heatB, differences.Select(d => d.Index).ToArray());
    }

    static Label Wrapped(string text, Color color)
    {
        var label = UiTheme.Label(text, UiTheme.Small, color);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    void ShowReport(Report report)
    {
        foreach (Node child in _report.GetChildren()) { _report.RemoveChild(child); child.QueueFree(); }
        _report.AddChild(UiTheme.Label("Resultaat", UiTheme.Heading + 10, null, true));
        _report.AddChild(Wrapped($"A  {report.A}    /    B  {report.B}", UiTheme.Muted));
        var cards = new HBoxContainer(); _report.AddChild(cards);
        foreach (var (title, value, color) in new[] { ("Winst A", report.Wins, UiTheme.Good), ("Gelijk", report.Draws, UiTheme.Muted), ("Verlies A", report.Losses, UiTheme.Bad) })
        {
            var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; cards.AddChild(card);
            var content = new VBoxContainer(); card.AddChild(content);
            content.AddChild(UiTheme.Label(title, UiTheme.Small, color)); content.AddChild(UiTheme.Label(value.ToString(), UiTheme.Heading, color));
        }
        _report.AddChild(UiTheme.Label("Waar spelen ze", UiTheme.Heading - 6));
        var heats = new HBoxContainer(); heats.AddThemeConstantOverride("separation", 28); _report.AddChild(heats);
        foreach (var (heat, color, title) in new[] { (report.HeatA, UiTheme.Home, "A"), (report.HeatB, UiTheme.Away, "B") })
        {
            var column = new VBoxContainer(); heats.AddChild(column);
            var view = new HeatmapView(); column.AddChild(view); view.Show(heat, color, title);
            var (back, middle, front) = heat.Thirds();
            column.AddChild(Wrapped($"eigen derde {back * 100:F0}%  •  midden {middle * 100:F0}%  •  aanvalsderde {front * 100:F0}%", UiTheme.Muted));
        }
        // Dashboard with the maker's own goals (PRD: never one number): tick what you care about, see who is better per goal.
        _report.AddChild(UiTheme.Label("Jouw doelen", UiTheme.Heading - 6));
        _report.AddChild(Wrapped("Vink aan waar je op let; per doel zie je of A of B beter is. Klik op een reden hieronder om die maat te volgen.", UiTheme.Muted));
        var goals = new System.Collections.Generic.HashSet<int> { 0 };
        var rows = new System.Collections.Generic.List<(CheckBox Check, Label Name, Label Verdict, double A, double B, bool HigherBetter, int Index)>();
        var grid = new GridContainer { Columns = 5 }; grid.AddThemeConstantOverride("h_separation", 32); grid.AddThemeConstantOverride("v_separation", 6); _report.AddChild(grid);
        void Refresh()
        {
            foreach (var r in rows)
            {
                bool on = goals.Contains(r.Index);
                r.Check.SetPressedNoSignal(on);
                r.Name.AddThemeColorOverride("font_color", on ? UiTheme.Accent : UiTheme.Text);
                bool aBetter = r.HigherBetter ? r.A > r.B : r.A < r.B;
                bool equal = Math.Abs(r.A - r.B) < 1e-9;
                r.Verdict.Text = !on ? "" : equal ? "gelijk" : aBetter ? "✓ A beter" : "✗ B beter";
                r.Verdict.AddThemeColorOverride("font_color", !on || equal ? UiTheme.Muted : aBetter ? UiTheme.Good : UiTheme.Bad);
            }
        }
        void Row(int index, string label, double a, double b, bool higherBetter, string format)
        {
            var check = new CheckBox();
            var name = UiTheme.Label(label, UiTheme.Body);
            var verdict = UiTheme.Label("", UiTheme.Body);
            check.Toggled += on => { if (on) goals.Add(index); else goals.Remove(index); Refresh(); };
            grid.AddChild(check); grid.AddChild(name);
            grid.AddChild(UiTheme.Label(a.ToString(format), UiTheme.Body, UiTheme.Home));
            grid.AddChild(UiTheme.Label(b.ToString(format), UiTheme.Body, UiTheme.Away));
            grid.AddChild(verdict);
            rows.Add((check, name, verdict, a, b, higherBetter, index));
        }
        grid.AddChild(new Control()); grid.AddChild(UiTheme.Label("Gemiddelden per wedstrijd", UiTheme.Small, UiTheme.Muted));
        grid.AddChild(UiTheme.Label("A", UiTheme.Small, UiTheme.Home)); grid.AddChild(UiTheme.Label("B", UiTheme.Small, UiTheme.Away)); grid.AddChild(new Control());
        for (int i = 0; i < report.Labels.Length; i++) Row(i, report.Labels[i], report.AvgA[i], report.AvgB[i], higherBetter: i <= 4, "F2");
        Row(8, "Balbezit (%)", report.Possession, 100 - report.Possession, higherBetter: true, "F1");
        Refresh();
        _report.AddChild(UiTheme.Label("Waarom: grootste verschillen", UiTheme.Heading - 6));
        for (int i = 0; i < report.Differences.Length; i++)
        {
            int metric = report.DiffIndices[i];
            var reason = new Button { Text = report.Differences[i], Flat = true, Alignment = HorizontalAlignment.Left,
                AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = "Klik om deze maat als doel te volgen" };
            reason.AddThemeFontSizeOverride("font_size", UiTheme.Small + 1);
            reason.Pressed += () => { goals.Add(metric); Refresh(); };
            _report.AddChild(reason);
        }
        _report.AddChild(Wrapped("Vergelijk doelpunten met schoten: veel schoten en weinig goals kan op toeval of lage kwaliteit wijzen. Deze verschillen bewijzen geen oorzaak.", UiTheme.Muted));
    }

    static void Add(double[] totals, TeamStatistics stats)
    {
        double[] values = { stats.Goals, stats.Shots, stats.Passes, stats.BallControls, stats.Interceptions, stats.Fouls, stats.YellowCards, stats.RedCards };
        for (int i = 0; i < totals.Length; i++) totals[i] += values[i];
    }

    void Back() => GetTree().ChangeSceneToFile("res://Menu.tscn");

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) Back();
    }

    public override void _ExitTree()
    {
        _cancellation.Cancel();
        _cancellation.Dispose();
    }
}
