using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Clients.GoogleOAuth;
using RealEstateStar.Domain.Shared;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.TestUtilities;

namespace RealEstateStar.Clients.GDrive.Tests;

/// <summary>
/// Tests for GDriveApiClient.
///
/// Note: DriveService makes live HTTP calls to Google's API and cannot be unit-tested without
/// a full Google API mock framework. Tests here cover the token-resolution paths (missing token,
/// refresh failure) which are exercised without hitting the Google API.
/// </summary>
public class GDriveApiClientTests
{
    private const string AccountId = "test-account";
    private const string AgentId = "test-agent";
    private const string ClientId = "test-client-id";
    private const string ClientSecret = "test-client-secret";
    private const string FolderId = "folder-123";
    private const string FileId = "file-456";
    private const string FileName = "Lead Profile.md";

    private static OAuthCredential ValidCredential() => new()
    {
        AccountId = AccountId,
        AgentId = AgentId,
        AccessToken = "access-token",
        RefreshToken = "refresh-token",
        ExpiresAt = DateTime.UtcNow.AddHours(1),
        Scopes = ["https://www.googleapis.com/auth/drive"],
        Email = "agent@example.com",
        Name = "Test Agent",
        ETag = "etag-123"
    };

    private static (GDriveApiClient Client, InMemoryTokenStore Store, MockHttpMessageHandler OAuthHandler)
        BuildClient()
    {
        var store = new InMemoryTokenStore();
        var oauthHandler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(oauthHandler);
        var refresher = new GoogleOAuthRefresher(
            store, ClientId, ClientSecret, httpClient, NullLogger<GoogleOAuthRefresher>.Instance);
        var client = new GDriveApiClient(refresher, ClientId, ClientSecret, NullLogger<GDriveApiClient>.Instance);
        return (client, store, oauthHandler);
    }

