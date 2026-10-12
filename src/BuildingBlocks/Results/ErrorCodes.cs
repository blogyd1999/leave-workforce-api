namespace LeaveWorkforce.BuildingBlocks.Results;

/// <summary>Shared stable machine-readable error codes.</summary>
public static class ErrorCodes
{
    public const string InternalError = "internal_error";
    public const string LockTimeout = "lock_timeout";
    public const string ConcurrencyConflict = "concurrency_conflict";
    public const string InvalidPaging = "invalid_paging";
}
