using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RealEstateStar.Api.Features.Preview;
using RealEstateStar.Api.Features.Preview.ExchangeToken;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Api.Tests.Features.Preview;

public sealed class ExchangeTokenEndpointTests
{
    private const string TestHmacKey = "test-hmac-key-at-least-32-chars-long!!";
    private const string TestAccountId = "account-xyz";

    private readonly Mock<IPreviewSessionStore> _sessionStore = new();
    private readonly Mock<ILogger<ExchangeTokenEndpoint>> _logger = new();
    private readonly IOptions<PreviewOptions> _options = Options.Create(new PreviewOptions { HmacKey = TestHmacKey });

    private static string BuildToken(string accountId, DateTime issuedAt, string nonce, string hmacKey)
    {
        var payload = JsonSerializer.Serialize(new { accountId, issuedAt, nonce });
        var payloadB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var sig = HMACSHA256.HashData(Encoding.UTF8.GetBytes(hmacKey), Encoding.UTF8.GetBytes(payloadB64));
        var sigB64 = Convert.ToBase64String(sig)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return $"{payloadB64}.{sigB64}";
    }

    private static string BuildToken(string accountId, DateTime issuedAt, string nonce) =>
        BuildToken(accountId, issuedAt, nonce, TestHmacKey);

    private HttpContext MakeHttpContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream(); // prevent null stream on cookie write
        return ctx;
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsOk_WithSessionIdAndAccountId_WhenTokenIsValid()
    {
        var token = BuildToken(TestAccountId, DateTime.UtcNow, Guid.NewGuid().ToString());
        _sessionStore
            .Setup(s => s.CreateAsync(It.IsAny<PreviewSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await ExchangeTokenEndpoint.Handle(
            new ExchangeTokenRequest(token),
            _sessionStore.Object,
            _options,
            MakeHttpContext(),
            _logger.Object,
            CancellationToken.None);

        var ok = result as Ok<ExchangeTokenResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.AccountId.Should().Be(TestAccountId);
        ok.Value.SessionId.Should().NotBeNullOrWhiteSpace();
        ok.Value.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Handle_SetsHttpOnlyCookie_WhenTokenIsValid()
    {
        var token = BuildToken(TestAccountId, DateTime.UtcNow, Guid.NewGuid().ToString());
        _sessionStore
            .Setup(s => s.CreateAsync(It.IsAny<PreviewSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var ctx = MakeHttpContext();
        await ExchangeTokenEndpoint.Handle(
            new ExchangeTokenRequest(token),
            _sessionStore.Object,
            _options,
            ctx,
            _logger.Object,
            CancellationToken.None);

        // Cookie header should contain preview_session
        var setCookieHeader = ctx.Response.Headers["Set-Cookie"].ToString();
        setCookieHeader.Should().Contain(ExchangeTokenEndpoint.SessionCookieName);
        setCookieHeader.ToLowerInvariant().Should().Contain("httponly");
    }

    [Fact]
    public async Task Handle_CallsCreateAsync_WithCorrectAccountId()
    {
        var token = BuildToken(TestAccountId, DateTime.UtcNow, Guid.NewGuid().ToString());
        PreviewSession? capturedSession = null;
        _sessionStore
            .Setup(s => s.CreateAsync(It.IsAny<PreviewSession>(), It.IsAny<CancellationToken>()))
            .Callback<PreviewSession, CancellationToken>((s, _) => capturedSession = s)
            .Returns(Task.CompletedTask);

        await ExchangeTokenEndpoint.Handle(
            new ExchangeTokenRequest(token),
            _sessionStore.Object,
            _options,
            MakeHttpContext(),
            _logger.Object,
            CancellationToken.None);

        capturedSession.Should().NotBeNull();
        capturedSession!.AccountId.Should().Be(TestAccountId);
        capturedSession.Revoked.Should().BeFalse();
    }

    // ── Invalid signature ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsBadRequest_WhenSignatureIsInvalid()
    {
        var token = BuildToken(TestAccountId, DateTime.UtcNow, Guid.NewGuid().ToString(), "wrong-key-12345678901234567890123456789");

        var result = await ExchangeTokenEndpoint.Handle(
            new ExchangeTokenRequest(token),
            _sessionStore.Object,
            _options,
            MakeHttpContext(),
            _logger.Object,
            CancellationToken.None);

        result.Should().BeAssignableTo<ProblemHttpResult>();
        var problem = (ProblemHttpResult)result;
        problem.StatusCode.Should().Be(400);
    }

    // ── Expired token ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsBadRequest_WhenTokenIsExpired()
    {
        // Token issued 20 minutes ago — exceeds 15-minute TTL
        var token = BuildToken(TestAccountId, DateTime.UtcNow.AddMinutes(-20), Guid.NewGuid().ToString());

        var result = await ExchangeTokenEndpoint.Handle(
            new ExchangeTokenRequest(token),
            _sessionStore.Object,
            _options,
            MakeHttpContext(),
            _logger.Object,
            CancellationToken.None);

        result.Should().BeAssignableTo<ProblemHttpResult>();
        var problem = (ProblemHttpResult)result;
        problem.StatusCode.Should().Be(400);
        problem.ProblemDetails.Title.Should().Contain("expired");
    }

    // ── Already consumed ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsBadRequest_WhenTokenAlreadyConsumed()
    {
        var token = BuildToken(TestAccountId, DateTime.UtcNow, Guid.NewGuid().ToString());
        _sessionStore
            .Setup(s => s.CreateAsync(It.IsAny<PreviewSession>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Exchange token already consumed."));

        var result = await ExchangeTokenEndpoint.Handle(
            new ExchangeTokenRequest(token),
            _sessionStore.Object,
            _options,
            MakeHttpContext(),
            _logger.Object,
            CancellationToken.None);

        result.Should().BeAssignableTo<ProblemHttpResult>();
        var problem = (ProblemHttpResult)result;
        problem.StatusCode.Should().Be(400);
    }

    // ── Malformed token ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsBadRequest_WhenTokenHasNoSignaturePart()
    {
        var result = await ExchangeTokenEndpoint.Handle(
            new ExchangeTokenRequest("notadottoken"),
            _sessionStore.Object,
            _options,
            MakeHttpContext(),
            _logger.Object,
            CancellationToken.None);

        result.Should().BeAssignableTo<ProblemHttpResult>();
    }

    // ── ParseAndValidateToken unit tests ─────────────────────────────────────

    [Fact]
    public void ParseAndValidateToken_Returns_WhenValid()
    {
        var token = BuildToken(TestAccountId, DateTime.UtcNow, "nonce-123");

        var payload = ExchangeTokenEndpoint.ParseAndValidateToken(token, TestHmacKey, out var error);

        payload.Should().NotBeNull();
        payload!.AccountId.Should().Be(TestAccountId);
        error.Should().BeEmpty();
    }

    [Fact]
    public void ParseAndValidateToken_ReturnsNull_WhenWrongKey()
    {
        var token = BuildToken(TestAccountId, DateTime.UtcNow, "nonce", "wrong-key-12345678901234567890123456789");

        var payload = ExchangeTokenEndpoint.ParseAndValidateToken(token, TestHmacKey, out var error);

        payload.Should().BeNull();
        error.Should().Contain("invalid");
    }

    [Fact]
    public void ParseAndValidateToken_ReturnsNull_WhenNoDot()
    {
        var payload = ExchangeTokenEndpoint.ParseAndValidateToken("nodottoken", TestHmacKey, out var error);

        payload.Should().BeNull();
        error.Should().NotBeEmpty();
    }
}
