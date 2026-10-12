namespace LeaveWorkforce.Concurrency.Tests.Infrastructure;

/// <summary>How one operation ended: with a value, with an exception, or by not finishing before the timeout.</summary>
public sealed record Outcome<T>
{
    private Outcome() { }

    public T? Value { get; private init; }

    public Exception? Exception { get; private init; }

    public bool TimedOut { get; private init; }

    public bool Succeeded => Exception is null && !TimedOut;

    public static Outcome<T> Success(T value) => new() { Value = value };

    public static Outcome<T> Failure(Exception exception) => new() { Exception = exception };

    public static Outcome<T> Timeout() => new() { TimedOut = true };
}

/// <summary>
/// Starts a set of operations together behind one start gate, waits for all of them and reports one
/// outcome per operation, in order. It never throws because an operation failed or hung.
/// </summary>
public static class ParallelRunner
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    // After the timeout cancels the operations, cooperative ones get this long to finish before they are reported as timed out.
    private static readonly TimeSpan CancellationGrace = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Runs the operations together and reports one outcome each. If the caller's token is cancelled, the operations are
    /// cancelled and the call throws <see cref="OperationCanceledException"/>; an operation that fails or hangs never does.
    /// </summary>
    public static async Task<IReadOnlyList<Outcome<T>>> RunAsync<T>(
        IReadOnlyList<Func<CancellationToken, Task<T>>> operations,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var limit = timeout ?? DefaultTimeout;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Each operation starts waiting on the gate as soon as it is created; none runs its body until all exist.
        var tasks = operations
            .Select(operation => Task.Run(async () =>
            {
                await gate.Task;
                return await operation(cts.Token);
            }, CancellationToken.None))
            .ToArray();
        var all = Task.WhenAll(tasks);

        gate.SetResult();

        // One timer decides: whatever has not finished when it fires is timed out, however it ends afterwards.
        var finished = await Task.WhenAny(all, Task.Delay(limit, cancellationToken));

        if (cancellationToken.IsCancellationRequested)
        {
            // The linked token has already cancelled the operations; give them a moment to stop, then propagate.
            await Task.WhenAny(all, Task.Delay(CancellationGrace, CancellationToken.None));
            throw new OperationCanceledException(cancellationToken);
        }

        var timedOut = new HashSet<int>();
        if (finished != all)
        {
            for (var i = 0; i < tasks.Length; i++)
            {
                if (!tasks[i].IsCompleted)
                {
                    timedOut.Add(i);
                }
            }
            await cts.CancelAsync();
            await Task.WhenAny(all, Task.Delay(CancellationGrace, CancellationToken.None));
        }

        return tasks.Select((task, index) => timedOut.Contains(index) ? Outcome<T>.Timeout() : ToOutcome(task)).ToArray();
    }

    // An operation that was cancelled on its own, before any timeout, has failed: report its exception.
    private static Outcome<T> ToOutcome<T>(Task<T> task) => task.Status switch
    {
        TaskStatus.RanToCompletion => Outcome<T>.Success(task.Result),
        TaskStatus.Faulted => Outcome<T>.Failure(task.Exception!.InnerException ?? task.Exception),
        TaskStatus.Canceled => Outcome<T>.Failure(new OperationCanceledException("The operation was cancelled before the timeout.")),
        _ => Outcome<T>.Timeout(),
    };
}
