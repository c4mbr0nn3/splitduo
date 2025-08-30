using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Entities;

namespace SplitDuo.Core.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Expense> Expenses { get; set; }
    public DbSet<ExpenseSplit> ExpenseSplits { get; set; }
    public DbSet<Group> Groups { get; set; }
    public DbSet<GroupMember> GroupMembers { get; set; }
    public DbSet<Import> Imports { get; set; }
    public DbSet<Settlement> Settlements { get; set; }
    public DbSet<User> Users { get; set; }
}