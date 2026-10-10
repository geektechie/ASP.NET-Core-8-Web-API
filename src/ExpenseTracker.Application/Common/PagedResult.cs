namespace ExpenseTracker.Application.Common;

/// <summary>A single page of results plus the metadata a client needs to page through the rest.</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> Empty(int page, int pageSize) => new([], page, pageSize, 0);

    public PagedResult<TOther> Map<TOther>(Func<T, TOther> selector) =>
        new(Items.Select(selector).ToList(), Page, PageSize, TotalCount);
}
