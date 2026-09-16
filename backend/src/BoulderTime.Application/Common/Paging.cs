namespace BoulderTime.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public bool HasMore => Page * PageSize < Total;
}

public static class Paging
{
    public const int MaxPageSize = 50;

    public static (int Page, int PageSize) Normalize(int? page, int? pageSize, int defaultSize = 20) =>
        (Math.Max(1, page ?? 1), Math.Clamp(pageSize ?? defaultSize, 1, MaxPageSize));
}
