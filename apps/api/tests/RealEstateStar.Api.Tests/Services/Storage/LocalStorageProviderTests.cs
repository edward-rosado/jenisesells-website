using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.DataServices.Leads;

namespace RealEstateStar.Api.Tests.Services.Storage;

public class LocalStorageProviderTests : FileStorageProviderContractTests, IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"res-test-{Guid.NewGuid():N}");

    protected override IFileStorageProvider CreateProvider() => new LocalStorageProvider(_tempDir);

    public void Dispose() { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }

    [Fact]
    public async Task WriteDocument_UsesAtomicTempRename()
    {
        var provider = CreateProvider();
        await provider.EnsureFolderExistsAsync("atomic-test", CancellationToken.None);
        await provider.WriteDocumentAsync("atomic-test", "file.md", "content", CancellationToken.None);

        // File should exist at final path, no temp files lingering
        var folder = Path.Combine(_tempDir, "atomic-test");
        Assert.Single(Directory.GetFiles(folder));
        Assert.Equal("file.md", Path.GetFileName(Directory.GetFiles(folder)[0]));
    }

    [Fact]
    public async Task WriteDocument_PathTraversal_Throws()
    {
        var provider = CreateProvider();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.WriteDocumentAsync("../escape", "file.md", "bad", CancellationToken.None));
    }

    [Fact]
    public async Task WriteDocument_PathTraversal_DotDotInFileName_Throws()
    {
        var provider = CreateProvider();
        await provider.EnsureFolderExistsAsync("safe", CancellationToken.None);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.WriteDocumentAsync("safe", "../../escape.md", "bad", CancellationToken.None));
    }

    [Fact]
    public async Task AppendRow_CreatesCsvFile()
    {
        var provider = CreateProvider();
        await provider.AppendRowAsync("my-sheet", ["a", "b", "c"], CancellationToken.None);

        var csvPath = Path.Combine(_tempDir, "logs", "my-sheet.csv");
        Assert.True(File.Exists(csvPath));
    }

    [Fact]
    public async Task AppendRow_ThenReadRows_HandlesCommasInValues()
    {
        var provider = CreateProvider();
        await provider.AppendRowAsync("csv-comma", ["123 Main St, Suite 100", "data"], CancellationToken.None);

        var rows = await provider.ReadRowsAsync("csv-comma", "123 Main St, Suite 100", "123 Main St, Suite 100", CancellationToken.None);
        Assert.Single(rows);
        Assert.Equal("123 Main St, Suite 100", rows[0][0]);
    }

    [Fact]
    public async Task AppendRow_PathTraversalInSheetName_Throws()
    {
        var provider = CreateProvider();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.AppendRowAsync("../escape", ["data"], CancellationToken.None));
    }
}
