using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RealEstateStar.Api.Features.Sites;
using RealEstateStar.Api.Features.Sites.Approve;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Api.Tests.Features.Sites;

public sealed class ApproveEndpointTests
{
    private const string AccountId = "acct-123";
    private const string SessionId = "sess-abc";
    private const string NamespaceId = "ns-test";

    private readonly Mock<ICloudflareKvClient> _kvClient = new();
    private readonly Mock<IPreviewSessionStore> _sessionStore = new();
    private readonly Mock<ILogger<ApproveEndpoint>> _logger = new();

    private readonly IOptions<SiteOptions> _siteOptions = Options.Create(new SiteOptions
    {
        KvNamespaceId = NamespaceId
    });

    private HttpContext MakeContextWithSession(string sessionId = SessionId)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Preview-Session"] = sessionId;
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    private void SetupValidSession(string accountId = AccountId)
    {
        var session = new PreviewSession(
            SessionId: SessionId,
            AccountId: accountId,
            ExpiresAt: DateTime.UtcNow.AddHours(1),
            Revoked: false,
            RevokedAt: null);

        _sessionStore
            .Setup(s => s.GetAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
    }

    private void SetupKvPutSucceeds()
    {
        _kvClient
            .Setup(k => k.PutAsync(NamespaceId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsOk_WithCheckoutUrl_WhenSessionIsValid()
    {
        SetupValidSession();
        SetupKvPutSucceeds();

        var result = await ApproveEndpoint.Handle(
            AccountId,
            _kvClient.Object,
            _sessionStore.Object,
            _siteOptions,
            MakeContextWithSession(),
            _logger.Object,
            CancellationToken.None);

        var ok = result as Ok<ApproveResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.CheckoutUrl.Should().Be(ApproveEndpoint.StubCheckoutUrl);
    }

    [Fact]
    public async Task Handle_WritesPendingBillingState_WhenApproved()
    {
        SetupValidSession();

        string? capturedKey = null;
        string? capturedValue = null;
        _kvClient
            .Setup(k => k.PutAsync(NamespaceId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, key, value, _) =>
            {
                capturedKey = key;
                capturedValue = value;
            })
            .Returns(Task.CompletedTask);

        await ApproveEndpoint.Handle(
            AccountId,
            _kvClient.Object,
            _sessionStore.Object,
            _siteOptions,
            MakeContextWithSession(),
            _logger.Object,
            CancellationToken.None);

        capturedKey.Should().Be($"site-state:v1:{AccountId}");
        capturedValue.Should().Be("\"pending_billing\"");
    }

    // ── Session validation ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenNoSession()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        // No cookie, no header

        var result = await ApproveEndpoint.Handle(
            AccountId,
            _kvClient.Object,
            _sessionStore.Object,
            _siteOptions,
            ctx,
            _logger.Object,
            CancellationToken.None);

        result.Should().BeAssignableTo<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenSessionIsRevoked()
    {
        var revokedSession = new PreviewSession(
            SessionId: SessionId,
            AccountId: AccountId,
            ExpiresAt: DateTime.UtcNow.AddHours(1),
            Revoked: true,
            RevokedAt: DateTime.UtcNow.AddMinutes(-5));

        _sessionStore
            .Setup(s => s.GetAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(revokedSession);

        var result = await ApproveEndpoint.Handle(
            AccountId,
            _kvClient.Object,
            _sessionStore.Object,
            _siteOptions,
            MakeContextWithSession(),
            _logger.Object,
            CancellationToken.None);

        result.Should().BeAssignableTo<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenSessionIsExpired()
    {
        var expiredSession = new PreviewSession(
            SessionId: SessionId,
            AccountId: AccountId,
            ExpiresAt: DateTime.UtcNow.AddHours(-1),
            Revoked: false,
            RevokedAt: null);

        _sessionStore
            .Setup(s => s.GetAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredSession);

        var result = await ApproveEndpoint.Handle(
            AccountId,
            _kvClient.Object,
            _sessionStore.Object,
            _siteOptions,
            MakeContextWithSession(),
            _logger.Object,
            CancellationToken.None);

        result.Should().BeAssignableTo<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_ReturnsForbid_WhenAccountIdDoesNotMatchSession()
    {
        // Session is for "acct-456", but we're requesting "acct-123"
        SetupValidSession(accountId: "acct-456");

        var result = await ApproveEndpoint.Handle(
            AccountId,
            _kvClient.Object,
            _sessionStore.Object,
            _siteOptions,
            MakeContextWithSession(),
            _logger.Object,
            CancellationToken.None);

        result.Should().BeAssignableTo<ForbidHttpResult>();
    }

    // ── KV failure ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsProblem_WhenKvWriteFails()
    {
        SetupValidSession();
        _kvClient
            .Setup(k => k.PutAsync(NamespaceId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("KV unavailable"));

        var result = await ApproveEndpoint.Handle(
            AccountId,
            _kvClient.Object,
            _sessionStore.Object,
            _siteOptions,
            MakeContextWithSession(),
            _logger.Object,
            CancellationToken.None);

        result.Should().BeAssignableTo<ProblemHttpResult>();
        var problem = (ProblemHttpResult)result;
        problem.StatusCode.Should().Be(500);
    }
}
