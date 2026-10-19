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
    record Report(string A, string B, int Wins, int Draws, int Losses, string[] Labels, double[] AvgA, double[] AvgB, double Possession, string[] Differences, Heatmap HeatA, Heatmap HeatB, int[] DiffIndices, double RulesA, double RulesB);
    Task<Report>? _work;
    double _halfSeconds = 2700;
    OptionButton _pickA = null!, _pickB = null!;
    Button _compare = null!, _cancel = null!;
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
        UiTheme.ApplyPaper(this);
        Welcome.Install(this, "meter");
        var bg = new UiPaper(); bg.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(bg);
        var margin = new MarginContainer(); AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var edge in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 20);
        var columns = new HBoxContainer(); margin.AddChild(columns);
        var side = new PanelContainer { CustomMinimumSize = new Vector2(300, 0) }; columns.AddChild(side);
        var box = new VBoxContainer(); side.AddChild(box);
        box.AddChild(UiTheme.Label(Loc.T("meter.title"), UiTheme.Heading + 10, null, true));
        box.AddChild(Wrapped(Loc.T("meter.setup"), UiTheme.Muted));
        var entries = GameSession.Tactics();
        OptionButton Pick(string title, string? preferred, int fallback)
        {
            box.AddChild(UiTheme.Label(title, UiTheme.Small, UiTheme.Muted));
            var pick = new OptionButton { FitToLongestItem = false, ClipText = true };
            foreach (var entry in entries) { pick.AddItem(entry.Label); pick.SetItemMetadata(pick.ItemCount - 1, entry.Path); }
            int selected = Array.FindIndex(entries, t => t.Path == preferred);
            if (entries.Length > 0) pick.Select(selected >= 0 ? selected : Math.Min(fallback, entries.Length - 1));
            box.AddChild(pick);
            return pick;
        }
        _pickA = Pick(Loc.T("common.team_a"), GameSession.HomePath, 0); _pickB = Pick(Loc.T("common.team_b"), GameSession.AwayPath, 1);
        var withoutRules = new CheckBox { Text = Loc.T("meter.without_rules"), ButtonPressed = GameSession.MeterWithoutRules, TooltipText = Loc.T("meter.without_rules_help") };
        withoutRules.Toggled += on => GameSession.MeterWithoutRules = on;
        box.AddChild(withoutRules);
        _compare = UiTheme.Secondary(Loc.T("meter.compare"));
        _compare.Pressed += () =>
        {
            if (_work is not null || entries.Length == 0) return;
            GameSession.HomePath = (string)_pickA.GetItemMetadata(_pickA.Selected);
            GameSession.AwayPath = (string)_pickB.GetItemMetadata(_pickB.Selected);
            GetTree().ReloadCurrentScene();
        };
        box.AddChild(_compare);
        _compare.Disabled = _pickA.Disabled = _pickB.Disabled = true;
        _status = Wrapped(Loc.T("meter.loading"), UiTheme.Muted); box.AddChild(_status);
        _progress = new ProgressBar { MaxValue = MatchCount, ShowPercentage = true, CustomMinimumSize = new Vector2(0, 28) }; box.AddChild(_progress);
        _cancel = UiTheme.Secondary(Loc.T("batch.cancel"));
        _cancel.Pressed += () =>
        {
            if (_work is null) return;
            _cancellation.Cancel();
            _cancel.Disabled = true;
            _status.Text = Loc.T("batch.cancelling");
        };
        box.AddChild(_cancel);
        box.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        box.AddChild(Wrapped(Loc.T("meter.possession_help"), UiTheme.Muted));
        var back = UiTheme.Secondary(Loc.T("meter.back")); back.Pressed += Back; box.AddChild(back);
        var reportPanel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; columns.AddChild(reportPanel);
        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; reportPanel.AddChild(_scroll);
        _report = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _scroll.AddChild(_report);
        // Capture paths on the main thread, including for a directly opened scene.
        if (entries.Length == 0) { _status.Text = Loc.T("meter.no_tactics"); _cancel.Visible = false; return; }
        var homePath = (string)_pickA.GetItemMetadata(_pickA.Selected);
        var awayPath = (string)_pickB.GetItemMetadata(_pickB.Selected);
        bool stripRules = GameSession.MeterWithoutRules;
        _report.AddChild(UiTheme.Label(Loc.T("meter.comparing"), UiTheme.Heading + 10, null, true));
        _report.AddChild(Wrapped(Loc.T("meter.tactic_pair", GameSession.TacticLabel(homePath), stripRules ? Loc.T("meter.a_without_rules") : GameSession.TacticLabel(awayPath)), UiTheme.Muted));
        var token = _cancellation.Token;
        Loc.Initialize();
        if (double.TryParse(GameSession.DebugEnvironment("ZEUS_METER_SECONDS"), System.Globalization.CultureInfo.InvariantCulture, out var shortHalf) && shortHalf > 0) _halfSeconds = shortHalf;
        _work = SimulationBatch.RunAsync(t => Compare(homePath, awayPath, stripRules, t), token);
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
        _status.Text = _cancellation.IsCancellationRequested ? Loc.T("batch.cancelling") : Loc.T("meter.progress", completed, MatchCount, Math.Min(SeedCount, completed / 2 + 1), completed % 2 == 0 ? Loc.T("meter.home") : Loc.T("meter.away"));
        if (!_work.IsCompleted) return;
        if (_work.IsCompletedSuccessfully && !_cancellation.IsCancellationRequested)
        {
            ShowReport(_work.Result);
            if ((string)_pickA.GetItemMetadata(_pickA.Selected) != (string)_pickB.GetItemMetadata(_pickB.Selected)) OnboardingProgress.Mark("meter");
            _status.AddThemeColorOverride("font_color", UiTheme.PaperColor(UiTheme.Good));
            _progress.Value = MatchCount;
            _status.Text = Loc.T("meter.finished", MatchCount, SeedCount);
        }
        else
        {
            _status.Text = _work.IsCanceled || _cancellation.IsCancellationRequested ? Loc.T("meter.cancelled") : Loc.T("meter.failed");
            _status.AddThemeColorOverride("font_color", UiTheme.PaperColor(UiTheme.Bad));
            if (_work.Exception is { } error) _report.AddChild(Wrapped(Loc.Error(error.GetBaseException()), UiTheme.Bad));
        }
        _finished = true;
        _work = null;
        _cancel.Visible = false;
        _compare.Disabled = _pickA.Disabled = _pickB.Disabled = false;
    }

    Report Compare(string homePath, string awayPath, bool stripRules, CancellationToken token)
    {
        var a = TacticLoader.LoadFile(homePath);
        var b = stripRules ? a.WithoutTriggers() : TacticLoader.LoadFile(awayPath);
        double rulesA = 0, rulesB = 0;
        var totalsA = new double[8];
        var totalsB = new double[8];
        int wins = 0, draws = 0, losses = 0;
        var heatA = new Heatmap();
        var heatB = new Heatmap();
        for (ulong seed = 1; seed <= SeedCount; seed++)
        for (int leg = 0; leg < 2; leg++)
        {
            token.ThrowIfCancellationRequested();
            var config = new MatchConfig(HalfSeconds: _halfSeconds);
            var match = Match.Create(config, leg == 0 ? a : b, leg == 0 ? b : a, seed);
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
                if (steps % 64 != 0) continue;
                token.ThrowIfCancellationRequested();
                // Added time has no fixed duration: keep the current match below 100% until finished.
                Volatile.Write(ref _matchPermille, (int)(Math.Min(.99, match.TimeSeconds / (2 * _halfSeconds)) * 1000));
            }
            if (match.Status != MatchStatus.Finished)
                throw new InvalidOperationException($"Seed {seed}, A {(leg == 0 ? Loc.T("meter.home") : Loc.T("meter.away"))}: wedstrijd {(match.Status == MatchStatus.Abandoned ? "gestaakt" : "niet afgerond binnen de stappenlimiet")}. Meting afgebroken.");
            var stats = MatchStatistics.FromMatch(match);
            rulesA += match.Events.Events.Count(e => e.Type == EventType.TriggerFired && e.Team == (leg == 0 ? Side.Home : Side.Away));
            rulesB += match.Events.Events.Count(e => e.Type == EventType.TriggerFired && e.Team == (leg == 0 ? Side.Away : Side.Home));
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
        string[] labels = { Loc.T("meter.goals"), Loc.T("meter.shots"), Loc.T("meter.passes"), Loc.T("meter.touches"), Loc.T("meter.interceptions"), Loc.T("meter.fouls"), Loc.T("meter.yellow"), Loc.T("meter.red") };
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
                ? Loc.T("meter.no_difference", labels[i], avgA[i], avgB[i])
                : Loc.T("meter.difference", labels[i], avgA[i] > avgB[i] ? Loc.T("common.team_a") : Loc.T("common.team_b"), avgA[i], avgB[i], 100 * diff.Relative);
        }).ToArray();
        return new Report(Loc.Name(a.Name), stripRules ? Loc.T("meter.a_without_rules") : Loc.Name(b.Name), wins, draws, losses, labels, avgA, avgB, 100 * totalsA[3] / controls, reasons, heatA, heatB,
            differences.Select(d => d.Index).ToArray(), rulesA / MatchCount, rulesB / MatchCount);
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
        // Two columns so the whole report fits the window without scrolling: result and heatmaps on the left, the goals table on the right.
        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; columns.AddThemeConstantOverride("separation", 24); _report.AddChild(columns);
        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkBegin }; left.AddThemeConstantOverride("separation", 8); columns.AddChild(left);
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; right.AddThemeConstantOverride("separation", 8); columns.AddChild(right);
        left.AddChild(UiTheme.Label(Loc.T("meter.result"), UiTheme.Heading + 6, null, true));
        left.AddChild(Wrapped(Loc.T("meter.tactic_pair_inline", report.A, report.B), UiTheme.Muted));
        var cards = new HBoxContainer(); left.AddChild(cards);
        foreach (var (title, value, color) in new[] { (Loc.T("meter.wins"), report.Wins, UiTheme.Good), (Loc.T("meter.draws"), report.Draws, UiTheme.Muted), (Loc.T("meter.losses"), report.Losses, UiTheme.Bad) })
        {
            var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; cards.AddChild(card);
            var content = new VBoxContainer(); card.AddChild(content);
            content.AddChild(UiTheme.Label(title, UiTheme.Small, color)); content.AddChild(UiTheme.Label(value.ToString(), UiTheme.Heading, color));
        }
        // How often the rules acted: shows whether a rule fires at all (0 = never), next to what changed in the table.
        left.AddChild(Wrapped(Loc.T("meter.rules_fired", report.RulesA, report.RulesB), UiTheme.Ink));
        left.AddChild(UiTheme.Label(Loc.T("meter.heatmaps"), UiTheme.Heading - 6));
        var heats = new HBoxContainer(); heats.AddThemeConstantOverride("separation", 20); left.AddChild(heats);
        foreach (var (heat, color, title) in new[] { (report.HeatA, UiTheme.Home, Loc.T("common.team_a")), (report.HeatB, UiTheme.Away, Loc.T("common.team_b")) })
        {
            var column = new VBoxContainer(); heats.AddChild(column);
            var view = new HeatmapView { Scale = 0.85f }; column.AddChild(view); view.Show(heat, color, title);
            var (back, middle, front) = heat.Thirds();
            var thirds = Wrapped(Loc.T("meter.thirds", back * 100, middle * 100, front * 100), UiTheme.Muted);
            thirds.CustomMinimumSize = new Vector2(280, 0); column.AddChild(thirds);
        }
        // Dashboard with the maker's own goals (PRD: never one number): tick what you care about, see who is better per goal.
        right.AddChild(UiTheme.Label(Loc.T("meter.your_goals"), UiTheme.Heading - 6));
        right.AddChild(Wrapped(Loc.T("meter.goals_help"), UiTheme.Muted));
        var goals = new System.Collections.Generic.HashSet<int> { 0 };
        var rows = new System.Collections.Generic.List<(CheckBox Check, Label Name, Label Verdict, double A, double B, bool HigherBetter, int Index)>();
        var grid = new GridContainer { Columns = 5 }; grid.AddThemeConstantOverride("h_separation", 24); grid.AddThemeConstantOverride("v_separation", 2); right.AddChild(grid);
        void Refresh()
        {
            foreach (var r in rows)
            {
                bool on = goals.Contains(r.Index);
                r.Check.SetPressedNoSignal(on);
                r.Name.AddThemeColorOverride("font_color", on ? UiTheme.Ink : UiTheme.Graphite);
                bool aBetter = r.HigherBetter ? r.A > r.B : r.A < r.B;
                bool equal = Math.Abs(r.A - r.B) < 1e-9;
                r.Verdict.Text = !on ? "" : equal ? Loc.T("meter.equal") : aBetter ? Loc.T("meter.a_better") : Loc.T("meter.b_better");
                r.Verdict.AddThemeColorOverride("font_color", !on || equal ? UiTheme.Graphite : aBetter ? new Color("3E6B3A") : new Color("8E1F14"));
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
        grid.AddChild(new Control()); grid.AddChild(UiTheme.Label(Loc.T("meter.averages"), UiTheme.Small, UiTheme.Muted));
        grid.AddChild(UiTheme.Label(Loc.T("common.team_a"), UiTheme.Small, UiTheme.Home)); grid.AddChild(UiTheme.Label(Loc.T("common.team_b"), UiTheme.Small, UiTheme.Away)); grid.AddChild(new Control());
        for (int i = 0; i < report.Labels.Length; i++) Row(i, report.Labels[i], report.AvgA[i], report.AvgB[i], higherBetter: i <= 4, "F2");
        Row(8, Loc.T("meter.possession_pct"), report.Possession, 100 - report.Possession, higherBetter: true, "F1");
        Refresh();
        left.AddChild(UiTheme.Label(Loc.T("meter.why"), UiTheme.Heading - 6));
        for (int i = 0; i < report.Differences.Length; i++)
        {
            int metric = report.DiffIndices[i];
            // Label, not an autowrapping Button: a wrapped Button under-reports its height and clips the last lines in the scroll area.
            var reason = Wrapped(report.Differences[i], UiTheme.Ink);
            reason.AddThemeFontSizeOverride("font_size", UiTheme.Small + 1);
            reason.MouseFilter = MouseFilterEnum.Stop; reason.MouseDefaultCursorShape = CursorShape.PointingHand; reason.TooltipText = Loc.T("meter.track_metric");
            reason.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { goals.Add(metric); Refresh(); } };
            reason.CustomMinimumSize = new Vector2(580, 0); left.AddChild(reason);
        }
        right.AddChild(Wrapped(Loc.T("meter.causation_help"), UiTheme.Muted));
        left.AddChild(new Control { CustomMinimumSize = new Vector2(0, 24) });      // room under the last line
    }

    static void Add(double[] totals, TeamStatistics stats)
    {
        double[] values = { stats.Goals, stats.Shots, stats.Passes, stats.BallControls, stats.Interceptions, stats.Fouls, stats.YellowCards, stats.RedCards };
        for (int i = 0; i < totals.Length; i++) totals[i] += values[i];
    }

    void Back() => GetTree().ChangeSceneToFile("res://Menu.tscn");

    public override void _ExitTree()
    {
        SimulationBatch.CancelAndDispose(_cancellation, _work);
    }
}
