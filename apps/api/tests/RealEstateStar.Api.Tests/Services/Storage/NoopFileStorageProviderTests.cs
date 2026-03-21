using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Api.Services.Storage;

namespace RealEstateStar.Api.Tests.Services.Storage;

public class NoopFileStorageProviderTests
{
    private readonly NoopFileStorageProvider _sut =
        new(NullLogger<NoopFileStorageProvider>.Instance);

    [Fact]
    public async Task WriteDocumentAsync_DoesNotThrow()
    {
        var act = async () =>
            await _sut.WriteDocumentAsync("folder/path", "file.md", "# Content", CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReadDocumentAsync_ReturnsNull()
    {
        var result = await _sut.ReadDocumentAsync("folder/path", "file.md", CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateDocumentAsync_DoesNotThrow()
    {
        var act = async () =>
            await _sut.UpdateDocumentAsync("folder/path", "file.md", "updated", CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteDocumentAsync_DoesNotThrow()
    {
        var act = async () =>
            await _sut.DeleteDocumentAsync("folder/path", "file.md", CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ListDocumentsAsync_ReturnsEmptyList()
    {
        var result = await _sut.ListDocumentsAsync("folder/path", CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AppendRowAsync_DoesNotThrow()
    {
        var act = async () =>
            await _sut.AppendRowAsync("Sheet1", ["col1", "col2"], CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReadRowsAsync_ReturnsEmptyList()
    {
        var result = await _sut.ReadRowsAsync("Sheet1", "Name", "Jane", CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RedactRowsAsync_DoesNotThrow()
    {
        var act = async () =>
            await _sut.RedactRowsAsync("Sheet1", "Name", "Jane", "[REDACTED]", CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureFolderExistsAsync_DoesNotThrow()
    {
        var act = async () =>
            await _sut.EnsureFolderExistsAsync("folder/path", CancellationToken.None);
        await act.Should().NotThrowAsync();
    }
}
