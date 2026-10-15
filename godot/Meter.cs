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
    record Report(string A, string B, int Wins, int Draws, int Losses, string[] Labels, double[] AvgA, double[] AvgB, double Possession, string[] Differences, Heatmap HeatA, Heatmap HeatB);
    Task<Report>? _work;
    Label _status = null!;
    ProgressBar _progress = null!;
    VBoxContainer _report = null!;
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
        _status = Wrapped("Tactieken laden...", UiTheme.Muted); box.AddChild(_status);
        _progress = new ProgressBar { MaxValue = MatchCount, ShowPercentage = true, CustomMinimumSize = new Vector2(0, 28) }; box.AddChild(_progress);
        var track = new StyleBoxFlat { BgColor = UiTheme.PanelLight }; var fill = new StyleBoxFlat { BgColor = UiTheme.Accent };
        _progress.AddThemeStyleboxOverride("background", track); _progress.AddThemeStyleboxOverride("fill", fill);
        box.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        box.AddChild(Wrapped("Balbezit = aandeel balcontacten. Dit meet verschillen in de simulatie; geen bewijs van voetbalrealisme.", UiTheme.Muted));
        var back = UiTheme.Secondary("Terug naar menu [Esc]"); back.Pressed += Back; box.AddChild(back);
        var reportPanel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; columns.AddChild(reportPanel);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; reportPanel.AddChild(scroll);
        _report = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; scroll.AddChild(_report);
        // Capture paths on the main thread, including for a directly opened scene.
        var dir = GameSession.TacticsDir();
        var homePath = GameSession.HomePath ?? Path.Combine(dir, "laag_blok_442.json");
        var awayPath = GameSession.AwayPath ?? Path.Combine(dir, "man_om_man_433.json");
        _report.AddChild(UiTheme.Label("Tactieken vergelijken", UiTheme.Heading + 10, null, true));
        _report.AddChild(Wrapped($"A = {Path.GetFileName(homePath)}\nB = {Path.GetFileName(awayPath)}", UiTheme.Muted));
        var token = _cancellation.Token;
        _work = Task.Run(() => Compare(homePath, awayPath, token), token);
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        _frames++;
        var shot = OS.GetEnvironment("ZEUS_SHOT");
        if (shot.Length > 0 && ((_finished && ++_readyFrames >= 10 && _frames >= 10) || _elapsed >= 300))
        {
            if (!_finished) { GD.PushError("Screenshotlimiet: rapport niet klaar binnen 300 seconden."); GetTree().Quit(1); return; }
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
        return new Report(a.Name, b.Name, wins, draws, losses, labels, avgA, avgB, 100 * totalsA[3] / controls, reasons, heatA, heatB);
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
        var grid = new GridContainer { Columns = 3 }; grid.AddThemeConstantOverride("h_separation", 48); grid.AddThemeConstantOverride("v_separation", 6); _report.AddChild(grid);
        void Row(string label, string a, string b)
        {
            grid.AddChild(UiTheme.Label(label, UiTheme.Body)); grid.AddChild(UiTheme.Label(a, UiTheme.Body, UiTheme.Home)); grid.AddChild(UiTheme.Label(b, UiTheme.Body, UiTheme.Away));
        }
        Row("Gemiddelden per wedstrijd", "A", "B");
        for (int i = 0; i < report.Labels.Length; i++) Row(report.Labels[i], $"{report.AvgA[i]:F2}", $"{report.AvgB[i]:F2}");
        Row("Balbezit", $"{report.Possession:F1}%", $"{100 - report.Possession:F1}%");
        _report.AddChild(UiTheme.Label("Waarom: grootste verschillen", UiTheme.Heading - 6));
        foreach (var reason in report.Differences) _report.AddChild(Wrapped(reason, UiTheme.Text));
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
