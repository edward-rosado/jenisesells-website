using Moq;
using RealEstateStar.Api.Features.Leads;
using RealEstateStar.Api.Features.Leads.Services;
using RealEstateStar.Api.Services.Storage;

namespace RealEstateStar.Api.Tests.Features.Leads.Services;

public class GDriveLeadStoreTests
{
    private readonly Mock<IFileStorageProvider> _storage = new(MockBehavior.Strict);
    private readonly GDriveLeadStore _sut;
    private const string AgentId = "jenise-buckalew";

    public GDriveLeadStoreTests()
    {
        _sut = new GDriveLeadStore(_storage.Object);
    }

    // ── Test data ─────────────────────────────────────────────────────────────

    private static Lead MakeLead(
        Guid? id = null,
        LeadStatus status = LeadStatus.Received,
        string firstName = "Jane",
        string lastName = "Doe") => new()
    {
        Id = id ?? new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
        AgentId = AgentId,
        LeadTypes = ["buying"],
        FirstName = firstName,
        LastName = lastName,
        Email = $"{firstName.ToLower()}@example.com",
        Phone = "5551234567",
        Timeline = "1-3months",
        Status = status,
        ReceivedAt = new DateTime(2026, 3, 19, 14, 0, 0, DateTimeKind.Utc),
    };

    private static string MakeLeadProfileMarkdown(Lead lead) =>
        LeadMarkdownRenderer.RenderLeadProfile(lead);

    // ── SaveAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_EnsuresFolderExistsBeforeWriting()
    {
        var lead = MakeLead();
        var folder = LeadPaths.LeadFolder(lead.FullName);
        var expectedContent = MakeLeadProfileMarkdown(lead);

        var callOrder = new List<string>();
        _storage.Setup(s => s.EnsureFolderExistsAsync(folder, It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((_, _) => callOrder.Add("ensure"))
            .Returns(Task.CompletedTask);
        _storage.Setup(s => s.WriteDocumentAsync(folder, "Lead Profile.md", expectedContent, It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, _, _) => callOrder.Add("write"))
            .Returns(Task.CompletedTask);

        await _sut.SaveAsync(lead, CancellationToken.None);

        Assert.Equal(["ensure", "write"], callOrder);
    }

    [Fact]
    public async Task SaveAsync_WritesRenderedLeadProfile()
    {
        var lead = MakeLead();
        var folder = LeadPaths.LeadFolder(lead.FullName);
        string? capturedContent = null;

        _storage.Setup(s => s.EnsureFolderExistsAsync(folder, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _storage.Setup(s => s.WriteDocumentAsync(folder, "Lead Profile.md", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, content, _) => capturedContent = content)
            .Returns(Task.CompletedTask);

        await _sut.SaveAsync(lead, CancellationToken.None);

        Assert.NotNull(capturedContent);
        Assert.Contains($"leadId: {lead.Id}", capturedContent);
        Assert.Contains("firstName: Jane", capturedContent);
        Assert.Contains("lastName: Doe", capturedContent);
    }

    // ── UpdateEnrichmentAsync ────────────────────────────────────────────────

    [Fact]
    public async Task UpdateEnrichmentAsync_WritesResearchInsightsFile()
    {
        var lead = MakeLead();
        var folder = LeadPaths.LeadFolder(lead.FullName);
        var profileDoc = MakeLeadProfileMarkdown(lead);
        var enrichment = LeadEnrichment.Empty() with { MotivationCategory = "relocating" };
        var score = LeadScore.Default("test");
        string? capturedFile = null;
        string? capturedContent = null;

        _storage.Setup(s => s.ListDocumentsAsync(LeadPaths.LeadsFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync([lead.FullName]);
        _storage.Setup(s => s.ReadDocumentAsync(folder, "Lead Profile.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profileDoc);
        _storage.Setup(s => s.WriteDocumentAsync(folder, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, file, content, _) => { capturedFile = file; capturedContent = content; })
            .Returns(Task.CompletedTask);

        await _sut.UpdateEnrichmentAsync(AgentId, lead.Id, enrichment, score, CancellationToken.None);

        Assert.Equal("Research & Insights.md", capturedFile);
        Assert.NotNull(capturedContent);
        Assert.Contains("motivationCategory: relocating", capturedContent);
    }

    // ── UpdateCmaJobIdAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task UpdateCmaJobIdAsync_UpdatesFrontmatterFieldInProfile()
    {
        var lead = MakeLead();
        var folder = LeadPaths.LeadFolder(lead.FullName);
        var profileDoc = MakeLeadProfileMarkdown(lead);
        var cmaJobId = "job-xyz";
        string? capturedContent = null;

        _storage.Setup(s => s.ListDocumentsAsync(LeadPaths.LeadsFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync([lead.FullName]);
        _storage.Setup(s => s.ReadDocumentAsync(folder, "Lead Profile.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profileDoc);
        _storage.Setup(s => s.UpdateDocumentAsync(folder, "Lead Profile.md", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, content, _) => capturedContent = content)
            .Returns(Task.CompletedTask);

        await _sut.UpdateCmaJobIdAsync(AgentId, lead.Id, cmaJobId, CancellationToken.None);

        Assert.NotNull(capturedContent);
        Assert.Contains($"cmaJobId: {cmaJobId}", capturedContent);
    }

    // ── UpdateHomeSearchIdAsync ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateHomeSearchIdAsync_UpdatesFrontmatterFieldInProfile()
    {
        var lead = MakeLead();
        var folder = LeadPaths.LeadFolder(lead.FullName);
        var profileDoc = MakeLeadProfileMarkdown(lead);
        var homeSearchId = "search-abc";
        string? capturedContent = null;

        _storage.Setup(s => s.ListDocumentsAsync(LeadPaths.LeadsFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync([lead.FullName]);
        _storage.Setup(s => s.ReadDocumentAsync(folder, "Lead Profile.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profileDoc);
        _storage.Setup(s => s.UpdateDocumentAsync(folder, "Lead Profile.md", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, content, _) => capturedContent = content)
            .Returns(Task.CompletedTask);

        await _sut.UpdateHomeSearchIdAsync(AgentId, lead.Id, homeSearchId, CancellationToken.None);

        Assert.NotNull(capturedContent);
        Assert.Contains($"homeSearchId: {homeSearchId}", capturedContent);
    }

    // ── UpdateStatusAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatusAsync_UpdatesStatusFieldInProfile()
    {
        var lead = MakeLead();
        var folder = LeadPaths.LeadFolder(lead.FullName);
        var profileDoc = MakeLeadProfileMarkdown(lead);
        string? capturedContent = null;

        _storage.Setup(s => s.ListDocumentsAsync(LeadPaths.LeadsFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync([lead.FullName]);
        _storage.Setup(s => s.ReadDocumentAsync(folder, "Lead Profile.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profileDoc);
        _storage.Setup(s => s.UpdateDocumentAsync(folder, "Lead Profile.md", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, content, _) => capturedContent = content)
            .Returns(Task.CompletedTask);

        await _sut.UpdateStatusAsync(AgentId, lead.Id, LeadStatus.Enriched, CancellationToken.None);

        Assert.NotNull(capturedContent);
        Assert.Contains("status: Enriched", capturedContent);
    }

    // ── GetAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_ParsesFrontmatterToReconstructLead()
    {
        var lead = MakeLead();
        var folder = LeadPaths.LeadFolder(lead.FullName);
        var profileDoc = MakeLeadProfileMarkdown(lead);

        _storage.Setup(s => s.ListDocumentsAsync(LeadPaths.LeadsFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync([lead.FullName]);
        _storage.Setup(s => s.ReadDocumentAsync(folder, "Lead Profile.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profileDoc);

        var result = await _sut.GetAsync(AgentId, lead.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(lead.Id, result.Id);
        Assert.Equal("Jane", result.FirstName);
        Assert.Equal("Doe", result.LastName);
        Assert.Equal("jane@example.com", result.Email);
        Assert.Equal(LeadStatus.Received, result.Status);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenLeadNotFound()
    {
        var otherId = new Guid("bbbbbbbb-0000-0000-0000-000000000002");
        var lead = MakeLead();
        var folder = LeadPaths.LeadFolder(lead.FullName);
        var profileDoc = MakeLeadProfileMarkdown(lead);

        _storage.Setup(s => s.ListDocumentsAsync(LeadPaths.LeadsFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync([lead.FullName]);
        _storage.Setup(s => s.ReadDocumentAsync(folder, "Lead Profile.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profileDoc);

        var result = await _sut.GetAsync(AgentId, otherId, CancellationToken.None);

        Assert.Null(result);
    }

    // ── GetByNameAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByNameAsync_ParsesFrontmatterToReconstructLead()
    {
        var lead = MakeLead();
        var folder = LeadPaths.LeadFolder(lead.FullName);
        var profileDoc = MakeLeadProfileMarkdown(lead);

        _storage.Setup(s => s.ReadDocumentAsync(folder, "Lead Profile.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profileDoc);

        var result = await _sut.GetByNameAsync(AgentId, lead.FullName, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(lead.Id, result.Id);
        Assert.Equal("Jane", result.FirstName);
        Assert.Equal(LeadStatus.Received, result.Status);
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsNull_WhenDocumentMissing()
    {
        var folder = LeadPaths.LeadFolder("Unknown Lead");

        _storage.Setup(s => s.ReadDocumentAsync(folder, "Lead Profile.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _sut.GetByNameAsync(AgentId, "Unknown Lead", CancellationToken.None);

        Assert.Null(result);
    }

    // ── ListByStatusAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ListByStatusAsync_ReturnsOnlyMatchingStatusLeads()
    {
        var receivedLead = MakeLead(new Guid("aaaaaaaa-0000-0000-0000-000000000001"));
        var enrichedLead = MakeLead(new Guid("bbbbbbbb-0000-0000-0000-000000000002"), LeadStatus.Enriched, firstName: "John", lastName: "Smith");

        var receivedFolder = LeadPaths.LeadFolder(receivedLead.FullName);
        var enrichedFolder = LeadPaths.LeadFolder(enrichedLead.FullName);

        _storage.Setup(s => s.ListDocumentsAsync(LeadPaths.LeadsFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync([receivedLead.FullName, enrichedLead.FullName]);
        _storage.Setup(s => s.ReadDocumentAsync(receivedFolder, "Lead Profile.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeLeadProfileMarkdown(receivedLead));
        _storage.Setup(s => s.ReadDocumentAsync(enrichedFolder, "Lead Profile.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeLeadProfileMarkdown(enrichedLead));

        var results = await _sut.ListByStatusAsync(AgentId, LeadStatus.Received, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(receivedLead.Id, results[0].Id);
    }

    [Fact]
    public async Task ListByStatusAsync_ReturnsEmpty_WhenNoLeadsFolder()
    {
        _storage.Setup(s => s.ListDocumentsAsync(LeadPaths.LeadsFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var results = await _sut.ListByStatusAsync(AgentId, LeadStatus.Received, CancellationToken.None);

        Assert.Empty(results);
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_CallsDeleteDocumentAsync()
    {
        var lead = MakeLead();
        var folder = LeadPaths.LeadFolder(lead.FullName);
        var profileDoc = MakeLeadProfileMarkdown(lead);

        _storage.Setup(s => s.ListDocumentsAsync(LeadPaths.LeadsFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync([lead.FullName]);
        _storage.Setup(s => s.ReadDocumentAsync(folder, "Lead Profile.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profileDoc);
        _storage.Setup(s => s.DeleteDocumentAsync(folder, "Lead Profile.md", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.DeleteAsync(AgentId, lead.Id, CancellationToken.None);

        _storage.Verify(s => s.DeleteDocumentAsync(folder, "Lead Profile.md", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_DoesNotThrow_WhenLeadNotFound()
    {
        var missingId = Guid.NewGuid();

        _storage.Setup(s => s.ListDocumentsAsync(LeadPaths.LeadsFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var ex = await Record.ExceptionAsync(() => _sut.DeleteAsync(AgentId, missingId, CancellationToken.None));
        Assert.Null(ex);
    }

    // ── Error paths ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatusAsync_ThrowsInvalidOperation_WhenLeadNotFound()
    {
        var missingId = Guid.NewGuid();

        _storage.Setup(s => s.ListDocumentsAsync(LeadPaths.LeadsFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateStatusAsync(AgentId, missingId, LeadStatus.Enriched, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateCmaJobIdAsync_ThrowsInvalidOperation_WhenLeadNotFound()
    {
        var missingId = Guid.NewGuid();

        _storage.Setup(s => s.ListDocumentsAsync(LeadPaths.LeadsFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateCmaJobIdAsync(AgentId, missingId, "cma-id", CancellationToken.None));
    }
}
