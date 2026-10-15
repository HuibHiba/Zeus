using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Zeus.Sim;

// Runs simulations without scene objects; only the main thread reads/writes the UI.
public partial class Meter : Control
{
    const int SeedCount = 10;
    const int MatchCount = SeedCount * 2;
    const int StepLimit = 150000;
    readonly CancellationTokenSource _cancellation = new();
    Task<string>? _work;
    Label _status = null!;
    ProgressBar _progress = null!;
    RichTextLabel _report = null!;
    int _completed;
    int _matchPermille;

    public override void _Ready()
    {
        var margin = new MarginContainer();
        AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var edge in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride("margin_" + edge, 32);
        var box = new VBoxContainer();
        margin.AddChild(box);
        box.AddChild(new Label { Text = "METER — tactiekvergelijking" });
        _status = new Label { Text = "Tactieken laden…" };
        box.AddChild(_status);
        _progress = new ProgressBar { MaxValue = MatchCount, ShowPercentage = true };
        box.AddChild(_progress);
        _report = new RichTextLabel { SizeFlagsVertical = SizeFlags.ExpandFill };
        box.AddChild(_report);
        box.AddChild(new Label { Text = "Balbezit = aandeel balcontacten. Dit meet verschillen in de simulatie; geen bewijs van voetbalrealisme." });
        var back = new Button { Text = "Terug naar menu [Esc]" };
        back.Pressed += Back;
        box.AddChild(back);

        // Capture paths on the main thread, including for a directly opened scene.
        var dir = GameSession.TacticsDir();
        var homePath = GameSession.HomePath ?? Path.Combine(dir, "laag_blok_442.json");
        var awayPath = GameSession.AwayPath ?? Path.Combine(dir, "man_om_man_433.json");
        _report.Text = $"A = {Path.GetFileName(homePath)}\nB = {Path.GetFileName(awayPath)}\n\n10 seeds (1–10), elk met A thuis en A uit.";
        var token = _cancellation.Token;
        _work = Task.Run(() => Compare(homePath, awayPath, token), token);
    }

    public override void _Process(double delta)
    {
        if (_work is null) return;
        int completed = Volatile.Read(ref _completed);
        _progress.Value = completed + Volatile.Read(ref _matchPermille) / 1000.0;
        _status.Text = $"{completed}/{MatchCount} wedstrijden klaar — seed {Math.Min(SeedCount, completed / 2 + 1)}, A {(completed % 2 == 0 ? "thuis" : "uit")}";
        if (!_work.IsCompleted) return;
        if (_work.IsCompletedSuccessfully)
        {
            _report.Text = _work.Result;
            _progress.Value = MatchCount;
            _status.Text = $"Klaar: {MatchCount} wedstrijden, {SeedCount} seeds, thuis/uit gewisseld.";
        }
        else
        {
            _status.Text = _work.IsCanceled ? "Meting geannuleerd." : "Meting mislukt; geen volledig resultaat.";
            if (_work.Exception is { } error) _report.Text = error.GetBaseException().Message;
        }
        _work = null;
    }

    string Compare(string homePath, string awayPath, CancellationToken token)
    {
        var a = TacticLoader.LoadFile(homePath);
        var b = TacticLoader.LoadFile(awayPath);
        var totalsA = new double[8];
        var totalsB = new double[8];
        int wins = 0, draws = 0, losses = 0;
        for (ulong seed = 1; seed <= SeedCount; seed++)
        for (int leg = 0; leg < 2; leg++)
        {
            token.ThrowIfCancellationRequested();
            var match = Match.Create(new MatchConfig(), leg == 0 ? a : b, leg == 0 ? b : a, seed);
            int steps = 0;
            while (match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned) && steps < StepLimit)
            {
                match.Step();
                if (++steps % 256 != 0) continue;
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
        var text = new StringBuilder($"A = {a.Name}\nB = {b.Name}\n\nUitslagen A (W/G/V): {wins}/{draws}/{losses}\n\nGemiddelden per wedstrijd                         A / B\n");
        for (int i = 0; i < labels.Length; i++)
            text.AppendLine($"{labels[i]}: {avgA[i]:F2} / {avgB[i]:F2}");
        double controls = Math.Max(1, totalsA[3] + totalsB[3]);
        text.AppendLine($"Balbezit (aandeel balcontacten): {100 * totalsA[3] / controls:F1}% / {100 * totalsB[3] / controls:F1}%");
        text.AppendLine("\nWaarom — drie grootste relatieve verschillen:");
        // Same candidates and normalization as scripts/tactic_meter.py (excluding goals/red cards).
        var differences = Enumerable.Range(1, 6)
            .Select(i => new { Index = i, Relative = Math.Abs(avgA[i] - avgB[i]) / Math.Max(1e-9, (avgA[i] + avgB[i]) / 2) })
            .OrderByDescending(d => d.Relative).ThenBy(d => labels[d.Index], StringComparer.Ordinal).Take(3);
        foreach (var diff in differences)
        {
            int i = diff.Index;
            text.AppendLine(diff.Relative == 0
                ? $"• {labels[i]}: geen verschil ({avgA[i]:F1} / {avgB[i]:F1})."
                : $"• {labels[i]}: {(avgA[i] > avgB[i] ? "A" : "B")} heeft meer ({avgA[i]:F1} / {avgB[i]:F1}); verschil {100 * diff.Relative:F0}% van het gemiddelde.");
        }
        text.AppendLine("\nVergelijk doelpunten met schoten: veel schoten en weinig goals kan op toeval of lage kwaliteit wijzen.\nDeze verschillen bewijzen geen oorzaak.");
        return text.ToString();
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
