namespace EscolaSystemApi.Common;

public sealed record PagedQuery(
    int Page = 1,
    int PageSize = 20
)
{
    public int Skip => (Page - 1) * PageSize;
    public int Take => PageSize;
}