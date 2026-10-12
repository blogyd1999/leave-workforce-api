using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace LeaveWorkforce.BuildingBlocks.UnitOfWork;

internal sealed class UnitOfWorkScope(NpgsqlConnection connection, NpgsqlTransaction transaction) : IUnitOfWorkScope, IAdvisoryLocks
{
    private readonly HashSet<AdvisoryLockKey> _held = [];
    private bool _ended;

    public NpgsqlConnection Connection => connection;

    public NpgsqlTransaction Transaction => transaction;

    public IAdvisoryLocks Locks => this;

    /// <summary>After this, the scope refuses locks and enlistment: its transaction is over.</summary>
    public void End() => _ended = true;

    public async Task EnlistAsync(DbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        EnsureActive();
        context.Database.SetDbConnection(connection);
        await context.Database.UseTransactionAsync(transaction, cancellationToken);
    }

    public async Task AcquireAsync(CancellationToken cancellationToken, params AdvisoryLockKey[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        EnsureActive();

        foreach (var key in keys.Distinct().Order())
        {
            if (_held.Contains(key))
            {
                continue;
            }
            if (_held.Count > 0 && key < _held.Max())
            {
                throw new InvalidOperationException(
                    $"Advisory lock {key} was requested after {_held.Max()}; locks must be taken in the fixed order.");
            }

            await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock($1)", connection, transaction);
            command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Bigint, Value = AdvisoryLockKeys.ToDatabaseKey(key) });
            await command.ExecuteNonQueryAsync(cancellationToken);
            _held.Add(key);
        }
    }

    private void EnsureActive()
    {
        if (_ended)
        {
            throw new InvalidOperationException("There is no active unit of work.");
        }
    }
}
