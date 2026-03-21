using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.DataServices.Leads;

namespace RealEstateStar.Api.Tests.Services.Storage;

public abstract class FileStorageProviderContractTests
{
    protected abstract IFileStorageProvider CreateProvider();

    [Fact]
    public async Task WriteDocument_ThenReadDocument_RoundTrips()
    {
        var provider = CreateProvider();
        await provider.EnsureFolderExistsAsync("test-folder", CancellationToken.None);
        await provider.WriteDocumentAsync("test-folder", "doc.md", "# Hello", CancellationToken.None);
        var content = await provider.ReadDocumentAsync("test-folder", "doc.md", CancellationToken.None);
        Assert.Equal("# Hello", content);
    }

    [Fact]
    public async Task ReadDocument_NonExistent_ReturnsNull()
    {
        var provider = CreateProvider();
        var content = await provider.ReadDocumentAsync("no-folder", "no-file.md", CancellationToken.None);
        Assert.Null(content);
    }

    [Fact]
    public async Task UpdateDocument_OverwritesContent()
    {
        var provider = CreateProvider();
        await provider.EnsureFolderExistsAsync("test-folder", CancellationToken.None);
        await provider.WriteDocumentAsync("test-folder", "doc.md", "v1", CancellationToken.None);
        await provider.UpdateDocumentAsync("test-folder", "doc.md", "v2", CancellationToken.None);
        var content = await provider.ReadDocumentAsync("test-folder", "doc.md", CancellationToken.None);
        Assert.Equal("v2", content);
    }

    [Fact]
    public async Task DeleteDocument_RemovesFile()
    {
        var provider = CreateProvider();
        await provider.EnsureFolderExistsAsync("test-folder", CancellationToken.None);
        await provider.WriteDocumentAsync("test-folder", "doc.md", "content", CancellationToken.None);
        await provider.DeleteDocumentAsync("test-folder", "doc.md", CancellationToken.None);
        var content = await provider.ReadDocumentAsync("test-folder", "doc.md", CancellationToken.None);
        Assert.Null(content);
    }

    [Fact]
    public async Task ListDocuments_ReturnsFileNames()
    {
        var provider = CreateProvider();
        await provider.EnsureFolderExistsAsync("list-test", CancellationToken.None);
        await provider.WriteDocumentAsync("list-test", "a.md", "a", CancellationToken.None);
        await provider.WriteDocumentAsync("list-test", "b.md", "b", CancellationToken.None);
        var files = await provider.ListDocumentsAsync("list-test", CancellationToken.None);
        Assert.Contains("a.md", files);
        Assert.Contains("b.md", files);
    }

    [Fact]
    public async Task AppendRow_ThenReadRows_RoundTrips()
    {
        var provider = CreateProvider();
        await provider.AppendRowAsync("test-sheet", ["col1", "col2", "col3"], CancellationToken.None);
        await provider.AppendRowAsync("test-sheet", ["other", "val2", "val3"], CancellationToken.None);

        var rows = await provider.ReadRowsAsync("test-sheet", "col1", "col1", CancellationToken.None);

        Assert.Single(rows);
        Assert.Equal(["col1", "col2", "col3"], rows[0]);
    }

    [Fact]
    public async Task RedactRows_ReplacesMatchingValues()
    {
        var provider = CreateProvider();
        await provider.AppendRowAsync("redact-test", ["jane@email.com", "data1"], CancellationToken.None);
        await provider.AppendRowAsync("redact-test", ["other@email.com", "data2"], CancellationToken.None);

        // filterColumn is implementation-specific: GDrive uses column name, local uses value search
        await provider.RedactRowsAsync("redact-test", "jane@email.com", "jane@email.com", "[REDACTED]", CancellationToken.None);

        // Redacted row should be findable by the redacted marker
        var redacted = await provider.ReadRowsAsync("redact-test", "[REDACTED]", "[REDACTED]", CancellationToken.None);
        Assert.Single(redacted);

        // Non-matching row should be untouched
        var untouched = await provider.ReadRowsAsync("redact-test", "other@email.com", "other@email.com", CancellationToken.None);
        Assert.Single(untouched);
    }
}
