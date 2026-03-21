using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using RealEstateStar.Api.Features.WhatsApp.Webhook.VerifyWebhook;

namespace RealEstateStar.Api.Tests.Features.WhatsApp.Webhook.VerifyWebhook;

public class VerifyWebhookEndpointTests
{
    [Fact]
    public void Handle_Returns200WithChallenge_WhenTokenMatches()
    {
        var result = VerifyWebhookEndpoint.Handle(
            "subscribe", "my-verify-token", "challenge_string_123", "my-verify-token");

        result.Should().BeOfType<ContentHttpResult>();
    }

    [Fact]
    public void Handle_Returns403_WhenTokenMismatches()
    {
        var result = VerifyWebhookEndpoint.Handle(
            "subscribe", "wrong-token", "challenge", "my-verify-token");

        result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public void Handle_Returns403_WhenModeIsNotSubscribe()
    {
        var result = VerifyWebhookEndpoint.Handle(
            "unsubscribe", "my-verify-token", "challenge", "my-verify-token");

        result.Should().BeOfType<ForbidHttpResult>();
    }
}
