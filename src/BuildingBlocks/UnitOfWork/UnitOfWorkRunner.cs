using System.Data;
using System.Globalization;
using LeaveWorkforce.BuildingBlocks.Results;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LeaveWorkforce.BuildingBlocks.UnitOfWork;

/// <summary>
/// The pipeline's unit of work (AD-3, AD-22): opens one connection and one READ COMMITTED transaction with a
/// lock timeout, runs the handler, commits on success, rolls back on a failure Result or exception, and re-runs
/// the handler once in a fresh transaction on a transient database conflict.
/// </summary>
public sealed class UnitOfWorkRunner(NpgsqlDataSource dataSource, IOptions<UnitOfWorkOptions> options)
{
    private const int MaxAttempts = 2;

    public Task<Result> ExecuteAsync(
        Func<IUnitOfWorkScope, CancellationToken, Task<Result>> handler, CancellationToken cancellationToken = default) =>
        RunAsync(handler, Result.Failure, cancellationToken);

    public Task<Result<T>> ExecuteAsync<T>(
        Func<IUnitOfWorkScope, CancellationToken, Task<Result<T>>> handler, CancellationToken cancellationToken = default) =>
        RunAsync(handler, Result.Failure<T>, cancellationToken);

    private async Task<TResult> RunAsync<TResult>(
        Func<IUnitOfWorkScope, CancellationToken, Task<TResult>> handler,
        Func<Error, TResult> failure,
        CancellationToken cancellationToken) where TResult : Result
    {
        ArgumentNullException.ThrowIfNull(handler);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await RunOnceAsync(handler, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && TransientConflict.FindRetryableSqlState(ex) is { } sqlState)
            {
                if (attempt >= MaxAttempts)
                {
                    return failure(TransientConflict.ToError(sqlState));
                }
            }
        }
    }

    private async Task<TResult> RunOnceAsync<TResult>(
        Func<IUnitOfWorkScope, CancellationToken, Task<TResult>> handler, CancellationToken cancellationToken) where TResult : Result
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var scope = new UnitOfWorkScope(connection, transaction);
        try
        {
            await using (var command = new NpgsqlCommand("SELECT set_config('lock_timeout', $1, true)", connection, transaction))
            {
                var milliseconds = (long)options.Value.LockTimeout.TotalMilliseconds;
                command.Parameters.AddWithValue(milliseconds.ToString(CultureInfo.InvariantCulture) + "ms");
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            var result = await handler(scope, cancellationToken)
                ?? throw new InvalidOperationException("A handler must return a Result, not null.");
            if (result.IsSuccess)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            return result;
        }
        finally
        {
            scope.End();
        }
        // An exception leaves the transaction uncommitted; disposing it rolls back and releases every advisory lock.
    }
}
