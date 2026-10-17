using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Zeus.Sim;
using Side = Zeus.Sim.Side;

// Quick test without leaving the editor: the tactic as it is now (also unsaved) plays a few short matches against a chosen
// opponent, home and away, the way the meter does it. A first impression only; the meter plays full matches over more seeds.
public partial class Editor
{
    const int TestSeeds = 2, TestMatches = TestSeeds * 2, TestStepLimit = 60000;
    const double TestHalfSeconds = 600;                 // 2 x 10 minutes
    record TestReport(int Wins, int Draws, int Losses, double GoalsFor, double GoalsAgainst, double ShotsFor, double ShotsAgainst, double Possession, Heatmap Heat);
    OptionButton _testOpponent = null!;
    Button _testRun = null!;
    Label _testResult = null!;
    HeatmapView _testHeat = null!;
    CancellationTokenSource? _testCancel;
    Task<TestReport>? _test;
    int _testDone;

    void BuildTestTab()
    {
        var t = Tab(_tabs, Loc.T("editor.test_tab"));
        t.AddChild(Small(Loc.T("editor.test_help", TestMatches, TestHalfSeconds / 60)));
        t.AddChild(Small(Loc.T("editor.test_opponent")));
        _testOpponent = new OptionButton { FitToLongestItem = false, CustomMinimumSize = new Vector2(0, 40), ClipText = true }; t.AddChild(_testOpponent);
        foreach (var entry in GameSession.Tactics()) { _testOpponent.AddItem(entry.Label); _testOpponent.SetItemMetadata(_testOpponent.ItemCount - 1, entry.Path); }
        _testRun = UiTheme.Secondary(Loc.T("editor.test_run")); t.AddChild(_testRun);
        BuildScout(t);
        _testRun.Pressed += StartTest;
        _testResult = Small(""); t.AddChild(_testResult);
        _testHeat = new HeatmapView { Scale = 0.75f, Visible = false }; t.AddChild(_testHeat);
    }

    void StartTest()
    {
        if (_model is null || _test is not null || _testOpponent.ItemCount == 0) return;
        Tactic own, opponent;
        try
        {
            own = TacticLoader.Parse(_model.ToJson());
            opponent = TacticLoader.LoadFile(Meta(_testOpponent));
        }
        catch (Exception e) { _testResult.Text = Loc.T("editor.test_failed") + Loc.Error(e); return; }
        _testCancel = new CancellationTokenSource();
        _testDone = 0;
        _testRun.Disabled = true;
        _testHeat.Visible = false;
        _testResult.Text = Loc.T("editor.test_progress", 0, TestMatches);
        _test = SimulationBatch.RunAsync(token => RunTest(own, opponent, token), _testCancel.Token);
    }

    TestReport RunTest(Tactic own, Tactic opponent, CancellationToken token)
    {
        int wins = 0, draws = 0, losses = 0;
        double goalsFor = 0, goalsAgainst = 0, shotsFor = 0, shotsAgainst = 0, controlsFor = 0, controlsAll = 0;
        var heat = new Heatmap();
        for (ulong seed = 1; seed <= TestSeeds; seed++)
        for (int leg = 0; leg < 2; leg++)
        {
            var side = leg == 0 ? Side.Home : Side.Away;
            var match = Match.Create(new MatchConfig(HalfSeconds: TestHalfSeconds), leg == 0 ? own : opponent, leg == 0 ? opponent : own, seed);
            for (int steps = 0; match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned) && steps < TestStepLimit; steps++)
            {
                match.Step();
                if (steps % 10 == 0) heat.AddSnapshot(match.GetSnapshot(), side);
                if (steps % 64 == 0) token.ThrowIfCancellationRequested();
            }
            if (match.Status != MatchStatus.Finished) throw new InvalidOperationException(Loc.T("editor.test_unfinished"));
            var stats = MatchStatistics.FromMatch(match);
            var (us, them) = leg == 0 ? (stats.Home, stats.Away) : (stats.Away, stats.Home);
            goalsFor += us.Goals; goalsAgainst += them.Goals; shotsFor += us.Shots; shotsAgainst += them.Shots;
            controlsFor += us.BallControls; controlsAll += us.BallControls + them.BallControls;
            if (us.Goals > them.Goals) wins++; else if (us.Goals < them.Goals) losses++; else draws++;
            Interlocked.Increment(ref _testDone);
        }
        return new TestReport(wins, draws, losses, goalsFor / TestMatches, goalsAgainst / TestMatches, shotsFor / TestMatches, shotsAgainst / TestMatches,
            100 * controlsFor / Math.Max(1, controlsAll), heat);
    }

    void PollTest()
    {
        if (_test is null) return;
        if (!_test.IsCompleted) { _testResult.Text = Loc.T("editor.test_progress", Volatile.Read(ref _testDone), TestMatches); return; }
        if (_test.IsCompletedSuccessfully)
        {
            var r = _test.Result;
            _testResult.Text = Loc.T("editor.test_result", r.Wins, r.Draws, r.Losses, r.GoalsFor, r.GoalsAgainst, r.ShotsFor, r.ShotsAgainst, r.Possession);
            _testHeat.Show(r.Heat, UiTheme.Home, Loc.T("editor.test_heat"));
            _testHeat.Visible = true;
        }
        else _testResult.Text = Loc.T("editor.test_failed") + (_test.Exception is { } e ? Loc.Error(e.GetBaseException()) : "");
        _testCancel?.Dispose(); _testCancel = null;
        _test = null;
        _testRun.Disabled = false;
    }

    void CancelTest()
    {
        if (_testCancel is not null) SimulationBatch.CancelAndDispose(_testCancel, _test);
        _testCancel = null; _test = null;
    }
}
