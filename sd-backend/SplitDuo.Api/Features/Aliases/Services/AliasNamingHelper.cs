using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Aliases.Services;

/// <summary>
/// Static helper for generating unique singleton alias names.
/// Reused by AliasesService and GroupsService (Task 3).
/// </summary>
public static class AliasNamingHelper
{
    /// <summary>
    /// Generates a unique singleton alias name for the given user within the group.
    /// Base name = user.FirstName; if taken, appends " (solo)"; if that's also taken,
    /// appends numeric suffix " (2)", " (3)", etc.
    /// </summary>
    public static async Task<string> GenerateUniqueSingletonNameAsync(
        IUnitOfWork unitOfWork,
        int groupId,
        User user)
    {
        var baseName = user.FirstName;

        var existingNames = await unitOfWork.Aliases
            .Where(a => a.GroupId == groupId && a.DeletedAt == null)
            .Select(a => a.Name)
            .ToListAsync();

        if (!existingNames.Contains(baseName))
            return baseName;

        var candidate = $"{baseName} (solo)";
        if (!existingNames.Contains(candidate))
            return candidate;

        var suffix = 2;
        while (true)
        {
            candidate = $"{baseName} ({suffix})";
            if (!existingNames.Contains(candidate))
                return candidate;
            suffix++;
        }
    }
}
