namespace LeaveWorkforce.BuildingBlocks.UnitOfWork;

/// <summary>Transaction-scoped advisory locks (pg_advisory_xact_lock), released when the unit of work ends.</summary>
public interface IAdvisoryLocks
{
    /// <summary>
    /// Takes the keys in the fixed order (Administrator, then Tree) whatever order they are passed in.
    /// Re-taking a held key is harmless. Throws <see cref="InvalidOperationException"/> when a key lower than
    /// one already held is requested, or when no unit of work is active.
    /// </summary>
    Task AcquireAsync(CancellationToken cancellationToken, params AdvisoryLockKey[] keys);
}
