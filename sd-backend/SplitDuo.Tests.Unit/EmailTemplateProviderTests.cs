using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SplitDuo.Core.Domain.Email;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Options;
using SplitDuo.Core.Services;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class EmailTemplateProviderTests
{
    private static readonly AppOptions TestAppOptions = new()
    {
        Environment = "Test",
        BaseUrl = "https://test.local",
        InitialUserEmail = "test@test.com",
        InitialUserFirstName = "Test",
        InitialUserLastName = "User",
        InitialUserPassword = "password"
    };

    private static readonly FakeTimeProvider TestTimeProvider = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static EmailTemplateProvider CreateProvider() =>
        new(Options.Create(TestAppOptions), TestTimeProvider);

    #region Template rendering — English

    [Fact]
    public void Render_PasswordResetModel_English_ReturnsSubjectAndBodyWithPlaceholders()
    {
        var provider = CreateProvider();
        var model = new PasswordResetModel
        {
            To = "user@example.com",
            FirstName = "John",
            ResetToken = "abc123token"
        };

        var result = provider.Render(model, "en");

        Assert.Equal("Reset your SplitDuo password", result.Subject);
        Assert.Contains("John", result.Body);
        Assert.Contains(
            "https://test.local/reset-password?email=user%40example.com&amp;token=abc123token",
            result.Body);
    }

    [Fact]
    public void Render_GroupInvitationModel_English_ReturnsSubjectAndBodyWithPlaceholders()
    {
        var provider = CreateProvider();
        var model = new GroupInvitationModel
        {
            To = "invited@example.com",
            GroupName = "Trip to Paris",
            InviterFirstName = "Alice",
            InviterLastName = "Smith",
            RawToken = "invitetoken123"
        };

        var result = provider.Render(model, "en");

        Assert.Equal("Alice invited you to join Trip to Paris on SplitDuo", result.Subject);
        Assert.Contains("Trip to Paris", result.Body);
        Assert.Contains("Alice", result.Body);
        Assert.Contains("Smith", result.Body);
        Assert.Contains("https://test.local/invite/accept?token=invitetoken123", result.Body);
    }

    [Fact]
    public void Render_ExpenseAddedModel_English_ReturnsSubjectAndBodyWithPlaceholders()
    {
        var provider = CreateProvider();
        var model = new ExpenseAddedModel
        {
            To = "member@example.com",
            RecipientFirstName = "Bob",
            AddedByFirstName = "Charlie",
            AddedByLastName = "Brown",
            GroupName = "House Expenses",
            GroupGuid = Guid.NewGuid(),
            ExpenseTitle = "Groceries",
            ExpenseAmount = 42.50m,
            ExpenseDate = new DateOnly(2026, 1, 15)
        };

        var result = provider.Render(model, "en");

        Assert.Equal("Charlie added an expense to House Expenses", result.Subject);
        Assert.Contains("Groceries", result.Body);
        Assert.Contains("42.50", result.Body);
        Assert.Contains("January 15, 2026", result.Body);
        Assert.Contains("House Expenses", result.Body);
        Assert.Contains($"https://test.local/groups/{model.GroupGuid}", result.Body);
    }

    [Fact]
    public void Render_PasswordResetSuccessModel_English_ReturnsSubjectAndBodyWithPlaceholders()
    {
        var provider = CreateProvider();
        var model = new PasswordResetSuccessModel
        {
            To = "user@example.com",
            FirstName = "John"
        };

        var result = provider.Render(model, "en");

        Assert.Equal("Your SplitDuo password has been reset", result.Subject);
        Assert.Contains("John", result.Body);
        Assert.Contains("user@example.com", result.Body);
        Assert.Contains("2026-01-01 00:00:00 UTC", result.Body);
    }

    [Fact]
    public void Render_TwoFactorEnabledModel_English_ReturnsSubjectAndBodyWithPlaceholders()
    {
        var provider = CreateProvider();
        var model = new TwoFactorEnabledModel
        {
            To = "user@example.com",
            FirstName = "Jane"
        };

        var result = provider.Render(model, "en");

        Assert.Equal("Two-factor authentication is now active", result.Subject);
        Assert.Contains("Jane", result.Body);
    }

    [Fact]
    public void Render_GroupMemberAddedModel_English_ReturnsSubjectAndBodyWithPlaceholders()
    {
        var provider = CreateProvider();
        var model = new GroupMemberAddedModel
        {
            To = "member@example.com",
            RecipientFirstName = "Bob",
            AddedByFirstName = "Alice",
            AddedByLastName = "Smith",
            GroupName = "Team Project",
            GroupGuid = Guid.NewGuid()
        };

        var result = provider.Render(model, "en");

        Assert.Equal("You've been added to Team Project", result.Subject);
        Assert.Contains("Bob", result.Body);
        Assert.Contains("Alice Smith", result.Body);
        Assert.Contains($"https://test.local/groups/{model.GroupGuid}", result.Body);
    }

    #endregion

    #region Template rendering — Italian

    [Fact]
    public void Render_PasswordResetModel_Italian_ReturnsItalianSubjectAndBody()
    {
        var provider = CreateProvider();
        var model = new PasswordResetModel
        {
            To = "user@example.com",
            FirstName = "John",
            ResetToken = "abc123token"
        };

        var result = provider.Render(model, "it");

        Assert.Equal("Reimposta la tua password di SplitDuo", result.Subject);
        Assert.Contains("Ciao John", result.Body);
        Assert.Contains(
            "https://test.local/reset-password?email=user%40example.com&amp;token=abc123token",
            result.Body);
    }

    [Fact]
    public void Render_GroupInvitationModel_Italian_ReturnsItalianSubjectAndBody()
    {
        var provider = CreateProvider();
        var model = new GroupInvitationModel
        {
            To = "invited@example.com",
            GroupName = "Viaggio a Roma",
            InviterFirstName = "Marco",
            InviterLastName = "Rossi",
            RawToken = "token456"
        };

        var result = provider.Render(model, "it");

        Assert.Equal("Marco ti ha invitato a unirti a Viaggio a Roma su SplitDuo", result.Subject);
        Assert.Contains("Ciao,", result.Body);
        Assert.Contains("Marco Rossi", result.Body);
        Assert.Contains("Viaggio a Roma", result.Body);
        Assert.Contains("https://test.local/invite/accept?token=token456", result.Body);
    }

    #endregion

    #region Language fallback

    [Fact]
    public void Render_UnknownLanguage_FallsBackToEnglish()
    {
        var provider = CreateProvider();
        var model = new PasswordResetModel
        {
            To = "user@example.com",
            FirstName = "John",
            ResetToken = "token123"
        };

        var frenchResult = provider.Render(model, "fr");
        var englishResult = provider.Render(model, "en");

        Assert.Equal(englishResult.Subject, frenchResult.Subject);
        Assert.Equal(englishResult.Body, frenchResult.Body);
    }

    [Fact]
    public void Render_NullLanguage_DefaultsToEnglish()
    {
        var provider = CreateProvider();
        var model = new PasswordResetModel
        {
            To = "user@example.com",
            FirstName = "John",
            ResetToken = "token123"
        };

        var defaultResult = provider.Render(model);
        var englishResult = provider.Render(model, "en");

        Assert.Equal(englishResult.Subject, defaultResult.Subject);
        Assert.Equal(englishResult.Body, defaultResult.Body);
    }

    #endregion

    #region Placeholder substitution

    [Fact]
    public void Render_AllPlaceholdersSubstituted()
    {
        var provider = CreateProvider();
        var model = new PasswordResetModel
        {
            To = "user@example.com",
            FirstName = "John",
            ResetToken = "token123"
        };

        var result = provider.Render(model, "en");

        Assert.DoesNotMatch(@"\{\{\w+\}\}", result.Body);
        Assert.DoesNotMatch(@"\{\{\w+\}\}", result.Subject);
    }

    [Fact]
    public void Render_HtmlEscaping()
    {
        var provider = CreateProvider();
        var model = new PasswordResetModel
        {
            To = "user@example.com",
            FirstName = "<script>alert(1)</script>",
            ResetToken = "token123"
        };

        var result = provider.Render(model, "en");

        Assert.Contains("&lt;script&gt;", result.Body);
        Assert.DoesNotContain("<script>", result.Body);
    }

    #endregion

    #region All 11 templates exist

    public static IEnumerable<object[]> AllTemplateModels()
    {
        yield return new object[] { new PasswordResetModel { To = "a@b.com", FirstName = "A", ResetToken = "tok" } };
        yield return new object[] { new PasswordResetSuccessModel { To = "a@b.com", FirstName = "A" } };
        yield return new object[] { new PasswordChangedModel { To = "a@b.com", FirstName = "A" } };
        yield return new object[] { new TwoFactorEnabledModel { To = "a@b.com", FirstName = "A" } };
        yield return new object[] { new TwoFactorDisabledModel { To = "a@b.com", FirstName = "A" } };
        yield return new object[] { new GroupInvitationModel { To = "a@b.com", GroupName = "G", InviterFirstName = "I", InviterLastName = "L", RawToken = "tok" } };
        yield return new object[] { new GroupMemberAddedModel { To = "a@b.com", RecipientFirstName = "R", AddedByFirstName = "A", AddedByLastName = "L", GroupName = "G", GroupGuid = Guid.NewGuid() } };
        yield return new object[] { new GroupDeletedModel { To = "a@b.com", RecipientFirstName = "R", DeletedByFirstName = "D", DeletedByLastName = "L", GroupName = "G" } };
        yield return new object[] { new GroupMemberRemovedModel { To = "a@b.com", RecipientFirstName = "R", RemovedByFirstName = "R", RemovedByLastName = "L", GroupName = "G" } };
        yield return new object[] { new ExpenseAddedModel { To = "a@b.com", RecipientFirstName = "R", AddedByFirstName = "A", AddedByLastName = "L", GroupName = "G", GroupGuid = Guid.NewGuid(), ExpenseTitle = "E", ExpenseAmount = 10m, ExpenseDate = new DateOnly(2026, 1, 1) } };
        yield return new object[] { new ExpenseDeletedModel { To = "a@b.com", RecipientFirstName = "R", DeletedByFirstName = "D", DeletedByLastName = "L", GroupName = "G", GroupGuid = Guid.NewGuid(), ExpenseTitle = "E", ExpenseAmount = 10m, ExpenseDate = new DateOnly(2026, 1, 1) } };
    }

    [Theory]
    [MemberData(nameof(AllTemplateModels))]
    public void Render_AllTemplates_English_ReturnsNonEmptyBody(ITemplateModel model)
    {
        var provider = CreateProvider();

        var result = provider.Render(model, "en");

        Assert.False(string.IsNullOrWhiteSpace(result.Subject));
        Assert.False(string.IsNullOrWhiteSpace(result.Body));
    }

    #endregion

    #region Subject parsing

    [Fact]
    public void Render_SubjectParsedFromHtmlComment()
    {
        var provider = CreateProvider();
        var model = new PasswordResetModel
        {
            To = "user@example.com",
            FirstName = "John",
            ResetToken = "token123"
        };

        var result = provider.Render(model, "en");

        Assert.DoesNotContain("<!--", result.Subject);
        Assert.DoesNotContain("-->", result.Subject);
        Assert.Equal("Reset your SplitDuo password", result.Subject);
    }

    #endregion
}
