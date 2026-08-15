using Microsoft.AspNetCore.Identity;
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
}
