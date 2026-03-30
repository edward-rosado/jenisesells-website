using FluentAssertions;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Workers.Activation.CmaStyle.Tests;

public class CmaStyleWorkerTests
{
    private static DriveIndex MakeEmptyDriveIndex() => new(
        FolderId: "folder-1",
        Files: [],
        Contents: new Dictionary<string, string>(),
        DiscoveredUrls: []);

    private static DriveFile MakeCmaFile(string id = "file-1", string name = "CMA Report 2024.pdf") =>
        new(Id: id, Name: name, MimeType: "application/pdf", Category: "cma", ModifiedDate: DateTime.UtcNow);

    private static DriveIndex MakeDriveIndexWith(IReadOnlyList<DriveFile> files, IReadOnlyDictionary<string, string>? contents = null) =>
        new("folder-1", files, contents ?? new Dictionary<string, string>(), []);

    private static AnthropicResponse MakeValidResponse() =>
        new(Content: """
            ## CMA Style Guide
            ### Layout
            Single-page layout with agent branding header.
            ### Data Emphasis
            Focus on price per sqft and neighborhood comps.
            ### Comp Presentation
            Table format with 5 comps, sorted by recency.
            ### Branding Treatment
            Logo in top-right corner, brand colors in headers.
            ### Unique Sections
            None detected.
            """,
            InputTokens: 100,
            OutputTokens: 200,
            DurationMs: 1500);

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — skip when no CMA docs
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_NoCmaDocuments_ReturnsNull()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<CmaStyleWorker>>();

        var worker = new CmaStyleWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var driveIndex = MakeEmptyDriveIndex();

        var result = await worker.AnalyzeAsync(driveIndex, CancellationToken.None);

        result.Should().BeNull();
        anthropic.Verify(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnalyzeAsync_NonCmaDocuments_ReturnsNull()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<CmaStyleWorker>>();

        var worker = new CmaStyleWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var nonCmaFiles = new List<DriveFile>
        {
            new("f1", "Contract 2024.pdf", "application/pdf", "contract", DateTime.UtcNow),
            new("f2", "Invoice March.xlsx", "application/vnd.ms-excel", "finance", DateTime.UtcNow)
        };
        var driveIndex = MakeDriveIndexWith(nonCmaFiles);

        var result = await worker.AnalyzeAsync(driveIndex, CancellationToken.None);

        result.Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — happy path
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_WithCmaDocs_CallsSanitizerThenClaude()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<CmaStyleWorker>>();

        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeValidResponse());

        var worker = new CmaStyleWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var cmaFile = MakeCmaFile();
        var contents = new Dictionary<string, string> { ["file-1"] = "CMA content here" };
        var driveIndex = MakeDriveIndexWith([cmaFile], contents);

        var result = await worker.AnalyzeAsync(driveIndex, CancellationToken.None);

        result.Should().NotBeNull();
        sanitizer.Verify(s => s.Sanitize("CMA content here"), Times.Once);
        anthropic.Verify(a => a.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), "activation-cma-style", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnalyzeAsync_ValidResponse_ReturnsMarkdown()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<CmaStyleWorker>>();

        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeValidResponse());

        var worker = new CmaStyleWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var driveIndex = MakeDriveIndexWith([MakeCmaFile()]);

        var result = await worker.AnalyzeAsync(driveIndex, CancellationToken.None);

        result.Should().Contain("## CMA Style Guide");
        result.Should().Contain("### Layout");
        result.Should().Contain("### Comp Presentation");
    }

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — output validation catches malformed response
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_MalformedResponse_ThrowsInvalidOperationException()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<CmaStyleWorker>>();

        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicResponse("This is just plain text without required sections.", 50, 20, 500));

        var worker = new CmaStyleWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var driveIndex = MakeDriveIndexWith([MakeCmaFile()]);

        var act = () => worker.AnalyzeAsync(driveIndex, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing required section*");
    }

    // ---------------------------------------------------------------------------
    // IsCmaRelated
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("CMA Report April.pdf", "docs", true)]
    [InlineData("Comparative Market Analysis.docx", "reports", true)]
    [InlineData("Market Analysis Q1.pdf", "docs", true)]
    [InlineData("Property Valuation 2024.pdf", "docs", true)]
    [InlineData("file.pdf", "cma", true)]
    [InlineData("file.pdf", "market-analysis", true)]
    [InlineData("Contract Draft.pdf", "contracts", false)]
    [InlineData("Invoice March.xlsx", "finance", false)]
    public void IsCmaRelated_FiltersCorrectly(string name, string category, bool expected)
    {
        var result = CmaStyleWorker.IsCmaRelated(name, category);
        result.Should().Be(expected);
    }

    // ---------------------------------------------------------------------------
    // BuildPrompt
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildPrompt_WithContent_WrapsInUserDataTags()
    {
        var sanitizer = new Mock<IContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);

        var files = new List<DriveFile> { MakeCmaFile("f1", "CMA April.pdf") };
        var contents = new Dictionary<string, string> { ["f1"] = "This is CMA content" };

        var prompt = CmaStyleWorker.BuildPrompt(files, contents, sanitizer.Object);

        prompt.Should().Contain("<user-data>");
        prompt.Should().Contain("</user-data>");
        prompt.Should().Contain("This is CMA content");
        sanitizer.Verify(s => s.Sanitize("This is CMA content"), Times.Once);
    }

    [Fact]
    public void BuildPrompt_ContentExceedsLimit_Truncates()
    {
        var sanitizer = new Mock<IContentSanitizer>();
        var longContent = new string('x', 3000);
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns(longContent);

        var files = new List<DriveFile> { MakeCmaFile() };
        var contents = new Dictionary<string, string> { ["file-1"] = longContent };

        var prompt = CmaStyleWorker.BuildPrompt(files, contents, sanitizer.Object);

        prompt.Should().Contain("...");
        prompt.Should().NotContain(new string('x', 2001));
    }

    [Fact]
    public void BuildPrompt_NoContent_UsesFileMetadata()
    {
        var sanitizer = new Mock<IContentSanitizer>();
        var files = new List<DriveFile>
        {
            new("f1", "CMA April.pdf", "application/pdf", "cma", new DateTime(2025, 1, 15))
        };

        var prompt = CmaStyleWorker.BuildPrompt(files, new Dictionary<string, string>(), sanitizer.Object);

        prompt.Should().Contain("CMA April.pdf");
        prompt.Should().Contain("File metadata only");
        sanitizer.Verify(s => s.Sanitize(It.IsAny<string>()), Times.Never);
    }

    // ---------------------------------------------------------------------------
    // ValidateMarkdownOutput
    // ---------------------------------------------------------------------------

    [Fact]
    public void ValidateMarkdownOutput_ValidContent_DoesNotThrow()
    {
        var validContent = """
            ## CMA Style Guide
            ### Layout
            Single page.
            ### Data Emphasis
            Price focus.
            ### Comp Presentation
            Table format.
            ### Branding Treatment
            Logo top-right.
            ### Unique Sections
            None.
            """;

        var act = () => CmaStyleWorker.ValidateMarkdownOutput(validContent);
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateMarkdownOutput_MissingSection_ThrowsWithSectionName()
    {
        var contentMissingCompPresentation = """
            ## CMA Style Guide
            ### Layout
            Single page.
            ### Data Emphasis
            Price focus.
            """;

        var act = () => CmaStyleWorker.ValidateMarkdownOutput(contentMissingCompPresentation);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Comp Presentation*");
    }
}
