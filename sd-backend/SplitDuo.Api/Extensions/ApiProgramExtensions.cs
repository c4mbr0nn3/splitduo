using Scalar.AspNetCore;
using Serilog;
using SplitDuo.Api.Services;
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
        builder.Services.AddScoped<INotificationService, EmailNotificationService>();
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