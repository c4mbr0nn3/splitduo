using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Entities;

namespace SplitDuo.Core.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.ComplexProperty(u => u.Settings, s => s.ToJson("settings"));
        });

        // Partial / mixed-sort-order indexes (not expressible via [Index] attributes).
        // HasFilter uses raw SQL against DB column names (snake_case).
        modelBuilder.Entity<Expense>(e =>
        {
            e.HasIndex(x => new { x.GroupId, x.ExpenseDate, x.CreatedAt }).HasFilter("deleted_at IS NULL");
            e.HasIndex(x => new { x.GroupId, x.CategoryId, x.ExpenseDate }).HasFilter("deleted_at IS NULL");
            e.HasIndex(x => new { x.GroupId, x.ExpenseTypeId, x.ExpenseDate }).HasFilter("deleted_at IS NULL");
        });

        modelBuilder.Entity<GroupMember>(e =>
        {
            e.HasIndex(x => new { x.GroupId, x.UserId }).HasFilter("deleted_at IS NULL");
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.HasIndex(x => x.RetryCount).HasFilter("sent_at IS NULL");
            e.HasIndex(x => x.SentAt).HasFilter("sent_at IS NOT NULL");
        });

        modelBuilder.Entity<RecurringExpenseInstance>(e =>
        {
            e.HasIndex(x => x.StatusId).HasFilter("expense_id IS NULL AND deleted_at IS NULL");
        });

        modelBuilder.Entity<RecurringExpenseTemplate>(e =>
        {
            e.HasIndex(x => x.AnchorDate).HasFilter("is_active AND deleted_at IS NULL");
        });

        modelBuilder.Entity<AiCallLog>(e =>
        {
            e.HasIndex(x => new { x.Success, x.RequestedAt, x.Id }).IsDescending(false, true, true);
            // Default admin view filters on time only (no success), so it needs its own ordered index.
            e.HasIndex(x => new { x.RequestedAt, x.Id }).IsDescending(true, true);
        });
    }

    public DbSet<Expense> Expenses { get; set; }
    public DbSet<ExpenseSplit> ExpenseSplits { get; set; }
    public DbSet<ExpenseAttachment> ExpenseAttachments { get; set; }
    public DbSet<UserAvatar> UserAvatars { get; set; }
    public DbSet<Group> Groups { get; set; }
    public DbSet<GroupMember> GroupMembers { get; set; }
    public DbSet<Import> Imports { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<TwoFactorToken> TwoFactorTokens { get; set; }
    public DbSet<InvitationToken> InvitationTokens { get; set; }
    public DbSet<AiCallLog> AiCallLogs { get; set; }
    public DbSet<Alias> Aliases { get; set; }
    public DbSet<ExpenseAliasSplit> ExpenseAliasSplits { get; set; }
    public DbSet<RecurringExpenseTemplate> RecurringExpenseTemplates { get; set; }
    public DbSet<RecurringExpenseTemplateSplit> RecurringExpenseTemplateSplits { get; set; }
    public DbSet<RecurringExpenseTemplateAliasSplit> RecurringExpenseTemplateAliasSplits { get; set; }
    public DbSet<RecurringExpenseInstance> RecurringExpenseInstances { get; set; }
    public DbSet<RecurringExpenseInstanceSplit> RecurringExpenseInstanceSplits { get; set; }
    public DbSet<RecurringExpenseInstanceAliasSplit> RecurringExpenseInstanceAliasSplits { get; set; }
}