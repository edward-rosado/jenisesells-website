using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Workers.Shared.Pdf;

namespace RealEstateStar.Workers.Shared.Pdf.Tests;

public sealed class PdfActivityTests
{
    private readonly Mock<ICmaPdfGenerator> _generatorMock = new();
    private readonly Mock<IDocumentStorageProvider> _storageMock = new();
    private readonly PdfActivity _activity;

    public PdfActivityTests()
    {
        _activity = new PdfActivity(
            _generatorMock.Object,
            _storageMock.Object,
            NullLogger<PdfActivity>.Instance);
    }

    // ---------------------------------------------------------------------------
    // Test data helpers
    // ---------------------------------------------------------------------------

    private static Lead MakeLead() => new()
    {
        Id = Guid.NewGuid(),
        AgentId = "jenise-buckalew",
        LeadType = LeadType.Seller,
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Phone = "555-000-0000",
        Timeline = "3-6 months",
        SellerDetails = new SellerDetails
        {
            Address = "123 Oak Ave",
            City = "Springfield",
            State = "NJ",
            Zip = "07081",
            Beds = 3,
            Baths = 2,
            Sqft = 1800
        }
    };

    private static CmaAnalysis MakeAnalysis() => new()
    {
        ValueLow = 480_000m,
        ValueMid = 510_000m,
        ValueHigh = 540_000m,
        MarketNarrative = "Competitive market.",
        MarketTrend = "Seller's Market",
        MedianDaysOnMarket = 14
    };

    private static List<Comp> MakeComps() =>
    [
        new Comp
        {
            Address = "100 Elm St",
            SalePrice = 500_000m,
            SaleDate = new DateOnly(2025, 1, 1),
            Beds = 3,
            Baths = 2,
            Sqft = 1800,
            DistanceMiles = 0.3,
            Source = CompSource.Zillow
        }
    ];

    private static AccountConfig MakeConfig() => new()
    {
        Handle = "jenise-buckalew",
        Agent = new AccountAgent { Name = "Jenise Buckalew", Phone = "555-123-4567", Email = "jenise@example.com" }
    };

