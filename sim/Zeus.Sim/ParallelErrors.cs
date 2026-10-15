using System.Runtime.ExceptionServices;

namespace Zeus.Sim;

internal static class ParallelErrors
{
    /// <summary>
    /// Parallel.For that rethrows the original exception instead of an AggregateException, so callers can catch the real type.
    /// Cancellation stays an OperationCanceledException.
    /// </summary>
    public static void For(int count, ParallelOptions options, Action<int> body)
    {
        try { Parallel.For(0, count, options, body); }
        catch (AggregateException e)
        {
            var inner = e.Flatten().InnerExceptions;
            var real = inner.FirstOrDefault(x => x is not OperationCanceledException) ?? inner[0];
            ExceptionDispatchInfo.Capture(real).Throw();
            throw;
        }
    }
}
