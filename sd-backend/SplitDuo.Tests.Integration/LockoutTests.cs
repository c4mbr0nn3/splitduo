using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitDuo.Api.Features.Authentication.Dto;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

/// <summary>
/// SEC-01: failed login attempts must persist to the database (the scoped DbContext
/// is discarded per request, so without an explicit save in the service the
/// counter never reaches 5 and lockout never engages).
/// </summary>
public class LockoutTests : IntegrationTest
{
    public LockoutTests(SplitDuoApiFactory factory) : base(factory) { }

    private const string LockoutEmail = "lockout-user@localhost";
    private const string CorrectPassword = "changeme123";
    private const string WrongPassword = "wrong-password";

    private async Task<string> SeedLockoutUserAsync()
    {
        return await TestDbSeeder.SeedUserAsync(Factory.Services,
            email: LockoutEmail, password: CorrectPassword);
    }

    private async Task<User> GetUserAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
    }

    private async Task<HttpResponseMessage> LoginAsync(string password)
    {
        var ct = TestContext.Current.CancellationToken;
        return await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = LockoutEmail,
            password,
        }, ct);
    }

    private async Task<string> GetErrorMessageAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(
            TestContext.Current.CancellationToken);
        return body!.Error!.Message;
    }

    [Fact]
    public async Task Login_FifthConsecutiveFailure_LocksAccountFor15Minutes()
    {
        await SeedLockoutUserAsync();

        for (var i = 1; i <= 4; i++)
        {
            var response = await LoginAsync(WrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            var user = await GetUserAsync(LockoutEmail);
            Assert.Equal(i, user.FailedLoginAttempts);
            Assert.Null(user.LockoutEnd);
        }

        // 5th consecutive failure engages the lockout
        var fifth = await LoginAsync(WrongPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, fifth.StatusCode);

        var now = Factory.TimeProvider.GetUtcNow().ToUnixTimeSeconds();
        var locked = await GetUserAsync(LockoutEmail);
        Assert.Equal(0, locked.FailedLoginAttempts);
        Assert.NotNull(locked.LockoutEnd);
        Assert.True(locked.LockoutEnd!.Value > now);
        // The lockout window is the spec's 15 minutes (10s slack for clock read ordering)
        Assert.InRange(locked.LockoutEnd!.Value, now + 890, now + 900);

        // While locked, further attempts keep failing (with the lockout message)
        var sixth = await LoginAsync(CorrectPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, sixth.StatusCode);
        Assert.Equal("Account temporarily locked. Try again later.", await GetErrorMessageAsync(sixth));
    }

    [Fact]
    public async Task Login_AfterLockoutExpiry_WrongPasswordStartsCounterFresh_ThenSuccessClearsIt()
    {
        await SeedLockoutUserAsync();

        // Lock the account via 5 wrong passwords
        for (var i = 0; i < 5; i++)
        {
            var failure = await LoginAsync(WrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
        }

        var lockoutEnd = (await GetUserAsync(LockoutEmail)).LockoutEnd!.Value;

        // Advance the shared fake clock past the lockout window
        var lockoutDuration = TimeSpan.FromSeconds(lockoutEnd
            - Factory.TimeProvider.GetUtcNow().ToUnixTimeSeconds());
        AdvanceTime(lockoutDuration.Add(TimeSpan.FromSeconds(1)));

        // First post-expiry attempt: fresh counter (no instant re-lock)
        var postExpiry = await LoginAsync(WrongPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, postExpiry.StatusCode);
        Assert.Equal("Invalid email or password", await GetErrorMessageAsync(postExpiry));

        var afterExpiry = await GetUserAsync(LockoutEmail);
        Assert.Equal(1, afterExpiry.FailedLoginAttempts);
        // LockoutEnd keeps the (now past) engagement timestamp — matching ASP.NET Core
        // Identity's AccessFailedAsync, which does not clear an expired LockoutEnd on a
        // sub-threshold failure. The security-relevant invariant is "not currently locked".
        Assert.False(afterExpiry.LockoutEnd.HasValue &&
            afterExpiry.LockoutEnd.Value > Factory.TimeProvider.GetUtcNow().ToUnixTimeSeconds());

        // Correct password succeeds and clears counters/lockout
        var success = await LoginAsync(CorrectPassword);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);

        var cleared = await GetUserAsync(LockoutEmail);
        Assert.Equal(0, cleared.FailedLoginAttempts);
        Assert.Null(cleared.LockoutEnd);
    }

    [Fact]
    public async Task Login_LockedThenExpiryThenCorrectPassword_Returns200_AndClearsLockoutState()
    {
        await SeedLockoutUserAsync();

        // Engage lockout
        for (var i = 0; i < 5; i++)
        {
            var failure = await LoginAsync(WrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
        }
        Assert.NotNull((await GetUserAsync(LockoutEmail)).LockoutEnd);

        // Expire the lockout and log in with the correct password
        var locked = await GetUserAsync(LockoutEmail);
        AdvanceTime(TimeSpan.FromSeconds(locked.LockoutEnd!.Value
            - Factory.TimeProvider.GetUtcNow().ToUnixTimeSeconds() + 1));

        var success = await LoginAsync(CorrectPassword);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);

        var cleared = await GetUserAsync(LockoutEmail);
        Assert.Equal(0, cleared.FailedLoginAttempts);
        Assert.Null(cleared.LockoutEnd);
    }
}