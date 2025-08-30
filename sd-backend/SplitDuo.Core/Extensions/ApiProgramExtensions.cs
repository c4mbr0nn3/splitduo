using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Quartz;
using Serilog;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Exceptions;
using SplitDuo.Core.Options;
using SplitDuo.Core.Options.Setup;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Persistence.Interceptors;
using SplitDuo.Core.Services;
using SplitDuo.Core.Services.Cron;

namespace SplitDuo.Core.Extensions;

public static class ApiProgramExtensions
{
    public static void AddInfrastructure(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, serviceProvider, configuration) =>
        {
            configuration.ReadFrom.Configuration(context.Configuration);

            if (context.HostingEnvironment.IsDevelopment()) return;

            var dbOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            configuration.WriteTo.PostgreSQL(
                connectionString: dbOptions.ConnectionString,
                tableName: "logs",
                schemaName: "logging",
                columnOptions: null,
                needAutoCreateTable: true,
                needAutoCreateSchema: true,
                period: TimeSpan.FromSeconds(30),
                batchSizeLimit: 100);
        });

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

        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();

        builder.Services.AddScoped<AuditSaveChangesInterceptor>();
        builder.Services.AddScoped<SoftDeleteSaveChangesInterceptor>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        builder.Services.AddHostedService<DataSeederService>();

        builder.AddQuartz();
    }

    private static void AddQuartz(this WebApplicationBuilder builder)
    {
        builder.Services.AddQuartz(q =>
        {
            q.SchedulerId = "SplitDuo-Scheduler";
            q.UseSimpleTypeLoader();
            q.UseInMemoryStore();
            q.UseDefaultThreadPool(tp => { tp.MaxConcurrency = 5; });

            var logCleanupJobKey = new JobKey("LogCleanupJob");
            q.AddJob<LogCleanupJob>(opts => opts.WithIdentity(logCleanupJobKey));
            q.AddTrigger(opts => opts
                .ForJob(logCleanupJobKey)
                .WithIdentity("LogCleanupTrigger")
                .WithCronSchedule("0 0 2 * * ?"));
        });

        builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
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