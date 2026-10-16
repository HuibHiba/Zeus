namespace Zeus.Sim.Tests;

public sealed class SimulationBatchTests
{
    [Theory]
    [InlineData(5, 1, 0, 1)]
    [InlineData(5, 2, 0, 1)]
    [InlineData(5, 4, 0, 2)]
    [InlineData(20, 32, 0, 2)]
    [InlineData(1, 8, 0, 1)]
    [InlineData(5, 8, 1, 1)]
    [InlineData(5, 4, 20, 3)]
    public void WorkerBudgetLeavesRoomForTheWindow(int jobs, int processors, int requested, int expected) =>
        Assert.Equal(expected, SimulationBatch.WorkerCount(jobs, processors, requested));

    [Fact]
    public async Task CancelledQueuedScreenDoesNotStartAndNextScreenCanRun()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var first = SimulationBatch.RunAsync(_ =>
        {
            started.SetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return 1;
        }, CancellationToken.None);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var cts = new CancellationTokenSource();
            bool invoked = false;
            var queued = SimulationBatch.RunAsync(_ => { invoked = true; return 2; }, cts.Token);
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.False(invoked);
        }
        finally { release.Set(); await first; }
        Assert.Equal(3, await SimulationBatch.RunAsync(_ => 3, CancellationToken.None));
    }

    [Fact]
    public async Task FailureReleasesTheSlotForTheNextScreen()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => SimulationBatch.RunAsync<int>(_ => throw new InvalidDataException(), CancellationToken.None));
        Assert.Equal(4, await SimulationBatch.RunAsync(_ => 4, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)));
    }

    static Tactic Load(string name) => TacticLoader.LoadFile(TacticLoaderTests.TacticPath(name));

    [Fact]
    public void OpponentPreparationCanStopInsideTheFirstMatch()
    {
        var player = Load("laag_blok_442");
        var history = Enumerable.Repeat(new HistoryEntry(player.Name, StyleProfile.From(player)), 3).ToArray();
        using var cts = new CancellationTokenSource();
        double reported = 0;
        Assert.ThrowsAny<OperationCanceledException>(() => OpponentPlanner.Plan(player,
            new[] { ("other", Load("hoge_druk_433")) }, "other", history, cts.Token,
            progress: value => { reported = value; if (value > 0) cts.Cancel(); }, maxParallel: 1));
        Assert.InRange(reported, double.Epsilon, 0.99);
    }

    [Fact]
    public void ChallengeCanStopInsideTheFirstMatchWithoutReturningPartialResults()
    {
        var player = Load("laag_blok_442");
        var spec = new ChallengeSpec("test", "Test", "", "laag_blok_442.json", 2, Array.Empty<ChallengeCriterion>(), "");
        using var cts = new CancellationTokenSource();
        double reported = 0;
        Assert.ThrowsAny<OperationCanceledException>(() => ChallengeEvaluator.Play(spec, player, player, cts.Token,
            progress: value => { reported = value; if (value > 0) cts.Cancel(); }, maxParallel: 1));
        Assert.InRange(reported, double.Epsilon, 0.99);
    }

    [Fact]
    public void WorkerBudgetDoesNotChangeChallengeResultsOrProgressOrdering()
    {
        var player = Load("laag_blok_442");
        var spec = new ChallengeSpec("test", "Test", "", "laag_blok_442.json", 2, Array.Empty<ChallengeCriterion>(), "");
        var serial = ChallengeEvaluator.Play(spec, player, player, maxParallel: 1);
        var progress = new List<double>();
        var limited = ChallengeEvaluator.Play(spec, player, player, progress: progress.Add);
        Assert.Equal(serial, limited);
        Assert.Equal(2, progress[^1]);
        Assert.True(progress.SequenceEqual(progress.OrderBy(v => v)));
    }
}
