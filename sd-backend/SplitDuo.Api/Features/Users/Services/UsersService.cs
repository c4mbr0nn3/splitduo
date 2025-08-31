using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Api.Features.Users.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Users.Services;

public interface IUsersService
{
    Task<Result<List<UserDto>>> GetUsersAsync();
    Task<Result<UserDto>> CreateUserAsync(CreateUserRequestDto request);
    Task<Result<UserDto>> UpdateCurrentUserAsync(Guid currentUserId, UpdateUserRequestDto request);
    Task<Result> ChangeCurrentUserPasswordAsync(Guid currentUserId, ChangePasswordRequestDto request);
    Task<Result<UserDto>> GetUserAsync(string userId);
    Task<Result<UserDto>> UpdateUserAsync(string userId, UpdateUserRequestDto request);
    Task<Result> DeleteUserAsync(string userId);
}

public class UsersService(
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> passwordHasher) : IUsersService
{
    public async Task<Result<List<UserDto>>> GetUsersAsync()
    {
        var users = await unitOfWork.Users
            .Where(u => u.DeletedAt == null)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();

        var userDtos = users.Select(MapToUserDto).ToList();
        return Result<List<UserDto>>.Success(userDtos);
    }

    public async Task<Result<UserDto>> CreateUserAsync(CreateUserRequestDto request)
    {
        var existingUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (existingUser != null)
            return Result<UserDto>.Conflict("User with this email already exists");

        var nameParts = request.Name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = nameParts[0];
        var lastName = nameParts.Length > 1 ? nameParts[1] : "";

        var user = new User
        {
            Email = request.Email,
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = passwordHasher.HashPassword(null!, request.Password)
        };

        unitOfWork.Users.Add(user);
        await unitOfWork.SaveChangesAsync();

        return Result<UserDto>.Success(MapToUserDto(user));
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

        await unitOfWork.SaveChangesAsync();

        return Result<UserDto>.Success(MapToUserDto(user));
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
        await unitOfWork.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<UserDto>> GetUserAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return Result<UserDto>.BadRequest("Invalid user ID format");

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        if (user == null)
            return Result<UserDto>.NotFound("User not found");

        return Result<UserDto>.Success(MapToUserDto(user));
    }

    public async Task<Result<UserDto>> UpdateUserAsync(string userId, UpdateUserRequestDto request)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return Result<UserDto>.BadRequest("Invalid user ID format");

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

        await unitOfWork.SaveChangesAsync();

        return Result<UserDto>.Success(MapToUserDto(user));
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
        await unitOfWork.SaveChangesAsync();

        return Result.Success();
    }

    private static UserDto MapToUserDto(User user)
    {
        return new UserDto
        {
            Id = user.Guid.ToString(),
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}