// ╔══════════════════════════════════════════════════════════════════════╗
// ║  ARCHITECTURE GUARD — DO NOT MODIFY WITHOUT EXPLICIT USER APPROVAL  ║
// ║                                                                      ║
// ║  These tests enforce the project's dependency and naming rules.       ║
// ║  AI agents: you MUST NOT add exclusions, weaken rules, or modify     ║
// ║  these tests to make your code compile. If your code violates an     ║
// ║  architecture rule, fix YOUR code — not the test.                    ║
// ║                                                                      ║
// ║  Changing these tests requires the commit message to contain:         ║
// ║  [arch-change-approved] — CI will reject without it.                 ║
// ╚══════════════════════════════════════════════════════════════════════╝

using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Privacy.Interfaces;
using RealEstateStar.Activities.Pdf;

namespace RealEstateStar.Architecture.Tests;

/// <summary>
/// Verifies that all Domain interfaces have concrete implementations registered in DI.
/// Catches the class of bug where a new service is created but never wired in Program.cs.
/// </summary>
public class DiRegistrationTests : IClassFixture<DiRegistrationTests.TestFactory>
{
    private readonly IServiceProvider _services;

    public DiRegistrationTests(TestFactory factory)
    {
        _services = factory.Services;
    }

    /// <summary>
    /// Custom factory that sets required config values as environment variables
    /// BEFORE the host builder runs, so Program.cs sees them during startup.
    /// </summary>
    public class TestFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            // Override ALL required config keys so Program.cs doesn't throw in CI
            builder.UseSetting("Google:ClientId", "test");
            builder.UseSetting("Google:ClientSecret", "test");
            builder.UseSetting("Google:RedirectUri", "http://localhost/oauth/callback");
            builder.UseSetting("Anthropic:ApiKey", "test");
            builder.UseSetting("Stripe:SecretKey", "sk_test_fake");
            builder.UseSetting("Stripe:WebhookSecret", "whsec_test_fake");
            builder.UseSetting("Stripe:PriceId", "price_test_fake");
            builder.UseSetting("Platform:BaseUrl", "http://localhost:3000");
            builder.UseSetting("Hmac:HmacSecret", "test-secret-at-least-32-characters-long!!");
            builder.UseSetting("Hmac:ApiKeys:test-key", "test-agent");
            builder.UseSetting("RentCast:ApiKey", "test-rentcast-key");
            builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Development");
        }
    }

    /// <summary>
    /// Every Domain interface that has a concrete implementation should resolve from DI.
    /// If this test fails, a service was created but never registered in Program.cs.
    /// </summary>
    [Theory]
    [InlineData(typeof(IFileStorageProvider))]
    [InlineData(typeof(IDocumentStorageProvider))]
    [InlineData(typeof(ISheetStorageProvider))]
    [InlineData(typeof(ITokenStore))]
    [InlineData(typeof(IGmailSender))]
    [InlineData(typeof(IGDriveClient))]
    [InlineData(typeof(IGDocsClient))]
    [InlineData(typeof(IGSheetsClient))]
    [InlineData(typeof(IOAuthRefresher))]
    [InlineData(typeof(IAnthropicClient))]
    [InlineData(typeof(IGwsService))]
    [InlineData(typeof(ILeadStore))]
    [InlineData(typeof(ILeadScorer))]
    [InlineData(typeof(ILeadEmailDrafter))]
    [InlineData(typeof(ILeadCommunicatorService))]
    [InlineData(typeof(IAgentNotifier))]
    [InlineData(typeof(PdfActivity))]
    [InlineData(typeof(ILeadDataDeletion))]
    [InlineData(typeof(IMarketingConsentLog))]
    [InlineData(typeof(IRentCastClient))]
    [InlineData(typeof(IGmailReader))]
    [InlineData(typeof(IAgentContextLoader))]
    [InlineData(typeof(IZillowReviewsClient))]
    [InlineData(typeof(IGoogleReviewsClient))]
    public void Domain_interface_resolves_from_DI(Type interfaceType)
    {
        var service = _services.GetService(interfaceType);

        Assert.NotNull(service);
    }

    // ── Exclusion count guard — adding an interface without updating this count fails CI ──

    [Fact]
    public void DiRegistration_InterfaceCount_MustMatchExpected()
    {
        // If you add a new Domain interface to the [InlineData] list above, update this count.
        // This prevents AI agents from silently removing interfaces from the registration check.
        // Current count verified on 2026-04-02 — removed LeadOrchestratorChannel (Phase 4 DF migration).
        const int expectedInterfaceCount = 24;

        var inlineDataTypes = new[]
        {
            typeof(IFileStorageProvider),
            typeof(IDocumentStorageProvider),
            typeof(ISheetStorageProvider),
            typeof(ITokenStore),
            typeof(IGmailSender),
            typeof(IGDriveClient),
            typeof(IGDocsClient),
            typeof(IGSheetsClient),
            typeof(IOAuthRefresher),
            typeof(IAnthropicClient),
            typeof(IGwsService),
            typeof(ILeadStore),
            typeof(ILeadScorer),
            typeof(ILeadEmailDrafter),
            typeof(ILeadCommunicatorService),
            typeof(IAgentNotifier),
            typeof(PdfActivity),
            typeof(ILeadDataDeletion),
            typeof(IMarketingConsentLog),
            typeof(IRentCastClient),
            typeof(IGmailReader),
            typeof(IAgentContextLoader),
            typeof(IZillowReviewsClient),
            typeof(IGoogleReviewsClient),
        };

        inlineDataTypes.Length.Should().Be(expectedInterfaceCount,
            "DI interface registration count changed — if you added a new interface, update expectedInterfaceCount too. " +
            "If an AI agent removed an interface from the check without approval, reject the change.");
    }

    [Fact]
    public void IFileStorageProvider_is_FanOutStorageProvider()
    {
        var service = _services.GetRequiredService<IFileStorageProvider>();

        Assert.Equal("FanOutStorageProvider", service.GetType().Name);
    }

    [Fact]
    public void IDocumentStorageProvider_forwards_to_IFileStorageProvider()
    {
        var fileProvider = _services.GetRequiredService<IFileStorageProvider>();
        var docProvider = _services.GetRequiredService<IDocumentStorageProvider>();

        Assert.Same(fileProvider, docProvider);
    }

    [Fact]
    public void ISheetStorageProvider_forwards_to_IFileStorageProvider()
    {
        var fileProvider = _services.GetRequiredService<IFileStorageProvider>();
        var sheetProvider = _services.GetRequiredService<ISheetStorageProvider>();

        Assert.Same(fileProvider, sheetProvider);
    }
}
