using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Api.Features.Cma.Download;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using NotFoundResult = Microsoft.AspNetCore.Http.HttpResults.NotFound<RealEstateStar.Api.Features.Cma.Download.CmaErrorResponse>;

namespace RealEstateStar.Api.Tests.Features.Cma.Download;

public class DownloadCmaEndpointTests
{
    private readonly Mock<IDocumentStorageProvider> _documentStorage = new();
    private readonly NullLogger<DownloadCmaEndpoint> _logger = new();

    private const string AccountId = "real-estate-star";
    private const string AgentId = "jenise-buckalew";
    private static readonly Guid LeadId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    // The leads root folder used by the endpoint
    private const string LeadsRoot = "Real Estate Star/1 - Leads";

    // A blob path that contains the leadId pattern (simulates a real nested path)
    private static string BlobPath => $"Edward Rosado/123 Main St/{LeadId}-CMA-Report.pdf.b64";

    [Fact]
    public async Task Handle_PdfBlobFound_ReturnsPdf()
    {
        // Arrange
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF magic bytes
        var base64Content = Convert.ToBase64String(pdfBytes);

        _documentStorage
            .Setup(d => d.ListDocumentsAsync(LeadsRoot, It.IsAny<CancellationToken>()))
            .ReturnsAsync([BlobPath]);

        _documentStorage
            .Setup(d => d.ReadDocumentAsync(LeadsRoot, BlobPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(base64Content);

        // Act
        var result = await DownloadCmaEndpoint.Handle(
            AccountId, AgentId, LeadId,
            _documentStorage.Object,
            _logger, CancellationToken.None);

        // Assert
        var fileResult = result.Should().BeAssignableTo<FileContentHttpResult>().Subject;
        fileResult.ContentType.Should().Be("application/pdf");
        fileResult.FileContents.ToArray().Should().Equal(pdfBytes);
    }

    [Fact]
    public async Task Handle_NoPdfBlob_Returns404()
    {
        // Arrange — listing returns no blobs containing the leadId pattern
        _documentStorage
            .Setup(d => d.ListDocumentsAsync(LeadsRoot, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await DownloadCmaEndpoint.Handle(
            AccountId, AgentId, LeadId,
            _documentStorage.Object,
            _logger, CancellationToken.None);

        // Assert
        result.Should().BeAssignableTo<NotFoundResult>();
    }

    [Fact]
    public async Task Handle_BlobListHasNoMatchingLeadId_Returns404()
    {
        // Arrange — blobs exist but none match this leadId
        var otherId = Guid.NewGuid();
        _documentStorage
            .Setup(d => d.ListDocumentsAsync(LeadsRoot, It.IsAny<CancellationToken>()))
            .ReturnsAsync([$"Edward Rosado/456 Elm St/{otherId}-CMA-Report.pdf.b64"]);

        // Act
        var result = await DownloadCmaEndpoint.Handle(
            AccountId, AgentId, LeadId,
            _documentStorage.Object,
            _logger, CancellationToken.None);

        // Assert
        result.Should().BeAssignableTo<NotFoundResult>();
        _documentStorage.Verify(d => d.ReadDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmptyBlobContent_Returns404()
    {
        // Arrange
        _documentStorage
            .Setup(d => d.ListDocumentsAsync(LeadsRoot, It.IsAny<CancellationToken>()))
            .ReturnsAsync([BlobPath]);

        _documentStorage
            .Setup(d => d.ReadDocumentAsync(LeadsRoot, BlobPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await DownloadCmaEndpoint.Handle(
            AccountId, AgentId, LeadId,
            _documentStorage.Object,
            _logger, CancellationToken.None);

        // Assert
        result.Should().BeAssignableTo<NotFoundResult>();
    }

    [Fact]
    public async Task Handle_CorruptBase64_Returns500()
    {
        // Arrange
        _documentStorage
            .Setup(d => d.ListDocumentsAsync(LeadsRoot, It.IsAny<CancellationToken>()))
            .ReturnsAsync([BlobPath]);

        _documentStorage
            .Setup(d => d.ReadDocumentAsync(LeadsRoot, BlobPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync("!!!not-valid-base64!!!");

        // Act
        var result = await DownloadCmaEndpoint.Handle(
            AccountId, AgentId, LeadId,
            _documentStorage.Object,
            _logger, CancellationToken.None);

        // Assert
        var problem = result.Should().BeAssignableTo<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task Handle_ListDocumentsThrows_Returns500()
    {
        // Arrange
        _documentStorage
            .Setup(d => d.ListDocumentsAsync(LeadsRoot, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Storage unavailable"));

        // Act
        var result = await DownloadCmaEndpoint.Handle(
            AccountId, AgentId, LeadId,
            _documentStorage.Object,
            _logger, CancellationToken.None);

        // Assert
        var problem = result.Should().BeAssignableTo<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task Handle_ReadDocumentThrows_Returns500()
    {
        // Arrange
        _documentStorage
            .Setup(d => d.ListDocumentsAsync(LeadsRoot, It.IsAny<CancellationToken>()))
            .ReturnsAsync([BlobPath]);

        _documentStorage
            .Setup(d => d.ReadDocumentAsync(LeadsRoot, BlobPath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Disk read error"));

        // Act
        var result = await DownloadCmaEndpoint.Handle(
            AccountId, AgentId, LeadId,
            _documentStorage.Object,
            _logger, CancellationToken.None);

        // Assert
        var problem = result.Should().BeAssignableTo<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task Handle_MultipleBlobsMatchingLeadId_ReturnsFirst()
    {
        // Arrange — multiple blobs contain the leadId pattern; endpoint takes the first match
        var blobPath1 = $"Edward Rosado/123 Main St/{LeadId}-CMA-Report.pdf.b64";
        var blobPath2 = $"Edward Rosado/456 Elm St/{LeadId}-CMA-Report.pdf.b64";
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var base64Content = Convert.ToBase64String(pdfBytes);

        _documentStorage
            .Setup(d => d.ListDocumentsAsync(LeadsRoot, It.IsAny<CancellationToken>()))
            .ReturnsAsync([blobPath1, blobPath2]);

        _documentStorage
            .Setup(d => d.ReadDocumentAsync(LeadsRoot, blobPath1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(base64Content);

        // Act
        var result = await DownloadCmaEndpoint.Handle(
            AccountId, AgentId, LeadId,
            _documentStorage.Object,
            _logger, CancellationToken.None);

        // Assert — only the first matched blob was read
        result.Should().BeAssignableTo<FileContentHttpResult>();
        _documentStorage.Verify(
            d => d.ReadDocumentAsync(LeadsRoot, blobPath1, It.IsAny<CancellationToken>()),
            Times.Once);
        _documentStorage.Verify(
            d => d.ReadDocumentAsync(LeadsRoot, blobPath2, It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
