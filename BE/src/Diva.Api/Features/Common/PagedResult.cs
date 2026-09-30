namespace Diva.Api.Features.Common;

/// <summary>One page of a longer list, plus what the frontend needs to draw page buttons.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Out-of-range paging values are clamped instead of rejected.</summary>
    public static (int Page, int PageSize) Normalize(int? page, int? pageSize) =>
        (Math.Max(page ?? 1, 1), Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));
}
