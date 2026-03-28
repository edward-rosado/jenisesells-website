using global::Azure;
using global::Azure.Storage.Blobs;
using global::Azure.Storage.Blobs.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace RealEstateStar.Clients.Azure.Tests;

public sealed class AzureBlobDocumentStoreTests
{
    private const string Folder = "1-Leads/Jane Doe";
    private const string FileName = "Lead Profile.md";
    private const string Content = "# Lead Profile\n\nSome content.";

    private readonly Mock<BlobContainerClient> _container = new();
    private readonly Mock<ILogger<AzureBlobDocumentStore>> _logger = new();

    private AzureBlobDocumentStore CreateSut() =>
        new(_container.Object, _logger.Object);

    private Mock<BlobClient> SetupBlobClient(string blobPath)
    {
        var blobClient = new Mock<BlobClient>();
        _container.Setup(c => c.GetBlobClient(blobPath)).Returns(blobClient.Object);
        return blobClient;
    }

    private void SetupContainerCreate()
    {
        _container.Setup(c => c.CreateIfNotExistsAsync(
                It.IsAny<global::Azure.Storage.Blobs.Models.PublicAccessType>(),
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<global::Azure.Storage.Blobs.Models.BlobContainerEncryptionScopeOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<Response<BlobContainerInfo>>().Object);
    }

    // ── WriteDocumentAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task WriteDocumentAsync_UploadsContentToCorrectBlobPath()
    {
        SetupContainerCreate();
        var blobPath = $"{Folder}/{FileName}";
        var blobClient = SetupBlobClient(blobPath);
        blobClient.Setup(b => b.UploadAsync(
                It.IsAny<Stream>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<Response<BlobContentInfo>>().Object);

        var sut = CreateSut();
        await sut.WriteDocumentAsync(Folder, FileName, Content, CancellationToken.None);

        blobClient.Verify(b => b.UploadAsync(
            It.IsAny<Stream>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── ReadDocumentAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task ReadDocumentAsync_ReturnsContent_WhenBlobExists()
    {
        SetupContainerCreate();
        var blobPath = $"{Folder}/{FileName}";
        var blobClient = SetupBlobClient(blobPath);

        var existsResponse = new Mock<Response<bool>>();
        existsResponse.Setup(r => r.Value).Returns(true);
        blobClient.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existsResponse.Object);

        var binaryData = BinaryData.FromString(Content);
        var downloadResponse = BlobsModelFactory.BlobDownloadResult(binaryData);
        var mockDownload = new Mock<Response<BlobDownloadResult>>();
        mockDownload.Setup(r => r.Value).Returns(downloadResponse);
        blobClient.Setup(b => b.DownloadContentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDownload.Object);

        var sut = CreateSut();
        var result = await sut.ReadDocumentAsync(Folder, FileName, CancellationToken.None);

        result.Should().Be(Content);
    }

    [Fact]
    public async Task ReadDocumentAsync_ReturnsNull_WhenBlobDoesNotExist()
    {
        SetupContainerCreate();
        var blobPath = $"{Folder}/{FileName}";
        var blobClient = SetupBlobClient(blobPath);

        var existsResponse = new Mock<Response<bool>>();
        existsResponse.Setup(r => r.Value).Returns(false);
        blobClient.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existsResponse.Object);

        var sut = CreateSut();
        var result = await sut.ReadDocumentAsync(Folder, FileName, CancellationToken.None);

        result.Should().BeNull();
        blobClient.Verify(b => b.DownloadContentAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── UpdateDocumentAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateDocumentAsync_DelegatesToWriteDocumentAsync()
    {
        SetupContainerCreate();
        var blobPath = $"{Folder}/{FileName}";
        var blobClient = SetupBlobClient(blobPath);
        blobClient.Setup(b => b.UploadAsync(
                It.IsAny<Stream>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<Response<BlobContentInfo>>().Object);

        var sut = CreateSut();
        await sut.UpdateDocumentAsync(Folder, FileName, Content, CancellationToken.None);

        // Update delegates to write (overwrite: true)
        blobClient.Verify(b => b.UploadAsync(
            It.IsAny<Stream>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── DeleteDocumentAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteDocumentAsync_CallsDeleteIfExists()
    {
        SetupContainerCreate();
        var blobPath = $"{Folder}/{FileName}";
        var blobClient = SetupBlobClient(blobPath);
        blobClient.Setup(b => b.DeleteIfExistsAsync(
                It.IsAny<global::Azure.Storage.Blobs.Models.DeleteSnapshotsOption>(),
                It.IsAny<global::Azure.Storage.Blobs.Models.BlobRequestConditions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<Response<bool>>().Object);

        var sut = CreateSut();
        await sut.DeleteDocumentAsync(Folder, FileName, CancellationToken.None);

        blobClient.Verify(b => b.DeleteIfExistsAsync(
            It.IsAny<global::Azure.Storage.Blobs.Models.DeleteSnapshotsOption>(),
            It.IsAny<global::Azure.Storage.Blobs.Models.BlobRequestConditions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteDocumentAsync_IsIdempotent_WhenBlobAbsent()
    {
        SetupContainerCreate();
        var blobPath = $"{Folder}/{FileName}";
        var blobClient = SetupBlobClient(blobPath);
        var falseResponse = new Mock<Response<bool>>();
        falseResponse.Setup(r => r.Value).Returns(false);
        blobClient.Setup(b => b.DeleteIfExistsAsync(
                It.IsAny<global::Azure.Storage.Blobs.Models.DeleteSnapshotsOption>(),
                It.IsAny<global::Azure.Storage.Blobs.Models.BlobRequestConditions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(falseResponse.Object);

        var sut = CreateSut();
        // Should not throw even though blob does not exist
        var act = async () => await sut.DeleteDocumentAsync(Folder, FileName, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // ── ListDocumentsAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ListDocumentsAsync_ReturnsFileNamesWithoutFolderPrefix()
    {
        SetupContainerCreate();

        var items = new[]
        {
            BlobsModelFactory.BlobItem(name: $"{Folder}/Lead Profile.md"),
            BlobsModelFactory.BlobItem(name: $"{Folder}/Research & Insights.md"),
        };

        var page = Page<BlobItem>.FromValues(items, null, new Mock<Response>().Object);
        var pages = AsyncPageable<BlobItem>.FromPages(new[] { page });

        _container.Setup(c => c.GetBlobsAsync(
                It.IsAny<BlobTraits>(),
                It.IsAny<BlobStates>(),
                $"{Folder}/",
                It.IsAny<CancellationToken>()))
            .Returns(pages);

        var sut = CreateSut();
        var result = await sut.ListDocumentsAsync(Folder, CancellationToken.None);

        result.Should().BeEquivalentTo(new[] { "Lead Profile.md", "Research & Insights.md" });
    }

    [Fact]
    public async Task ListDocumentsAsync_ReturnsEmpty_WhenNoBlobs()
    {
        SetupContainerCreate();

        var page = Page<BlobItem>.FromValues(Array.Empty<BlobItem>(), null, new Mock<Response>().Object);
        var pages = AsyncPageable<BlobItem>.FromPages(new[] { page });

        _container.Setup(c => c.GetBlobsAsync(
                It.IsAny<BlobTraits>(),
                It.IsAny<BlobStates>(),
                $"{Folder}/",
                It.IsAny<CancellationToken>()))
            .Returns(pages);

        var sut = CreateSut();
        var result = await sut.ListDocumentsAsync(Folder, CancellationToken.None);

        result.Should().BeEmpty();
    }

    // ── EnsureFolderExistsAsync ────────────────────────────────────────────────

    [Fact]
    public async Task EnsureFolderExistsAsync_IsNoOp()
    {
        var sut = CreateSut();
        // Should complete without calling any blob methods (blob storage has no real folders)
        var act = async () => await sut.EnsureFolderExistsAsync(Folder, CancellationToken.None);

        await act.Should().NotThrowAsync();
        _container.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
    }
}
