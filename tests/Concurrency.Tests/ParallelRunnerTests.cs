using LeaveWorkforce.Concurrency.Tests.Infrastructure;
using Xunit;

namespace LeaveWorkforce.Concurrency.Tests;

public sealed class ParallelRunnerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task No_operation_runs_its_body_before_all_are_created()
    {
        // The list hands out its elements slowly, so without a start gate the first body would run long before the last
        // operation exists. With the gate, every body starts after the last element was handed over.
        var operations = new SlowList<DateTime>(4, TimeSpan.FromMilliseconds(150), _ => Task.FromResult(DateTime.UtcNow));

        var outcomes = await ParallelRunner.RunAsync(operations, cancellationToken: Ct);

        Assert.All(outcomes, o => Assert.True(o.Succeeded));
        Assert.All(outcomes, o => Assert.True(o.Value >= operations.LastHandedOverUtc,
            "A body started before the last operation was created."));
    }

    [Fact]
    public async Task All_operations_are_released_together()
    {
        // Each body waits for the other, so this only completes if both run at the same time.
        var arrived = new CountdownEvent(2);
        Func<CancellationToken, Task<string>> Meet(string name) => async token =>
        {
            arrived.Signal();
            await Task.Run(() => arrived.Wait(token), token);
            return name;
        };

        var outcomes = await ParallelRunner.RunAsync([Meet("a"), Meet("b")], TimeSpan.FromSeconds(10), Ct);

        Assert.Equal(["a", "b"], outcomes.Select(o => o.Value));
    }

    [Fact]
    public async Task Both_outcomes_are_returned_in_order()
    {
        var outcomes = await ParallelRunner.RunAsync<int>(
            [async _ => { await Task.Delay(100, Ct); return 1; }, _ => Task.FromResult(2)],
            cancellationToken: Ct);

        Assert.Equal(2, outcomes.Count);
        Assert.Equal(1, outcomes[0].Value);
        Assert.Equal(2, outcomes[1].Value);
        Assert.All(outcomes, o => Assert.True(o.Succeeded));
    }

    [Fact]
    public async Task A_failure_is_reported_without_hiding_the_other_outcome()
    {
        var outcomes = await ParallelRunner.RunAsync<int>(
            [_ => throw new InvalidOperationException("boom"), _ => Task.FromResult(7)],
            cancellationToken: Ct);

        Assert.False(outcomes[0].Succeeded);
        Assert.False(outcomes[0].TimedOut);
        Assert.IsType<InvalidOperationException>(outcomes[0].Exception);
        Assert.Equal("boom", outcomes[0].Exception!.Message);
        Assert.True(outcomes[1].Succeeded);
        Assert.Equal(7, outcomes[1].Value);
    }

    [Fact]
    public async Task An_operation_that_never_finishes_is_cancelled_and_reported_as_timed_out()
    {
        CancellationToken seen = default;
        var outcomes = await ParallelRunner.RunAsync<int>(
            [
                async token => { seen = token; await Task.Delay(Timeout.Infinite, token); return 0; },
                _ => Task.FromResult(3),
            ],
            TimeSpan.FromMilliseconds(300),
            Ct);

        Assert.True(outcomes[0].TimedOut);
        Assert.False(outcomes[0].Succeeded);
        Assert.True(seen.IsCancellationRequested);
        Assert.True(outcomes[1].Succeeded);
        Assert.Equal(3, outcomes[1].Value);
    }

    [Fact]
    public async Task An_operation_that_ignores_cancellation_is_still_reported_as_timed_out()
    {
        var never = new TaskCompletionSource<int>();
        var outcomes = await ParallelRunner.RunAsync<int>(
            [_ => never.Task],
            TimeSpan.FromMilliseconds(200),
            Ct);

        Assert.True(outcomes[0].TimedOut);
    }

    [Fact]
    public async Task An_operation_that_cancels_itself_before_the_timeout_is_reported_as_a_failure()
    {
        var outcomes = await ParallelRunner.RunAsync<int>(
            [_ => throw new OperationCanceledException("own"), _ => Task.FromResult(1)],
            cancellationToken: Ct);

        Assert.False(outcomes[0].TimedOut);
        Assert.False(outcomes[0].Succeeded);
        Assert.IsAssignableFrom<OperationCanceledException>(outcomes[0].Exception);
        Assert.True(outcomes[1].Succeeded);
    }

    [Fact]
    public async Task An_operation_that_fails_because_of_the_timeout_cancellation_is_still_reported_as_timed_out()
    {
        var outcomes = await ParallelRunner.RunAsync<int>(
            [async token =>
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                    return 0;
                }
                catch (OperationCanceledException)
                {
                    throw new InvalidOperationException("wrapped by the operation after cancellation");
                }
            }],
            TimeSpan.FromMilliseconds(200),
            Ct);

        Assert.True(outcomes[0].TimedOut);
        Assert.Null(outcomes[0].Exception);
    }

    [Fact]
    public async Task Cancelling_the_caller_token_cancels_the_operations_and_throws()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        CancellationToken seen = default;
        var running = RunAndCancelAsync();

        async Task RunAndCancelAsync()
        {
            var started = new TaskCompletionSource();
            var task = ParallelRunner.RunAsync<int>(
                [async token => { seen = token; started.SetResult(); await Task.Delay(Timeout.Infinite, token); return 0; }],
                TimeSpan.FromSeconds(30),
                cts.Token);
            await started.Task;
            await cts.CancelAsync();
            await task;
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.True(seen.IsCancellationRequested);
    }

    [Fact]
    public async Task Missing_operations_are_refused()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => ParallelRunner.RunAsync<int>(null!, cancellationToken: Ct));
    }

    // A list that sleeps before handing out each element and remembers when it handed out the last one.
    private sealed class SlowList<T>(int count, TimeSpan delay, Func<CancellationToken, Task<T>> operation)
        : IReadOnlyList<Func<CancellationToken, Task<T>>>
    {
        public DateTime LastHandedOverUtc { get; private set; }

        public int Count => count;

        public Func<CancellationToken, Task<T>> this[int index] => HandOver();

        public IEnumerator<Func<CancellationToken, Task<T>>> GetEnumerator()
        {
            for (var i = 0; i < count; i++)
            {
                yield return HandOver();
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        private Func<CancellationToken, Task<T>> HandOver()
        {
            Thread.Sleep(delay);
            LastHandedOverUtc = DateTime.UtcNow;
            return operation;
        }
    }
}
