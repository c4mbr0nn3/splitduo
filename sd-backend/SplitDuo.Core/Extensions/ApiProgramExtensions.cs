using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Quartz;
using Serilog;
using SplitDuo.Core.Caching;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Exceptions;
using SplitDuo.Core.Options;
using SplitDuo.Core.Options.Setup;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Persistence.Interceptors;
using SplitDuo.Core.Services;
using SplitDuo.Core.Services.BackgroundJobs;

namespace SplitDuo.Core.Extensions;

public static class ApiProgramExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public void AddInfrastructure()
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

        private void ConfigureOptions()
        {
            builder.Services.ConfigureOptions<AppOptionsSetup>();
            builder.Services.ConfigureOptions<DatabaseOptionsSetup>();
            builder.Services.ConfigureOptions<JwtOptionsSetup>();
            builder.Services.ConfigureOptions<JwtBearerOptionsSetup>();
            builder.Services.ConfigureOptions<SmtpOptionsSetup>();
            builder.Services.ConfigureOptions<AiOptionsSetup>();
            builder.Services.ConfigureOptions<UpdateCheckOptionsSetup>();
            builder.Services.ConfigureOptions<RequestLocalizationOptionsSetup>();
        }

        private void ConfigureServices()
        {
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
            builder.Services.AddProblemDetails();

            builder.Services.AddScoped<AuditSaveChangesInterceptor>();
            builder.Services.AddScoped<SoftDeleteSaveChangesInterceptor>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
            builder.Services.AddScoped<ISmtpService, SmtpService>();
            builder.Services.AddScoped<INotificationService, EmailNotificationService>();
            builder.Services.AddScoped<IEmailTemplateProvider, EmailTemplateProvider>();

            // Caching
            builder.Services.AddMemoryCache(o => o.SizeLimit = 10_000);
            builder.Services.AddHybridCache(options =>
            {
                options.MaximumPayloadBytes = 1024 * 1024; // 1 MB
                options.MaximumKeyLength = 512;
                options.DefaultEntryOptions = new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromSeconds(60),
                };
            });
            builder.Services.AddSingleton<HybridCacheInvalidator>();
            builder.Services.AddSingleton<ICacheInvalidator>(sp => sp.GetRequiredService<HybridCacheInvalidator>());
            builder.Services.AddSingleton<ITestCacheInvalidator>(sp => sp.GetRequiredService<HybridCacheInvalidator>());

            // hosted services
            builder.Services.AddHostedService<DataSeederService>();

            builder.AddQuartz();
        }

        private void AddQuartz()
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
                    .WithCronSchedule("0 0 2 * * ?")); // every day at 02:00

                var tempFileCleanupJobKey = new JobKey("TempFileCleanupJob");
                q.AddJob<TempFileCleanupJob>(opts => opts.WithIdentity(tempFileCleanupJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(tempFileCleanupJobKey)
                    .WithIdentity("TempFileCleanupTrigger")
                    .WithCronSchedule("0 0 4 * * ?")); // every day at 04:00

                var emailNotificationProcessingJobKey = new JobKey("EmailNotificationProcessingJob");
                q.AddJob<EmailNotificationProcessingJob>(opts => opts.WithIdentity(emailNotificationProcessingJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(emailNotificationProcessingJobKey)
                    .WithIdentity("EmailNotificationProcessingTrigger")
                    .WithCronSchedule("0 */2 * ? * *")); // every 2 minutes

                var emailNotificationPruneJobKey = new JobKey("EmailNotificationPruneJob");
                q.AddJob<EmailNotificationPruneJob>(opts => opts.WithIdentity(emailNotificationPruneJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(emailNotificationPruneJobKey)
                    .WithIdentity("EmailNotificationPruneTrigger")
                    .WithCronSchedule("0 0 1 * * ?")); // every day at 01:00

                var refreshTokenCleanupJobKey = new JobKey("RefreshTokenCleanupJob");
                q.AddJob<RefreshTokenCleanupJob>(opts => opts.WithIdentity(refreshTokenCleanupJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(refreshTokenCleanupJobKey)
                    .WithIdentity("RefreshTokenCleanupTrigger")
                    .WithCronSchedule("0 0 3 * * ?")); // every day at 03:00
            });

            builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
        }

        private void AddAuthentication()
        {
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer();

            builder.Services.AddAuthorizationBuilder()
                .AddPolicy("SystemAdmin", policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c =>
                            c.Type == "role" && c.Value == ((int)GlobalRole.SystemAdmin).ToString())));
        }
    }
}