using System.Net;

namespace SplitDuo.Core.Common;

public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; private set; }
    public string Error { get; private set; } = "";
    public HttpStatusCode StatusCode { get; private set; } = HttpStatusCode.OK;

    private Result(T value, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        IsSuccess = true;
        Value = value;
        StatusCode = statusCode;
    }

    private Result(string error, HttpStatusCode statusCode = HttpStatusCode.BadRequest)
    {
        IsSuccess = false;
        Error = error;
        StatusCode = statusCode;
    }

    public static Result<T> Success(T value, HttpStatusCode statusCode = HttpStatusCode.OK) => new(value, statusCode);

    // Common failure shortcuts
    public static Result<T> BadRequest(string error = "Bad Request") => new(error);
    public static Result<T> NotFound(string error = "Resource not found") => new(error, HttpStatusCode.NotFound);

    public static Result<T> Unauthorized(string error = "Unauthorized access") =>
        new(error, HttpStatusCode.Unauthorized);

    public static Result<T> Forbidden(string error = "Forbidden access") => new(error, HttpStatusCode.Forbidden);
    public static Result<T> Conflict(string error = "Resource conflict") => new(error, HttpStatusCode.Conflict);

    public static Result<T> UnprocessableEntity(string error = "Unprocessable entity") =>
        new(error, HttpStatusCode.UnprocessableEntity);

    public static Result<T> InternalServerError(string error = "Internal server error") =>
        new(error, HttpStatusCode.InternalServerError);

    public static Result<T> ServiceUnavailable(string error = "Service unavailable") =>
        new(error, HttpStatusCode.ServiceUnavailable);
}

public class Result
{
    public bool IsSuccess { get; private set; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; private set; } = "";
    public HttpStatusCode StatusCode { get; private set; }

    private Result(bool isSuccess, string error = "", HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        IsSuccess = isSuccess;
        Error = error;
        StatusCode = isSuccess
            ? statusCode
            : (statusCode == HttpStatusCode.OK ? HttpStatusCode.BadRequest : statusCode);
    }

    public static Result Success(HttpStatusCode statusCode = HttpStatusCode.OK) => new(true, "", statusCode);


    // Common failure shortcuts
    public static Result BadRequest(string error = "Bad Request") => new(false, error, HttpStatusCode.BadRequest);

    public static Result NotFound(string error = "Resource not found") => new(false, error, HttpStatusCode.NotFound);

    public static Result Unauthorized(string error = "Unauthorized access") =>
        new(false, error, HttpStatusCode.Unauthorized);

    public static Result Forbidden(string error = "Forbidden access") => new(false, error, HttpStatusCode.Forbidden);
    public static Result Conflict(string error = "Resource conflict") => new(false, error, HttpStatusCode.Conflict);

    public static Result UnprocessableEntity(string error = "Unprocessable entity") =>
        new(false, error, HttpStatusCode.UnprocessableEntity);

    public static Result InternalServerError(string error = "Internal server error") =>
        new(false, error, HttpStatusCode.InternalServerError);

    public static Result ServiceUnavailable(string error = "Service Unavailable") =>
        new(false, error, HttpStatusCode.ServiceUnavailable);
}