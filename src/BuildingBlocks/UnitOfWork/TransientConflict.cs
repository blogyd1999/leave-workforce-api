using LeaveWorkforce.BuildingBlocks.Results;
using Npgsql;

namespace LeaveWorkforce.BuildingBlocks.UnitOfWork;

/// <summary>AD-22: which database failures are retried once, and how they are reported after the retry.</summary>
internal static class TransientConflict
{
    private const string LockNotAvailable = "55P03";

    private static readonly HashSet<string> RetryableSqlStates = ["23505", "23P01", "40001", "40P01", LockNotAvailable];

    /// <summary>The first listed SQLSTATE found anywhere in the exception chain, or null.</summary>
    public static string? FindRetryableSqlState(Exception exception)
    {
        var seen = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<Exception>();
        pending.Push(exception);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!seen.Add(current))
            {
                continue;
            }
            if (current is PostgresException { SqlState: { } state } && RetryableSqlStates.Contains(state))
            {
                return state;
            }
            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    pending.Push(inner);
                }
            }
            else if (current.InnerException is { } next)
            {
                pending.Push(next);
            }
        }
        return null;
    }

    /// <summary>A Conflict with no database detail.</summary>
    public static Error ToError(string sqlState) =>
        Error.Conflict(sqlState == LockNotAvailable ? ErrorCodes.LockTimeout : ErrorCodes.ConcurrencyConflict);
}
