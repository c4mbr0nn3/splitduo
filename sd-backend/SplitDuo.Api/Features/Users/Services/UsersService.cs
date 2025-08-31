using System.Security.Cryptography;
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
    Task<Result<CreateUserDto>> CreateUserAsync(CreateUserRequestDto request);
    Task<Result<UserDto>> UpdateCurrentUserAsync(Guid currentUserId, UpdateUserRequestDto request);
    Task<Result> ChangeCurrentUserPasswordAsync(Guid currentUserId, ChangePasswordRequestDto request);
    Task<Result<UserDto>> GetUserAsync(string userId);
    Task<Result<UserDto>> UpdateUserAsync(string userId, UpdateUserRequestDto request);
    Task<Result> DeleteUserAsync(string userId);
}

public class UsersService(IUnitOfWork unitOfWork, IPasswordHasher<User> passwordHasher) : IUsersService
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