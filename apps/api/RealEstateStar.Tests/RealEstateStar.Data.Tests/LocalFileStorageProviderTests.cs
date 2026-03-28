using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace RealEstateStar.Data.Tests;

public class LocalFileStorageProviderTests : IDisposable
{
    private readonly string _testDir;
    private readonly LocalFileStorageProvider _sut;

    public LocalFileStorageProviderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"res-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _sut = new LocalFileStorageProvider(_testDir,
            new Mock<ILogger<LocalFileStorageProvider>>().Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    // ── Document operations ───────────────────────────────────────────────

    [Fact]
    public async Task WriteDocument_CreatesFileOnDisk()
    {
        await _sut.WriteDocumentAsync("leads/jane", "conversation.md", "# Hello", CancellationToken.None);

        var path = Path.Combine(_testDir, "leads", "jane", "conversation.md");
        File.Exists(path).Should().BeTrue();
        (await File.ReadAllTextAsync(path)).Should().Be("# Hello");
    }

    [Fact]
    public async Task ReadDocument_ReturnsContent_WhenFileExists()
    {
        var dir = Path.Combine(_testDir, "leads", "jane");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "note.md"), "content here");

        var result = await _sut.ReadDocumentAsync("leads/jane", "note.md", CancellationToken.None);
        result.Should().Be("content here");
    }

    [Fact]
    public async Task ReadDocument_ReturnsNull_WhenFileMissing()
    {
        var result = await _sut.ReadDocumentAsync("nonexistent", "file.md", CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateDocument_AppendsToExistingContent()
    {
        await _sut.WriteDocumentAsync("leads/jane", "log.md", "Line 1\n", CancellationToken.None);
        await _sut.UpdateDocumentAsync("leads/jane", "log.md", "Line 2\n", CancellationToken.None);

        var result = await _sut.ReadDocumentAsync("leads/jane", "log.md", CancellationToken.None);
        result.Should().Be("Line 1\nLine 2\n");
    }

    [Fact]
    public async Task UpdateDocument_CreatesFile_WhenMissing()
    {
        await _sut.UpdateDocumentAsync("new-folder", "new.md", "fresh content", CancellationToken.None);

        var result = await _sut.ReadDocumentAsync("new-folder", "new.md", CancellationToken.None);
        result.Should().Be("fresh content");
    }

    [Fact]
    public async Task DeleteDocument_RemovesFile()
    {
        await _sut.WriteDocumentAsync("leads/jane", "temp.md", "delete me", CancellationToken.None);
        await _sut.DeleteDocumentAsync("leads/jane", "temp.md", CancellationToken.None);

        var result = await _sut.ReadDocumentAsync("leads/jane", "temp.md", CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteDocument_NoOp_WhenFileMissing()
    {
        // Should not throw
        await _sut.DeleteDocumentAsync("nonexistent", "file.md", CancellationToken.None);
    }

    [Fact]
    public async Task ListDocuments_ReturnsFileNames()
    {
        await _sut.WriteDocumentAsync("leads/jane", "a.md", "a", CancellationToken.None);
        await _sut.WriteDocumentAsync("leads/jane", "b.md", "b", CancellationToken.None);

        var files = await _sut.ListDocumentsAsync("leads/jane", CancellationToken.None);
        files.Should().Contain("a.md").And.Contain("b.md");
    }

    [Fact]
    public async Task ListDocuments_ReturnsEmpty_WhenFolderMissing()
    {
        var files = await _sut.ListDocumentsAsync("nonexistent", CancellationToken.None);
        files.Should().BeEmpty();
    }

    [Fact]
    public async Task EnsureFolderExists_CreatesDirectory()
    {
        await _sut.EnsureFolderExistsAsync("agents/jenise/cma", CancellationToken.None);

        Directory.Exists(Path.Combine(_testDir, "agents", "jenise", "cma")).Should().BeTrue();
    }

    // ── Path traversal protection ─────────────────────────────────────────

    [Fact]
    public async Task WriteDocument_ThrowsOnPathTraversal()
    {
        Func<Task> act = () => _sut.WriteDocumentAsync("../../etc", "passwd", "hacked", CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Path traversal*");
    }

    [Fact]
    public async Task ListDocuments_ThrowsOnPathTraversal()
    {
        Func<Task> act = () => _sut.ListDocumentsAsync("../../etc", CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Path traversal*");
    }

    // ── Spreadsheet operations ────────────────────────────────────────────

    [Fact]
    public async Task AppendRow_CreatesCSVFile()
    {
        await _sut.AppendRowAsync("leads", ["Jane", "555-1234", "jane@test.com"], CancellationToken.None);

        var path = Path.Combine(_testDir, "sheets", "leads.csv");
        File.Exists(path).Should().BeTrue();
        var content = await File.ReadAllTextAsync(path);
        content.Should().Contain("Jane,555-1234,jane@test.com");
    }

    [Fact]
    public async Task AppendRow_EscapesCommasInValues()
    {
        await _sut.AppendRowAsync("leads", ["Smith, Jane", "phone"], CancellationToken.None);

        var path = Path.Combine(_testDir, "sheets", "leads.csv");
        var content = await File.ReadAllTextAsync(path);
        content.Should().Contain("\"Smith, Jane\"");
    }

    [Fact]
    public async Task ReadRows_FiltersCorrectly()
    {
        var path = Path.Combine(_testDir, "sheets", "leads.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path,
            "name,phone,status\nJane,555-1234,new\nBob,555-5678,contacted\nJane,555-9999,new\n");

        var rows = await _sut.ReadRowsAsync("leads", "name", "Jane", CancellationToken.None);
        rows.Should().HaveCount(2);
        rows[0][1].Should().Be("555-1234");
        rows[1][1].Should().Be("555-9999");
    }

    [Fact]
    public async Task ReadRows_ReturnsEmpty_WhenFileMissing()
    {
        var rows = await _sut.ReadRowsAsync("nonexistent", "name", "Jane", CancellationToken.None);
        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadRows_ReturnsEmpty_WhenColumnNotFound()
    {
        var path = Path.Combine(_testDir, "sheets", "leads.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "name,phone\nJane,555-1234\n");

        var rows = await _sut.ReadRowsAsync("leads", "nonexistent_col", "Jane", CancellationToken.None);
        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task RedactRows_ReplacesMatchingValues()
    {
        var path = Path.Combine(_testDir, "sheets", "leads.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path,
            "name,phone,email\nJane,555-1234,jane@test.com\nBob,555-5678,bob@test.com\n");

        await _sut.RedactRowsAsync("leads", "name", "Jane", "[REDACTED]", CancellationToken.None);

        var content = await File.ReadAllTextAsync(path);
        content.Should().Contain("[REDACTED]");
        content.Should().Contain("Jane"); // Filter column preserved
        content.Should().Contain("Bob"); // Other rows untouched
    }

    [Fact]
    public async Task ReadRows_ReturnsEmpty_WhenFileEmpty()
    {
        var path = Path.Combine(_testDir, "sheets", "empty.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "");

        var rows = await _sut.ReadRowsAsync("empty", "name", "Jane", CancellationToken.None);
        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task RedactRows_NoOp_WhenFileMissing()
    {
        // Should not throw
        await _sut.RedactRowsAsync("nonexistent", "name", "Jane", "[REDACTED]", CancellationToken.None);
    }

    [Fact]
    public async Task RedactRows_NoOp_WhenFileEmpty()
    {
        var path = Path.Combine(_testDir, "sheets", "empty.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "");

        await _sut.RedactRowsAsync("empty", "name", "Jane", "[REDACTED]", CancellationToken.None);
    }

    [Fact]
    public async Task RedactRows_NoOp_WhenColumnNotFound()
    {
        var path = Path.Combine(_testDir, "sheets", "leads.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "name,phone\nJane,555-1234\n");

        await _sut.RedactRowsAsync("leads", "nonexistent_col", "Jane", "[REDACTED]", CancellationToken.None);

        // File should be unchanged
        var content = await File.ReadAllTextAsync(path);
        content.Should().Contain("Jane,555-1234");
    }

    [Fact]
    public async Task ReadRows_ParsesQuotedCsvFields_Correctly()
    {
        // Write a CSV that has quoted fields containing commas — ParseCsvLine must handle quotes
        var path = Path.Combine(_testDir, "sheets", "leads.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Manually write quoted CSV: name contains a comma, so it's quoted
        await File.WriteAllTextAsync(path,
            "name,address,status\n\"Smith, Jane\",\"123 Main St\",new\nBob,456 Oak Ave,new\n");

        var rows = await _sut.ReadRowsAsync("leads", "status", "new", CancellationToken.None);

        rows.Should().HaveCount(2);
        // First row has a quoted name with a comma in it
        rows[0][0].Should().Be("Smith, Jane");
        rows[0][1].Should().Be("123 Main St");
    }
}
