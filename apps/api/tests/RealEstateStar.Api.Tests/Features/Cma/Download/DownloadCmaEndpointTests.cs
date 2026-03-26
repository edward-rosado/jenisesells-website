using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Api.Features.Cma.Download;
using RealEstateStar.Domain.Leads.Interfaces;
using NotFoundResult = Microsoft.AspNetCore.Http.HttpResults.NotFound<RealEstateStar.Api.Features.Cma.Download.CmaErrorResponse>;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Api.Tests.Features.Cma.Download;

public class DownloadCmaEndpointTests
{
    private readonly Mock<ILeadStore> _leadStore = new();
    private readonly Mock<IDocumentStorageProvider> _documentStorage = new();
    private readonly NullLogger<DownloadCmaEndpoint> _logger = new();

    private const string AccountId = "real-estate-star";
    private const string AgentId = "jenise-buckalew";
    private static readonly Guid LeadId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    private static Lead BuildLead(bool withSeller = true) => new()
    {
        Id = LeadId,
        AgentId = AgentId,
        LeadType = LeadType.Seller,
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Phone = "555-1234",
        Timeline = "ASAP",
        Status = LeadStatus.Complete,
        SellerDetails = withSeller
            ? new SellerDetails
            {
                Address = "123 Main St",
                City = "Springfield",
                State = "NJ",
                Zip = "07001"
            }
            : null
    };

    private static string ExpectedFolder =>
        "Real Estate Star/1 - Leads/Jane Doe/123 Main St, Springfield, NJ 07001";

