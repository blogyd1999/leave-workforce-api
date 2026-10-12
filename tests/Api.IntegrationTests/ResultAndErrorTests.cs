using LeaveWorkforce.BuildingBlocks.Results;
using Xunit;

namespace LeaveWorkforce.Api.IntegrationTests;

public sealed class ResultAndErrorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Error_with_blank_code_is_refused(string? code)
    {
        Assert.ThrowsAny<ArgumentException>(() => Error.Conflict(code!));
        Assert.ThrowsAny<ArgumentException>(() => Error.Validation(code!));
    }

    [Fact]
    public void Factories_set_kind_and_code()
    {
        Assert.Equal(ErrorKind.Unauthenticated, Error.Unauthenticated("a").Kind);
        Assert.Equal(ErrorKind.Throttled, Error.Throttled("a").Kind);
        Assert.Equal(ErrorKind.Forbidden, Error.Forbidden("a").Kind);
        Assert.Equal(ErrorKind.NotFound, Error.NotFound("a").Kind);
        Assert.Equal(ErrorKind.Validation, Error.Validation("a").Kind);
        var conflict = Error.Conflict(ErrorCodes.LockTimeout);
        Assert.Equal(ErrorKind.Conflict, conflict.Kind);
        Assert.Equal("lock_timeout", conflict.Code);
    }

    [Fact]
    public void Successful_result_has_no_error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Successful_generic_result_exposes_value()
    {
        var result = Result.Success(42);

        Assert.Equal(42, result.Value);
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Failed_result_exposes_error_but_not_value()
    {
        var error = Error.NotFound("thing_missing");
        Result<int> result = error;

        Assert.True(result.IsFailure);
        Assert.Same(error, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Validation_field_errors_are_copied_at_construction()
    {
        var messages = new[] { "required" };
        var error = Error.Validation("invalid_input", fieldErrors: new Dictionary<string, string[]> { ["reason"] = messages });

        messages[0] = "changed";

        Assert.Equal("required", error.FieldErrors!["reason"][0]);
    }
}
