namespace EscolaSystemApi.Common;

public sealed record PagedQuery(
    int Page = 1,
    int PageSize = 20
)
{
    public const int MaxPageSize = 500;

    public int Page { get; init; } = Page < 1 ? 1 : Page;
    public int PageSize { get; init; } = PageSize < 1 ? 20 : Math.Min(PageSize, MaxPageSize);

    public int Skip => (Page - 1) * PageSize;
    public int Take => PageSize;
}
