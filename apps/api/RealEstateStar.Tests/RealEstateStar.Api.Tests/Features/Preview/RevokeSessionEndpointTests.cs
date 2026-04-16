using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Api.Features.Preview.RevokeSession;
using RealEstateStar.Domain.Activation.Interfaces;

namespace RealEstateStar.Api.Tests.Features.Preview;

public sealed class RevokeSessionEndpointTests
{
    private readonly Mock<IPreviewSessionStore> _sessionStore = new();
    private readonly Mock<ILogger<RevokeSessionEndpoint>> _logger = new();

    private static HttpContext MakeHttpContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    private static int StatusCode(IResult result) =>
        result is IStatusCodeHttpResult sc ? (sc.StatusCode ?? 200) : 200;

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Returns200_WhenSessionExists()
    {
        _sessionStore
            .Setup(s => s.RevokeAsync("sess-abc", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await RevokeSessionEndpoint.Handle(
            "sess-abc",
            _sessionStore.Object,
            MakeHttpContext(),
            _logger.Object,
            CancellationToken.None);

        StatusCode(result).Should().Be(200);
    }

    [Fact]
    public async Task Handle_Returns200_WhenSessionAlreadyRevoked_Idempotent()
    {
        // RevokeAsync is idempotent in the store — no exception thrown
        _sessionStore
            .Setup(s => s.RevokeAsync("sess-already-revoked", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await RevokeSessionEndpoint.Handle(
            "sess-already-revoked",
            _sessionStore.Object,
            MakeHttpContext(),
            _logger.Object,
            CancellationToken.None);

        StatusCode(result).Should().Be(200);
    }

    // ── Not found ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Returns404_WhenSessionDoesNotExist()
    {
        _sessionStore
            .Setup(s => s.RevokeAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Session not found."));

        var result = await RevokeSessionEndpoint.Handle(
            "nonexistent",
            _sessionStore.Object,
            MakeHttpContext(),
            _logger.Object,
            CancellationToken.None);

        StatusCode(result).Should().Be(404);
    }

    // ── Cookie cleared ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ClearsCookie_OnSuccess()
    {
        _sessionStore
            .Setup(s => s.RevokeAsync("sess-abc", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var ctx = MakeHttpContext();
        await RevokeSessionEndpoint.Handle(
            "sess-abc",
            _sessionStore.Object,
            ctx,
            _logger.Object,
            CancellationToken.None);

        // After revocation the Set-Cookie header should contain the cookie name with an expiry in the past
        var setCookieHeader = ctx.Response.Headers["Set-Cookie"].ToString();
        setCookieHeader.Should().Contain("preview_session");
    }
}
