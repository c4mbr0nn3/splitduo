using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.ExpenseAttachments.Dto;
using SplitDuo.Core.Persistence;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class ExpenseAttachmentsTests : IntegrationTest
{
    public ExpenseAttachmentsTests(SplitDuoApiFactory factory) : base(factory) { }

    #region Happy paths

    [Fact]
    public async Task UploadAttachment_Jpeg_Returns201()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;
        var expense = await client.CreateExpenseAsync(group.Id, adminId);

        var response = await client.UploadAttachmentAsync(group.Id, expense.Id, AttachmentTestExtensions.JpegBytes, "test.jpg");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseAttachmentDto>>(ct);
        Assert.True(Guid.TryParse(body!.Data!.Id, out _));
        Assert.Equal(expense.Id, body.Data.ExpenseId);
        Assert.Equal("test.jpg", body.Data.FilenameOriginal);
        Assert.Equal("image/jpeg", body.Data.MimeType);
        Assert.Equal(AttachmentTestExtensions.JpegBytes.Length, body.Data.SizeBytes);
        Assert.True(body.Data.CreatedAt > 0);
        Assert.True(body.Data.UpdatedAt > 0);
    }

    [Fact]
    public async Task UploadAttachment_Pdf_Returns201()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;
        var expense = await client.CreateExpenseAsync(group.Id, adminId);

        var response = await client.UploadAttachmentAsync(group.Id, expense.Id, AttachmentTestExtensions.PdfBytes, "receipt.pdf");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseAttachmentDto>>(ct);
        Assert.Equal("application/pdf", body!.Data!.MimeType);
        Assert.Equal("receipt.pdf", body.Data.FilenameOriginal);
    }

    [Fact]
    public async Task ListAttachments_ReturnsMetadata()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;
        var expense = await client.CreateExpenseAsync(group.Id, adminId);

        await client.UploadAttachmentAsync(group.Id, expense.Id, AttachmentTestExtensions.JpegBytes, "a.jpg");
        await client.UploadAttachmentAsync(group.Id, expense.Id, AttachmentTestExtensions.PdfBytes, "b.pdf");

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/expenses/{expense.Id}/attachments", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<ExpenseAttachmentDto>>>(ct);
        Assert.Equal(2, body!.Data!.Count);
        Assert.Equal("a.jpg", body.Data[0].FilenameOriginal);
        Assert.Equal("image/jpeg", body.Data[0].MimeType);
        Assert.Equal("b.pdf", body.Data[1].FilenameOriginal);
        Assert.Equal("application/pdf", body.Data[1].MimeType);

        // No bytes in the response — the DTO exposes metadata only
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var first = doc.RootElement.GetProperty("data")[0];
        Assert.False(first.TryGetProperty("content", out _));
    }

    [Fact]
    public async Task DownloadAttachment_ReturnsBytesWithHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;
        var expense = await client.CreateExpenseAsync(group.Id, adminId);

        var uploadResponse = await client.UploadAttachmentAsync(group.Id, expense.Id, AttachmentTestExtensions.JpegBytes, "test.jpg");
        var uploadBody = await uploadResponse.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseAttachmentDto>>(ct);

        var response = await client.GetAsync(
            $"/api/v1/groups/{group.Id}/expenses/{expense.Id}/attachments/{uploadBody!.Data!.Id}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType!.MediaType);

        Assert.True(response.Content.Headers.TryGetValues("Content-Disposition", out var disposition));
        Assert.StartsWith("inline", disposition!.Single());

        Assert.True(response.Headers.TryGetValues("ETag", out var etag));
        Assert.StartsWith("\"", etag!.Single());
        Assert.EndsWith("\"", etag.Single());

        Assert.True(response.Content.Headers.TryGetValues("Last-Modified", out var lastModified));
        Assert.True(DateTimeOffset.TryParse(lastModified!.Single(), out _));

        Assert.True(response.Headers.TryGetValues("Cache-Control", out var cacheControl));
        Assert.Equal("private", cacheControl!.Single());

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        Assert.Equal(AttachmentTestExtensions.JpegBytes, bytes);
    }

    [Fact]
    public async Task DeleteAttachment_RemovesRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;
        var expense = await client.CreateExpenseAsync(group.Id, adminId);

        var uploadResponse = await client.UploadAttachmentAsync(group.Id, expense.Id, AttachmentTestExtensions.JpegBytes, "test.jpg");
        var uploadBody = await uploadResponse.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseAttachmentDto>>(ct);

        var deleteResponse = await client.DeleteAsync(
            $"/api/v1/groups/{group.Id}/expenses/{expense.Id}/attachments/{uploadBody!.Data!.Id}", ct);

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var listResponse = await client.GetAsync($"/api/v1/groups/{group.Id}/expenses/{expense.Id}/attachments", ct);
        var listBody = await listResponse.Content.ReadFromJsonAsync<ApiResponseDto<List<ExpenseAttachmentDto>>>(ct);
        Assert.Empty(listBody!.Data!);
    }

    #endregion

    #region Not a member — 403

    [Fact]
    public async Task UploadAttachment_NonMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        var user2Email = await TestDbSeeder.SeedUserAsync(Factory.Services);
        var user2Client = await CreateAuthenticatedClientAsync(user2Email, "changeme123");
        var user2Group = await user2Client.CreateGroupAsync();
        var user2Expense = await user2Client.CreateExpenseAsync(user2Group.Id, user2Group.CreatedByUserId);

        var response = await adminClient.UploadAttachmentAsync(
            user2Group.Id, user2Expense.Id, AttachmentTestExtensions.JpegBytes, "test.jpg");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseAttachmentDto>>(ct);
        Assert.Equal("You do not have access to this group", body!.Error!.Message);
    }

    [Fact]
    public async Task ListAttachments_NonMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        var user2Email = await TestDbSeeder.SeedUserAsync(Factory.Services);
        var user2Client = await CreateAuthenticatedClientAsync(user2Email, "changeme123");
        var user2Group = await user2Client.CreateGroupAsync();
        var user2Expense = await user2Client.CreateExpenseAsync(user2Group.Id, user2Group.CreatedByUserId);

        var response = await adminClient.GetAsync(
            $"/api/v1/groups/{user2Group.Id}/expenses/{user2Expense.Id}/attachments", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<ExpenseAttachmentDto>>>(ct);
        Assert.Equal("You do not have access to this group", body!.Error!.Message);
    }

    [Fact]
    public async Task DownloadAttachment_NonMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        var user2Email = await TestDbSeeder.SeedUserAsync(Factory.Services);
        var user2Client = await CreateAuthenticatedClientAsync(user2Email, "changeme123");
        var user2Group = await user2Client.CreateGroupAsync();
        var user2Expense = await user2Client.CreateExpenseAsync(user2Group.Id, user2Group.CreatedByUserId);
        var uploadResponse = await user2Client.UploadAttachmentAsync(
            user2Group.Id, user2Expense.Id, AttachmentTestExtensions.JpegBytes, "test.jpg");
        var uploadBody = await uploadResponse.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseAttachmentDto>>(ct);

        var response = await adminClient.GetAsync(
            $"/api/v1/groups/{user2Group.Id}/expenses/{user2Expense.Id}/attachments/{uploadBody!.Data!.Id}", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAttachment_NonMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        var user2Email = await TestDbSeeder.SeedUserAsync(Factory.Services);
        var user2Client = await CreateAuthenticatedClientAsync(user2Email, "changeme123");
        var user2Group = await user2Client.CreateGroupAsync();
        var user2Expense = await user2Client.CreateExpenseAsync(user2Group.Id, user2Group.CreatedByUserId);
        var uploadResponse = await user2Client.UploadAttachmentAsync(
            user2Group.Id, user2Expense.Id, AttachmentTestExtensions.JpegBytes, "test.jpg");
        var uploadBody = await uploadResponse.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseAttachmentDto>>(ct);

        var response = await adminClient.DeleteAsync(
            $"/api/v1/groups/{user2Group.Id}/expenses/{user2Expense.Id}/attachments/{uploadBody!.Data!.Id}", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region File-type rejection

    [Fact]
    public async Task UploadAttachment_BadExtension_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;
        var expense = await client.CreateExpenseAsync(group.Id, adminId);

        var response = await client.UploadAttachmentAsync(group.Id, expense.Id, AttachmentTestExtensions.JpegBytes, "notes.txt");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseAttachmentDto>>(ct);
        Assert.Equal("File type not allowed. Accepted: .jpg, .jpeg, .png, .webp, .heic, .heif, .pdf", body!.Error!.Message);
    }

    [Fact]
    public async Task UploadAttachment_BadMagicNumber_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;
        var expense = await client.CreateExpenseAsync(group.Id, adminId);

        // EXE (MZ header) renamed as .jpg — passes the extension check, fails the sniff
        var response = await client.UploadAttachmentAsync(group.Id, expense.Id, AttachmentTestExtensions.BadMagicBytes, "fake.jpg");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseAttachmentDto>>(ct);
        Assert.Equal("File content does not match the declared type", body!.Error!.Message);
    }

    #endregion

    #region Size limit

    [Fact]
    public async Task UploadAttachment_ExceedsSizeLimit_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;
        var expense = await client.CreateExpenseAsync(group.Id, adminId);

        // 11MB of zeros — size check runs before content sniffing, so content doesn't matter
        var oversized = new byte[11 * 1024 * 1024];
        var response = await client.UploadAttachmentAsync(group.Id, expense.Id, oversized, "big.jpg");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseAttachmentDto>>(ct);
        Assert.Equal("File size exceeds the maximum allowed size of 10MB", body!.Error!.Message);
    }

    #endregion

    #region Per-expense cap

    [Fact]
    public async Task UploadAttachment_SixthUpload_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;
        var expense = await client.CreateExpenseAsync(group.Id, adminId);

        for (var i = 0; i < 5; i++)
        {
            var response = await client.UploadAttachmentAsync(
                group.Id, expense.Id, AttachmentTestExtensions.DistinctJpegBytes(i), $"file{i}.jpg");
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        var sixth = await client.UploadAttachmentAsync(
            group.Id, expense.Id, AttachmentTestExtensions.DistinctJpegBytes(5), "file5.jpg");

        Assert.Equal(HttpStatusCode.BadRequest, sixth.StatusCode);
        var body = await sixth.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseAttachmentDto>>(ct);
        Assert.Equal("Maximum of 5 attachments per expense reached", body!.Error!.Message);
    }

    #endregion

    #region Dedup

    [Fact]
    public async Task UploadAttachment_DuplicateToSameExpense_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;
        var expense = await client.CreateExpenseAsync(group.Id, adminId);

        var first = await client.UploadAttachmentAsync(group.Id, expense.Id, AttachmentTestExtensions.JpegBytes, "test.jpg");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var duplicate = await client.UploadAttachmentAsync(group.Id, expense.Id, AttachmentTestExtensions.JpegBytes, "copy.jpg");

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        var body = await duplicate.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseAttachmentDto>>(ct);
        Assert.Equal("This file has already been attached to this expense", body!.Error!.Message);
    }

    [Fact]
    public async Task UploadAttachment_SameFileToDifferentExpense_Returns201()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;
        var expenseA = await client.CreateExpenseAsync(group.Id, adminId, title: "Expense A");
        var expenseB = await client.CreateExpenseAsync(group.Id, adminId, title: "Expense B");

        var first = await client.UploadAttachmentAsync(group.Id, expenseA.Id, AttachmentTestExtensions.JpegBytes, "test.jpg");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.UploadAttachmentAsync(group.Id, expenseB.Id, AttachmentTestExtensions.JpegBytes, "test.jpg");
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseAttachmentDto>>(ct);
        Assert.Equal(expenseB.Id, body!.Data!.ExpenseId);
    }

    #endregion

    #region Cascade on expense delete

    [Fact]
    public async Task DeleteExpense_CascadesAttachmentHardDelete()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;
        var expense = await client.CreateExpenseAsync(group.Id, adminId);

        var uploadResponse = await client.UploadAttachmentAsync(group.Id, expense.Id, AttachmentTestExtensions.JpegBytes, "test.jpg");
        var uploadBody = await uploadResponse.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseAttachmentDto>>(ct);

        var deleteResponse = await client.DeleteAsync($"/api/v1/groups/{group.Id}/expenses/{expense.Id}", ct);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        // Attachment is unreachable via the API — the expense is gone
        var downloadResponse = await client.GetAsync(
            $"/api/v1/groups/{group.Id}/expenses/{expense.Id}/attachments/{uploadBody!.Data!.Id}", ct);
        Assert.Equal(HttpStatusCode.NotFound, downloadResponse.StatusCode);

        // Attachment row is hard-deleted from the DB (not soft-deleted)
        var expenseIntId = await ResolveExpenseIntIdAsync(expense.Id);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var remaining = await db.ExpenseAttachments.CountAsync(a => a.ExpenseId == expenseIntId, ct);
        Assert.Equal(0, remaining);
    }

    #endregion

    /// <summary>
    /// Resolves the int DB id for an expense from its API-facing Guid.
    /// </summary>
    private async Task<int> ResolveExpenseIntIdAsync(string expenseGuid)
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expense = await db.Expenses.SingleAsync(e => e.Guid == Guid.Parse(expenseGuid), ct);
        return expense.Id;
    }
}
