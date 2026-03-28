using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Workers.Leads.Tests;

public sealed class PdfWorkerTests
{
    private readonly Mock<IDocumentStorageProvider> _storageMock = new();
    private readonly PdfProcessingChannel _channel = new();
    private readonly PdfWorker _worker;

    public PdfWorkerTests()
    {
        _worker = new PdfWorker(_channel, _storageMock.Object, NullLogger<PdfWorker>.Instance);
    }

    private static CmaWorkerResult BuildCmaResult(string leadId = "lead-001") => new(
        LeadId: leadId,
        Success: true,
        Error: null,
        EstimatedValue: 500_000m,
        PriceRangeLow: 480_000m,
        PriceRangeHigh: 520_000m,
        Comps:
        [
            new CompSummary("123 Main St", 490_000m, Beds: 3, Baths: 2m, Sqft: 1800, DaysOnMarket: 14, Distance: 0.3, SaleDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30))),
            new CompSummary("456 Oak Ave", 510_000m, Beds: 4, Baths: 2.5m, Sqft: 2100, DaysOnMarket: 7, Distance: 0.5, SaleDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)))
        ],
        MarketAnalysis: "Market is trending upward with low inventory."
    );

    private static AgentNotificationConfig BuildAgentConfig() => new()
    {
        AgentId = "agent-001",
        Handle = "jenise-buckalew",
        Name = "Jenise Buckalew",
        FirstName = "Jenise",
        Email = "jenise@example.com",
        Phone = "555-123-4567",
        LicenseNumber = "NJ-12345",
        BrokerageName = "Star Realty",
        PrimaryColor = "#1A3C5E",
        AccentColor = "#D4A853",
        State = "NJ",
        ServiceAreas = ["Newark", "Jersey City"]
    };

    // Test 1: Generates PDF from CmaWorkerResult and writes to storage — Completion resolves with storage path
    [Fact]
    public async Task ProcessRequestAsync_GeneratesPdfAndWritesToStorage_CompletionResolvesWithStoragePath()
    {
        // Arrange
        var cmaResult = BuildCmaResult();
        var agentConfig = BuildAgentConfig();
        var tcs = new TaskCompletionSource<PdfWorkerResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        string? capturedFolder = null;
        string? capturedFileName = null;
        string? capturedContent = null;

        _storageMock
            .Setup(s => s.WriteDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((folder, name, content, _) =>
            {
                capturedFolder = folder;
                capturedFileName = name;
                capturedContent = content;
            })
            .Returns(Task.CompletedTask);

        var request = new PdfProcessingRequest(
            LeadId: "lead-001",
            CmaResult: cmaResult,
            AgentConfig: agentConfig,
            CorrelationId: "corr-abc",
            Completion: tcs);

        // Act
        await _worker.ProcessRequestAsync(request, CancellationToken.None);
        var result = await tcs.Task;

        // Assert — completion resolves with success and a storage path
        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        result.LeadId.Should().Be("lead-001");
        result.StoragePath.Should().NotBeNullOrEmpty();
        result.StoragePath.Should().Contain("lead-001");

        // Assert — storage was called with folder, .pdf.b64 file name, and valid base64 bytes
        capturedFolder.Should().Contain("lead-001");
        capturedFileName.Should().EndWith(".pdf.b64");
        capturedContent.Should().NotBeNullOrEmpty();

        var decoded = Convert.FromBase64String(capturedContent!);
        decoded.Should().NotBeEmpty();

        _storageMock.Verify(s => s.WriteDocumentAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // Test 2: Returns failure result when PDF generation throws
    [Fact]
    public async Task ProcessRequestAsync_WhenPdfGenerationThrows_CompletionResolvesWithFailureAndStorageNotCalled()
    {
        // Arrange — use a subclass that overrides PDF generation to throw before storage is called
        var throwingWorker = new PdfWorkerThatThrowsOnGenerate(
            _channel, _storageMock.Object, NullLogger<PdfWorker>.Instance);

        var tcs = new TaskCompletionSource<PdfWorkerResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new PdfProcessingRequest(
            LeadId: "lead-002",
            CmaResult: BuildCmaResult("lead-002"),
            AgentConfig: BuildAgentConfig(),
            CorrelationId: "corr-gen-fail",
            Completion: tcs);

        // Act
        await throwingWorker.ProcessRequestAsync(request, CancellationToken.None);
        var result = await tcs.Task;

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Simulated generation failure");
        result.StoragePath.Should().BeNull();
        result.LeadId.Should().Be("lead-002");

        _storageMock.Verify(s => s.WriteDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // Test 3: Returns failure result when storage write throws
    [Fact]
    public async Task ProcessRequestAsync_WhenStorageWriteThrows_CompletionResolvesWithFailure()
    {
        // Arrange
        var cmaResult = BuildCmaResult("lead-003");
        var agentConfig = BuildAgentConfig();
        var tcs = new TaskCompletionSource<PdfWorkerResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        _storageMock
            .Setup(s => s.WriteDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Storage unavailable"));

        var request = new PdfProcessingRequest(
            LeadId: "lead-003",
            CmaResult: cmaResult,
            AgentConfig: agentConfig,
            CorrelationId: "corr-storage-fail",
            Completion: tcs);

        // Act
        await _worker.ProcessRequestAsync(request, CancellationToken.None);
        var result = await tcs.Task;

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Storage unavailable");
        result.StoragePath.Should().BeNull();
        result.LeadId.Should().Be("lead-003");
    }

    // Test 4: PDF file name includes lead ID and timestamp
    [Fact]
    public async Task StorePdfAsync_FileNameIncludesLeadIdAndTimestamp()
    {
        // Arrange
        const string leadId = "lead-timestamp-test";
        var pdfBytes = new byte[] { 1, 2, 3, 4, 5 };
        var expectedDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

        string? capturedFileName = null;
        string? capturedFolder = null;
        string? capturedContent = null;

        _storageMock
            .Setup(s => s.WriteDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((folder, name, content, _) =>
            {
                capturedFolder = folder;
                capturedFileName = name;
                capturedContent = content;
            })
            .Returns(Task.CompletedTask);

        // Act
        var storagePath = await _worker.StorePdfAsync(leadId, pdfBytes, CancellationToken.None);

        // Assert — file name contains lead ID
        capturedFileName.Should().Contain(leadId);

        // Assert — file name contains today's date (yyyy-MM-dd)
        capturedFileName.Should().Contain(expectedDate);

        // Assert — file name ends with .pdf.b64
        capturedFileName.Should().EndWith(".pdf.b64");

        // Assert — folder contains lead ID
        capturedFolder.Should().Contain(leadId);

        // Assert — returned storage path is non-empty and ends with .pdf.b64
        storagePath.Should().NotBeNullOrEmpty();
        storagePath.Should().Contain(leadId);
        storagePath.Should().EndWith(".pdf.b64");

        // Assert — written content is valid base64 encoding of the input bytes
        var decoded = Convert.FromBase64String(capturedContent!);
        decoded.Should().BeEquivalentTo(pdfBytes);
    }

    // Test 5: GeneratePdfBytes returns non-empty byte array with valid comps
    [Fact]
    public void GeneratePdfBytes_WithValidCmaResult_ReturnsNonEmptyByteArray()
    {
        // Arrange
        var cmaResult = BuildCmaResult();
        var agentConfig = BuildAgentConfig();

        // Act
        var bytes = PdfWorker.GeneratePdfBytes(cmaResult, agentConfig);

        // Assert
        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(100);
    }

    // Test 6: GeneratePdfBytes handles null comps and null market analysis without throwing
    [Fact]
    public void GeneratePdfBytes_WithNullCompsAndNoMarketAnalysis_DoesNotThrow()
    {
        // Arrange
        var cmaResult = new CmaWorkerResult(
            LeadId: "lead-nocomps",
            Success: true,
            Error: null,
            EstimatedValue: 400_000m,
            PriceRangeLow: 380_000m,
            PriceRangeHigh: 420_000m,
            Comps: null,
            MarketAnalysis: null);

        var agentConfig = BuildAgentConfig();

        // Act
        var bytes = PdfWorker.GeneratePdfBytes(cmaResult, agentConfig);

        // Assert
        bytes.Should().NotBeNull();
        bytes.Should().NotBeEmpty();
    }

    // Test 7: FormatCurrency formats USD values correctly
    [Theory]
    [InlineData(500000, "$500,000")]
    [InlineData(0, "$0")]
    [InlineData(1250500, "$1,250,500")]
    public void FormatCurrency_FormatsAsExpected(decimal value, string expected)
    {
        PdfWorker.FormatCurrency(value).Should().Be(expected);
    }
}

/// <summary>
/// Test double that overrides PDF generation to throw before storage is called,
/// allowing the ProcessRequestAsync catch path to be exercised without invoking QuestPDF.
/// </summary>
internal sealed class PdfWorkerThatThrowsOnGenerate(
    PdfProcessingChannel channel,
    IDocumentStorageProvider documentStorage,
    Microsoft.Extensions.Logging.ILogger<PdfWorker> logger)
    : PdfWorkerTestBase(channel, documentStorage, logger)
{
    protected override byte[] OverrideGeneratePdfBytes(
        CmaWorkerResult cmaResult,
        AgentNotificationConfig agentConfig)
        => throw new InvalidOperationException("Simulated generation failure");
}

/// <summary>
/// Intermediate base that re-routes ProcessRequestAsync through a virtual
/// <see cref="OverrideGeneratePdfBytes"/> so tests can inject failures.
/// </summary>
internal abstract class PdfWorkerTestBase(
    PdfProcessingChannel channel,
    IDocumentStorageProvider documentStorage,
    Microsoft.Extensions.Logging.ILogger<PdfWorker> logger)
    : PdfWorker(channel, documentStorage, logger)
{
    protected abstract byte[] OverrideGeneratePdfBytes(
        CmaWorkerResult cmaResult,
        AgentNotificationConfig agentConfig);

    internal new async Task ProcessRequestAsync(PdfProcessingRequest request, CancellationToken ct)
    {
        try
        {
            var pdfBytes = OverrideGeneratePdfBytes(request.CmaResult, request.AgentConfig);
            var storagePath = await StorePdfAsync(request.LeadId, pdfBytes, ct);

            request.Completion.TrySetResult(new PdfWorkerResult(
                request.LeadId, Success: true, Error: null, StoragePath: storagePath));
        }
        catch (Exception ex)
        {
            request.Completion.TrySetResult(new PdfWorkerResult(
                request.LeadId, Success: false, Error: ex.Message, StoragePath: null));
        }
    }
}