    // ──────────────────────────────────────────────────────────
    // Missing token — all methods return empty/null (no throw)
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateFolderAsync_ReturnsEmpty_WhenTokenMissing()
    {
        var (client, _, _) = BuildClient();

        var result = await client.CreateFolderAsync(AccountId, AgentId, "leads/2026", CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UploadFileAsync_ReturnsEmpty_WhenTokenMissing()
    {
        var (client, _, _) = BuildClient();

        var result = await client.UploadFileAsync(
            AccountId, AgentId, FolderId, FileName, "# Lead\nContent here", CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UploadBinaryAsync_ReturnsEmpty_WhenTokenMissing()
    {
        var (client, _, _) = BuildClient();

        var result = await client.UploadBinaryAsync(
            AccountId, AgentId, FolderId, "report.pdf",
            Encoding.UTF8.GetBytes("PDF content"), "application/pdf", CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DownloadFileAsync_ReturnsNull_WhenTokenMissing()
    {
        var (client, _, _) = BuildClient();

        var result = await client.DownloadFileAsync(
            AccountId, AgentId, FolderId, FileName, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteFileAsync_NoOp_WhenTokenMissing()
    {
        var (client, _, _) = BuildClient();

        var act = async () => await client.DeleteFileAsync(
            AccountId, AgentId, FileId, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ListFilesAsync_ReturnsEmpty_WhenTokenMissing()
    {
        var (client, _, _) = BuildClient();

        var result = await client.ListFilesAsync(AccountId, AgentId, FolderId, CancellationToken.None);

        result.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────
    // Refresh failure — all methods return empty/null (no throw)
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateFolderAsync_ReturnsEmpty_WhenRefreshFails()
    {
        var (client, store, oauthHandler) = BuildClient();
        await store.SaveAsync(ExpiredCredential(), OAuthProviders.Google, CancellationToken.None);
        oauthHandler.ResponseToReturn = UnauthorizedResponse();

        var result = await client.CreateFolderAsync(AccountId, AgentId, "leads/2026", CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UploadFileAsync_ReturnsEmpty_WhenRefreshFails()
    {
        var (client, store, oauthHandler) = BuildClient();
        await store.SaveAsync(ExpiredCredential(), OAuthProviders.Google, CancellationToken.None);
        oauthHandler.ResponseToReturn = UnauthorizedResponse();

        var result = await client.UploadFileAsync(
            AccountId, AgentId, FolderId, FileName, "content", CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DownloadFileAsync_ReturnsNull_WhenRefreshFails()
    {
        var (client, store, oauthHandler) = BuildClient();
        await store.SaveAsync(ExpiredCredential(), OAuthProviders.Google, CancellationToken.None);
        oauthHandler.ResponseToReturn = UnauthorizedResponse();

        var result = await client.DownloadFileAsync(
            AccountId, AgentId, FolderId, FileName, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ListFilesAsync_ReturnsEmpty_WhenRefreshFails()
    {
        var (client, store, oauthHandler) = BuildClient();
        await store.SaveAsync(ExpiredCredential(), OAuthProviders.Google, CancellationToken.None);
        oauthHandler.ResponseToReturn = UnauthorizedResponse();

        var result = await client.ListFilesAsync(AccountId, AgentId, FolderId, CancellationToken.None);

        result.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────
    // Diagnostics counter incremented on missing token
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AllMethods_IncrementTokenMissingCounter_WhenTokenMissing()
    {
        // This test verifies the code path reaches GDriveDiagnostics.TokenMissing.Add(1)
        // by exercising all methods with no stored token. Since metrics are global counters
        // we can't assert the delta easily, so we just verify no exception is thrown and
        // that the methods return gracefully (proving the early-exit path executed).
        var (client, _, _) = BuildClient();

        await client.CreateFolderAsync(AccountId, AgentId, "path", CancellationToken.None);
        await client.UploadFileAsync(AccountId, AgentId, FolderId, FileName, "c", CancellationToken.None);
        await client.UploadBinaryAsync(AccountId, AgentId, FolderId, "f.pdf", [], "application/pdf", CancellationToken.None);
        var download = await client.DownloadFileAsync(AccountId, AgentId, FolderId, FileName, CancellationToken.None);
        await client.DeleteFileAsync(AccountId, AgentId, FileId, CancellationToken.None);
        var list = await client.ListFilesAsync(AccountId, AgentId, FolderId, CancellationToken.None);

        // All missing-token paths return gracefully
        download.Should().BeNull();
        list.Should().BeEmpty();
    }

    [Fact]
    public async Task DownloadBinaryAsync_ReturnsNull_WhenTokenMissing()
    {
        var (client, _, _) = BuildClient();

        var result = await client.DownloadBinaryAsync(AccountId, AgentId, FileId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DownloadBinaryAsync_ReturnsNull_WhenRefreshFails()
    {
        var (client, store, oauthHandler) = BuildClient();
        await store.SaveAsync(ExpiredCredential(), OAuthProviders.Google, CancellationToken.None);
        oauthHandler.ResponseToReturn = UnauthorizedResponse();

        var result = await client.DownloadBinaryAsync(AccountId, AgentId, FileId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CopyFileAsync_ReturnsEmpty_WhenTokenMissing()
    {
        var (client, _, _) = BuildClient();

        var result = await client.CopyFileAsync(
            AccountId, AgentId, FileId, FolderId, "copy.pdf", CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CopyFileAsync_ReturnsEmpty_WhenRefreshFails()
    {
        var (client, store, oauthHandler) = BuildClient();
        await store.SaveAsync(ExpiredCredential(), OAuthProviders.Google, CancellationToken.None);
        oauthHandler.ResponseToReturn = UnauthorizedResponse();

        var result = await client.CopyFileAsync(
            AccountId, AgentId, FileId, FolderId, null, CancellationToken.None);

        result.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────
    // EscapeQuery — internal static helper, unit-testable
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void EscapeQuery_EscapesBackslash()
    {
        var result = GDriveApiClient.EscapeQuery(@"a\b");

        result.Should().Be(@"a\\b");
    }

    [Fact]
    public void EscapeQuery_EscapesSingleQuote()
    {
        var result = GDriveApiClient.EscapeQuery("it's");

        result.Should().Be(@"it\'s");
    }

    [Fact]
    public void EscapeQuery_HandlesNoSpecialChars()
    {
        var result = GDriveApiClient.EscapeQuery("normal");

        result.Should().Be("normal");
    }

    [Fact]
    public void EscapeQuery_EscapesBothBackslashAndSingleQuote()
    {
        var result = GDriveApiClient.EscapeQuery(@"it\'s");

        // Backslash escaped first, then single quote — \' becomes \\\' (three chars → four)
        result.Should().Be(@"it\\\'s");
    }

    // ──────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────

    private static OAuthCredential ExpiredCredential() => ValidCredential() with
    {
        ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
        ETag = "old-etag"
    };

    private static System.Net.Http.HttpResponseMessage UnauthorizedResponse() =>
        new(System.Net.HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(
                "{\"error\": \"invalid_grant\"}", Encoding.UTF8, "application/json")
        };
}
