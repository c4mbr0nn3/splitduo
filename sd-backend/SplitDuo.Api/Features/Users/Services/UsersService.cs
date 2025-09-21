using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Api.Features.Common.Services;
using SplitDuo.Api.Features.Users.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Dto.Imports;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Users.Services;

public interface IUsersService
{
    Task<Result<List<UserDto>>> GetUsersAsync();
    Task<Result<CreateUserDto>> CreateUserAsync(CreateUserRequestDto request);
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
    IUserContextService userContextService) : IUsersService
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

    public async Task<Result<CreateUserDto>> CreateUserAsync(CreateUserRequestDto request)
    {
        var existingUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (existingUser != null)
            return Result<CreateUserDto>.Conflict("User with this email already exists");

        var generatedPassword = GenerateSecurePassword();

        var user = new User
        {
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PasswordHash = passwordHasher.HashPassword(null!, generatedPassword)
        };

        unitOfWork.Users.Add(user);

        var response = new CreateUserDto
        {
            User = new UserDto(user),
            GeneratedPassword = generatedPassword
        };

        return Result<CreateUserDto>.Success(response);
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

        // Get total groups for the user
        var totalGroups = await unitOfWork.GroupMembers
            .Where(gm => gm.UserId == user.OriginalId)
            .CountAsync();

        // Get all expenses for the user's groups
        var userGroupIds = await unitOfWork.GroupMembers
            .Where(gm => gm.UserId == user.OriginalId)
            .Select(gm => gm.GroupId)
            .ToListAsync();

        var totalExpenses = await unitOfWork.Expenses
            .Where(e => userGroupIds.Contains(e.GroupId) && e.DeletedAt == null)
            .SumAsync(e => e.Amount);

        // Calculate what user owes (splits where user owes money)
        var totalSplitsOwed = await unitOfWork.ExpenseSplits
            .Where(es => es.UserId == user.OriginalId)
            .SumAsync(es => es.SplitAmount);

        var totalPaidByUser = await unitOfWork.Expenses
            .Where(e => e.PaidBy == user.OriginalId && e.DeletedAt == null)
            .SumAsync(e => e.Amount);

        // User owes: amount they owe in splits minus what they've paid
        var youOwe = Math.Max(0, totalSplitsOwed - totalPaidByUser);

        // User is owed: amount they've paid minus what they owe in splits
        var youreOwed = Math.Max(0, totalPaidByUser - totalSplitsOwed);

        var stats = new UserStatsDto
        {
            TotalGroups = totalGroups,
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

    private static string GenerateSecurePassword()
    {
        const string upperCase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowerCase = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string specialChars = "!@#$%^&*";

        const string allChars = upperCase + lowerCase + digits + specialChars;
        const int passwordLength = 12;

        using var rng = RandomNumberGenerator.Create();
        var password = new char[passwordLength];

        // Ensure at least one character from each category
        password[0] = GetRandomChar(upperCase, rng);
        password[1] = GetRandomChar(lowerCase, rng);
        password[2] = GetRandomChar(digits, rng);
        password[3] = GetRandomChar(specialChars, rng);

        // Fill the rest randomly
        for (var i = 4; i < passwordLength; i++)
        {
            password[i] = GetRandomChar(allChars, rng);
        }

        // Shuffle the password to avoid predictable patterns
        for (var i = passwordLength - 1; i > 0; i--)
        {
            var randomBytes = new byte[4];
            rng.GetBytes(randomBytes);
            var j = Math.Abs(BitConverter.ToInt32(randomBytes, 0)) % (i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }

        return new string(password);
    }

    private static char GetRandomChar(string chars, RandomNumberGenerator rng)
    {
        var randomBytes = new byte[4];
        rng.GetBytes(randomBytes);
        var randomIndex = Math.Abs(BitConverter.ToInt32(randomBytes, 0)) % chars.Length;
        return chars[randomIndex];
    }
}