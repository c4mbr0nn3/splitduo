using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SplitDuo.Core.Domain.Entities;

namespace SplitDuo.Core.Persistence;

public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    DbSet<User> Users { get; }
    DbSet<Group> Groups { get; }
    DbSet<GroupMember> GroupMembers { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<ExpenseSplit> ExpenseSplits { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Import> Imports { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<TwoFactorToken> TwoFactorTokens { get; }
    DbSet<InvitationToken> InvitationTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

public class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;

    public DbSet<User> Users => context.Users;
    public DbSet<Group> Groups => context.Groups;
    public DbSet<GroupMember> GroupMembers => context.GroupMembers;
    public DbSet<Expense> Expenses => context.Expenses;
    public DbSet<ExpenseSplit> ExpenseSplits => context.ExpenseSplits;
    public DbSet<RefreshToken> RefreshTokens => context.RefreshTokens;
    public DbSet<Import> Imports => context.Imports;
    public DbSet<Notification> Notifications => context.Notifications;
    public DbSet<TwoFactorToken> TwoFactorTokens => context.TwoFactorTokens;
    public DbSet<InvitationToken> InvitationTokens => context.InvitationTokens;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        context.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction != null) await _transaction.DisposeAsync();
        await context.DisposeAsync();
    }
}