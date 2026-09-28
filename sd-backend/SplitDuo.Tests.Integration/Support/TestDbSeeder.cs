using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Tests.Integration.Support;

/// <summary>
/// Static helpers for seeding test data directly into the database.
/// </summary>
public static class TestDbSeeder
{
    /// <summary>
    /// Seeds a user (BaseUser role by default) into the database and returns the email.
    /// Mirror of SplitDuoApiFactory.SeedAdminUserAsync but configurable.
    /// </summary>
    public static async Task<string> SeedUserAsync(
        IServiceProvider services,
        string email = "user2@localhost",
        string password = "changeme123",
        string firstName = "Second",
        string lastName = "User",
        GlobalRole role = GlobalRole.BaseUser)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var user = new User
        {
            Guid = Guid.CreateVersion7(),
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = passwordHasher.HashPassword(null!, password),
            GlobalRoleId = (int)role,
            SecurityStamp = Guid.CreateVersion7().ToString(),
            Settings = new UserSettings(),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return email;
    }

    /// <summary>Seeds an ai_call_logs row directly and returns its int Id.</summary>
    public static async Task<int> SeedAiCallLogAsync(
        IServiceProvider services,
        int userId,
        string feature = "receipt_parse",
        string model = "test-model",
        long? requestedAt = null,
        bool success = true,
        int? inputTokens = 10,
        int? outputTokens = 20,
        int? totalTokens = 30,
        int? latencyMs = 100,
        string? errorMessage = null)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var at = requestedAt ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var log = new AiCallLog
        {
            UserId = userId,
            Feature = feature,
            Model = model,
            RequestedAt = at,
            RespondedAt = at + 1,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = totalTokens,
            LatencyMs = latencyMs,
            Success = success,
            ErrorMessage = errorMessage,
        };
        db.AiCallLogs.Add(log);
        await db.SaveChangesAsync();
        return log.Id;
    }

    /// <summary>Returns the int Id of the seeded SystemAdmin user.</summary>
    public static async Task<int> GetAdminIntIdAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users
            .Where(u => u.GlobalRoleId == (int)GlobalRole.SystemAdmin)
            .Select(u => u.Id)
            .FirstAsync();
    }

    /// <summary>Returns the Guid of the user with the given email.</summary>
    public static async Task<Guid> GetUserGuidAsync(IServiceProvider services, string email)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users
            .Where(u => u.Email == email)
            .Select(u => u.Guid)
            .FirstAsync();
    }

    /// <summary>Returns the int Id of the user with the given email.</summary>
    public static async Task<int> GetUserIntIdAsync(IServiceProvider services, string email)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users
            .Where(u => u.Email == email)
            .Select(u => u.Id)
            .FirstAsync();
    }
}
