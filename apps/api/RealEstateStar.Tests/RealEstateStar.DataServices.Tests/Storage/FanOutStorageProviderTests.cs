using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.DataServices.Storage;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.DataServices.Tests.Storage;

public class FanOutStorageProviderTests
{
    private const string AccountId = "acct-001";
    private const string AgentId = "jenise-buckalew";
    private const string PlatformEmail = "platform@real-estate-star.com";
    private const string AccountTierAgentId = "__account__";

    private readonly Mock<IGDriveClient> _driveClient = new(MockBehavior.Strict);
    private readonly Mock<IGSheetsClient> _sheetsClient = new(MockBehavior.Strict);
    private readonly Mock<IGwsService> _gwsService = new(MockBehavior.Strict);
    private readonly Mock<IDocumentStorageProvider> _platformStore = new(MockBehavior.Strict);
    private readonly Mock<ILogger> _logger = new();
    private readonly FanOutStorageProvider _sut;

    public FanOutStorageProviderTests()
    {
        _sut = new FanOutStorageProvider(
            _driveClient.Object,
            _sheetsClient.Object,
            _gwsService.Object,
            _platformStore.Object,
            AccountId,
            AgentId,
            PlatformEmail,
            _logger.Object);
    }

    // ── WriteDocumentAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task WriteDocumentAsync_WritesToAllThreeTiers()
    {
        const string folder = "1 - Leads/Jane Doe";
        const string fileName = "Lead Profile.md";
        const string content = "# Lead Profile";

        _driveClient.Setup(d => d.UploadFileAsync(AccountId, AgentId, folder, fileName, content, It.IsAny<CancellationToken>()))
            .ReturnsAsync("file-agent-id");
        _driveClient.Setup(d => d.UploadFileAsync(AccountId, AccountTierAgentId, folder, fileName, content, It.IsAny<CancellationToken>()))
            .ReturnsAsync("file-account-id");
        _platformStore.Setup(p => p.WriteDocumentAsync(folder, fileName, content, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.WriteDocumentAsync(folder, fileName, content, CancellationToken.None);

        _driveClient.Verify(d => d.UploadFileAsync(AccountId, AgentId, folder, fileName, content, It.IsAny<CancellationToken>()), Times.Once);
        _driveClient.Verify(d => d.UploadFileAsync(AccountId, AccountTierAgentId, folder, fileName, content, It.IsAny<CancellationToken>()), Times.Once);
        _platformStore.Verify(p => p.WriteDocumentAsync(folder, fileName, content, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WriteDocumentAsync_ContinuesOnAgentDriveFailure()
    {
        const string folder = "1 - Leads/Jane Doe";
        const string fileName = "Lead Profile.md";
        const string content = "# Lead Profile";

        _driveClient.Setup(d => d.UploadFileAsync(AccountId, AgentId, folder, fileName, content, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Agent Drive unavailable"));
        _driveClient.Setup(d => d.UploadFileAsync(AccountId, AccountTierAgentId, folder, fileName, content, It.IsAny<CancellationToken>()))
            .ReturnsAsync("file-account-id");
        _platformStore.Setup(p => p.WriteDocumentAsync(folder, fileName, content, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Should not throw
        await _sut.WriteDocumentAsync(folder, fileName, content, CancellationToken.None);

        _driveClient.Verify(d => d.UploadFileAsync(AccountId, AccountTierAgentId, folder, fileName, content, It.IsAny<CancellationToken>()), Times.Once);
        _platformStore.Verify(p => p.WriteDocumentAsync(folder, fileName, content, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WriteDocumentAsync_ContinuesOnAccountDriveFailure()
    {
        const string folder = "1 - Leads/Jane Doe";
        const string fileName = "Lead Profile.md";
        const string content = "# Lead Profile";

        _driveClient.Setup(d => d.UploadFileAsync(AccountId, AgentId, folder, fileName, content, It.IsAny<CancellationToken>()))
            .ReturnsAsync("file-agent-id");
        _driveClient.Setup(d => d.UploadFileAsync(AccountId, AccountTierAgentId, folder, fileName, content, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Account Drive unavailable"));
        _platformStore.Setup(p => p.WriteDocumentAsync(folder, fileName, content, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Should not throw
        await _sut.WriteDocumentAsync(folder, fileName, content, CancellationToken.None);

        _driveClient.Verify(d => d.UploadFileAsync(AccountId, AgentId, folder, fileName, content, It.IsAny<CancellationToken>()), Times.Once);
        _platformStore.Verify(p => p.WriteDocumentAsync(folder, fileName, content, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WriteDocumentAsync_ContinuesOnPlatformStoreFailure()
    {
        const string folder = "1 - Leads/Jane Doe";
        const string fileName = "Lead Profile.md";
        const string content = "# Lead Profile";

        _driveClient.Setup(d => d.UploadFileAsync(AccountId, AgentId, folder, fileName, content, It.IsAny<CancellationToken>()))
            .ReturnsAsync("file-agent-id");
        _driveClient.Setup(d => d.UploadFileAsync(AccountId, AccountTierAgentId, folder, fileName, content, It.IsAny<CancellationToken>()))
            .ReturnsAsync("file-account-id");
        _platformStore.Setup(p => p.WriteDocumentAsync(folder, fileName, content, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Blob Storage unavailable"));

        // Should not throw
        await _sut.WriteDocumentAsync(folder, fileName, content, CancellationToken.None);

        _driveClient.Verify(d => d.UploadFileAsync(AccountId, AgentId, folder, fileName, content, It.IsAny<CancellationToken>()), Times.Once);
        _driveClient.Verify(d => d.UploadFileAsync(AccountId, AccountTierAgentId, folder, fileName, content, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── ReadDocumentAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task ReadDocumentAsync_ReturnsAgentTierResult_WhenAgentSucceeds()
    {
        const string folder = "1 - Leads/Jane Doe";
        const string fileName = "Lead Profile.md";
        const string content = "# Lead Profile";

        _driveClient.Setup(d => d.DownloadFileAsync(AccountId, AgentId, folder, fileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        var result = await _sut.ReadDocumentAsync(folder, fileName, CancellationToken.None);

        result.Should().Be(content);
        _driveClient.Verify(d => d.DownloadFileAsync(AccountId, AccountTierAgentId, folder, fileName, It.IsAny<CancellationToken>()), Times.Never);
        _platformStore.Verify(p => p.ReadDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReadDocumentAsync_FallsBackToAccountOnAgentFailure()
    {
        const string folder = "1 - Leads/Jane Doe";
        const string fileName = "Lead Profile.md";
        const string content = "# Lead Profile from Account";

        _driveClient.Setup(d => d.DownloadFileAsync(AccountId, AgentId, folder, fileName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Agent Drive unavailable"));
        _driveClient.Setup(d => d.DownloadFileAsync(AccountId, AccountTierAgentId, folder, fileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        var result = await _sut.ReadDocumentAsync(folder, fileName, CancellationToken.None);

        result.Should().Be(content);
        _platformStore.Verify(p => p.ReadDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReadDocumentAsync_FallsBackToPlatformOnAgentAndAccountFailure()
    {
        const string folder = "1 - Leads/Jane Doe";
        const string fileName = "Lead Profile.md";
        const string content = "# Lead Profile from Platform";

        _driveClient.Setup(d => d.DownloadFileAsync(AccountId, AgentId, folder, fileName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Agent Drive unavailable"));
        _driveClient.Setup(d => d.DownloadFileAsync(AccountId, AccountTierAgentId, folder, fileName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Account Drive unavailable"));
        _platformStore.Setup(p => p.ReadDocumentAsync(folder, fileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        var result = await _sut.ReadDocumentAsync(folder, fileName, CancellationToken.None);

        result.Should().Be(content);
    }

    [Fact]
    public async Task ReadDocumentAsync_ReturnsNull_WhenAllTiersFail()
    {
        const string folder = "1 - Leads/Jane Doe";
        const string fileName = "Lead Profile.md";

        _driveClient.Setup(d => d.DownloadFileAsync(AccountId, AgentId, folder, fileName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Agent Drive unavailable"));
        _driveClient.Setup(d => d.DownloadFileAsync(AccountId, AccountTierAgentId, folder, fileName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Account Drive unavailable"));
        _platformStore.Setup(p => p.ReadDocumentAsync(folder, fileName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Blob Storage unavailable"));

        var result = await _sut.ReadDocumentAsync(folder, fileName, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ReadDocumentAsync_FallsBackToAccount_WhenAgentReturnsNull()
    {
        const string folder = "1 - Leads/Jane Doe";
        const string fileName = "Lead Profile.md";
        const string content = "# Lead Profile from Account";

        _driveClient.Setup(d => d.DownloadFileAsync(AccountId, AgentId, folder, fileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _driveClient.Setup(d => d.DownloadFileAsync(AccountId, AccountTierAgentId, folder, fileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        var result = await _sut.ReadDocumentAsync(folder, fileName, CancellationToken.None);

        result.Should().Be(content);
    }

    // ── AppendRowAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AppendRowAsync_PassesToGwsService()
    {
        const string sheetName = "Consent Log";
        var values = new List<string> { "2026-03-23", "jane@example.com", "GDPR" };

        _gwsService.Setup(g => g.AppendSheetRowAsync(PlatformEmail, sheetName, values, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.AppendRowAsync(sheetName, values, CancellationToken.None);

        _gwsService.Verify(g => g.AppendSheetRowAsync(PlatformEmail, sheetName, values, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── ReadRowsAsync ──────────────────────────────────────────────

    [Fact]
    public async Task ReadRowsAsync_FiltersRowsByColumn()
    {
        const string sheetName = "Consent Log";
        var allRows = new List<List<string>>
        {
            new() { "date", "email", "law" },                                      // header row
            new() { "2026-03-23", "jane@example.com", "GDPR" },
            new() { "2026-03-23", "other@example.com", "CAN-SPAM" },
            new() { "2026-03-24", "jane@example.com", "GDPR" },
        };

        _gwsService.Setup(g => g.ReadSheetAsync(PlatformEmail, sheetName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allRows);

        var result = await _sut.ReadRowsAsync(sheetName, "email", "jane@example.com", CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(row => row.Should().Contain("jane@example.com"));
    }

    [Fact]
    public async Task ReadRowsAsync_ReturnsEmpty_WhenColumnNotFound()
    {
        const string sheetName = "Consent Log";
        var allRows = new List<List<string>>
        {
            new() { "date", "email", "law" },
            new() { "2026-03-23", "jane@example.com", "GDPR" },
        };

        _gwsService.Setup(g => g.ReadSheetAsync(PlatformEmail, sheetName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allRows);

        var result = await _sut.ReadRowsAsync(sheetName, "nonexistent", "any-value", CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadRowsAsync_ReturnsEmpty_WhenSheetIsEmpty()
    {
        const string sheetName = "Consent Log";

        _gwsService.Setup(g => g.ReadSheetAsync(PlatformEmail, sheetName, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.ReadRowsAsync(sheetName, "email", "jane@example.com", CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadRowsAsync_DoesNotIncludeHeaderRow_InResults()
    {
        const string sheetName = "Consent Log";
        var allRows = new List<List<string>>
        {
            new() { "date", "email", "law" },    // header - should never be returned
            new() { "2026-03-23", "date", "GDPR" }, // data row where value happens to equal a header name
        };

        _gwsService.Setup(g => g.ReadSheetAsync(PlatformEmail, sheetName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allRows);

        // Filter on "email" column for value "date" - matches only data row
        var result = await _sut.ReadRowsAsync(sheetName, "email", "date", CancellationToken.None);

        result.Should().HaveCount(1);
        result[0][0].Should().Be("2026-03-23"); // confirms data row, not header
    }

// ── RedactRowsAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task RedactRowsAsync_PassesToGwsService()
    {
        const string sheetName = "Consent Log";

        _gwsService.Setup(g => g.UpdateSheetRowsAsync(PlatformEmail, sheetName, "email", "jane@example.com", "[REDACTED]", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.RedactRowsAsync(sheetName, "email", "jane@example.com", "[REDACTED]", CancellationToken.None);

        _gwsService.Verify(g => g.UpdateSheetRowsAsync(PlatformEmail, sheetName, "email", "jane@example.com", "[REDACTED]", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── EnsureFolderExistsAsync ────────────────────────────────────────────────

    [Fact]
    public async Task EnsureFolderExistsAsync_FansOutToAllThreeTiers()
    {
        const string folder = "1 - Leads/Jane Doe";

        _driveClient.Setup(d => d.CreateFolderAsync(AccountId, AgentId, folder, It.IsAny<CancellationToken>()))
            .ReturnsAsync("folder-agent-id");
        _driveClient.Setup(d => d.CreateFolderAsync(AccountId, AccountTierAgentId, folder, It.IsAny<CancellationToken>()))
            .ReturnsAsync("folder-account-id");
        _platformStore.Setup(p => p.EnsureFolderExistsAsync(folder, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.EnsureFolderExistsAsync(folder, CancellationToken.None);

        _driveClient.Verify(d => d.CreateFolderAsync(AccountId, AgentId, folder, It.IsAny<CancellationToken>()), Times.Once);
        _driveClient.Verify(d => d.CreateFolderAsync(AccountId, AccountTierAgentId, folder, It.IsAny<CancellationToken>()), Times.Once);
        _platformStore.Verify(p => p.EnsureFolderExistsAsync(folder, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureFolderExistsAsync_ContinuesOnTierFailure()
    {
        const string folder = "1 - Leads/Jane Doe";

        _driveClient.Setup(d => d.CreateFolderAsync(AccountId, AgentId, folder, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Agent Drive unavailable"));
        _driveClient.Setup(d => d.CreateFolderAsync(AccountId, AccountTierAgentId, folder, It.IsAny<CancellationToken>()))
            .ReturnsAsync("folder-account-id");
        _platformStore.Setup(p => p.EnsureFolderExistsAsync(folder, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Should not throw
        await _sut.EnsureFolderExistsAsync(folder, CancellationToken.None);

        _driveClient.Verify(d => d.CreateFolderAsync(AccountId, AccountTierAgentId, folder, It.IsAny<CancellationToken>()), Times.Once);
        _platformStore.Verify(p => p.EnsureFolderExistsAsync(folder, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── DeleteDocumentAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteDocumentAsync_InvokesAllThreeTiers()
    {
        const string folder = "1 - Leads/Jane Doe";
        const string fileName = "Lead Profile.md";

        _driveClient.Setup(d => d.DeleteFileByNameAsync(AccountId, AgentId, folder, fileName, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _driveClient.Setup(d => d.DeleteFileByNameAsync(AccountId, AccountTierAgentId, folder, fileName, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _platformStore.Setup(p => p.DeleteDocumentAsync(folder, fileName, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.DeleteDocumentAsync(folder, fileName, CancellationToken.None);

        _driveClient.Verify(d => d.DeleteFileByNameAsync(AccountId, AgentId, folder, fileName, It.IsAny<CancellationToken>()), Times.Once);
        _driveClient.Verify(d => d.DeleteFileByNameAsync(AccountId, AccountTierAgentId, folder, fileName, It.IsAny<CancellationToken>()), Times.Once);
        _platformStore.Verify(p => p.DeleteDocumentAsync(folder, fileName, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteDocumentAsync_DoesNotThrow_WhenPlatformTierFails()
    {
        const string folder = "1 - Leads/Jane Doe";
        const string fileName = "Lead Profile.md";

        _driveClient.Setup(d => d.DeleteFileByNameAsync(AccountId, AgentId, folder, fileName, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _driveClient.Setup(d => d.DeleteFileByNameAsync(AccountId, AccountTierAgentId, folder, fileName, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _platformStore.Setup(p => p.DeleteDocumentAsync(folder, fileName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Blob Storage unavailable"));

        var act = async () => await _sut.DeleteDocumentAsync(folder, fileName, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // ── ListDocumentsAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ListDocumentsAsync_ReturnsFromAgentTier()
    {
        const string folder = "1 - Leads/Jane Doe";
        var files = new List<string> { "Lead Profile.md", "Research & Insights.md" };

        _driveClient.Setup(d => d.ListFilesAsync(AccountId, AgentId, folder, It.IsAny<CancellationToken>()))
            .ReturnsAsync(files);

        var result = await _sut.ListDocumentsAsync(folder, CancellationToken.None);

        result.Should().BeEquivalentTo(files);
        _driveClient.Verify(d => d.ListFilesAsync(AccountId, AccountTierAgentId, folder, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ListDocumentsAsync_FallsBackToAccountOnAgentFailure()
    {
        const string folder = "1 - Leads/Jane Doe";
        var files = new List<string> { "Lead Profile.md" };

        _driveClient.Setup(d => d.ListFilesAsync(AccountId, AgentId, folder, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Agent Drive unavailable"));
        _driveClient.Setup(d => d.ListFilesAsync(AccountId, AccountTierAgentId, folder, It.IsAny<CancellationToken>()))
            .ReturnsAsync(files);

        var result = await _sut.ListDocumentsAsync(folder, CancellationToken.None);

        result.Should().BeEquivalentTo(files);
    }

    [Fact]
    public async Task ListDocumentsAsync_FallsBackToPlatformOnAgentAndAccountFailure()
    {
        const string folder = "1 - Leads/Jane Doe";
        var files = new List<string> { "Lead Profile.md" };

        _driveClient.Setup(d => d.ListFilesAsync(AccountId, AgentId, folder, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Agent Drive unavailable"));
        _driveClient.Setup(d => d.ListFilesAsync(AccountId, AccountTierAgentId, folder, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Account Drive unavailable"));
        _platformStore.Setup(p => p.ListDocumentsAsync(folder, It.IsAny<CancellationToken>()))
            .ReturnsAsync(files);

        var result = await _sut.ListDocumentsAsync(folder, CancellationToken.None);

        result.Should().BeEquivalentTo(files);
    }

    [Fact]
    public async Task ListDocumentsAsync_ReturnsEmpty_WhenAllTiersFail()
    {
        const string folder = "1 - Leads/Jane Doe";

        _driveClient.Setup(d => d.ListFilesAsync(AccountId, AgentId, folder, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Agent Drive unavailable"));
        _driveClient.Setup(d => d.ListFilesAsync(AccountId, AccountTierAgentId, folder, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Account Drive unavailable"));
        _platformStore.Setup(p => p.ListDocumentsAsync(folder, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Blob Storage unavailable"));

        var result = await _sut.ListDocumentsAsync(folder, CancellationToken.None);

        result.Should().BeEmpty();
    }

    // ── UpdateDocumentAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateDocumentAsync_UpdatesAllThreeTiers()
    {
        const string folder = "1 - Leads/Jane Doe";
        const string fileName = "Research & Insights.md";
        const string content = "# Updated Research";

        _driveClient.Setup(d => d.UploadFileAsync(AccountId, AgentId, folder, fileName, content, It.IsAny<CancellationToken>()))
            .ReturnsAsync("file-agent-id");
        _driveClient.Setup(d => d.UploadFileAsync(AccountId, AccountTierAgentId, folder, fileName, content, It.IsAny<CancellationToken>()))
            .ReturnsAsync("file-account-id");
        _platformStore.Setup(p => p.UpdateDocumentAsync(folder, fileName, content, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.UpdateDocumentAsync(folder, fileName, content, CancellationToken.None);

        _driveClient.Verify(d => d.UploadFileAsync(AccountId, AgentId, folder, fileName, content, It.IsAny<CancellationToken>()), Times.Once);
        _driveClient.Verify(d => d.UploadFileAsync(AccountId, AccountTierAgentId, folder, fileName, content, It.IsAny<CancellationToken>()), Times.Once);
        _platformStore.Verify(p => p.UpdateDocumentAsync(folder, fileName, content, It.IsAny<CancellationToken>()), Times.Once);
    }
}
