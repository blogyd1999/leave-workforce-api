using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LeaveWorkforce.BuildingBlocks.UnitOfWork;

/// <summary>The connection and transaction owned by the runner, handed to the handler. Handlers never commit.</summary>
public interface IUnitOfWorkScope
{
    NpgsqlConnection Connection { get; }

    NpgsqlTransaction Transaction { get; }

    IAdvisoryLocks Locks { get; }

    /// <summary>Makes the DbContext use this scope's connection and transaction (EF Core UseTransactionAsync).</summary>
    Task EnlistAsync(DbContext context, CancellationToken cancellationToken);
}
