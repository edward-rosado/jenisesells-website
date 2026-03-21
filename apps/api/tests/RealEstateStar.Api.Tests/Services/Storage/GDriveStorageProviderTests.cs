using Moq;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.DataServices.Leads;

namespace RealEstateStar.Api.Tests.Services.Storage;

public class GDriveStorageProviderTests
{
    private readonly Mock<IGwsService> _gws = new();
    private readonly GDriveStorageProvider _sut;

    public GDriveStorageProviderTests()
    {
        _sut = new GDriveStorageProvider(_gws.Object, "agent@email.com");
    }

    [Fact]
    public async Task WriteDocumentAsync_DelegatesToCreateDocAsync()
    {
        _gws.Setup(g => g.CreateDocAsync("agent@email.com", "folder", "file.md", "content", It.IsAny<CancellationToken>()))
            .ReturnsAsync("doc-id");

        await _sut.WriteDocumentAsync("folder", "file.md", "content", CancellationToken.None);

        _gws.Verify(g => g.CreateDocAsync("agent@email.com", "folder", "file.md", "content", It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task ReadDocumentAsync_DelegatesToDownloadDocAsync()
    {
        _gws.Setup(g => g.DownloadDocAsync("agent@email.com", "folder", "file.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("content");

        var result = await _sut.ReadDocumentAsync("folder", "file.md", CancellationToken.None);

        Assert.Equal("content", result);
    }

    [Fact]
    public async Task UpdateDocumentAsync_DelegatesToUpdateDocAsync()
    {
        await _sut.UpdateDocumentAsync("folder", "file.md", "new-content", CancellationToken.None);

        _gws.Verify(g => g.UpdateDocAsync("agent@email.com", "folder", "file.md", "new-content", It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task DeleteDocumentAsync_DelegatesToDeleteDocAsync()
    {
        await _sut.DeleteDocumentAsync("folder", "file.md", CancellationToken.None);

        _gws.Verify(g => g.DeleteDocAsync("agent@email.com", "folder", "file.md", It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task ListDocumentsAsync_DelegatesToListFilesAsync()
    {
        var expected = new List<string> { "a.md", "b.md" };
        _gws.Setup(g => g.ListFilesAsync("agent@email.com", "folder", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.ListDocumentsAsync("folder", CancellationToken.None);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task AppendRowAsync_DelegatesToAppendSheetRowAsync()
    {
        var values = new List<string> { "a", "b" };

        await _sut.AppendRowAsync("sheet", values, CancellationToken.None);

        _gws.Verify(g => g.AppendSheetRowAsync("agent@email.com", "sheet", values, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task ReadRowsAsync_FiltersRowsContainingFilterValue()
    {
        var allRows = new List<List<string>>
        {
            new() { "alice@email.com", "data1" },
            new() { "bob@email.com", "data2" },
            new() { "alice@email.com", "data3" }
        };
        _gws.Setup(g => g.ReadSheetAsync("agent@email.com", "sheet", It.IsAny<CancellationToken>()))
            .ReturnsAsync(allRows);

        var result = await _sut.ReadRowsAsync("sheet", "email", "alice@email.com", CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.All(result, row => Assert.Contains("alice@email.com", row));
    }

    [Fact]
    public async Task ReadRowsAsync_NoMatches_ReturnsEmptyList()
    {
        _gws.Setup(g => g.ReadSheetAsync("agent@email.com", "sheet", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<List<string>>
            {
                new() { "other@email.com", "data" }
            });

        var result = await _sut.ReadRowsAsync("sheet", "email", "nobody@email.com", CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task RedactRowsAsync_DelegatesToUpdateSheetRowsAsync()
    {
        await _sut.RedactRowsAsync("sheet", "email", "jane@email.com", "[REDACTED]", CancellationToken.None);

        _gws.Verify(g => g.UpdateSheetRowsAsync(
            "agent@email.com", "sheet", "email", "jane@email.com", "[REDACTED]",
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task EnsureFolderExistsAsync_DelegatesToCreateDriveFolderAsync()
    {
        _gws.Setup(g => g.CreateDriveFolderAsync("agent@email.com", "my/folder", It.IsAny<CancellationToken>()))
            .ReturnsAsync("folder-id");

        await _sut.EnsureFolderExistsAsync("my/folder", CancellationToken.None);

        _gws.Verify(g => g.CreateDriveFolderAsync("agent@email.com", "my/folder", It.IsAny<CancellationToken>()));
    }
}
