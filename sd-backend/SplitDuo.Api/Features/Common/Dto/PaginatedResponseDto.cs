namespace SplitDuo.Api.Features.Common.Dto;

public class PaginatedResponseDto<T>
{
    public bool Success { get; set; } = true;
    public List<T> Data { get; set; } = [];
    public PaginationDto Pagination { get; set; } = new();
    public string? Message { get; set; }
    public ApiErrorDto? Error { get; set; }

    public static PaginatedResponseDto<T> SuccessResponse(List<T> data, PaginationDto pagination,
        string? message = null)
    {
        return new PaginatedResponseDto<T>
        {
            Success = true,
            Data = data,
            Pagination = pagination,
            Message = message
        };
    }

    public static PaginatedResponseDto<T> ErrorResponse(string code, string message, List<string>? details = null)
    {
        return new PaginatedResponseDto<T>
        {
            Success = false,
            Data = [],
            Pagination = new PaginationDto(),
            Error = new ApiErrorDto
            {
                Code = code,
                Message = message,
                Details = details
            }
        };
    }
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