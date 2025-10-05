using Scalar.AspNetCore;
using Serilog;
using SplitDuo.Api.Features.Authentication.Services;
using SplitDuo.Api.Features.Common.Services;
using SplitDuo.Api.Features.Expenses.Services;
using SplitDuo.Api.Features.Groups.Services;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Api.Features.Settlements.Services;
using SplitDuo.Api.Features.Users.Services;
using SplitDuo.Core.Extensions;
using SplitDuo.Core.Factories;
using SplitDuo.Core.Services.Exports;
using SplitDuo.Core.Services.Imports;

namespace SplitDuo.Api.Extensions;

public static class ApiProgramExtensions
{
    public static void AddServices(this WebApplicationBuilder builder)
    {
        builder.AddInfrastructure();
        builder.Services.AddControllers();
        builder.Services.AddOpenApi();

        // Register API layer services
        builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
        builder.Services.AddScoped<ITwoFactorService, TwoFactorService>();
        builder.Services.AddScoped<IUserContextService, UserContextService>();
        builder.Services.AddScoped<IUsersService, UsersService>();
        builder.Services.AddScoped<IGroupsService, GroupsService>();
        builder.Services.AddScoped<IExpensesService, ExpensesService>();
        builder.Services.AddScoped<IBalancesService, BalancesService>();
        builder.Services.AddScoped<ISettlementsService, SettlementsService>();
        builder.Services.AddScoped<IExportsService, SplitDuoExportsService>();

        // Register keyed services
        builder.Services.AddKeyedScoped<IImportsService, CospendImportsService>(ImportType.Cospend);
        builder.Services.AddKeyedScoped<IImportsService, SplitDuoImportsService>(ImportType.SplitDuo);

        // Register factories
        builder.Services.AddScoped<IImportServiceFactory, ImportServiceFactory>();
    }

    public static void ConfigureServices(this WebApplication app)
    {
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
            app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        }

        app.UseHttpsRedirection();
        app.UseSerilogRequestLogging();

        // Serve static files from wwwroot
        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        // Fallback to serve the SPA for client-side routing
        app.MapFallbackToFile("index.html");
    }
}