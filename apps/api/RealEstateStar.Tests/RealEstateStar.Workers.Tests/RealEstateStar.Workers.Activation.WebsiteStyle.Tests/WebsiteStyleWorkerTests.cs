using FluentAssertions;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Workers.Activation.WebsiteStyle.Tests;

public class WebsiteStyleWorkerTests
{
    private static AgentDiscovery MakeEmptyDiscovery() =>
        new(HeadshotBytes: null, LogoBytes: null, Phone: null,
            Websites: [], Reviews: [], Profiles: [],
            Ga4MeasurementId: null, WhatsAppEnabled: false);

    private static AgentDiscovery MakeDiscoveryWith(params DiscoveredWebsite[] websites) =>
        new(HeadshotBytes: null, LogoBytes: null, Phone: null,
            Websites: websites, Reviews: [], Profiles: [],
            Ga4MeasurementId: null, WhatsAppEnabled: false);

    private static DiscoveredWebsite MakeWebsite(string html = "<html><body>Agent website</body></html>") =>
        new(Url: "https://agent.example.com", Source: "agent-site", Html: html);

    private static AnthropicResponse MakeValidResponse() =>
        new(Content: """
            ## Website Style Guide
            ### Layout Patterns
            Hero section with full-width image, grid of listings below.
            ### Content Structure
            Home, About, Listings, Contact.
            ### Featured Listings Presentation
            Card grid with 3 columns.
            ### Lead Capture Form Style
            Single-step form in hero section.
            ### Photo Usage
            Professional headshot, high-res listing photos.
            ### IDX/MLS Patterns
            Embedded IDX search widget.
            ### Mobile Approach
            Responsive, mobile-first design.
            """,
            InputTokens: 100, OutputTokens: 200, DurationMs: 1500);

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — skip when no websites
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_NoWebsites_ReturnsNull()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<WebsiteStyleWorker>>();

        var worker = new WebsiteStyleWorker(anthropic.Object, sanitizer.Object, logger.Object);

        var result = await worker.AnalyzeAsync(MakeEmptyDiscovery(), CancellationToken.None);

        result.Should().BeNull();
        anthropic.Verify(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnalyzeAsync_WebsitesWithNoHtml_ReturnsNull()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<WebsiteStyleWorker>>();

        var worker = new WebsiteStyleWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var discovery = MakeDiscoveryWith(
            new DiscoveredWebsite("https://agent.example.com", "agent", null));

        var result = await worker.AnalyzeAsync(discovery, CancellationToken.None);

        result.Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — sanitizer called before Claude
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_WithWebsites_CallsSanitizerBeforeClaude()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<WebsiteStyleWorker>>();

        var callOrder = new List<string>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>()))
            .Callback<string>(_ => callOrder.Add("sanitize"))
            .Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>((_, _, _, _, _, _) => callOrder.Add("claude"))
            .ReturnsAsync(MakeValidResponse());

        var worker = new WebsiteStyleWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var discovery = MakeDiscoveryWith(MakeWebsite());

        await worker.AnalyzeAsync(discovery, CancellationToken.None);

        callOrder.Should().StartWith(["sanitize"]);
        callOrder.IndexOf("sanitize").Should().BeLessThan(callOrder.IndexOf("claude"));
    }

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — happy path
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_ValidResponse_ReturnsMarkdownWithRequiredSections()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<WebsiteStyleWorker>>();

        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeValidResponse());

        var worker = new WebsiteStyleWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var discovery = MakeDiscoveryWith(MakeWebsite());

        var result = await worker.AnalyzeAsync(discovery, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().Contain("## Website Style Guide");
        result.Should().Contain("### Layout Patterns");
        result.Should().Contain("### Lead Capture");
    }

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — malformed response
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_MalformedResponse_ReturnsPartialContent()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<WebsiteStyleWorker>>();

        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicResponse("Just some text.", 50, 20, 500));

        var worker = new WebsiteStyleWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var discovery = MakeDiscoveryWith(MakeWebsite());

        var result = await worker.AnalyzeAsync(discovery, CancellationToken.None);

        result.Should().Be("Just some text.");
    }

    [Fact]
    public async Task AnalyzeAsync_MalformedResponse_LogsWarningWithMissingSections()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<WebsiteStyleWorker>>();

        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicResponse("Just some text.", 50, 20, 500));

        var worker = new WebsiteStyleWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var discovery = MakeDiscoveryWith(MakeWebsite());

        await worker.AnalyzeAsync(discovery, CancellationToken.None);

        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, _) => o.ToString()!.Contains("[WEB-STYLE-005]")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ---------------------------------------------------------------------------
    // BuildPrompt
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildPrompt_WrapsHtmlInUserDataTags()
    {
        var sanitizer = new Mock<IContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);

        var websites = new List<DiscoveredWebsite> { MakeWebsite() };

        var prompt = WebsiteStyleWorker.BuildPrompt(websites, sanitizer.Object);

        prompt.Should().Contain("<user-data>");
        prompt.Should().Contain("</user-data>");
    }

    [Fact]
    public void BuildPrompt_LongHtml_IncludesFullContent()
    {
        var sanitizer = new Mock<IContentSanitizer>();
        var longHtml = new string('x', 5000);
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns(longHtml);

        var websites = new List<DiscoveredWebsite>
        {
            new("https://example.com", "agent", "<html></html>")
        };

        var prompt = WebsiteStyleWorker.BuildPrompt(websites, sanitizer.Object);

        prompt.Should().Contain(longHtml);
    }

    [Fact]
    public void BuildPrompt_CallsSanitizerOnHtml()
    {
        var sanitizer = new Mock<IContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns("sanitized html");

        var websites = new List<DiscoveredWebsite>
        {
            new("https://example.com", "agent", "<html>raw</html>")
        };

        WebsiteStyleWorker.BuildPrompt(websites, sanitizer.Object);

        sanitizer.Verify(s => s.Sanitize("<html>raw</html>"), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // ValidateMarkdownOutput
    // ---------------------------------------------------------------------------

    [Fact]
    public void ValidateMarkdownOutput_MissingContentStructure_Throws()
    {
        var content = "## Website Style Guide\n### Layout Patterns\ntext\n### Lead Capture\nform";

        var act = () => WebsiteStyleWorker.ValidateMarkdownOutput(content);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Content Structure*");
    }

    [Fact]
    public void ValidateMarkdownOutput_AllSectionsPresent_DoesNotThrow()
    {
        var content = """
            ## Website Style Guide
            ### Layout Patterns
            Hero section.
            ### Content Structure
            Standard nav.
            ### Lead Capture
            Single-step form.
            """;

        var act = () => WebsiteStyleWorker.ValidateMarkdownOutput(content);
        act.Should().NotThrow();
    }
}
