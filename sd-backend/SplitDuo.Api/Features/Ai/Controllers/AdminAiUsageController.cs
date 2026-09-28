using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SplitDuo.Api.Features.Ai.Dto;
using SplitDuo.Api.Features.Ai.Services;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Core.Options;

namespace SplitDuo.Api.Features.Ai.Controllers;

[ApiController]
[Route("api/v1/admin/ai")]
[Authorize]
public class AdminAiUsageController(
    IAiUsageService usageService,
    IOptions<AiOptions> aiOptions) : BaseApiController
{
    [HttpGet("usage")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(PaginatedResponseDto<AiUsageEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PaginatedResponseDto<AiUsageEntryDto>>> GetUsage(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] long? from = null,
        [FromQuery] long? to = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] bool? success = null,
        CancellationToken ct = default)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandlePaginatedResult(NotAuthenticated<PaginatedResponseDto<AiUsageEntryDto>>());

        var result = await usageService.GetUsageAsync(page, limit, from, to, userId, success, ct);
        return HandlePaginatedResult(result, "AI usage retrieved successfully");
    }

    [HttpGet("usage/summary")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(ApiResponseDto<AiUsageSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponseDto<AiUsageSummaryDto>>> GetSummary(
        [FromQuery] long? from = null,
        [FromQuery] long? to = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] bool? success = null,
        CancellationToken ct = default)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<AiUsageSummaryDto>());

        var result = await usageService.GetSummaryAsync(from, to, userId, success, ct);
        return HandleResult(result, "AI usage summary retrieved successfully");
    }

    [HttpGet("config")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(ApiResponseDto<AiAdminConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<ApiResponseDto<AiAdminConfigDto>> GetConfig()
    {
        var opts = aiOptions.Value;
        var dto = new AiAdminConfigDto
        {
            Enabled = opts.IsEnabled,
            Model = opts.Model,
            BaseUrlHost = TryGetHost(opts.BaseUrl),
        };
        return Ok(ApiResponseDto<AiAdminConfigDto>.SuccessResponse(dto));
    }

    private static string? TryGetHost(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return null;
        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ? uri.Host : null;
    }
}