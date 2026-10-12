namespace LeaveWorkforce.BuildingBlocks.Results;

public class Result
{
    private readonly Error? _error;

    protected Result(Error? error) => _error = error;

    public bool IsSuccess => _error is null;

    public bool IsFailure => !IsSuccess;

    /// <summary>The failure. Throws <see cref="InvalidOperationException"/> on a successful result.</summary>
    public Error Error => _error ?? throw new InvalidOperationException("A successful result has no error.");

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error ?? throw new ArgumentNullException(nameof(error)));

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T? value, Error? error) : base(error) => _value = value;

    /// <summary>The value. Throws <see cref="InvalidOperationException"/> on a failed result.</summary>
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("A failed result has no value.");

    public static Result<T> Success(T value) => new(value, null);

    public static new Result<T> Failure(Error error) => new(default, error ?? throw new ArgumentNullException(nameof(error)));

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
