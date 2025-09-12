namespace SplitDuo.Api.Features.Common.Dto;

public class PaginatedResponseDto<T>
{
    public bool Success { get; set; } = true;
    public List<T> Data { get; set; } = [];
    public PaginationDto Pagination { get; set; } = new();
}

public class PaginationDto
{
    public int Page { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
    public int TotalPages { get; set; }
    public bool HasNext { get; set; }
    public bool HasPrev { get; set; }
}