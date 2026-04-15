using System.Net.Mime;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Serilog;
using SplitDuo.Api.Features.Authentication.Services;
using SplitDuo.Api.Features.Common.Services;
using SplitDuo.Api.Features.Expenses.Services;
using SplitDuo.Api.Features.Groups.Services;
using SplitDuo.Api.Features.Invitations.Services;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Api.Features.Users.Services;
using SplitDuo.Core.Extensions;
using SplitDuo.Core.Factories;
using SplitDuo.Core.Options;
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
        builder.Services.AddScoped<IInvitationsService, InvitationsService>();
        builder.Services.AddScoped<IExpensesService, ExpensesService>();
        builder.Services.AddScoped<IBalancesService, BalancesService>();
        builder.Services.AddScoped<IExportsService, SplitDuoExportsService>();
        builder.Services.AddScoped<IImportValidatorService, ImportValidatorServiceService>();

        // Register keyed services
        builder.Services.AddKeyedScoped<IImportsService, CospendImportsService>(ImportType.Cospend);
        builder.Services.AddKeyedScoped<IImportsService, SplitDuoImportsService>(ImportType.SplitDuo);
        builder.Services.AddKeyedScoped<IImportsService, SplitwiseImportsService>(ImportType.Splitwise);

        // Register factories
        builder.Services.AddScoped<IImportServiceFactory, ImportServiceFactory>();

        builder.Services.AddHealthChecks()
            .AddNpgSql(
                connectionStringFactory: sp =>
                    sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString,
                name: "database",
                tags: ["db", "postgresql"]);

        builder.Services.AddRateLimiter(opts =>
        {
            opts.AddFixedWindowLimiter("auth", o =>
            {
                o.PermitLimit = 10;
                o.Window = TimeSpan.FromMinutes(1);
                o.QueueLimit = 0;
            });
            opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });
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

        // Serve static files from wwwroot (.webmanifest needs explicit MIME type)
        var contentTypeProvider = new FileExtensionContentTypeProvider();
        contentTypeProvider.Mappings[".webmanifest"] = "application/manifest+json";
        app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypeProvider });

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteHealthCheckResponse
        });

        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        // Fallback to serve the SPA for client-side routing
        app.MapFallbackToFile("index.html");
    }

    private static Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = MediaTypeNames.Application.Json;
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => new { status = e.Value.Status.ToString(), description = e.Value.Description }),
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
        return context.Response.WriteAsync(result);
    }
}