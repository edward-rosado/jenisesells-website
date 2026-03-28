using Xunit;
using Moq;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.HomeSearch.Interfaces;
using RealEstateStar.Domain.Privacy.Interfaces;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.Domain.Onboarding.Services;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.DataServices.WhatsApp;
using RealEstateStar.Api.Features.Leads;
using RealEstateStar.Api.Features.Leads.Submit;
using RealEstateStar.Workers.Onboarding;
using RealEstateStar.Workers.Onboarding.Tools;
using RealEstateStar.TestUtilities;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.Lead.CMA;
using RealEstateStar.Workers.Lead.HomeSearch;
using FluentAssertions;
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
