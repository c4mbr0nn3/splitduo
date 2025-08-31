using Scalar.AspNetCore;
using Serilog;
using SplitDuo.Api.Features.Authentication.Services;
using SplitDuo.Api.Features.Common.Services;
using SplitDuo.Api.Features.Expenses.Services;
using SplitDuo.Api.Features.Groups.Services;
using SplitDuo.Api.Features.Import.Services;
using SplitDuo.Api.Features.Settlements.Services;
using SplitDuo.Api.Features.Users.Services;
using SplitDuo.Core.Extensions;

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
        builder.Services.AddScoped<IUserContextService, UserContextService>();
        builder.Services.AddScoped<IUsersService, UsersService>();
        builder.Services.AddScoped<IGroupsService, GroupsService>();
        builder.Services.AddScoped<IExpensesService, ExpensesService>();
        builder.Services.AddScoped<IBalancesService, BalancesService>();
        builder.Services.AddScoped<ISettlementsService, SettlementsService>();
        builder.Services.AddScoped<IImportService, CospendImportService>();
    }

    public static void ConfigureServices(this WebApplication app)
    {
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.UseHttpsRedirection();
        app.UseSerilogRequestLogging();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
    }
}