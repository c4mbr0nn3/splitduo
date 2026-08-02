using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Authentication.Dto;
using SplitDuo.Api.Features.Authentication.Services;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Authentication.Controllers;

[Route("api/v1/2fa")]
[Authorize]
public class TwoFactorController(
    ITwoFactorService twoFactorService,
    IUnitOfWork unitOfWork,
    ILogger<TwoFactorController> logger) : BaseApiController
{
    [HttpPost("setup/initiate")]
    public async Task<ActionResult<ApiResponseDto<TwoFactorSetupDto>>> InitiateSetup()
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result<TwoFactorSetupDto>.Unauthorized("User not authenticated"));

        logger.LogInformation("Initiating 2FA setup for user: {UserId}", currentUserId);

        var result = await twoFactorService.InitiateSetupAsync(currentUserId.Value);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("2FA setup initiated successfully for user: {UserId}", currentUserId);
        }
        else
        {
            logger.LogWarning("Failed to initiate 2FA setup for user: {UserId}. Error: {Error}", currentUserId,
                result.Error);
        }

        return HandleResult(result, "2FA setup initiated successfully");
    }

    [HttpPost("setup/verify")]
    public async Task<ActionResult<ApiResponseDto<EmptyDto>>> VerifySetup([FromBody] VerifyTwoFactorSetupDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result.Unauthorized("User not authenticated"));

        logger.LogInformation("Verifying 2FA setup for user: {UserId}", currentUserId);

        var result = await twoFactorService.VerifySetupAsync(currentUserId.Value, request);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("2FA setup verified and enabled for user: {UserId}", currentUserId);
        }
        else
        {
            logger.LogWarning("Failed to verify 2FA setup for user: {UserId}. Error: {Error}", currentUserId,
                result.Error);
        }

        return HandleResult(result, "Two-factor authentication has been successfully enabled");
    }

    [HttpPost("disable")]
    public async Task<ActionResult<ApiResponseDto<EmptyDto>>> Disable([FromBody] DisableTwoFactorDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result.Unauthorized("User not authenticated"));

        logger.LogInformation("Disabling 2FA for user: {UserId}", currentUserId);

        var result = await twoFactorService.DisableAsync(currentUserId.Value, request);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("2FA disabled successfully for user: {UserId}", currentUserId);
        }
        else
        {
            logger.LogWarning("Failed to disable 2FA for user: {UserId}. Error: {Error}", currentUserId, result.Error);
        }

        return HandleResult(result, "Two-factor authentication has been disabled");
    }

    [HttpPost("backup-codes/generate")]
    public async Task<ActionResult<ApiResponseDto<List<string>>>> GenerateBackupCodes()
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result<List<string>>.Unauthorized("User not authenticated"));

        logger.LogInformation("Generating new backup codes for user: {UserId}", currentUserId);

        var result = await twoFactorService.GenerateBackupCodesAsync(currentUserId.Value);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("New backup codes generated for user: {UserId}", currentUserId);
        }
        else
        {
            logger.LogWarning("Failed to generate backup codes for user: {UserId}. Error: {Error}", currentUserId,
                result.Error);
        }

        return HandleResult(result, "New backup codes generated successfully");
    }
}