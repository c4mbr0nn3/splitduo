using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using SplitDuo.Core.Options;
using SplitDuo.Core.Options.Setup;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Persistence.Interceptors;
using SplitDuo.Core.Services;

namespace SplitDuo.Core.Extensions;

public static class ApiProgramExtensions
{
    public static void AddInfrastructure(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, configuration) =>
            configuration.ReadFrom.Configuration(context.Configuration));

        builder.ConfigureOptions();
        builder.ConfigureServices();

        builder.Services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(dbOptions.ConnectionString);
            options.AddInterceptors(
                sp.GetRequiredService<SoftDeleteSaveChangesInterceptor>(),
                sp.GetRequiredService<AuditSaveChangesInterceptor>()
            );
        });

        builder.AddAuthentication();
    }

    private static void ConfigureOptions(this WebApplicationBuilder builder)
    {
        builder.Services.ConfigureOptions<AppOptionsSetup>();
        builder.Services.ConfigureOptions<DatabaseOptionsSetup>();
        builder.Services.ConfigureOptions<JwtOptionsSetup>();
    }

    private static void ConfigureServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<AuditSaveChangesInterceptor>();
        builder.Services.AddScoped<SoftDeleteSaveChangesInterceptor>();
        builder.Services.AddHostedService<DataSeederService>();
    }


    private static void AddAuthentication(this WebApplicationBuilder builder)
    {
        var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();
        if (jwtOptions == null)
            throw new InvalidOperationException($"Configuration section '{JwtOptions.SectionName}' is missing.");

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtOptions.SecretKey))
            };
        });

        builder.Services.AddAuthorization();
    }
}