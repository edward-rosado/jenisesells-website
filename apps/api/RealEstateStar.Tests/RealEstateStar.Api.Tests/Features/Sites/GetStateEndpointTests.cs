using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RealEstateStar.Api.Features.Sites;
using RealEstateStar.Api.Features.Sites.GetState;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Api.Tests.Features.Sites;

public sealed class GetStateEndpointTests
{
    private const string AccountId = "acct-123";
    private const string SessionId = "sess-abc";
    private const string NamespaceId = "ns-test";

    private readonly Mock<ICloudflareKvClient> _kvClient = new();
    private readonly Mock<IPreviewSessionStore> _sessionStore = new();
    private readonly Mock<ILogger<GetStateEndpoint>> _logger = new();

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

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsOk_WithCurrentState_WhenStateExists()
    {
        SetupValidSession();
        _kvClient
            .Setup(k => k.GetAsync(NamespaceId, $"site-state:v1:{AccountId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync("\"pending_approval\"");

        var result = await GetStateEndpoint.Handle(
            AccountId,
            _kvClient.Object,
            _sessionStore.Object,
            _siteOptions,
            MakeContextWithSession(),
            _logger.Object,
            CancellationToken.None);

        var ok = result as Ok<GetStateResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.AccountId.Should().Be(AccountId);
        ok.Value.State.Should().Be("pending_approval");
    }

    [Fact]
    public async Task Handle_StripsQuotes_FromRawKvValue()
    {
        SetupValidSession();
        _kvClient
            .Setup(k => k.GetAsync(NamespaceId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("\"pending_billing\"");

        var result = await GetStateEndpoint.Handle(
            AccountId,
            _kvClient.Object,
            _sessionStore.Object,
            _siteOptions,
            MakeContextWithSession(),
            _logger.Object,
            CancellationToken.None);

        var ok = (Ok<GetStateResponse>)result;
        ok.Value!.State.Should().Be("pending_billing");
    }

    // ── Not found ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenStateKeyAbsent()
    {
        SetupValidSession();
        _kvClient
            .Setup(k => k.GetAsync(NamespaceId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await GetStateEndpoint.Handle(
            AccountId,
            _kvClient.Object,
            _sessionStore.Object,
            _siteOptions,
            MakeContextWithSession(),
            _logger.Object,
            CancellationToken.None);

        var statusCode = result is IStatusCodeHttpResult sc ? (sc.StatusCode ?? 200) : 200;
        statusCode.Should().Be(404);
    }

    // ── Session validation ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenNoSession()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();

        var result = await GetStateEndpoint.Handle(
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
    public async Task Handle_ReturnsForbid_WhenAccountIdDoesNotMatchSession()
    {
        // Session is scoped to "acct-other"
        SetupValidSession(accountId: "acct-other");

        var result = await GetStateEndpoint.Handle(
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
    public async Task Handle_ReturnsProblem_WhenKvReadFails()
    {
        SetupValidSession();
        _kvClient
            .Setup(k => k.GetAsync(NamespaceId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("KV unavailable"));

        var result = await GetStateEndpoint.Handle(
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
