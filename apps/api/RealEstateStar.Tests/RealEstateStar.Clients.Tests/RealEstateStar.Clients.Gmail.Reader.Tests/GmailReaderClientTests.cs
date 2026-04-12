using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using RealEstateStar.Clients.GoogleOAuth;
using RealEstateStar.Domain.Shared;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.TestUtilities;

namespace RealEstateStar.Clients.Gmail.Reader.Tests;

/// <summary>
/// Tests for GmailReaderClient.
///
/// Note: GmailService.Users.Messages.List() / Get() make live HTTP calls to Google's API
/// and cannot be unit-tested without a full Google API mock. The throw paths (missing token,
/// refresh failure) are fully exercised. The MIME parsing helpers are tested as unit tests.
/// </summary>
public class GmailReaderClientTests
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

    private static (GmailReaderClient Client, InMemoryTokenStore Store, MockHttpMessageHandler OAuthHandler)
        BuildClient()
    {
        var store = new InMemoryTokenStore();
        var oauthHandler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(oauthHandler);
        var refresher = new GoogleOAuthRefresher(
            store, ClientId, ClientSecret, httpClient, NullLogger<GoogleOAuthRefresher>.Instance);
        var client = new GmailReaderClient(
            refresher, ClientId, ClientSecret, NullLogger<GmailReaderClient>.Instance);
        return (client, store, oauthHandler);
    }

    // ──────────────────────────────────────────────────────────
    // Throw paths (no token / refresh fails)
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSentEmailsAsync_Throws_WhenTokenMissing()
    {
        var (client, _, _) = BuildClient();

        var act = async () => await client.GetSentEmailsAsync(AccountId, AgentId, 10, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetInboxEmailsAsync_Throws_WhenTokenMissing()
    {
        var (client, _, _) = BuildClient();

        var act = async () => await client.GetInboxEmailsAsync(AccountId, AgentId, 10, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetSentEmailsAsync_Throws_WhenRefreshFails()
    {
        var (client, store, oauthHandler) = BuildClient();

        var expiredCredential = ValidCredential() with
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            ETag = "old-etag"
        };
        await store.SaveAsync(expiredCredential, OAuthProviders.Google, CancellationToken.None);

        oauthHandler.ResponseToReturn = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\": \"invalid_grant\"}", Encoding.UTF8, "application/json")
        };

        var act = async () => await client.GetSentEmailsAsync(AccountId, AgentId, 10, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetInboxEmailsAsync_Throws_WhenRefreshFails()
    {
        var (client, store, oauthHandler) = BuildClient();

        var expiredCredential = ValidCredential() with
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            ETag = "old-etag"
        };
        await store.SaveAsync(expiredCredential, OAuthProviders.Google, CancellationToken.None);

        oauthHandler.ResponseToReturn = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\": \"invalid_grant\"}", Encoding.UTF8, "application/json")
        };

        var act = async () => await client.GetInboxEmailsAsync(AccountId, AgentId, 10, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ──────────────────────────────────────────────────────────
    // MIME parsing helpers
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void ParseRawMessage_ReturnsNull_WhenRawIsNull()
    {
        var raw = new Google.Apis.Gmail.v1.Data.Message { Id = "msg-1", Raw = null };

        var result = GmailReaderClient.ParseRawMessage(raw);

        result.Should().BeNull();
    }

    [Fact]
    public void ParseRawMessage_ParsesValidBase64UrlMimeMessage()
    {
        var mimeMessage = BuildMime(
            from: "agent@example.com",
            to: "buyer@example.com",
            subject: "Hello",
            body: "Test body text.");

        var rawBytes = ToBase64Url(mimeMessage);
        var raw = new Google.Apis.Gmail.v1.Data.Message { Id = "msg-1", Raw = rawBytes };

        var result = GmailReaderClient.ParseRawMessage(raw);

        result.Should().NotBeNull();
        result!.Subject.Should().Be("Hello");
        result.From.Should().Be("agent@example.com");
        result.To.Should().ContainSingle().Which.Should().Be("buyer@example.com");
        result.Body.Should().Contain("Test body text.");
    }

    [Fact]
    public void ExtractBody_ReturnsPlainText_WhenAvailable()
    {
        var mime = BuildMime("a@b.com", "c@d.com", "s", "Plain text body");

        var body = GmailReaderClient.ExtractBody(mime);

        body.Should().Contain("Plain text body");
    }

    [Fact]
    public void ExtractBody_StripsHtml_WhenOnlyHtmlAvailable()
    {
        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse("a@b.com"));
        mime.To.Add(MailboxAddress.Parse("c@d.com"));
        mime.Subject = "Test";
        mime.Body = new TextPart("html") { Text = "<p>Hello <b>world</b></p>" };

        var body = GmailReaderClient.ExtractBody(mime);

        body.Should().Contain("Hello world");
        body.Should().NotContain("<p>");
        body.Should().NotContain("<b>");
    }

    [Fact]
    public void ExtractBody_PrefersPart_PlainTextInMultipart()
    {
        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse("a@b.com"));
        mime.To.Add(MailboxAddress.Parse("c@d.com"));
        mime.Subject = "Test";

        var multipart = new Multipart("alternative");
        multipart.Add(new TextPart("plain") { Text = "Plain version" });
        multipart.Add(new TextPart("html") { Text = "<p>HTML version</p>" });
        mime.Body = multipart;

        var body = GmailReaderClient.ExtractBody(mime);

        body.Should().Be("Plain version");
    }

    [Fact]
    public void ParseRawMessage_ReturnsNull_WhenMalformedBase64()
    {
        var raw = new Google.Apis.Gmail.v1.Data.Message { Id = "msg-1", Raw = "NOT_VALID_BASE64!!!" };

        var result = GmailReaderClient.ParseRawMessage(raw);

        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────

    private static MimeMessage BuildMime(string from, string to, string subject, string body)
    {
        var msg = new MimeMessage();
        msg.From.Add(MailboxAddress.Parse(from));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = subject;
        msg.Body = new TextPart("plain") { Text = body };
        return msg;
    }

    private static string ToBase64Url(MimeMessage mimeMessage)
    {
        using var stream = new MemoryStream();
        mimeMessage.WriteTo(stream);
        return Convert.ToBase64String(stream.ToArray())
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
