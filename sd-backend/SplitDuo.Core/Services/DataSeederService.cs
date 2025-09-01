using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Options;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Core.Services;

public class DataSeederService(IServiceProvider serviceProvider, ILogger<DataSeederService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var appOptions = scope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>();

        await SeedInitialUserAsync(context, appOptions);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedInitialUserAsync(AppDbContext context, IOptions<AppOptions> appOptions)
    {
        if (await context.Users.AnyAsync())
        {
            logger.LogInformation("Users already exist, skipping initial user creation");
            return;
        }

        using var scope = serviceProvider.CreateScope();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var firstUser = new User
        {
            Email = appOptions.Value.InitialUserEmail,
            FirstName = appOptions.Value.InitialUserFirstName,
            LastName = appOptions.Value.InitialUserLastName,
            PasswordHash = passwordHasher.HashPassword(null!, appOptions.Value.InitialUserPassword),
            GlobalRole = GlobalRole.SystemAdmin
        };

        context.Users.Add(firstUser);
        await context.SaveChangesAsync();

        logger.LogInformation("Initial system admin user created: {Email}", firstUser.Email);
    }
}