using System.Net;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Common.Services;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;

namespace SplitDuo.Api.Features.Common.Controllers;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    private IUserContextService? _userContextService;

    private IUserContextService UserContextService =>
        _userContextService ??= HttpContext.RequestServices.GetRequiredService<IUserContextService>();

    protected Guid? GetCurrentUserId() => UserContextService.GetCurrentUserId();

    protected Task<User?> GetCurrentUserAsync() => UserContextService.GetCurrentUserAsync();

    protected bool IsUserAuthenticated() => UserContextService.IsAuthenticated();

    protected GlobalRole? GetCurrentUserGlobalRole() => UserContextService.GetCurrentUserGlobalRole();

    protected bool IsCurrentUserSystemAdmin() => UserContextService.IsSystemAdmin();

    protected ActionResult<ApiResponseDto<T>> HandleResult<T>(Result<T> result, string? successMessage = null)
    {
        if (result.IsSuccess)
        {
            var response = ApiResponseDto<T>.SuccessResponse(result.Value!, successMessage);
            return result.StatusCode switch
            {
                HttpStatusCode.OK => Ok(response),
                HttpStatusCode.Created => Created(string.Empty, response),
                HttpStatusCode.NoContent => NoContent(),
                _ => StatusCode((int)result.StatusCode, response)
            };
        }

        var errorResponse = ApiResponseDto<T>.ErrorResponse(
            GetErrorCodeFromStatus(result.StatusCode),
            result.Error
        );

        return result.StatusCode switch
        {
            HttpStatusCode.BadRequest => BadRequest(errorResponse),
            HttpStatusCode.Unauthorized => Unauthorized(errorResponse),
            HttpStatusCode.Forbidden => StatusCode(403, errorResponse),
            HttpStatusCode.NotFound => NotFound(errorResponse),
            HttpStatusCode.Conflict => Conflict(errorResponse),
            HttpStatusCode.UnprocessableEntity => StatusCode(422, errorResponse),
            HttpStatusCode.InternalServerError => StatusCode(500, errorResponse),
            _ => StatusCode((int)result.StatusCode, errorResponse)
        };
    }

    protected ActionResult HandleResult(Result result, string? successMessage = null)
    {
        if (result.IsSuccess)
        {
            return result.StatusCode switch
            {
                HttpStatusCode.OK => Ok(new { Success = true, Message = successMessage }),
                HttpStatusCode.Created => Created(string.Empty, new { Success = true, Message = successMessage }),
                HttpStatusCode.NoContent => NoContent(),
                _ => StatusCode((int)result.StatusCode, new { Success = true, Message = successMessage })
            };
        }

        var errorResponse = new
        {
            Success = false,
            Error = new
            {
                Code = GetErrorCodeFromStatus(result.StatusCode),
                Message = result.Error
            }
        };

        return result.StatusCode switch
        {
            HttpStatusCode.BadRequest => BadRequest(errorResponse),
            HttpStatusCode.Unauthorized => Unauthorized(errorResponse),
            HttpStatusCode.Forbidden => StatusCode(403, errorResponse),
            HttpStatusCode.NotFound => NotFound(errorResponse),
            HttpStatusCode.Conflict => Conflict(errorResponse),
            HttpStatusCode.UnprocessableEntity => StatusCode(422, errorResponse),
            HttpStatusCode.InternalServerError => StatusCode(500, errorResponse),
            _ => StatusCode((int)result.StatusCode, errorResponse)
        };
    }

    private static string GetErrorCodeFromStatus(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => "BAD_REQUEST",
        HttpStatusCode.Unauthorized => "UNAUTHORIZED",
        HttpStatusCode.Forbidden => "FORBIDDEN",
        HttpStatusCode.NotFound => "NOT_FOUND",
        HttpStatusCode.Conflict => "CONFLICT",
        HttpStatusCode.UnprocessableEntity => "UNPROCESSABLE_ENTITY",
        HttpStatusCode.InternalServerError => "INTERNAL_SERVER_ERROR",
        _ => "ERROR"
    };

    protected ActionResult<PaginatedResponseDto<T>> HandlePaginatedResult<T>(Result<PaginatedResponseDto<T>> result,
        string? successMessage = null)
    {
        if (result.IsSuccess)
        {
            var response = result.Value!;
            if (!string.IsNullOrEmpty(successMessage))
                response.Message = successMessage;

            return result.StatusCode switch
            {
                HttpStatusCode.OK => Ok(response),
                HttpStatusCode.Created => Created(string.Empty, response),
                HttpStatusCode.NoContent => NoContent(),
                _ => StatusCode((int)result.StatusCode, response)
            };
        }

        var errorResponse = PaginatedResponseDto<T>.ErrorResponse(
            GetErrorCodeFromStatus(result.StatusCode),
            result.Error
        );

        return result.StatusCode switch
        {
            HttpStatusCode.BadRequest => BadRequest(errorResponse),
            HttpStatusCode.Unauthorized => Unauthorized(errorResponse),
            HttpStatusCode.Forbidden => StatusCode(403, errorResponse),
            HttpStatusCode.NotFound => NotFound(errorResponse),
            HttpStatusCode.Conflict => Conflict(errorResponse),
            HttpStatusCode.UnprocessableEntity => StatusCode(422, errorResponse),
            HttpStatusCode.InternalServerError => StatusCode(500, errorResponse),
            _ => StatusCode((int)result.StatusCode, errorResponse)
        };
    }
}