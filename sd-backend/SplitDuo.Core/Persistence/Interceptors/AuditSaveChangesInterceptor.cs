using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using SplitDuo.Core.Domain.Interfaces;

namespace SplitDuo.Core.Persistence.Interceptors;

public class AuditSaveChangesInterceptor(
    ILogger<AuditSaveChangesInterceptor> logger,
    TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AuditEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        AuditEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AuditEntities(DbContext? context)
    {
        if (context == null) return;

        var entries = context.ChangeTracker.Entries<IAuditableEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .ToList();

        if (entries.Count > 0)
        {
            logger.LogDebug("Auditing {EntityCount} entities", entries.Count);
        }

        foreach (var entry in entries)
        {
            var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();
            var entityType = entry.Entity.GetType().Name;

            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                logger.LogDebug("Setting CreatedAt for new {EntityType}", entityType);
            }

            entry.Entity.UpdatedAt = now;
            logger.LogDebug("Setting UpdatedAt for {EntityType}", entityType);
        }
    }
}