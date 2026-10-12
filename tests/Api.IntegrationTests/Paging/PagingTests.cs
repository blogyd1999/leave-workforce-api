using System.Text.Json;
using LeaveWorkforce.BuildingBlocks.Http;
using LeaveWorkforce.BuildingBlocks.Paging;
using LeaveWorkforce.BuildingBlocks.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;

namespace LeaveWorkforce.Api.IntegrationTests.Paging;

public sealed class PagingTests
{
    [Fact]
    public void Defaults_are_page_1_size_25()
    {
        var request = PageRequest.Create(null, null).Value;
        Assert.Equal(1, request.Page);
        Assert.Equal(25, request.PageSize);
        Assert.Equal(0, request.Offset);
    }

    [Fact]
    public void Size_is_capped_at_100()
    {
        var request = PageRequest.Create(3, 500).Value;
        Assert.Equal(100, request.PageSize);
        Assert.Equal(200, request.Offset);
    }

    [Theory]
    [InlineData(0, null, "page")]
    [InlineData(null, 0, "pageSize")]
    [InlineData(-1, -5, "page")]
    public void Page_or_size_below_1_is_a_validation_error_mapped_to_400(int? page, int? size, string field)
    {
        var result = PageRequest.Create(page, size);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        Assert.Contains(field, result.Error.FieldErrors!.Keys);
        var problem = Assert.IsType<ProblemHttpResult>(result.Error.ToProblem());
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public void Paged_serialises_as_items_and_total()
    {
        var json = JsonSerializer.Serialize(new Paged<int>([1, 2], 7), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("""{"items":[1,2],"total":7}""", json);
    }

    [Fact]
    public void A_page_whose_offset_would_overflow_is_a_validation_error_not_an_exception()
    {
        var result = PageRequest.Create(int.MaxValue, 100);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        Assert.Contains("page", result.Error.FieldErrors!.Keys);
        Assert.Equal(200, PageRequest.Create(3, 100).Value.Offset);
    }
}
