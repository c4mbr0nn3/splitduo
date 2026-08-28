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

        var userIds = users.Select(u => u.Id).ToList();
        var avatarUserIds = await unitOfWork.UserAvatars
            .Where(a => userIds.Contains(a.UserId))
            .Select(a => a.UserId)
            .ToHashSetAsync();

        foreach (var dto in response)
        {
            dto.HasAvatar = avatarUserIds.Contains(dto.OriginalId);
        }

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

        var dto = new UserDto(user);
        dto.HasAvatar = await unitOfWork.UserAvatars.AnyAsync(a => a.UserId == user.Id);

        return Result<UserDto>.Success(dto);
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

        if (user == null)
            return Result<UserDto>.NotFound(loc["UserNotFound"]);

        var dto = new UserDto(user);
        dto.HasAvatar = await unitOfWork.UserAvatars.AnyAsync(a => a.UserId == user.Id);

        return Result<UserDto>.Success(dto);
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
        {
            var dto = new UserDto(user);
            dto.HasAvatar = await unitOfWork.UserAvatars.AnyAsync(a => a.UserId == user.Id);
            return Result<UserDto>.Success(dto);
        }

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

        var updatedDto = new UserDto(user);
        updatedDto.HasAvatar = await unitOfWork.UserAvatars.AnyAsync(a => a.UserId == user.Id);

        return Result<UserDto>.Success(updatedDto);
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