    // ---------------------------------------------------------------------------
    // Test 1: StorePdfAsync — file name includes lead ID and timestamp
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task StorePdfAsync_FileNameIncludesLeadIdAndTimestamp()
    {
        // Arrange
        const string leadId = "lead-timestamp-test";
        var expectedDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

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

        // Write a temporary file for StorePdfAsync to read
        var tempFile = Path.Combine(Path.GetTempPath(), $"{leadId}-test.pdf");
        var fakeBytes = new byte[] { 1, 2, 3, 4, 5 };
        await File.WriteAllBytesAsync(tempFile, fakeBytes);

        try
        {
            // Act
            var storagePath = await _activity.StorePdfAsync(leadId, tempFile, CancellationToken.None);

            // Assert — storage was written
            capturedFolder.Should().Contain(leadId);
            capturedFileName.Should().Contain(leadId);
            capturedFileName.Should().Contain(expectedDate);
            capturedFileName.Should().EndWith(".pdf.b64");

            storagePath.Should().NotBeNullOrEmpty();
            storagePath.Should().Contain(leadId);
            storagePath.Should().EndWith(".pdf.b64");

            // Assert — content is valid base64 of the input bytes
            var decoded = Convert.FromBase64String(capturedContent!);
            decoded.Should().BeEquivalentTo(fakeBytes);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    // ---------------------------------------------------------------------------
    // Test 2: ExecuteAsync — delegates to generator + storage, returns storage path
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_CallsGeneratorAndStorage_ReturnsStoragePath()
    {
        // Arrange
        var lead = MakeLead();
        var analysis = MakeAnalysis();
        var comps = MakeComps();
        var config = MakeConfig();

        var tempFile = Path.Combine(Path.GetTempPath(), $"fake-{Guid.NewGuid()}.pdf");
        await File.WriteAllBytesAsync(tempFile, new byte[] { 10, 20, 30 });

        _generatorMock
            .Setup(g => g.GenerateAsync(
                It.IsAny<Lead>(),
                It.IsAny<CmaAnalysis>(),
                It.IsAny<List<Comp>>(),
                It.IsAny<AccountConfig>(),
                It.IsAny<ReportType>(),
                It.IsAny<byte[]?>(),
                It.IsAny<byte[]?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tempFile);

        _storageMock
            .Setup(s => s.WriteDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        try
        {
            // Act
            var result = await _activity.ExecuteAsync(
                lead, analysis, comps, config,
                ReportType.Comprehensive,
                logoBytes: null, headshotBytes: null,
                correlationId: "corr-001",
                ct: CancellationToken.None);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().EndWith(".pdf.b64");

            _generatorMock.Verify(g => g.GenerateAsync(
                lead, analysis, comps, config,
                ReportType.Comprehensive,
                null, null,
                CancellationToken.None), Times.Once);

            _storageMock.Verify(s => s.WriteDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    // ---------------------------------------------------------------------------
    // Test 3: ExecuteAsync — null accountConfig uses lead AgentId as Handle
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_NullAccountConfig_UsesLeadAgentIdAsHandle()
    {
        // Arrange
        var lead = MakeLead();
        var analysis = MakeAnalysis();
        var comps = MakeComps();

        AccountConfig? capturedConfig = null;

        var tempFile = Path.Combine(Path.GetTempPath(), $"fake-{Guid.NewGuid()}.pdf");
        await File.WriteAllBytesAsync(tempFile, new byte[] { 1, 2, 3 });

        _generatorMock
            .Setup(g => g.GenerateAsync(
                It.IsAny<Lead>(),
                It.IsAny<CmaAnalysis>(),
                It.IsAny<List<Comp>>(),
                It.IsAny<AccountConfig>(),
                It.IsAny<ReportType>(),
                It.IsAny<byte[]?>(),
                It.IsAny<byte[]?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Lead, CmaAnalysis, List<Comp>, AccountConfig, ReportType, byte[]?, byte[]?, CancellationToken>(
                (_, _, _, cfg, _, _, _, _) => capturedConfig = cfg)
            .ReturnsAsync(tempFile);

        _storageMock
            .Setup(s => s.WriteDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        try
        {
            // Act
            await _activity.ExecuteAsync(
                lead, analysis, comps, accountConfig: null,
                ReportType.Lean,
                logoBytes: null, headshotBytes: null,
                correlationId: "corr-002",
                ct: CancellationToken.None);

            // Assert — a synthetic config was created using the lead's AgentId
            capturedConfig.Should().NotBeNull();
            capturedConfig!.Handle.Should().Be(lead.AgentId);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    // ---------------------------------------------------------------------------
    // Test 4: ExecuteAsync — propagates generator exceptions
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenGeneratorThrows_PropagatesException()
    {
        // Arrange
        var lead = MakeLead();
        var analysis = MakeAnalysis();
        var comps = MakeComps();
        var config = MakeConfig();

        _generatorMock
            .Setup(g => g.GenerateAsync(
                It.IsAny<Lead>(),
                It.IsAny<CmaAnalysis>(),
                It.IsAny<List<Comp>>(),
                It.IsAny<AccountConfig>(),
                It.IsAny<ReportType>(),
                It.IsAny<byte[]?>(),
                It.IsAny<byte[]?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Generator failed"));

        // Act
        var act = async () => await _activity.ExecuteAsync(
            lead, analysis, comps, config,
            ReportType.Lean,
            logoBytes: null, headshotBytes: null,
            correlationId: "corr-003",
            ct: CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Generator failed");

        _storageMock.Verify(s => s.WriteDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------------------
    // Test 5: ExecuteAsync — propagates storage exceptions
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenStorageThrows_PropagatesException()
    {
        // Arrange
        var lead = MakeLead();
        var analysis = MakeAnalysis();
        var comps = MakeComps();
        var config = MakeConfig();

        var tempFile = Path.Combine(Path.GetTempPath(), $"fake-{Guid.NewGuid()}.pdf");
        await File.WriteAllBytesAsync(tempFile, new byte[] { 1, 2, 3 });

        _generatorMock
            .Setup(g => g.GenerateAsync(
                It.IsAny<Lead>(),
                It.IsAny<CmaAnalysis>(),
                It.IsAny<List<Comp>>(),
                It.IsAny<AccountConfig>(),
                It.IsAny<ReportType>(),
                It.IsAny<byte[]?>(),
                It.IsAny<byte[]?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tempFile);

        _storageMock
            .Setup(s => s.WriteDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Storage unavailable"));

        try
        {
            // Act
            var act = async () => await _activity.ExecuteAsync(
                lead, analysis, comps, config,
                ReportType.Lean,
                logoBytes: null, headshotBytes: null,
                correlationId: "corr-004",
                ct: CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<IOException>()
                .WithMessage("Storage unavailable");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
