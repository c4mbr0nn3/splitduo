using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Api.Features.Authentication.Services;
using SplitDuo.Api.Features.Common.Services;
using SplitDuo.Api.Features.Users.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Email;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Dto.Imports;
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
    ILogger<UsersService> logger) : IUsersService
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

        if (user == null) return Result<List<ImportStatusDto>>.NotFound("User not found");

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

        if (user == null) return Result<UserStatsDto>.NotFound("User not found");

        var userGroupIds = await unitOfWork.GroupMembers
            .Where(gm => gm.UserId == user.OriginalId)
            .Select(gm => gm.GroupId)
            .ToListAsync();

        // Per-group: how much the user paid
        var paidByGroup = await unitOfWork.Expenses
            .Where(e => userGroupIds.Contains(e.GroupId) && e.PaidBy == user.OriginalId && e.DeletedAt == null)
            .GroupBy(e => e.GroupId)
            .Select(g => new { GroupId = g.Key, Total = g.Sum(e => e.Amount) })
            .ToDictionaryAsync(x => x.GroupId, x => x.Total);

        // Per-group: how much of the user's split share (joined to exclude soft-deleted expenses)
        var splitByGroup = await unitOfWork.ExpenseSplits
            .Where(es => es.UserId == user.OriginalId)
            .Join(unitOfWork.Expenses.Where(e => e.DeletedAt == null),
                es => es.ExpenseId, e => e.Id,
                (es, e) => new { e.GroupId, es.SplitAmount })
            .GroupBy(x => x.GroupId)
            .Select(g => new { GroupId = g.Key, Total = g.Sum(x => x.SplitAmount) })
            .ToDictionaryAsync(x => x.GroupId, x => x.Total);

        // Sum per-group nets: positive → user is owed, negative → user owes
        var youOwe = 0m;
        var youreOwed = 0m;
        foreach (var groupId in userGroupIds)
        {
            var net = paidByGroup.GetValueOrDefault(groupId, 0m) - splitByGroup.GetValueOrDefault(groupId, 0m);
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
            return Result<UserDto>.NotFound("User not found");

        if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.Email)
        {
            var existingUser = await unitOfWork.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.Id != user.Id);

            if (existingUser != null)
                return Result<UserDto>.Conflict("User with this email already exists");

            user.Email = request.Email;
        }

        if (!string.IsNullOrWhiteSpace(request.FirstName))
            user.FirstName = request.FirstName;

        if (request.LastName != null)
            user.LastName = request.LastName;

        return Result<UserDto>.Success(new UserDto(user));
    }

    public async Task<Result> ChangeCurrentUserPasswordAsync(Guid currentUserId, ChangePasswordRequestDto request)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result.NotFound("User not found");

        var verificationResult = passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, request.CurrentPassword);

        if (verificationResult == PasswordVerificationResult.Failed)
            return Result.Unauthorized("Current password is incorrect");

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);

        // Revoke all refresh tokens for security (force logout on all devices)
        logger.LogInformation("Revoking all refresh tokens for user {UserId} after password change", user.Guid);
        var revokeResult = await authenticationService.RevokeAllUserTokensAsync(user.Guid.ToString());

        if (revokeResult.IsFailure)
        {
            logger.LogWarning("Failed to revoke tokens for user {UserId}: {Error}", user.Guid, revokeResult.Error);
        }

        // Send password change notification email
        var emailResult = await notificationService.EnqueueAsync(emailTemplateProvider.Render(new PasswordChangedModel
            { To = user.Email, FirstName = user.FirstName, LastName = user.LastName }));

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
            return Result<UserDto>.BadRequest("Invalid user ID format");

        var currentUserId = userContextService.GetCurrentUserId();
        var isSystemAdmin = userContextService.IsSystemAdmin();

        if (currentUserId == null)
            return Result<UserDto>.Unauthorized("User not authenticated");

        if (!isSystemAdmin && currentUserId != userGuid)
            return Result<UserDto>.Forbidden("You can only access your own user data");

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        return user == null
            ? Result<UserDto>.NotFound("User not found")
            : Result<UserDto>.Success(new UserDto(user));
    }

    public async Task<Result<UserDto>> UpdateUserAsync(string userId, UpdateUserRequestDto request)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return Result<UserDto>.BadRequest("Invalid user ID format");

        var currentUserId = userContextService.GetCurrentUserId();
        var isSystemAdmin = userContextService.IsSystemAdmin();

        if (currentUserId == null)
            return Result<UserDto>.Unauthorized("User not authenticated");

        if (!isSystemAdmin && currentUserId != userGuid)
            return Result<UserDto>.Forbidden("You can only update your own user data");

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        if (user == null)
            return Result<UserDto>.NotFound("User not found");

        if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.Email)
        {
            var existingUser = await unitOfWork.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.Id != user.Id);

            if (existingUser != null)
                return Result<UserDto>.Conflict("User with this email already exists");

            user.Email = request.Email;
        }

        if (!string.IsNullOrWhiteSpace(request.FirstName))
            user.FirstName = request.FirstName;

        if (request.LastName != null)
            user.LastName = request.LastName;

        if (!request.GlobalRole.HasValue) return Result<UserDto>.Success(new UserDto(user));

        if (!isSystemAdmin)
            return Result<UserDto>.Forbidden("Only system administrators can modify user roles");

        user.GlobalRole = request.GlobalRole.Value;

        return Result<UserDto>.Success(new UserDto(user));
    }

    public async Task<Result> DeleteUserAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return Result.BadRequest("Invalid user ID format");

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        if (user == null)
            return Result.NotFound("User not found");

        user.DeletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return Result.Success();
    }
}