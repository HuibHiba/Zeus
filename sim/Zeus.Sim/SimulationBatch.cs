namespace Zeus.Sim;

/// <summary>Conservative defaults for interactive batches; leave CPU capacity for the game window.</summary>
public static class SimulationBatch
{
    static readonly SemaphoreSlim InteractiveSlot = new(1, 1);

    public static int WorkerCount(int jobs, int processors, int requested = 0) =>
        Math.Max(1, Math.Min(Math.Min(jobs, Math.Max(1, processors - 1)), requested > 0 ? requested : 2));

    /// <summary>Only one interactive batch at once, including while an old scene's cancellation unwinds.</summary>
    public static Task<T> RunAsync<T>(Func<CancellationToken, T> work, CancellationToken token) => Task.Run(async () =>
    {
        await InteractiveSlot.WaitAsync(token).ConfigureAwait(false);
        try { token.ThrowIfCancellationRequested(); return work(token); }
        finally { InteractiveSlot.Release(); }
    }, token);

    /// <summary>Cancellation is immediate; dispose and observe errors only after the worker releases its token.</summary>
    public static void CancelAndDispose(CancellationTokenSource source, Task? work)
    {
        source.Cancel();
        if (work is null) { source.Dispose(); return; }
        _ = work.ContinueWith(finished =>
        {
            _ = finished.Exception;
            source.Dispose();
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
