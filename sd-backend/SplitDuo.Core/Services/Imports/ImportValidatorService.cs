using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Dto.Imports;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Core.Services.Imports;

public interface IImportValidatorService
{
    Task<Result> IsValidImportAsync(IFormFile file, int groupId);
    Task<Result> IsDuplicateFileAsync(string fileHash, int groupId);
    Task<Result> ValidateMappingConfigurationAsync(ImportMappingDto mappings, int groupId);
}

public class ImportValidatorServiceService(ILogger<ImportValidatorServiceService> logger, IUnitOfWork unitOfWork)
    : IImportValidatorService
{
    public async Task<Result> IsValidImportAsync(IFormFile file, int groupId)
    {
        var result = FileUtils.CheckExtensionAndSize(file);
        if (result.IsFailure) return result;

        var hash = await HashUtils.ComputeSha256Async(file);
        var isDuplicateResult = await IsDuplicateFileAsync(hash, groupId);

        return isDuplicateResult.IsSuccess
            ? Result.Conflict("This file has already been imported")
            : Result.Success();
    }

    public async Task<Result> IsDuplicateFileAsync(string fileHash, int groupId)
    {
        var any = await unitOfWork.Imports
            .AnyAsync(i => i.GroupId == groupId && i.FileHash == fileHash);
        return any ? Result.Success() : Result.NotFound("No duplicate file found");
    }

    public async Task<Result> ValidateMappingConfigurationAsync(ImportMappingDto mappings, int groupId)
    {
        var groupMemberIds = await unitOfWork.GroupMembers
            .Where(g => g.GroupId == groupId)
            .Include(g => g.User)
            .Select(g => g.User.Guid)
            .ToListAsync();

        foreach (var userMapping in mappings.UserMappings)
        {
            if (Guid.TryParse(userMapping.Value, out var userGuid) && groupMemberIds.Contains(userGuid)) continue;

            var message =
                $"User mapping for '{userMapping.Key}' maps to invalid or non-member user ID '{userMapping.Value}'";
            logger.LogWarning("User mapping error: {Message}", message);
            return Result.BadRequest(message);
        }

        var validCategories = Enum.GetValues<ExpenseCategory>().Cast<int>().ToHashSet();
        foreach (var categoryMapping in mappings.CategoryMappings)
        {
            if (validCategories.Contains(categoryMapping.Value)) continue;
            var message =
                $"Category mapping for ID '{categoryMapping.Key}' maps to invalid category '{categoryMapping.Value}'";
            logger.LogWarning("Category mapping error: {Message}", message);
            return Result.BadRequest(message);
        }

        var validPaymentModes = Enum.GetValues<PaymentMode>().Cast<int>().ToHashSet();
        foreach (var paymentModeMapping in mappings.PaymentModeMappings)
        {
            if (validPaymentModes.Contains(paymentModeMapping.Value)) continue;
            var message =
                $"Payment mode mapping for ID '{paymentModeMapping.Key}' maps to invalid payment mode '{paymentModeMapping.Value}'";
            logger.LogWarning("Payment mode mapping error: {Message}", message);
            return Result.BadRequest(message);
        }

        return Result.Success();
    }
}