using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
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

public class ImportValidatorServiceService(
    ILogger<ImportValidatorServiceService> logger,
    IUnitOfWork unitOfWork,
    IStringLocalizer<ImportValidatorServiceService> loc)
    : IImportValidatorService
{
    public async Task<Result> IsValidImportAsync(IFormFile file, int groupId)
    {
        var result = FileUtils.CheckExtensionAndSize(file);
        if (result.IsFailure) return result;

        var hash = await HashUtils.ComputeSha256Async(file);
        var isDuplicateResult = await IsDuplicateFileAsync(hash, groupId);

        return isDuplicateResult.IsSuccess
            ? Result.Conflict(loc["FileAlreadyImported"])
            : Result.Success();
    }

    public async Task<Result> IsDuplicateFileAsync(string fileHash, int groupId)
    {
        var any = await unitOfWork.Imports
            .AnyAsync(i => i.GroupId == groupId && i.FileHash == fileHash);
        return any ? Result.Success() : Result.NotFound(loc["NoDuplicateFileFound"]);
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
                string.Format(loc["UserMappingInvalid"], userMapping.Key, userMapping.Value);
            logger.LogWarning("User mapping error: {Message}", message);
            return Result.BadRequest(message);
        }

        var validCategories = Enum.GetValues<ExpenseCategory>().Cast<int>().ToHashSet();
        foreach (var categoryMapping in mappings.CategoryMappings)
        {
            if (validCategories.Contains(categoryMapping.Value)) continue;
            var message =
                string.Format(loc["CategoryMappingInvalid"], categoryMapping.Key, categoryMapping.Value);
            logger.LogWarning("Category mapping error: {Message}", message);
            return Result.BadRequest(message);
        }

        var validPaymentModes = Enum.GetValues<PaymentMode>().Cast<int>().ToHashSet();
        foreach (var paymentModeMapping in mappings.PaymentModeMappings)
        {
            if (validPaymentModes.Contains(paymentModeMapping.Value)) continue;
            var message =
                string.Format(loc["PaymentModeMappingInvalid"], paymentModeMapping.Key, paymentModeMapping.Value);
            logger.LogWarning("Payment mode mapping error: {Message}", message);
            return Result.BadRequest(message);
        }

        // Validate alias mappings
        if (mappings.AliasMappings.Count > 0)
        {
            foreach (var aliasMapping in mappings.AliasMappings)
            {
                if (Guid.TryParse(aliasMapping.Value, out var aliasGuid) &&
                    await unitOfWork.Aliases.AnyAsync(a => a.Guid == aliasGuid && a.GroupId == groupId && a.DeletedAt == null))
                {
                    continue;
                }

                var message =
                    string.Format(loc["AliasMappingInvalid"], aliasMapping.Key, aliasMapping.Value);
                logger.LogWarning("Alias mapping error: {Message}", message);
                return Result.BadRequest(message);
            }
        }

        return Result.Success();
    }
}