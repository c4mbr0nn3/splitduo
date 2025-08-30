using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SplitDuo.Core.Domain.Entities;
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

        var passwordHasher = new PasswordHasher<User>();
        var firstUser = new User
        {
            Email = appOptions.Value.InitialUserEmail,
            FirstName = appOptions.Value.InitialUserFirstName,
            LastName = appOptions.Value.InitialUserLastName,
        };

        firstUser.PasswordHash = passwordHasher.HashPassword(firstUser, appOptions.Value.InitialUserPassword);

        context.Users.Add(firstUser);
        await context.SaveChangesAsync();

        logger.LogInformation("Initial user created: {Email}", firstUser.Email);
    }
}