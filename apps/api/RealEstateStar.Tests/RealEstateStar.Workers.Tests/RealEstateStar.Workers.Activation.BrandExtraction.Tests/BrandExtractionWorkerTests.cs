using FluentAssertions;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Workers.Activation.BrandExtraction.Tests;

public class BrandExtractionWorkerTests
{
    private static EmailCorpus MakeEmptyCorpus() =>
        new(SentEmails: [], InboxEmails: [], Signature: null);

    private static EmailCorpus MakeCorpusWithSignature() =>
        new(SentEmails: [], InboxEmails: [],
            Signature: new EmailSignature(
                Name: "Jenise Buckalew", Title: "REALTOR", Phone: "555-1234",
                LicenseNumber: "NJ-12345", BrokerageName: "Keller Williams",
                SocialLinks: [], HeadshotUrl: null, WebsiteUrl: null, LogoUrl: null));

    private static DriveIndex MakeEmptyDriveIndex() => new(
        FolderId: "f1", Files: [],
        Contents: new Dictionary<string, string>(), DiscoveredUrls: []);

    private static AgentDiscovery MakeDiscoveryWithBrokerageWebsite() =>
        new(HeadshotBytes: null, LogoBytes: null, Phone: null,
            Websites: [new DiscoveredWebsite("https://kw.com", "brokerage", "<html>Keller Williams brand content</html>")],
            Reviews: [], Profiles: [], Ga4MeasurementId: null, WhatsAppEnabled: false);

    private static AgentDiscovery MakeEmptyDiscovery() =>
        new(HeadshotBytes: null, LogoBytes: null, Phone: null,
            Websites: [], Reviews: [], Profiles: [],
            Ga4MeasurementId: null, WhatsAppEnabled: false);

    private static AnthropicResponse MakeBrandSignalsResponse() =>
        new(Content: """
            brokerage_name: Keller Williams
            tagline: Built on Success
            value_proposition: Agent support and technology
            market_positioning: Full-service residential
            brand_colors: #CC0000, #000000
            brand_personality: Professional and ambitious
            """,
            InputTokens: 100, OutputTokens: 150, DurationMs: 1000);

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — sanitizer called before Claude
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_WithBrokerageWebsite_CallsSanitizerBeforeClaude()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<BrandExtractionWorker>>();

        var callOrder = new List<string>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>()))
            .Callback<string>(_ => callOrder.Add("sanitize"))
            .Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>((_, _, _, _, _, _) => callOrder.Add("claude"))
            .ReturnsAsync(MakeBrandSignalsResponse());

        var worker = new BrandExtractionWorker(anthropic.Object, sanitizer.Object, logger.Object);

        await worker.AnalyzeAsync(MakeEmptyCorpus(), MakeEmptyDriveIndex(), MakeDiscoveryWithBrokerageWebsite(), CancellationToken.None);

        callOrder.Should().Contain("sanitize");
        callOrder.Should().Contain("claude");
        callOrder.IndexOf("sanitize").Should().BeLessThan(callOrder.IndexOf("claude"));
    }

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — returns brand signals string
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_WithData_ReturnsBrandSignalsString()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<BrandExtractionWorker>>();

        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeBrandSignalsResponse());

        var worker = new BrandExtractionWorker(anthropic.Object, sanitizer.Object, logger.Object);

        var result = await worker.AnalyzeAsync(MakeEmptyCorpus(), MakeEmptyDriveIndex(), MakeDiscoveryWithBrokerageWebsite(), CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().Contain("brokerage_name");
        result.Should().Contain("Keller Williams");
    }

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — always calls Claude (no skip logic)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_NoWebsiteOrDocs_StillCallsClaude()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<BrandExtractionWorker>>();

        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicResponse("brokerage_name: Unknown", 10, 10, 200));

        var worker = new BrandExtractionWorker(anthropic.Object, sanitizer.Object, logger.Object);

        await worker.AnalyzeAsync(MakeEmptyCorpus(), MakeEmptyDriveIndex(), MakeEmptyDiscovery(), CancellationToken.None);

        anthropic.Verify(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), "activation-brand-extraction", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // BuildPrompt
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildPrompt_BrokerageWebsiteMarkedAsPrimarySource()
    {
        var sanitizer = new Mock<IContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);

        var corpus = MakeEmptyCorpus();
        var driveIndex = MakeEmptyDriveIndex();
        var discovery = MakeDiscoveryWithBrokerageWebsite();

        var prompt = BrandExtractionWorker.BuildPrompt(corpus, driveIndex, discovery, sanitizer.Object);

        prompt.Should().Contain("PRIMARY SOURCE");
    }

    [Fact]
    public void BuildPrompt_WithSignature_IncludesBrokerageName()
    {
        var sanitizer = new Mock<IContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);

        var corpus = MakeCorpusWithSignature();

        var prompt = BrandExtractionWorker.BuildPrompt(corpus, MakeEmptyDriveIndex(), MakeEmptyDiscovery(), sanitizer.Object);

        prompt.Should().Contain("Keller Williams");
    }

    [Fact]
    public void BuildPrompt_HtmlWrappedInUserDataTags()
    {
        var sanitizer = new Mock<IContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);

        var discovery = MakeDiscoveryWithBrokerageWebsite();

        var prompt = BrandExtractionWorker.BuildPrompt(MakeEmptyCorpus(), MakeEmptyDriveIndex(), discovery, sanitizer.Object);

        prompt.Should().Contain("<user-data>");
        prompt.Should().Contain("</user-data>");
    }
}
