namespace LeaveWorkforce.BuildingBlocks.Results;

/// <summary>The six error kinds of AD-7, each mapped to a SEC-11 status code.</summary>
public enum ErrorKind
{
    Unauthenticated,
    Throttled,
    Forbidden,
    NotFound,
    Validation,
    Conflict,
}
