using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SplitDuo.Api.Features.Authentication.Services;
using SplitDuo.Api.Features.Common.Services;
using SplitDuo.Api.Features.Users.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Email;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Dto.Imports;
using SplitDuo.Core.Localization;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services;

namespace SplitDuo.Api.Features.Users.Services;

public interface IUsersService
{
    Task<Result<List<UserDto>>> GetUsersAsync();
    Task<Result<List<ImportStatusDto>>> GetCurrentUserImports(string currentUserId);
    Task<Result<UserStatsDto>> GetCurrentUserStatsAsync(string currentUserId);
    Task<Result<UserDto>> UpdateCurrentUserAsync(Guid currentUserId, UpdateUserRequestDto request);
    Task<Result> ChangeCurrentUserPasswordAsync(Guid currentUserId, ChangePasswordRequestDto request);
    Task<Result<UpdateUserSettingsResponseDto>> UpdateCurrentUserSettingsAsync(Guid userGuid, UpdateUserSettingsRequestDto request);
    Task<Result<UserDto>> GetUserAsync(string userId);
    Task<Result<UserDto>> UpdateUserAsync(string userId, UpdateUserRequestDto request);
    Task<Result> DeleteUserAsync(string userId);
}

public class UsersService(
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> passwordHasher,
    IUserContextService userContextService,
    IAuthenticationService authenticationService,
    INotificationService notificationService,
    IEmailTemplateProvider emailTemplateProvider,
    ILogger<UsersService> logger,
    TimeProvider timeProvider,
    IStringLocalizer<UsersService> loc,
    ITokenGenerator tokenGenerator) : IUsersService
{
    public async Task<Result<List<UserDto>>> GetUsersAsync()
    {
        var users = await unitOfWork.Users
            .Where(u => u.DeletedAt == null)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync();

        var response = users.Select(x => new UserDto(x)).ToList();
        return Result<List<UserDto>>.Success(response);
    }

    public async Task<Result<List<ImportStatusDto>>> GetCurrentUserImports(string currentUserId)
    {
        var userResult = await GetUserAsync(currentUserId);
        if (userResult.IsFailure) return userResult.MapTo<List<ImportStatusDto>>();

        var user = userResult.Value;

        if (user == null) return Result<List<ImportStatusDto>>.NotFound(loc["UserNotFound"]);

        var imports = await unitOfWork.Imports
            .Where(i => i.UserId == user.OriginalId)
            .OrderByDescending(i => i.ImportDate)
            .ThenByDescending(i => i.CreatedAt)
            .ToListAsync();

        var response = imports.Select(i => new ImportStatusDto(i)).ToList();
        return Result<List<ImportStatusDto>>.Success(response);
    }

    // TODO: this should be under balances service
    public async Task<Result<UserStatsDto>> GetCurrentUserStatsAsync(string currentUserId)
    {
        var userResult = await GetUserAsync(currentUserId);
        if (userResult.IsFailure) return userResult.MapTo<UserStatsDto>();

        var user = userResult.Value;

        if (user == null) return Result<UserStatsDto>.NotFound(loc["UserNotFound"]);

        // Fetch the user's group memberships with mode info (individual vs alias).
        // Alias-mode groups never create ExpenseSplit rows — they use ExpenseAliasSplit
        // keyed by AliasId, so balances must be computed at the alias level.
        var userMemberships = await unitOfWork.GroupMembers
            .AsNoTracking()
            .Where(gm => gm.UserId == user.OriginalId)
            .Join(unitOfWork.Groups.AsNoTracking(),
                gm => gm.GroupId, g => g.Id,
                (gm, g) => new { gm.GroupId, g.UseAliases, gm.AliasId })
            .ToListAsync();

        var userGroupIds = userMemberships.Select(m => m.GroupId).ToList();
        var individualGroupIds = userMemberships.Where(m => !m.UseAliases).Select(m => m.GroupId).ToList();
        var aliasGroupIds = userMemberships.Where(m => m.UseAliases).Select(m => m.GroupId).ToList();

        // Per-group: how much the user paid (INDIVIDUAL MODE ONLY)
        var paidByGroup = individualGroupIds.Count > 0
            ? await unitOfWork.Expenses
                .AsNoTracking()
                .Where(e => individualGroupIds.Contains(e.GroupId) && e.PaidBy == user.OriginalId && e.DeletedAt == null)
                .GroupBy(e => e.GroupId)
                .Select(g => new { GroupId = g.Key, Total = g.Sum(e => e.Amount) })
                .ToDictionaryAsync(x => x.GroupId, x => x.Total)
            : new Dictionary<int, decimal>();

        // Per-group: how much of the user's split share (INDIVIDUAL MODE ONLY)
        var splitByGroup = individualGroupIds.Count > 0
            ? await unitOfWork.ExpenseSplits
                .AsNoTracking()
                .Where(es => es.UserId == user.OriginalId)
                .Join(unitOfWork.Expenses.AsNoTracking().Where(e => e.DeletedAt == null),
                    es => es.ExpenseId, e => e.Id,
                    (es, e) => new { e.GroupId, es.SplitAmount })
                .Where(x => individualGroupIds.Contains(x.GroupId))
                .GroupBy(x => x.GroupId)
                .Select(g => new { GroupId = g.Key, Total = g.Sum(x => x.SplitAmount) })
                .ToDictionaryAsync(x => x.GroupId, x => x.Total)
            : new Dictionary<int, decimal>();

        // Alias-mode "paid" per (group, alias): sum of expenses paid by the alias, with
        // COALESCE(PaidByAliasId, payer's current alias) fallback — same semantics as
        // GroupsService.GetUserGroupsAsync and BalancesService.CalculateAliasBalancesAsync.
        // LEFT JOIN group_members so expenses with null PaidByAliasId (pre-migration data)
        // are attributed to the payer's current alias.
        var aliasPaidByGroup = aliasGroupIds.Count > 0
            ? await unitOfWork.Expenses
                .AsNoTracking()
                .Where(e => aliasGroupIds.Contains(e.GroupId) && e.DeletedAt == null)
                .GroupJoin(
                    unitOfWork.GroupMembers.AsNoTracking()
                        .Where(gm => gm.DeletedAt == null && gm.AliasId != null),
                    e => new { e.GroupId, e.PaidBy },
                    gm => new { GroupId = gm.GroupId, PaidBy = gm.UserId },
                    (e, gms) => new { e, gms })
                .SelectMany(
                    x => x.gms.DefaultIfEmpty(),
                    (x, gm) => new { x.e.GroupId, AliasId = x.e.PaidByAliasId ?? (gm == null ? null : gm.AliasId), x.e.Amount })
                .Where(x => x.AliasId != null)
                .GroupBy(x => new { x.GroupId, x.AliasId })
                .Select(g => new { g.Key.GroupId, g.Key.AliasId, Total = g.Sum(x => x.Amount) })
                .ToDictionaryAsync(x => (x.GroupId, x.AliasId!.Value), x => x.Total)
            : new Dictionary<(int GroupId, int AliasId), decimal>();

        // Alias-mode "owed" per (group, alias): sum of ExpenseAliasSplit.SplitAmount
        // over non-deleted expenses (same pattern as GroupsService.GetUserGroupsAsync)
        var aliasSplitByGroup = aliasGroupIds.Count > 0
            ? await unitOfWork.ExpenseAliasSplits
                .AsNoTracking()
                .Join(unitOfWork.Expenses.AsNoTracking()
                        .Where(e => aliasGroupIds.Contains(e.GroupId) && e.DeletedAt == null),
                    eas => eas.ExpenseId, e => e.Id,
                    (eas, e) => new { e.GroupId, eas.AliasId, eas.SplitAmount })
                .GroupBy(x => new { x.GroupId, x.AliasId })
                .Select(g => new { g.Key.GroupId, g.Key.AliasId, Total = g.Sum(x => x.SplitAmount) })
                .ToDictionaryAsync(x => (x.GroupId, x.AliasId), x => x.Total)
            : new Dictionary<(int GroupId, int AliasId), decimal>();

        // Sum per-group nets: positive → user is owed, negative → user owes
        var youOwe = 0m;
        var youreOwed = 0m;
        foreach (var membership in userMemberships)
        {
            decimal net;
            if (membership.UseAliases)
            {
                // Alias-mode: the user's net is their alias's net (paid − owed)
                if (membership.AliasId == null)
                {
                    net = 0m;
                }
                else
                {
                    var aliasId = membership.AliasId.Value;
                    net = aliasPaidByGroup.GetValueOrDefault((membership.GroupId, aliasId), 0m)
                          - aliasSplitByGroup.GetValueOrDefault((membership.GroupId, aliasId), 0m);
                }
            }
            else
            {
                // Individual-mode: existing per-user computation
                net = paidByGroup.GetValueOrDefault(membership.GroupId, 0m)
                      - splitByGroup.GetValueOrDefault(membership.GroupId, 0m);
            }

            if (net > 0) youreOwed += net;
            else youOwe += -net;
        }

        var stats = new UserStatsDto
        {
            TotalGroups = userGroupIds.Count,
            YouOwe = youOwe,
            YoureOwed = youreOwed
        };

        return Result<UserStatsDto>.Success(stats);
    }

    public async Task<Result<UserDto>> UpdateCurrentUserAsync(Guid currentUserId, UpdateUserRequestDto request)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result<UserDto>.NotFound(loc["UserNotFound"]);

        if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.Email)
        {
            var existingUser = await unitOfWork.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.Id != user.Id);

            if (existingUser != null)
                return Result<UserDto>.Conflict(loc["EmailAlreadyExists"]);

            user.Email = request.Email;
        }

        if (!string.IsNullOrWhiteSpace(request.FirstName))
            user.FirstName = request.FirstName;

        if (request.LastName != null)
            user.LastName = request.LastName;

        return Result<UserDto>.Success(new UserDto(user));
    }

    public async Task<Result<UpdateUserSettingsResponseDto>> UpdateCurrentUserSettingsAsync(Guid userGuid, UpdateUserSettingsRequestDto request)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        if (user == null)
            return Result<UpdateUserSettingsResponseDto>.NotFound(loc["UserNotFound"]);

        var oldLanguage = user.Settings.UiLanguage;

        if (request.Theme != null)
            user.Settings.Theme = request.Theme;

        if (request.UiLanguage != null)
        {
            if (!SupportedLanguages.IsSupported(request.UiLanguage))
                return Result<UpdateUserSettingsResponseDto>.BadRequest(loc["UnsupportedLanguage"]);

            user.Settings.UiLanguage = request.UiLanguage;
        }

        var response = new UpdateUserSettingsResponseDto
        {
            Settings = new UserSettingsDto(user.Settings)
        };

        // If uiLanguage changed, re-issue the JWT so the lang claim is updated (REQ-006, REQ-009)
        if (request.UiLanguage != null && request.UiLanguage != oldLanguage)
        {
            var jwtId = Guid.CreateVersion7().ToString();
            response.Token = tokenGenerator.GenerateJwtToken(user, jwtId);
        }

        return Result<UpdateUserSettingsResponseDto>.Success(response);
    }

    public async Task<Result> ChangeCurrentUserPasswordAsync(Guid currentUserId, ChangePasswordRequestDto request)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result.NotFound(loc["UserNotFound"]);

        var verificationResult = passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, request.CurrentPassword);

        if (verificationResult == PasswordVerificationResult.Failed)
            return Result.Unauthorized(loc["CurrentPasswordIncorrect"]);

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);

        // Revoke all refresh tokens for security (force logout on all devices)
        logger.LogInformation("Revoking all refresh tokens for user {UserId} after password change", user.Guid);
        var revokeResult = await authenticationService.RevokeAllUserTokensAsync(user.Id, "Password changed");

        if (revokeResult.IsFailure)
        {
            logger.LogWarning("Failed to revoke tokens for user {UserId}: {Error}", user.Guid, revokeResult.Error);
        }

        // Send password change notification email
        var language = userContextService.GetCurrentUserLanguage();
        var emailResult = await notificationService.EnqueueAsync(emailTemplateProvider.Render(new PasswordChangedModel
            { To = user.Email, FirstName = user.FirstName }, language));

        if (emailResult.IsFailure)
        {
            logger.LogWarning("Failed to enqueue password change email for user {UserId}: {Error}",
                user.Guid, emailResult.Error);
        }
        else
        {
            logger.LogInformation("Password change notification email enqueued for user {UserId}", user.Guid);
        }

        return Result.Success();
    }

    public async Task<Result<UserDto>> GetUserAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return Result<UserDto>.BadRequest(loc["InvalidUserIdFormat"]);

        var currentUserId = userContextService.GetCurrentUserId();
        var isSystemAdmin = userContextService.IsSystemAdmin();

        if (currentUserId == null)
            return Result<UserDto>.Unauthorized(loc["UserNotAuthenticated"]);

        if (!isSystemAdmin && currentUserId != userGuid)
            return Result<UserDto>.Forbidden(loc["OnlyOwnUserData"]);

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        return user == null
            ? Result<UserDto>.NotFound(loc["UserNotFound"])
            : Result<UserDto>.Success(new UserDto(user));
    }

    public async Task<Result<UserDto>> UpdateUserAsync(string userId, UpdateUserRequestDto request)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return Result<UserDto>.BadRequest(loc["InvalidUserIdFormat"]);

        var currentUserId = userContextService.GetCurrentUserId();
        var isSystemAdmin = userContextService.IsSystemAdmin();

        if (currentUserId == null)
            return Result<UserDto>.Unauthorized(loc["UserNotAuthenticated"]);

        if (!isSystemAdmin && currentUserId != userGuid)
            return Result<UserDto>.Forbidden(loc["OnlyOwnUserUpdate"]);

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        if (user == null)
            return Result<UserDto>.NotFound(loc["UserNotFound"]);

        if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.Email)
        {
            var existingUser = await unitOfWork.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.Id != user.Id);

            if (existingUser != null)
                return Result<UserDto>.Conflict(loc["EmailAlreadyExists"]);

            user.Email = request.Email;
        }

        if (!string.IsNullOrWhiteSpace(request.FirstName))
            user.FirstName = request.FirstName;

        if (request.LastName != null)
            user.LastName = request.LastName;

        if (!request.GlobalRole.HasValue || user.GlobalRole == request.GlobalRole.Value)
            return Result<UserDto>.Success(new UserDto(user));

        if (!Enum.IsDefined(request.GlobalRole.Value))
            return Result<UserDto>.BadRequest(loc["InvalidRoleValue"]);

        if (!isSystemAdmin)
            return Result<UserDto>.Forbidden(loc["OnlyAdminsCanModifyRoles"]);

        if (request.GlobalRole.Value == GlobalRole.BaseUser && currentUserId == userGuid)
            return Result<UserDto>.Forbidden(loc["CannotChangeOwnRole"]);

        if (request.GlobalRole.Value == GlobalRole.BaseUser && user.GlobalRole == GlobalRole.SystemAdmin)
        {
            var adminCount = await unitOfWork.Users
                .CountAsync(u => u.GlobalRoleId == (int)GlobalRole.SystemAdmin && u.DeletedAt == null);
            if (adminCount <= 1)
                return Result<UserDto>.Conflict(loc["CannotDemoteOnlyAdmin"]);
        }

        user.GlobalRole = request.GlobalRole.Value;

        return Result<UserDto>.Success(new UserDto(user));
    }

    public async Task<Result> DeleteUserAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return Result.BadRequest(loc["InvalidUserIdFormat"]);

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        if (user == null)
            return Result.NotFound(loc["UserNotFound"]);

        user.DeletedAt = timeProvider.GetUtcNow().ToUnixTimeSeconds();

        return Result.Success();
    }
}