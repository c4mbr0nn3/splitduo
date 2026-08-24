using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class UserAvatarsTests : IntegrationTest
{
    public UserAvatarsTests(SplitDuoApiFactory factory) : base(factory) { }

    #region Happy paths

    [Fact]
    public async Task UploadAvatar_Jpeg_Returns204()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.UploadAvatarAsync(AvatarTestExtensions.JpegBytes, "avatar.jpg");

        // BUG: UserAvatarsService.UploadAvatarAsync returns Result.Success() (HttpStatusCode.OK),
        // so the controller responds 200 instead of the spec'd 204 No Content.
        // EXPECTED TO FAIL until the service returns Result.Success(HttpStatusCode.NoContent).
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UploadAvatar_Png_Returns204()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.UploadAvatarAsync(AvatarTestExtensions.PngBytes, "avatar.png");

        // BUG: same as UploadAvatar_Jpeg_Returns204 — service returns 200, spec requires 204.
        // EXPECTED TO FAIL until the service returns Result.Success(HttpStatusCode.NoContent).
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UploadAvatar_Webp_Returns204()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.UploadAvatarAsync(AvatarTestExtensions.WebpBytes, "avatar.webp");

        // BUG: same as UploadAvatar_Jpeg_Returns204 — service returns 200, spec requires 204.
        // EXPECTED TO FAIL until the service returns Result.Success(HttpStatusCode.NoContent).
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DownloadAvatar_ReturnsBytesWithHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var me = await client.GetCurrentUserAsync();

        await client.UploadAvatarAsync(AvatarTestExtensions.JpegBytes, "test.jpg");

        var response = await client.GetAsync($"/api/v1/users/{me.Id}/avatar", ct);

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
        Assert.Equal(AvatarTestExtensions.JpegBytes, bytes);
    }

    [Fact]
    public async Task DeleteAvatar_RemovesRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var me = await client.GetCurrentUserAsync();

        await client.UploadAvatarAsync(AvatarTestExtensions.JpegBytes, "test.jpg");

        var deleteResponse = await client.DeleteAsync("/api/v1/users/me/avatar", ct);

        // BUG: UserAvatarsService.DeleteAvatarAsync returns Result.Success() (HttpStatusCode.OK),
        // so the controller responds 200 instead of the spec'd 204 No Content.
        // EXPECTED TO FAIL until the service returns Result.Success(HttpStatusCode.NoContent).
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/v1/users/{me.Id}/avatar", ct);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task ReplaceAvatar_OldBytesGone()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var me = await client.GetCurrentUserAsync();

        var first = await client.UploadAvatarAsync(AvatarTestExtensions.DistinctJpegBytes(1), "first.jpg");
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await client.UploadAvatarAsync(AvatarTestExtensions.DistinctJpegBytes(2), "second.jpg");
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        var response = await client.GetAsync($"/api/v1/users/{me.Id}/avatar", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        Assert.Equal(AvatarTestExtensions.DistinctJpegBytes(2), bytes);
        Assert.NotEqual(AvatarTestExtensions.DistinctJpegBytes(1), bytes);
    }

    #endregion

    #region Error cases

    [Fact]
    public async Task DownloadAvatar_NoAvatar_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var me = await client.GetCurrentUserAsync();

        var response = await client.GetAsync($"/api/v1/users/{me.Id}/avatar", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Avatar not found.", body!.Error!.Message);
    }

    [Fact]
    public async Task DeleteAvatar_NoAvatar_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.DeleteAsync("/api/v1/users/me/avatar", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Avatar not found.", body!.Error!.Message);
    }

    [Fact]
    public async Task UploadAvatar_BadExtension_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.UploadAvatarAsync(AvatarTestExtensions.JpegBytes, "notes.txt");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("File type is not allowed. Only JPG, PNG, and WebP images are accepted.", body!.Error!.Message);
    }

    [Fact]
    public async Task UploadAvatar_BadMagicNumber_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        // EXE (MZ header) renamed as .jpg — passes the extension check, fails the sniff
        var response = await client.UploadAvatarAsync(AvatarTestExtensions.BadMagicBytes, "fake.jpg");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("File content does not match its extension.", body!.Error!.Message);
    }

    [Fact]
    public async Task UploadAvatar_Heic_RejectedByPostSniffFilter_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        // HEIC has a valid magic number (ftyp box) but is not in the allowed
        // jpeg/png/webp set — the post-sniff MIME filter must reject it
        var response = await client.UploadAvatarAsync(AvatarTestExtensions.HeicBytes, "photo.heic");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("File type is not allowed. Only JPG, PNG, and WebP images are accepted.", body!.Error!.Message);
    }

    [Fact]
    public async Task UploadAvatar_ExceedsSizeLimit_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        // 201KB of zeros — size check runs before content sniffing, so content doesn't matter
        var oversized = new byte[201 * 1024];
        var response = await client.UploadAvatarAsync(oversized, "big.jpg");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("File size exceeds the 200KB limit.", body!.Error!.Message);
    }

    #endregion

    #region Cross-user + auth

    [Fact]
    public async Task DownloadAvatar_AnyAuthenticatedUser_CanFetchOtherUserAvatar()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        // Seed a second user and upload an avatar as them (AD3: any authenticated
        // user can fetch any user's avatar — no group membership required)
        var user2Email = await TestDbSeeder.SeedUserAsync(Factory.Services);
        var user2Client = await CreateAuthenticatedClientAsync(user2Email, "changeme123");
        var user2 = await user2Client.GetCurrentUserAsync();

        var uploadResponse = await user2Client.UploadAvatarAsync(AvatarTestExtensions.PngBytes, "avatar.png");
        Assert.Equal(HttpStatusCode.NoContent, uploadResponse.StatusCode);

        var response = await adminClient.GetAsync($"/api/v1/users/{user2.Id}/avatar", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        Assert.Equal(AvatarTestExtensions.PngBytes, bytes);
    }

    [Fact]
    public async Task UploadAvatar_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.UploadAvatarAsync(AvatarTestExtensions.JpegBytes, "avatar.jpg");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DownloadAvatar_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.GetAsync($"/api/v1/users/{Guid.CreateVersion7()}/avatar", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAvatar_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.DeleteAsync("/api/v1/users/me/avatar", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region ETag caching

    [Fact]
    public async Task DownloadAvatar_ETagCaching_Returns304OnUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var me = await client.GetCurrentUserAsync();

        await client.UploadAvatarAsync(AvatarTestExtensions.JpegBytes, "test.jpg");

        var first = await client.GetAsync($"/api/v1/users/{me.Id}/avatar", ct);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.True(first.Headers.TryGetValues("ETag", out var etagValues));
        var etag = etagValues!.Single();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/users/{me.Id}/avatar");
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        var second = await client.SendAsync(request, ct);

        // BUG: UserAvatarsController.DownloadAvatar sets the ETag header but never evaluates
        // If-None-Match, so an unchanged avatar is re-served with 200 instead of 304.
        // EXPECTED TO FAIL until the controller implements conditional GET (304 + empty body).
        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    #endregion
}
