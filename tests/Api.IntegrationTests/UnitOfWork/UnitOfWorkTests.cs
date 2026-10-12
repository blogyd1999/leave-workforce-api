using LeaveWorkforce.Api.IntegrationTests.Infrastructure;
using LeaveWorkforce.BuildingBlocks.Http;
using LeaveWorkforce.BuildingBlocks.Results;
using LeaveWorkforce.BuildingBlocks.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace LeaveWorkforce.Api.IntegrationTests.UnitOfWork;

public sealed class ScratchRow
{
    public Guid Id { get; set; }
}

public sealed class ScratchContext(DbContextOptions<ScratchContext> options) : DbContext(options)
{
    public DbSet<ScratchRow> Rows => Set<ScratchRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<ScratchRow>(e =>
        {
            e.ToTable("uow_scratch", "leave");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasColumnName("id");
        });
}

[Collection(PostgresCollection.Name)]
public sealed class UnitOfWorkTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private NpgsqlDataSource _dataSource = null!;

    public async ValueTask InitializeAsync()
    {
        await RunSqlAsync(PostgresFixture.Migrator,
            "DROP TABLE IF EXISTS leave.uow_scratch; CREATE TABLE leave.uow_scratch (id uuid PRIMARY KEY)");
        _dataSource = NpgsqlDataSource.Create(postgres.ConnectionString(PostgresFixture.AppRw));
    }

    public async ValueTask DisposeAsync()
    {
        await _dataSource.DisposeAsync();
        await RunSqlAsync(PostgresFixture.Migrator, "DROP TABLE IF EXISTS leave.uow_scratch");
    }

    private UnitOfWorkRunner Runner(TimeSpan? lockTimeout = null) =>
        new(_dataSource, Options.Create(new UnitOfWorkOptions { LockTimeout = lockTimeout ?? TimeSpan.FromSeconds(5) }));

    private async Task RunSqlAsync(string role, string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString(role));
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private async Task<long> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }

    private Task<long> RowCountAsync() => ScalarAsync("SELECT count(*) FROM leave.uow_scratch");

    private async Task<Guid> InsertAsync(IUnitOfWorkScope scope, Guid? id = null)
    {
        await using var context = new ScratchContext(new DbContextOptionsBuilder<ScratchContext>().UseNpgsql(postgres.ConnectionString(PostgresFixture.AppRw)).Options);
        await scope.EnlistAsync(context, Ct);
        var row = new ScratchRow { Id = id ?? Guid.NewGuid() };
        context.Rows.Add(row);
        await context.SaveChangesAsync(Ct);
        return row.Id;
    }

    // Advisory locks vanish with the session; poll briefly because a cancelled backend may need a moment to exit.
    private async Task AssertNoAdvisoryLocksAsync()
    {
        long count = -1;
        for (var i = 0; i < 50; i++)
        {
            count = await ScalarAsync("SELECT count(*) FROM pg_locks WHERE locktype = 'advisory'");
            if (count == 0)
            {
                return;
            }
            await Task.Delay(100, Ct);
        }
        Assert.Equal(0, count);
    }

    private static PostgresException Pg(string sqlState) =>
        new("test", "ERROR", "ERROR", sqlState);

    [Fact]
    public async Task Success_commits_the_write()
    {
        var result = await Runner().ExecuteAsync(async (scope, _) =>
        {
            await InsertAsync(scope);
            return Result.Success();
        }, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await RowCountAsync());
    }

    [Fact]
    public async Task Failure_result_rolls_back_and_is_returned()
    {
        var error = Error.NotFound("thing_missing");

        var result = await Runner().ExecuteAsync(async (scope, _) =>
        {
            await InsertAsync(scope);
            return Result.Failure(error);
        }, Ct);

        Assert.Same(error, result.Error);
        Assert.Equal(0, await RowCountAsync());
    }

    [Fact]
    public async Task Typed_results_commit_and_roll_back()
    {
        var ok = await Runner().ExecuteAsync(async (scope, _) => Result.Success(await InsertAsync(scope)), Ct);
        Assert.True(ok.IsSuccess);
        Assert.Equal(1, await RowCountAsync());

        var failed = await Runner().ExecuteAsync<Guid>(async (scope, _) =>
        {
            await InsertAsync(scope);
            return Error.Conflict("nope");
        }, Ct);
        Assert.True(failed.IsFailure);
        Assert.Equal(1, await RowCountAsync());
    }

    [Fact]
    public async Task Non_retryable_exception_rolls_back_propagates_and_is_not_retried()
    {
        var calls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Runner().ExecuteAsync(async (scope, _) =>
        {
            calls++;
            await InsertAsync(scope);
            throw new InvalidOperationException("boom");
#pragma warning disable CS0162
            return Result.Success();
#pragma warning restore CS0162
        }, Ct));

        Assert.Equal(1, calls);
        Assert.Equal(0, await RowCountAsync());
    }

    [Theory]
    [InlineData("23505")]
    [InlineData("23P01")]
    [InlineData("40001")]
    [InlineData("40P01")]
    [InlineData("55P03")]
    public async Task Listed_sqlstate_on_first_attempt_runs_the_handler_again_in_a_fresh_transaction(string sqlState)
    {
        var calls = 0;

        var result = await Runner().ExecuteAsync(async (scope, _) =>
        {
            calls++;
            await InsertAsync(scope, Guid.Parse("00000000-0000-0000-0000-000000000001"));
            if (calls == 1)
            {
                // wrapped as EF Core wraps database failures
                throw new DbUpdateException("save failed", Pg(sqlState));
            }
            return Result.Success();
        }, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, calls);
        Assert.Equal(1, await RowCountAsync());
    }

    [Fact]
    public async Task A_real_unique_violation_is_found_in_the_chain_and_retried_once()
    {
        var existing = Guid.NewGuid();
        await RunSqlAsync(PostgresFixture.AppRw, $"INSERT INTO leave.uow_scratch VALUES ('{existing}')");
        var calls = 0;

        var result = await Runner().ExecuteAsync(async (scope, _) =>
        {
            calls++;
            await InsertAsync(scope, existing);
            return Result.Success();
        }, Ct);

        Assert.Equal(2, calls);
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.Error.Code);
        Assert.Equal(1, await RowCountAsync());
    }

    [Theory]
    [InlineData("55P03", ErrorCodes.LockTimeout)]
    [InlineData("23505", ErrorCodes.ConcurrencyConflict)]
    [InlineData("40001", ErrorCodes.ConcurrencyConflict)]
    [InlineData("40P01", ErrorCodes.ConcurrencyConflict)]
    [InlineData("23P01", ErrorCodes.ConcurrencyConflict)]
    public async Task Listed_sqlstate_twice_returns_a_conflict_without_database_detail(string sqlState, string code)
    {
        var calls = 0;

        var result = await Runner().ExecuteAsync((_, _) =>
        {
            calls++;
            throw Pg(sqlState);
#pragma warning disable CS0162
            return Task.FromResult(Result.Success());
#pragma warning restore CS0162
        }, Ct);

        Assert.Equal(2, calls);
        Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
        Assert.Equal(code, result.Error.Code);
        Assert.Null(result.Error.Detail);
        var problem = Assert.IsType<ProblemHttpResult>(result.Error.ToProblem());
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    [Fact]
    public async Task Two_operations_on_one_advisory_key_never_overlap_and_leave_no_locks()
    {
        var inside = 0;
        var overlapped = false;

        Task<Result> Operation() => Runner().ExecuteAsync(async (scope, ct) =>
        {
            await scope.Locks.AcquireAsync(ct, AdvisoryLockKey.Tree);
            if (Interlocked.Increment(ref inside) > 1)
            {
                overlapped = true;
            }
            await Task.Delay(400, ct);
            Interlocked.Decrement(ref inside);
            return Result.Success();
        }, Ct);

        var results = await Task.WhenAll(Operation(), Operation());

        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.False(overlapped);
        await AssertNoAdvisoryLocksAsync();
    }

    [Fact]
    public async Task Waiting_past_the_lock_timeout_takes_the_retry_path_then_reports_lock_timeout()
    {
        var holderHasLock = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var holder = Runner().ExecuteAsync(async (scope, ct) =>
        {
            await scope.Locks.AcquireAsync(ct, AdvisoryLockKey.Administrator);
            holderHasLock.SetResult();
            await release.Task.WaitAsync(ct);
            return Result.Success();
        }, Ct);
        await holderHasLock.Task.WaitAsync(Ct);

        var attempts = 0;
        var waiter = await Runner(TimeSpan.FromMilliseconds(300)).ExecuteAsync(async (scope, ct) =>
        {
            attempts++;
            await scope.Locks.AcquireAsync(ct, AdvisoryLockKey.Administrator);
            return Result.Success();
        }, Ct);

        release.SetResult();
        await holder;

        Assert.Equal(2, attempts);
        Assert.Equal(ErrorCodes.LockTimeout, waiter.Error.Code);
        await AssertNoAdvisoryLocksAsync();
    }

    [Fact]
    public async Task Locks_are_taken_in_order_and_a_held_key_can_be_taken_again()
    {
        var result = await Runner().ExecuteAsync(async (scope, ct) =>
        {
            await scope.Locks.AcquireAsync(ct, AdvisoryLockKey.Tree, AdvisoryLockKey.Administrator); // sorted into order
            await scope.Locks.AcquireAsync(ct, AdvisoryLockKey.Administrator);
            await scope.Locks.AcquireAsync(ct, AdvisoryLockKey.Tree);
            await scope.Locks.AcquireAsync(ct, AdvisoryLockKey.Administrator, AdvisoryLockKey.Tree);
            return Result.Success();
        }, Ct);

        Assert.True(result.IsSuccess);
        await AssertNoAdvisoryLocksAsync();
    }

    [Fact]
    public async Task Administrator_after_tree_is_refused()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Runner().ExecuteAsync(async (scope, ct) =>
        {
            await scope.Locks.AcquireAsync(ct, AdvisoryLockKey.Tree);
            await scope.Locks.AcquireAsync(ct, AdvisoryLockKey.Administrator);
            return Result.Success();
        }, Ct));

        await AssertNoAdvisoryLocksAsync();
    }

    [Fact]
    public async Task A_lock_requested_with_no_active_unit_of_work_is_refused()
    {
        IUnitOfWorkScope? captured = null;
        await Runner().ExecuteAsync((scope, _) =>
        {
            captured = scope;
            return Task.FromResult(Result.Success());
        }, Ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => captured!.Locks.AcquireAsync(Ct, AdvisoryLockKey.Tree));
        await AssertNoAdvisoryLocksAsync();
    }

    [Fact]
    public async Task Cancelling_while_waiting_for_a_lock_cancels_the_operation_and_leaves_no_lock_behind()
    {
        var holderHasLock = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var holder = Runner().ExecuteAsync(async (scope, ct) =>
        {
            await scope.Locks.AcquireAsync(ct, AdvisoryLockKey.Tree);
            holderHasLock.SetResult();
            await release.Task.WaitAsync(ct);
            return Result.Success();
        }, Ct);
        await holderHasLock.Task.WaitAsync(Ct);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var waiting = Runner().ExecuteAsync(async (scope, ct) =>
        {
            await scope.Locks.AcquireAsync(ct, AdvisoryLockKey.Tree);
            return Result.Success();
        }, cts.Token);
        await Task.Delay(300, Ct);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);

        release.SetResult();
        await holder;
        await AssertNoAdvisoryLocksAsync();
    }

    [Fact]
    public async Task Disposing_after_an_exception_leaves_no_advisory_lock()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Runner().ExecuteAsync(async (scope, ct) =>
        {
            await scope.Locks.AcquireAsync(ct, AdvisoryLockKey.Administrator, AdvisoryLockKey.Tree);
            throw new InvalidOperationException("boom");
#pragma warning disable CS0162
            return Result.Success();
#pragma warning restore CS0162
        }, Ct));

        await AssertNoAdvisoryLocksAsync();
    }

    [Fact]
    public async Task The_transaction_is_read_committed_with_the_configured_lock_timeout()
    {
        string? isolation = null, timeout = null;
        await Runner(TimeSpan.FromMilliseconds(1234)).ExecuteAsync(async (scope, ct) =>
        {
            await using var command = new NpgsqlCommand("SHOW transaction_isolation", scope.Connection, scope.Transaction);
            isolation = (string?)await command.ExecuteScalarAsync(ct);
            await using var second = new NpgsqlCommand("SHOW lock_timeout", scope.Connection, scope.Transaction);
            timeout = (string?)await second.ExecuteScalarAsync(ct);
            return Result.Success();
        }, Ct);

        Assert.Equal("read committed", isolation);
        Assert.Equal("1234ms", timeout);
    }

    [Fact]
    public async Task A_handler_that_returns_null_is_refused_not_retried()
    {
        var calls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Runner().ExecuteAsync(
            (_, _) =>
            {
                calls++;
                return Task.FromResult<Result>(null!);
            }, Ct));

        Assert.Equal(1, calls);
    }
}
