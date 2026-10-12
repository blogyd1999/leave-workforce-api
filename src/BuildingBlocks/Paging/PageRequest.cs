using LeaveWorkforce.BuildingBlocks.Results;

namespace LeaveWorkforce.BuildingBlocks.Paging;

/// <summary>AD-18 paging input: page defaults to 1, size to 25, size is capped at 100.</summary>
public sealed record PageRequest
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    private PageRequest(int page, int pageSize)
    {
        Page = page;
        PageSize = pageSize;
    }

    public int Page { get; }

    public int PageSize { get; }

    /// <summary>Rows to skip.</summary>
    public int Offset => checked((Page - 1) * PageSize);

    public static Result<PageRequest> Create(int? page, int? pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (page is < 1)
        {
            errors["page"] = ["page must be at least 1."];
        }
        if (pageSize is < 1)
        {
            errors["pageSize"] = ["pageSize must be at least 1."];
        }
        if (errors.Count > 0)
        {
            return Error.Validation(ErrorCodes.InvalidPaging, fieldErrors: errors);
        }

        var effectivePage = page ?? DefaultPage;
        var effectiveSize = Math.Min(pageSize ?? DefaultPageSize, MaxPageSize);
        if ((long)(effectivePage - 1) * effectiveSize > int.MaxValue)
        {
            // Offset is an int (EF Core Skip takes one); refuse pages beyond it instead of overflowing later.
            return Error.Validation(ErrorCodes.InvalidPaging,
                fieldErrors: new Dictionary<string, string[]> { ["page"] = ["page is too large."] });
        }

        return new PageRequest(effectivePage, effectiveSize);
    }
}
