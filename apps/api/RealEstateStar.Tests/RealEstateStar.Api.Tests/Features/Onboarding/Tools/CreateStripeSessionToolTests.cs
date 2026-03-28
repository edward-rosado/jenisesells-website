using Xunit;
using FluentAssertions;
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
using System.Text.Json;
using Moq;
using RealEstateStar.Clients.Stripe;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Tools;

public class CreateStripeSessionToolTests
{
    [Fact]
    public void Name_ReturnsCreateStripeSession()
    {
        var mockStripe = new Mock<IStripeService>();
        var tool = new CreateStripeSessionTool(mockStripe.Object);

        Assert.Equal("create_stripe_session", tool.Name);
    }

    [Fact]
    public async Task ExecuteAsync_CallsStripeServiceWithSessionAndEmail()
    {
        var expectedUrl = "https://checkout.stripe.com/c/pay_xyz";
        var mockStripe = new Mock<IStripeService>();
        mockStripe
            .Setup(s => s.CreateCheckoutSessionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUrl);

        var tool = new CreateStripeSessionTool(mockStripe.Object);
        var session = OnboardingSession.Create(null);
        session.Profile = new ScrapedProfile { Email = "agent@example.com" };

        var parameters = JsonDocument.Parse("{}").RootElement;

        var result = await tool.ExecuteAsync(parameters, session, CancellationToken.None);

        Assert.Contains(expectedUrl, result);
        mockStripe.Verify(s => s.CreateCheckoutSessionAsync(
            session.Id,
            "agent@example.com",
            CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsErrorWhenProfileMissing()
    {
        var mockStripe = new Mock<IStripeService>();
        var tool = new CreateStripeSessionTool(mockStripe.Object);
        var session = OnboardingSession.Create(null);
        // No profile set

        var parameters = JsonDocument.Parse("{}").RootElement;

        var result = await tool.ExecuteAsync(parameters, session, CancellationToken.None);

        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.TryGetProperty("error", out _));
        mockStripe.Verify(s => s.CreateCheckoutSessionAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsErrorWhenEmailEmpty()
    {
        var mockStripe = new Mock<IStripeService>();
        var tool = new CreateStripeSessionTool(mockStripe.Object);
        var session = OnboardingSession.Create(null);
        session.Profile = new ScrapedProfile { Email = "" };

        var parameters = JsonDocument.Parse("{}").RootElement;

        var result = await tool.ExecuteAsync(parameters, session, CancellationToken.None);

        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.TryGetProperty("error", out _));
        mockStripe.Verify(s => s.CreateCheckoutSessionAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCheckoutUrlInResult()
    {
        var expectedUrl = "https://checkout.stripe.com/c/pay_checkout";
        var mockStripe = new Mock<IStripeService>();
        mockStripe
            .Setup(s => s.CreateCheckoutSessionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUrl);

        var tool = new CreateStripeSessionTool(mockStripe.Object);
        var session = OnboardingSession.Create(null);
        session.Profile = new ScrapedProfile { Email = "agent@example.com" };

        var parameters = JsonDocument.Parse("{}").RootElement;
        var result = await tool.ExecuteAsync(parameters, session, CancellationToken.None);

        // Result should be parseable JSON with a checkoutUrl field
        var json = JsonDocument.Parse(result);
        Assert.Equal(expectedUrl, json.RootElement.GetProperty("checkoutUrl").GetString());
    }
}
