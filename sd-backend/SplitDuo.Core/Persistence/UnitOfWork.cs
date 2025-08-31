using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SplitDuo.Core.Domain.Entities;

namespace SplitDuo.Core.Persistence;

public interface IUnitOfWork : IDisposable
{
    DbSet<User> Users { get; }
    DbSet<Group> Groups { get; }
    DbSet<GroupMember> GroupMembers { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<ExpenseSplit> ExpenseSplits { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Settlement> Settlements { get; }
    DbSet<Import> Imports { get; }
    DbSet<Notification> Notifications { get; }

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
    public DbSet<Settlement> Settlements => context.Settlements;
    public DbSet<Import> Imports => context.Imports;
    public DbSet<Notification> Notifications => context.Notifications;

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
}