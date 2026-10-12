using System.Collections.ObjectModel;

namespace LeaveWorkforce.BuildingBlocks.Results;

/// <summary>
/// An expected failure: a kind (which fixes the HTTP status), a stable machine-readable code,
/// optional caller-authored client-safe detail, and for Validation optional field errors.
/// </summary>
public sealed class Error
{
    private Error(ErrorKind kind, string code, string? detail, IReadOnlyDictionary<string, string[]>? fieldErrors)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Kind = kind;
        Code = code;
        Detail = detail;
        FieldErrors = fieldErrors;
    }

    public ErrorKind Kind { get; }

    public string Code { get; }

    /// <summary>Client-safe text written by the caller; never an exception message.</summary>
    public string? Detail { get; }

    /// <summary>Field to messages; only set for Validation errors.</summary>
    public IReadOnlyDictionary<string, string[]>? FieldErrors { get; }

    public static Error Unauthenticated(string code, string? detail = null) => new(ErrorKind.Unauthenticated, code, detail, null);

    public static Error Throttled(string code, string? detail = null) => new(ErrorKind.Throttled, code, detail, null);

    public static Error Forbidden(string code, string? detail = null) => new(ErrorKind.Forbidden, code, detail, null);

    public static Error NotFound(string code, string? detail = null) => new(ErrorKind.NotFound, code, detail, null);

    public static Error Conflict(string code, string? detail = null) => new(ErrorKind.Conflict, code, detail, null);

    public static Error Validation(string code, string? detail = null, IDictionary<string, string[]>? fieldErrors = null) =>
        new(ErrorKind.Validation, code, detail,
            fieldErrors is null
                ? null
                : new ReadOnlyDictionary<string, string[]>(fieldErrors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray())));
}
