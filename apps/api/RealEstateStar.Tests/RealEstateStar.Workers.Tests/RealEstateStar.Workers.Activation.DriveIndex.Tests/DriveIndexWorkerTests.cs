using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Workers.Activation.DriveIndex.Tests;

public class DriveIndexWorkerTests
{
    private const string AccountId = "test-account";
    private const string AgentId = "test-agent";
    private const string FolderId = "folder-123";

    private static DriveIndexWorker BuildWorker(
        Mock<IGDriveClient>? mockClient = null,
        Mock<IAnthropicClient>? mockAnthropic = null)
    {
        mockClient ??= new Mock<IGDriveClient>();
        mockAnthropic ??= BuildDefaultAnthropicMock();
        return new DriveIndexWorker(
            mockClient.Object,
            mockAnthropic.Object,
            NullLogger<DriveIndexWorker>.Instance);
    }

    private static Mock<IAnthropicClient> BuildDefaultAnthropicMock()
    {
        var mock = new Mock<IAnthropicClient>();
        mock.Setup(a => a.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicResponse("{}", 10, 20, 50));
        mock.Setup(a => a.SendWithImagesAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyList<(byte[], string)>>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicResponse("{}", 10, 20, 50));
        return mock;
    }

    private static Mock<IGDriveClient> SetupMockClient(
        string folderId = FolderId,
        IReadOnlyList<DriveFileInfo>? files = null,
        Dictionary<string, string>? contents = null)
    {
        var mock = new Mock<IGDriveClient>();
        mock.Setup(c => c.GetOrCreateFolderAsync(AccountId, AgentId, DriveIndexWorker.PlatformFolderName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folderId);
        mock.Setup(c => c.ListAllFilesAsync(AccountId, AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(files ?? []);

        // Default: DownloadBinaryAsync returns null (no PDF bytes available)
        mock.Setup(c => c.DownloadBinaryAsync(AccountId, AgentId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        if (contents is not null)
        {
            foreach (var (fileId, content) in contents)
            {
                mock.Setup(c => c.GetFileContentAsync(AccountId, AgentId, fileId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(content);
            }
        }

        return mock;
    }

    // ──────────────────────────────────────────────────────────
    // RunAsync — overall behavior
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CallsGetOrCreateFolder()
    {
        var mock = SetupMockClient();
        var worker = BuildWorker(mock);

        await worker.RunAsync(AccountId, AgentId, CancellationToken.None);

        mock.Verify(c => c.GetOrCreateFolderAsync(
            AccountId, AgentId, DriveIndexWorker.PlatformFolderName, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_CallsListAllFiles()
    {
        var mock = SetupMockClient();
        var worker = BuildWorker(mock);

        await worker.RunAsync(AccountId, AgentId, CancellationToken.None);

        mock.Verify(c => c.ListAllFilesAsync(AccountId, AgentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_ReturnsFolderIdInResult()
    {
        var mock = SetupMockClient(folderId: "special-folder-id");
        var worker = BuildWorker(mock);

        var result = await worker.RunAsync(AccountId, AgentId, CancellationToken.None);

        result.FolderId.Should().Be("special-folder-id");
    }

    [Fact]
    public async Task RunAsync_ReturnsEmptyIndex_WhenNoDriveFiles()
    {
        var mock = SetupMockClient(files: []);
        var worker = BuildWorker(mock);

        var result = await worker.RunAsync(AccountId, AgentId, CancellationToken.None);

        result.Files.Should().BeEmpty();
        result.Contents.Should().BeEmpty();
        result.DiscoveredUrls.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_FiltersForRealEstateDocuments()
    {
        var files = new List<DriveFileInfo>
        {
            new("doc-1", "Listing Agreement.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                DateTime.UtcNow),
            new("doc-2", "Vacation Photos.jpg", "image/jpeg", DateTime.UtcNow),
            new("doc-3", "CMA Report.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                DateTime.UtcNow),
        };
        var mock = SetupMockClient(files: files, contents: new Dictionary<string, string>
        {
            ["doc-1"] = "Listing agreement content.",
            ["doc-3"] = "CMA report content."
        });
        var worker = BuildWorker(mock);

        var result = await worker.RunAsync(AccountId, AgentId, CancellationToken.None);

        // Should include listing + CMA docs but not the photo
        result.Files.Should().HaveCount(2);
        result.Files.Select(f => f.Id).Should().Contain("doc-1");
        result.Files.Select(f => f.Id).Should().Contain("doc-3");
        result.Files.Select(f => f.Id).Should().NotContain("doc-2");
    }

    [Fact]
    public async Task RunAsync_ReadsContentOfRealEstateDocs()
    {
        var files = new List<DriveFileInfo>
        {
            new("contract-1", "Purchase Contract.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                DateTime.UtcNow),
        };
        var mock = SetupMockClient(files: files, contents: new Dictionary<string, string>
        {
            ["contract-1"] = "This is the contract content."
        });
        var worker = BuildWorker(mock);

        var result = await worker.RunAsync(AccountId, AgentId, CancellationToken.None);

        result.Contents.Should().ContainKey("contract-1");
        result.Contents["contract-1"].Should().Be("This is the contract content.");
    }

    [Fact]
    public async Task RunAsync_ExtractsUrlsFromDocumentContent()
    {
        var files = new List<DriveFileInfo>
        {
            new("listing-1", "Listing Presentation.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                DateTime.UtcNow),
        };
        var mock = SetupMockClient(files: files, contents: new Dictionary<string, string>
        {
            ["listing-1"] = "See https://www.example.com/listing and https://mls.com/property/123"
        });
        var worker = BuildWorker(mock);

        var result = await worker.RunAsync(AccountId, AgentId, CancellationToken.None);

        result.DiscoveredUrls.Should().Contain("https://www.example.com/listing");
        result.DiscoveredUrls.Should().Contain("https://mls.com/property/123");
    }

    [Fact]
    public async Task RunAsync_ContinuesWhenContentFetchFails()
    {
        var files = new List<DriveFileInfo>
        {
            new("contract-1", "Purchase Agreement.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                DateTime.UtcNow),
        };
        var mock = SetupMockClient(files: files);
        mock.Setup(c => c.GetFileContentAsync(AccountId, AgentId, "contract-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Drive API error"));

        var worker = BuildWorker(mock);

        var act = async () => await worker.RunAsync(AccountId, AgentId, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    // ──────────────────────────────────────────────────────────
    // IsRealEstateFile
    // ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Listing Agreement.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", true)]
    [InlineData("CMA Report Q1.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", true)]
    [InlineData("Marketing Flyer.pdf", "application/pdf", true)]
    [InlineData("Purchase Contract.txt", "text/plain", true)]
    [InlineData("Vacation Photos.jpg", "image/jpeg", false)]
    [InlineData("Budget Spreadsheet.xlsx", "application/vnd.ms-excel", false)]
    [InlineData("Contract.jpg", "image/jpeg", false)] // keyword match but wrong mime type
    public void IsRealEstateFile_DetectsCorrectly(string name, string mimeType, bool expected)
    {
        var result = DriveIndexWorker.IsRealEstateFile(name, mimeType);

        result.Should().Be(expected);
    }

    // ──────────────────────────────────────────────────────────
    // CategorizeFile
    // ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("CMA Report 123 Main St.docx", "CMA")]
    [InlineData("Comparative Market Analysis.pdf", "CMA")]
    [InlineData("Purchase Contract.docx", "Contract")]
    [InlineData("Listing Presentation.pptx", "Listing")]
    [InlineData("Marketing Flyer Main St.pdf", "Marketing")]
    [InlineData("Seller Disclosure Form.docx", "Listing")]
    [InlineData("Property Inspection Report.txt", "Document")]
    public void CategorizeFile_ReturnsCorrectCategory(string name, string expectedCategory)
    {
        var result = DriveIndexWorker.CategorizeFile(name);

        result.Should().Be(expectedCategory);
    }

    // ──────────────────────────────────────────────────────────
    // ExtractUrls
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void ExtractUrls_ReturnsEmptyForNoUrls()
    {
        var urls = DriveIndexWorker.ExtractUrls("No URLs in this text.");

        urls.Should().BeEmpty();
    }

    [Fact]
    public void ExtractUrls_ExtractsMultipleUrls()
    {
        var content = "Visit https://mls.com and check https://zillow.com/listing/123.";

        var urls = DriveIndexWorker.ExtractUrls(content);

        urls.Should().HaveCount(2);
        urls.Should().Contain("https://mls.com");
        urls.Should().Contain("https://zillow.com/listing/123");
    }

    [Fact]
    public void ExtractUrls_DeduplicatesUrls()
    {
        var content = "See https://example.com and also https://example.com.";

        var urls = DriveIndexWorker.ExtractUrls(content);

        urls.Should().HaveCount(1);
    }

    [Fact]
    public void ExtractUrls_TrimsPunctuationFromEnd()
    {
        var content = "See https://example.com/path.";

        var urls = DriveIndexWorker.ExtractUrls(content);

        urls.Should().ContainSingle().Which.Should().NotEndWith(".");
    }
}