    [Fact]
    public async Task Handle_LeadExists_WithPdf_ReturnsPdf()
    {
        // Arrange
        var lead = BuildLead();
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF magic bytes
        var base64Content = Convert.ToBase64String(pdfBytes);
        var pdfFileName = "2026-03-26-CMA-Report.pdf.b64";

        _leadStore
            .Setup(s => s.GetAsync(AgentId, LeadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        _documentStorage
            .Setup(d => d.ListDocumentsAsync(ExpectedFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync([pdfFileName]);

        _documentStorage
            .Setup(d => d.ReadDocumentAsync(ExpectedFolder, pdfFileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(base64Content);

        // Act
        var result = await DownloadCmaEndpoint.Handle(
            AccountId, AgentId, LeadId,
            _leadStore.Object, _documentStorage.Object,
            _logger, CancellationToken.None);

        // Assert
        var fileResult = result.Should().BeAssignableTo<FileContentHttpResult>().Subject;
        fileResult.ContentType.Should().Be("application/pdf");
        fileResult.FileContents.ToArray().Should().Equal(pdfBytes);
    }

    [Fact]
    public async Task Handle_LeadNotFound_Returns404()
    {
        // Arrange
        _leadStore
            .Setup(s => s.GetAsync(AgentId, LeadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);

        // Act
        var result = await DownloadCmaEndpoint.Handle(
            AccountId, AgentId, LeadId,
            _leadStore.Object, _documentStorage.Object,
            _logger, CancellationToken.None);

        // Assert
        result.Should().BeAssignableTo<NotFoundResult>();
        _documentStorage.Verify(d => d.ListDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_LeadHasNoSellerDetails_Returns404()
    {
        // Arrange
        var lead = BuildLead(withSeller: false);
        _leadStore
            .Setup(s => s.GetAsync(AgentId, LeadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        // Act
        var result = await DownloadCmaEndpoint.Handle(
            AccountId, AgentId, LeadId,
            _leadStore.Object, _documentStorage.Object,
            _logger, CancellationToken.None);

        // Assert
        result.Should().BeAssignableTo<NotFoundResult>();
        _documentStorage.Verify(d => d.ListDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoPdfBlob_Returns404()
    {
        // Arrange
        var lead = BuildLead();
        _leadStore
            .Setup(s => s.GetAsync(AgentId, LeadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        _documentStorage
            .Setup(d => d.ListDocumentsAsync(ExpectedFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await DownloadCmaEndpoint.Handle(
            AccountId, AgentId, LeadId,
            _leadStore.Object, _documentStorage.Object,
            _logger, CancellationToken.None);

        // Assert
        result.Should().BeAssignableTo<NotFoundResult>();
    }

    [Fact]
    public async Task Handle_EmptyBlobContent_Returns404()
    {
        // Arrange
        var lead = BuildLead();
        var pdfFileName = "2026-03-26-CMA-Report.pdf.b64";

        _leadStore
            .Setup(s => s.GetAsync(AgentId, LeadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        _documentStorage
            .Setup(d => d.ListDocumentsAsync(ExpectedFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync([pdfFileName]);

        _documentStorage
            .Setup(d => d.ReadDocumentAsync(ExpectedFolder, pdfFileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await DownloadCmaEndpoint.Handle(
            AccountId, AgentId, LeadId,
            _leadStore.Object, _documentStorage.Object,
            _logger, CancellationToken.None);

        // Assert
        result.Should().BeAssignableTo<NotFoundResult>();
    }

    [Fact]
    public async Task Handle_CorruptBase64_Returns500()
    {
        // Arrange
        var lead = BuildLead();
        var pdfFileName = "2026-03-26-CMA-Report.pdf.b64";

        _leadStore
            .Setup(s => s.GetAsync(AgentId, LeadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        _documentStorage
            .Setup(d => d.ListDocumentsAsync(ExpectedFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync([pdfFileName]);

        _documentStorage
            .Setup(d => d.ReadDocumentAsync(ExpectedFolder, pdfFileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync("!!!not-valid-base64!!!");

        // Act
        var result = await DownloadCmaEndpoint.Handle(
            AccountId, AgentId, LeadId,
            _leadStore.Object, _documentStorage.Object,
            _logger, CancellationToken.None);

        // Assert
        var problem = result.Should().BeAssignableTo<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task Handle_ListDocumentsThrows_Returns500()
    {
        // Arrange
        var lead = BuildLead();

        _leadStore
            .Setup(s => s.GetAsync(AgentId, LeadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        _documentStorage
            .Setup(d => d.ListDocumentsAsync(ExpectedFolder, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Storage unavailable"));

        // Act
        var result = await DownloadCmaEndpoint.Handle(
            AccountId, AgentId, LeadId,
            _leadStore.Object, _documentStorage.Object,
            _logger, CancellationToken.None);

        // Assert
        var problem = result.Should().BeAssignableTo<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task Handle_ReadDocumentThrows_Returns500()
    {
        // Arrange
        var lead = BuildLead();
        var pdfFileName = "2026-03-26-CMA-Report.pdf.b64";

        _leadStore
            .Setup(s => s.GetAsync(AgentId, LeadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        _documentStorage
            .Setup(d => d.ListDocumentsAsync(ExpectedFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync([pdfFileName]);

        _documentStorage
            .Setup(d => d.ReadDocumentAsync(ExpectedFolder, pdfFileName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Disk read error"));

        // Act
        var result = await DownloadCmaEndpoint.Handle(
            AccountId, AgentId, LeadId,
            _leadStore.Object, _documentStorage.Object,
            _logger, CancellationToken.None);

        // Assert
        var problem = result.Should().BeAssignableTo<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task Handle_MultiplePdfBlobs_ReturnsLatest()
    {
        // Arrange — ensure the most recent file (by sorted name desc) is returned
        var lead = BuildLead();
        var oldPdfFileName = "2026-01-01-CMA-Report.pdf.b64";
        var newPdfFileName = "2026-03-26-CMA-Report.pdf.b64";
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var base64Content = Convert.ToBase64String(pdfBytes);

        _leadStore
            .Setup(s => s.GetAsync(AgentId, LeadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        _documentStorage
            .Setup(d => d.ListDocumentsAsync(ExpectedFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync([oldPdfFileName, newPdfFileName]);

        _documentStorage
            .Setup(d => d.ReadDocumentAsync(ExpectedFolder, newPdfFileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(base64Content);

        // Act
        var result = await DownloadCmaEndpoint.Handle(
            AccountId, AgentId, LeadId,
            _leadStore.Object, _documentStorage.Object,
            _logger, CancellationToken.None);

        // Assert — only the latest PDF was read
        result.Should().BeAssignableTo<FileContentHttpResult>();
        _documentStorage.Verify(
            d => d.ReadDocumentAsync(ExpectedFolder, newPdfFileName, It.IsAny<CancellationToken>()),
            Times.Once);
        _documentStorage.Verify(
            d => d.ReadDocumentAsync(ExpectedFolder, oldPdfFileName, It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
