namespace SplitDuo.Api.Features.Common.Dto;

public class ApiResponseDto<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public ApiErrorDto? Error { get; set; }

    public static ApiResponseDto<T> SuccessResponse(T data, string? message = null)
    {
        return new ApiResponseDto<T>
        {
            Success = true,
            Data = data,
            Message = message
        };
    }

    public static ApiResponseDto<T> ErrorResponse(string code, string message, List<string>? details = null)
    {
        return new ApiResponseDto<T>
        {
            Success = false,
            Error = new ApiErrorDto
            {
                Code = code,
                Message = message,
                Details = details
            }
        };
    }
}

public class ApiErrorDto
{
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public List<string>? Details { get; set; }
}