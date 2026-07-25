using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Api.Features.Expenses.Services;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Expenses.Controllers;

[ApiController]
[Route("api/v1/groups/{groupId}/balances")]
[Authorize]
public class BalancesController(
    IBalancesService balancesService,
    IUnitOfWork unitOfWork) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult> GetBalances(string groupId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return Unauthorized();

        // Branch on alias mode: for alias-mode groups, return AliasBalanceDto list;
        // for individual-mode groups, return the existing BalanceDto list.
        // The response shape differs: alias-mode returns { aliasId, aliasName, balance, ... }
        // while individual-mode returns { userId, user, balance, ... }.
        // Frontend (Task 6) must check group.UseAliases to determine which shape to expect.
        if (!Guid.TryParse(groupId, out var groupGuid))
            return BadRequest();

        var group = await unitOfWork.Groups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return NotFound();

        if (group.UseAliases)
        {
            var result = await balancesService.GetAliasBalancesAsync(groupId, currentUserId.Value);
            if (result.IsSuccess)
            {
                var response = ApiResponseDto<List<AliasBalanceDto>>.SuccessResponse(
                    result.Value!, "Alias balances retrieved successfully");
                return Ok(response);
            }

            var errorResponse = ApiResponseDto<List<AliasBalanceDto>>.ErrorResponse(
                GetErrorCodeFromStatus(result.StatusCode), result.Error);
            return StatusCode((int)result.StatusCode, errorResponse);
        }
        else
        {
            var result = await balancesService.GetBalancesAsync(groupId, currentUserId.Value);
            if (result.IsSuccess)
            {
                var response = ApiResponseDto<List<BalanceDto>>.SuccessResponse(
                    result.Value!, "Balances retrieved successfully");
                return Ok(response);
            }

            var errorResponse = ApiResponseDto<List<BalanceDto>>.ErrorResponse(
                GetErrorCodeFromStatus(result.StatusCode), result.Error);
            return StatusCode((int)result.StatusCode, errorResponse);
        }
    }

    [HttpGet("summary")]
    public async Task<ActionResult> GetBalanceSummary(string groupId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return Unauthorized();

        // Branch on alias mode: for alias-mode groups, return AliasBalanceSummaryDto;
        // for individual-mode groups, return the existing BalanceSummaryDto.
        if (!Guid.TryParse(groupId, out var groupGuid))
            return BadRequest();

        var group = await unitOfWork.Groups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return NotFound();

        if (group.UseAliases)
        {
            var result = await balancesService.GetAliasBalanceSummaryAsync(groupId, currentUserId.Value);
            if (result.IsSuccess)
            {
                var response = ApiResponseDto<AliasBalanceSummaryDto>.SuccessResponse(
                    result.Value!, "Alias balance summary retrieved successfully");
                return Ok(response);
            }

            var errorResponse = ApiResponseDto<AliasBalanceSummaryDto>.ErrorResponse(
                GetErrorCodeFromStatus(result.StatusCode), result.Error);
            return StatusCode((int)result.StatusCode, errorResponse);
        }
        else
        {
            var result = await balancesService.GetBalanceSummaryAsync(groupId, currentUserId.Value);
            if (result.IsSuccess)
            {
                var response = ApiResponseDto<BalanceSummaryDto>.SuccessResponse(
                    result.Value!, "Balance summary retrieved successfully");
                return Ok(response);
            }

            var errorResponse = ApiResponseDto<BalanceSummaryDto>.ErrorResponse(
                GetErrorCodeFromStatus(result.StatusCode), result.Error);
            return StatusCode((int)result.StatusCode, errorResponse);
        }
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
}
