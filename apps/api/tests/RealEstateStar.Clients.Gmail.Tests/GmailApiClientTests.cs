using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using RealEstateStar.Clients.Gmail;
using RealEstateStar.Clients.GoogleOAuth;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.TestUtilities;

namespace RealEstateStar.Clients.Gmail.Tests;

/// <summary>
/// Tests for GmailApiClient.
///
/// Note: GmailService.Users.Messages.Send() makes a live HTTP call to Google's API and cannot
/// be unit-tested without a full Google API mock framework. The happy-path email send test is
/// therefore tested via the MimeKit MIME-building and base64-encoding helpers, which are the
/// core logic owned by this client. The no-op paths (missing token, refresh failure) are fully
/// exercised without hitting the Google API.
/// </summary>
public class GmailApiClientTests
{
    private const string AccountId = "test-account";
    private const string AgentId = "test-agent";
    private const string ClientId = "test-client-id";
    private const string ClientSecret = "test-client-secret";

    private static OAuthCredential ValidCredential() => new()
    {
        AccountId = AccountId,
        AgentId = AgentId,
        AccessToken = "access-token",
        RefreshToken = "refresh-token",
        ExpiresAt = DateTime.UtcNow.AddHours(1),
        Scopes = ["https://mail.google.com/"],
        Email = "agent@example.com",
        Name = "Test Agent",
        ETag = "etag-123"
    };

    private static (GmailApiClient Client, InMemoryTokenStore Store, MockHttpMessageHandler OAuthHandler)
        BuildClient()
    {
        var store = new InMemoryTokenStore();
        var oauthHandler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(oauthHandler);
        var refresher = new GoogleOAuthRefresher(
            store, ClientId, ClientSecret, httpClient, NullLogger<GoogleOAuthRefresher>.Instance);
        var client = new GmailApiClient(refresher, NullLogger<GmailApiClient>.Instance);
        return (client, store, oauthHandler);
    }

    // ──────────────────────────────────────────────────────────
    // No-op paths (no token / refresh fails) — no Google API hit
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_NoOp_WhenTokenMissing()
    {
        var (client, _, _) = BuildClient();

        // No credential stored — should be a no-op (no throw)
        var act = async () => await client.SendAsync(
            AccountId, AgentId, "buyer@example.com", "Subject", "<p>Hello</p>", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendWithAttachmentAsync_NoOp_WhenTokenMissing()
    {
        var (client, _, _) = BuildClient();

        var act = async () => await client.SendWithAttachmentAsync(
            AccountId, AgentId, "buyer@example.com", "Subject", "<p>Hello</p>",
            Encoding.UTF8.GetBytes("PDF content"), "report.pdf", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendAsync_NoOp_WhenRefreshFails()
    {
        var (client, store, oauthHandler) = BuildClient();

        // Store an expired credential
        var expiredCredential = ValidCredential() with
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            ETag = "old-etag"
        };
        await store.SaveAsync(expiredCredential, CancellationToken.None);

        // Refresh endpoint returns 401 (token revoked)
        oauthHandler.ResponseToReturn = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\": \"invalid_grant\"}", Encoding.UTF8, "application/json")
        };

        var act = async () => await client.SendAsync(
            AccountId, AgentId, "buyer@example.com", "Subject", "<p>Hello</p>", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendWithAttachmentAsync_NoOp_WhenRefreshFails()
    {
        var (client, store, oauthHandler) = BuildClient();

        var expiredCredential = ValidCredential() with
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            ETag = "old-etag"
        };
        await store.SaveAsync(expiredCredential, CancellationToken.None);

        oauthHandler.ResponseToReturn = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\": \"invalid_grant\"}", Encoding.UTF8, "application/json")
        };

        var act = async () => await client.SendWithAttachmentAsync(
            AccountId, AgentId, "buyer@example.com", "Subject", "<p>Hello</p>",
            Encoding.UTF8.GetBytes("PDF content"), "report.pdf", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // ──────────────────────────────────────────────────────────
    // MIME building (internal static helpers, unit-testable)
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void BuildMimeMessage_SetsFromToSubject()
    {
        var message = GmailApiClient.BuildMimeMessage(
            "agent@example.com", "buyer@example.com", "Hello World", "<p>Body</p>");

        message.From.Mailboxes.First().Address.Should().Be("agent@example.com");
        message.To.Mailboxes.First().Address.Should().Be("buyer@example.com");
        message.Subject.Should().Be("Hello World");
    }

    [Fact]
    public void BuildMimeMessage_SetsHtmlBody()
    {
        var message = GmailApiClient.BuildMimeMessage(
            "agent@example.com", "buyer@example.com", "Subject", "<p>Hello!</p>");

        var html = message.HtmlBody;
        html.Should().Contain("<p>Hello!</p>");
    }

    [Fact]
    public void BuildMimeMessage_WithAttachment_IncludesAttachment()
    {
        var attachmentBytes = Encoding.UTF8.GetBytes("PDF data here");

        var message = GmailApiClient.BuildMimeMessage(
            "agent@example.com", "buyer@example.com", "Subject", "<p>Body</p>",
            attachmentBytes, "report.pdf");

        var multipart = message.Body as Multipart;
        multipart.Should().NotBeNull();

        var attachmentPart = message.Attachments.OfType<MimePart>().FirstOrDefault();
        attachmentPart.Should().NotBeNull();
        attachmentPart!.FileName.Should().Be("report.pdf");
    }

    [Fact]
    public void BuildMimeMessage_WithoutAttachment_IsNotMultipart()
    {
        var message = GmailApiClient.BuildMimeMessage(
            "agent@example.com", "buyer@example.com", "Subject", "<p>Body</p>");

        // Without attachment, should not have any attachment parts
        message.Attachments.Should().BeEmpty();
    }

    [Fact]
    public void EncodeMessage_ProducesValidBase64Url()
    {
        var message = GmailApiClient.BuildMimeMessage(
            "agent@example.com", "buyer@example.com", "Subject", "<p>Body</p>");

        var encoded = GmailApiClient.EncodeMessage(message);

        encoded.Raw.Should().NotBeNullOrEmpty();
        // Base64URL: should not contain +, /, or = characters
        encoded.Raw.Should().NotContain("+");
        encoded.Raw.Should().NotContain("/");
        encoded.Raw.Should().NotContain("=");
    }

    [Fact]
    public void EncodeMessage_IsDecodable_ToValidMimeMessage()
    {
        var originalMessage = GmailApiClient.BuildMimeMessage(
            "agent@example.com", "buyer@example.com", "Test Subject", "<p>Test Body</p>");

        var encoded = GmailApiClient.EncodeMessage(originalMessage);

        // Decode and verify round-trip
        var base64 = encoded.Raw!
            .Replace('-', '+')
            .Replace('_', '/');
        var paddingNeeded = (4 - base64.Length % 4) % 4;
        base64 += new string('=', paddingNeeded);
        var bytes = Convert.FromBase64String(base64);

        using var stream = new MemoryStream(bytes);
        var decoded = MimeMessage.Load(stream);
        decoded.Subject.Should().Be("Test Subject");
        decoded.From.Mailboxes.First().Address.Should().Be("agent@example.com");
        decoded.To.Mailboxes.First().Address.Should().Be("buyer@example.com");
    }
}
