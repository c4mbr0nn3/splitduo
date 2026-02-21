using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Options;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Core.Services;

public class DataSeederService(IServiceProvider serviceProvider, ILogger<DataSeederService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var appOptions = scope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>();

        await SeedInitialUserAsync(context, appOptions);
        await SeedDemoDataAsync(context, appOptions);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedInitialUserAsync(AppDbContext context, IOptions<AppOptions> appOptions)
    {
        if (await context.Users.AnyAsync())
        {
            logger.LogInformation("Users already exist, skipping initial user creation");
            return;
        }

        using var scope = serviceProvider.CreateScope();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var firstUser = new User
        {
            Email = appOptions.Value.InitialUserEmail,
            FirstName = appOptions.Value.InitialUserFirstName,
            LastName = appOptions.Value.InitialUserLastName,
            PasswordHash = passwordHasher.HashPassword(null!, appOptions.Value.InitialUserPassword),
            GlobalRole = GlobalRole.SystemAdmin
        };

        context.Users.Add(firstUser);
        await context.SaveChangesAsync();

        logger.LogInformation("Initial system admin user created: {Email}", firstUser.Email);
    }

    private async Task SeedDemoDataAsync(AppDbContext context, IOptions<AppOptions> appOptions)
    {
        if (!appOptions.Value.SeedDemoData) return;
        if (await context.Users.AnyAsync(u => u.Email == "alice@splitduo.demo")) return;

        using var scope = serviceProvider.CreateScope();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var demoHash = passwordHasher.HashPassword(null!, "demo1234");

        var alice = new UserBuilder().Email("alice@splitduo.demo").Name("Alice", "Martin").Hash(demoHash).Build();
        var bob = new UserBuilder().Email("bob@splitduo.demo").Name("Bob", "Martin").Hash(demoHash).Build();
        var clara = new UserBuilder().Email("clara@splitduo.demo").Name("Clara", "Dubois").Hash(demoHash).Build();
        var sam = new UserBuilder().Email("sam@splitduo.demo").Name("Sam", "Rivera").Hash(demoHash).Build();
        var jamie = new UserBuilder().Email("jamie@splitduo.demo").Name("Jamie", "Ngo").Hash(demoHash).Build();
        context.Users.AddRange(alice, bob, clara, sam, jamie);
        await context.SaveChangesAsync();

        // ── Group 1: Our Expenses (Alice + Bob) ──────────────────────────────
        var group1 = new GroupBuilder().Name("Our Expenses").CreatedBy(alice.Id).Build();
        context.Groups.Add(group1);
        await context.SaveChangesAsync();

        context.GroupMembers.AddRange(
            new GroupMemberBuilder().Group(group1.Id).User(alice.Id).Role(GroupRole.Admin).Build(),
            new GroupMemberBuilder().Group(group1.Id).User(bob.Id).Role(GroupRole.Member).Build()
        );
        await context.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var group1Expenses =
            new (string Title, decimal Amount, bool PaidByAlice, ExpenseCategory Category, PaymentMode PaymentMode)[]
            {
                ("Rent (the real villain)", 750.00m, true, ExpenseCategory.Housing, PaymentMode.Transfer),
                ("Suspiciously large cheese haul", 38.60m, true, ExpenseCategory.Groceries, PaymentMode.Card),
                ("Gym membership (aspirational)", 40.00m, false, ExpenseCategory.Health, PaymentMode.Card),
                ("IKEA: 3 hours, 1 shelf, 1 argument", 49.90m, false, ExpenseCategory.Shopping, PaymentMode.Card),
                ("Plants that will definitely survive", 27.50m, true, ExpenseCategory.Shopping, PaymentMode.Card),
                ("Electricity bill", 64.00m, false, ExpenseCategory.Utilities, PaymentMode.Transfer),
                ("Late night kebab after the movie", 23.80m, false, ExpenseCategory.Dining, PaymentMode.Cash),
                ("Wine for \"cooking\"", 18.40m, true, ExpenseCategory.Groceries, PaymentMode.Card),
                ("Netflix (we watch 1 show per year)", 13.99m, false, ExpenseCategory.Entertainment,
                    PaymentMode.OnlineService),
                ("Spontaneous sushi date", 74.00m, true, ExpenseCategory.Dining, PaymentMode.Card),
                ("Doctor visit (WebMD said it was fine)", 30.00m, true, ExpenseCategory.Health, PaymentMode.Cash),
                ("Spotify Family", 15.99m, true, ExpenseCategory.Entertainment, PaymentMode.OnlineService),
                ("Metro monthly pass", 38.00m, false, ExpenseCategory.Transportation, PaymentMode.Card),
                ("Couples pottery class (R.I.P. the vase)", 55.00m, true, ExpenseCategory.Entertainment,
                    PaymentMode.Card),
                ("Impulse buy: bread maker", 89.00m, false, ExpenseCategory.Shopping, PaymentMode.OnlineService),
                ("Fancy olive oil we'll never finish", 14.90m, true, ExpenseCategory.Groceries, PaymentMode.Card),
                ("Parking fine (Bob's fault, allegedly)", 40.00m, false, ExpenseCategory.Transportation,
                    PaymentMode.Cash),
                ("Internet bill", 29.99m, false, ExpenseCategory.Utilities, PaymentMode.Transfer),
                ("Weekend trip to the coast", 210.00m, true, ExpenseCategory.Travel, PaymentMode.Card),
                ("Third pizza this week (no regrets)", 26.00m, false, ExpenseCategory.Dining, PaymentMode.Card),
            };

        for (var i = 0; i < group1Expenses.Length; i++)
        {
            var (title, amount, paidByAlice, category, paymentMode) = group1Expenses[i];
            var expense = new ExpenseBuilder()
                .InGroup(group1.Id).Title(title).Amount(amount)
                .PaidBy(paidByAlice ? alice.Id : bob.Id)
                .Date(today.AddDays(-(7 + i * 6)))
                .Category(category).PaymentMode(paymentMode)
                .Build();
            await AddExpenseWithSplitsAsync(context, expense, [
                (alice.Id, amount / 2),
                (bob.Id, amount / 2),
            ]);
        }

        // ── Group 2: The Flat (Alice + Clara + Sam) ──────────────────────────
        var group2 = new GroupBuilder().Name("The Flat").CreatedBy(alice.Id).Build();
        context.Groups.Add(group2);
        await context.SaveChangesAsync();

        context.GroupMembers.AddRange(
            new GroupMemberBuilder().Group(group2.Id).User(alice.Id).Role(GroupRole.Admin).Build(),
            new GroupMemberBuilder().Group(group2.Id).User(clara.Id).Role(GroupRole.Member).Build(),
            new GroupMemberBuilder().Group(group2.Id).User(sam.Id).Role(GroupRole.Member).Build()
        );
        await context.SaveChangesAsync();

        // Newest ~8 days ago, spread ~5 days apart (oldest ~7 weeks ago)
        var group2Expenses =
            new (string Title, decimal Amount, int PaidById, ExpenseCategory Category, PaymentMode PaymentMode)[]
            {
                ("Rent — this month", 900.00m, sam.Id, ExpenseCategory.Housing, PaymentMode.Transfer),
                ("Electricity bill", 72.30m, clara.Id, ExpenseCategory.Utilities, PaymentMode.Transfer),
                ("Internet", 34.99m, sam.Id, ExpenseCategory.Utilities, PaymentMode.Transfer),
                ("Groceries run", 81.50m, clara.Id, ExpenseCategory.Groceries, PaymentMode.Card),
                ("Cleaning supplies", 22.40m, alice.Id, ExpenseCategory.Shopping, PaymentMode.Card),
                ("New vacuum (it was necessary)", 119.00m, sam.Id, ExpenseCategory.Shopping, PaymentMode.OnlineService),
                ("Thai takeaway Friday", 47.70m, alice.Id, ExpenseCategory.Dining, PaymentMode.Cash),
                ("Coffee machine filter packs", 18.90m, clara.Id, ExpenseCategory.Groceries, PaymentMode.Card),
                ("Shared Netflix", 17.99m, sam.Id, ExpenseCategory.Entertainment, PaymentMode.OnlineService),
            };

        for (var i = 0; i < group2Expenses.Length; i++)
        {
            var (title, amount, paidById, category, paymentMode) = group2Expenses[i];
            var expense = new ExpenseBuilder()
                .InGroup(group2.Id).Title(title).Amount(amount).PaidBy(paidById)
                .Date(today.AddDays(-(8 + i * 5)))
                .Category(category).PaymentMode(paymentMode)
                .Build();
            var share3 = Math.Round(amount / 3, 2, MidpointRounding.AwayFromZero);
            await AddExpenseWithSplitsAsync(context, expense, [
                (alice.Id, share3),
                (clara.Id, share3),
                (sam.Id, share3),
            ]);
        }

        // ── Group 3: Lisbon Weekend (Alice + Bob + Clara + Jamie) ─────────────
        var group3 = new GroupBuilder().Name("Lisbon Weekend").CreatedBy(alice.Id).Build();
        context.Groups.Add(group3);
        await context.SaveChangesAsync();

        context.GroupMembers.AddRange(
            new GroupMemberBuilder().Group(group3.Id).User(alice.Id).Role(GroupRole.Admin).Build(),
            new GroupMemberBuilder().Group(group3.Id).User(bob.Id).Role(GroupRole.Member).Build(),
            new GroupMemberBuilder().Group(group3.Id).User(clara.Id).Role(GroupRole.Member).Build(),
            new GroupMemberBuilder().Group(group3.Id).User(jamie.Id).Role(GroupRole.Member).Build()
        );
        await context.SaveChangesAsync();

        // Trip was ~6 weeks ago, all expenses within a 4-day window
        var tripStart = today.AddDays(-42);
        var group3Expenses =
            new (string Title, decimal Amount, int PaidById, ExpenseCategory Category, PaymentMode PaymentMode, int
                DayOffset)[]
                {
                    ("Airbnb (3 nights)", 480.00m, bob.Id, ExpenseCategory.Travel, PaymentMode.OnlineService, 0),
                    ("Flights (group booking)", 620.00m, bob.Id, ExpenseCategory.Travel, PaymentMode.Card, 0),
                    ("Dinner at Tasca do Chico", 112.00m, clara.Id, ExpenseCategory.Dining, PaymentMode.Card, 1),
                    ("Pastéis de Belém (40 pastries)", 24.00m, jamie.Id, ExpenseCategory.Dining, PaymentMode.Cash, 1),
                    ("Hop-on hop-off bus", 68.00m, alice.Id, ExpenseCategory.Transportation, PaymentMode.Card, 1),
                    ("Uber from airport", 31.50m, bob.Id, ExpenseCategory.Transportation, PaymentMode.Cash, 0),
                    ("Fado show tickets", 80.00m, clara.Id, ExpenseCategory.Entertainment, PaymentMode.Card, 2),
                    ("Day trip to Sintra", 96.00m, alice.Id, ExpenseCategory.Travel, PaymentMode.Card, 2),
                    ("Supermarket for the apartment", 58.40m, jamie.Id, ExpenseCategory.Groceries, PaymentMode.Card, 1),
                    ("Last-night cocktails", 74.00m, bob.Id, ExpenseCategory.Dining, PaymentMode.Card, 3),
                };

        foreach (var (title, amount, paidById, category, paymentMode, dayOffset) in group3Expenses)
        {
            var expense = new ExpenseBuilder()
                .InGroup(group3.Id).Title(title).Amount(amount).PaidBy(paidById)
                .Date(tripStart.AddDays(dayOffset))
                .Category(category).PaymentMode(paymentMode)
                .Build();
            await AddExpenseWithSplitsAsync(context, expense, [
                (alice.Id, amount / 4),
                (bob.Id, amount / 4),
                (clara.Id, amount / 4),
                (jamie.Id, amount / 4),
            ]);
        }

        logger.LogInformation("Demo data seeded");
    }

    private static async Task AddExpenseWithSplitsAsync(
        AppDbContext context,
        Expense expense,
        IEnumerable<(int UserId, decimal Amount)> splits)
    {
        context.Expenses.Add(expense);
        await context.SaveChangesAsync();
        foreach (var (userId, amount) in splits)
            context.ExpenseSplits.Add(
                new ExpenseSplit { ExpenseId = expense.Id, UserId = userId, SplitAmount = amount });
        await context.SaveChangesAsync();
    }

    private sealed class UserBuilder
    {
        private string _email = "", _firstName = "", _lastName = "", _hash = "";
        private GlobalRole _role = GlobalRole.BaseUser;

        public UserBuilder Email(string v)
        {
            _email = v;
            return this;
        }

        public UserBuilder Name(string first, string last)
        {
            _firstName = first;
            _lastName = last;
            return this;
        }

        public UserBuilder Hash(string v)
        {
            _hash = v;
            return this;
        }

        public UserBuilder Role(GlobalRole v)
        {
            _role = v;
            return this;
        }

        public User Build() => new()
            { Email = _email, FirstName = _firstName, LastName = _lastName, PasswordHash = _hash, GlobalRole = _role };
    }

    private sealed class GroupBuilder
    {
        private string _name = "";
        private int _createdBy;

        public GroupBuilder Name(string v)
        {
            _name = v;
            return this;
        }

        public GroupBuilder CreatedBy(int v)
        {
            _createdBy = v;
            return this;
        }

        public Group Build() => new() { Name = _name, CreatedBy = _createdBy };
    }

    private sealed class GroupMemberBuilder
    {
        private int _groupId, _userId;
        private GroupRole _role = GroupRole.Member;

        public GroupMemberBuilder Group(int v)
        {
            _groupId = v;
            return this;
        }

        public GroupMemberBuilder User(int v)
        {
            _userId = v;
            return this;
        }

        public GroupMemberBuilder Role(GroupRole v)
        {
            _role = v;
            return this;
        }

        public GroupMember Build() => new() { GroupId = _groupId, UserId = _userId, Role = _role };
    }

    private sealed class ExpenseBuilder
    {
        private int _groupId, _paidBy;
        private string _title = "";
        private decimal _amount;
        private DateOnly _date;
        private ExpenseCategory _category;
        private PaymentMode _paymentMode;

        public ExpenseBuilder InGroup(int v)
        {
            _groupId = v;
            return this;
        }

        public ExpenseBuilder Title(string v)
        {
            _title = v;
            return this;
        }

        public ExpenseBuilder Amount(decimal v)
        {
            _amount = v;
            return this;
        }

        public ExpenseBuilder PaidBy(int v)
        {
            _paidBy = v;
            return this;
        }

        public ExpenseBuilder Date(DateOnly v)
        {
            _date = v;
            return this;
        }

        public ExpenseBuilder Category(ExpenseCategory v)
        {
            _category = v;
            return this;
        }

        public ExpenseBuilder PaymentMode(PaymentMode v)
        {
            _paymentMode = v;
            return this;
        }

        public Expense Build() => new()
        {
            GroupId = _groupId, Title = _title, Amount = _amount,
            PaidBy = _paidBy, ExpenseDate = _date, Category = _category, PaymentMode = _paymentMode
        };
    }
}