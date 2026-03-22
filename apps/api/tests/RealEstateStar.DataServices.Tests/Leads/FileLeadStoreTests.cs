
namespace RealEstateStar.DataServices.Tests.Leads;

public class FileLeadStoreTests : IDisposable
{
    private readonly string _basePath;
    private readonly LocalStorageProvider _storage;
    private readonly FileLeadStore _sut;
    private const string AgentId = "jenise-buckalew";

    public FileLeadStoreTests()
    {
        _basePath = Path.Combine(Path.GetTempPath(), $"file-lead-store-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_basePath);
        _storage = new LocalStorageProvider(_basePath);
        _sut = new FileLeadStore(_storage, _basePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_basePath))
            Directory.Delete(_basePath, recursive: true);
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
        LeadType = LeadType.Buyer,
        FirstName = firstName,
        LastName = lastName,
        Email = $"{firstName.ToLower()}@example.com",
        Phone = "5551234567",
        Timeline = "1-3months",
        Status = status,
        ReceivedAt = new DateTime(2026, 3, 19, 14, 0, 0, DateTimeKind.Utc),
    };

    // ── SaveAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_WritesLeadProfileToCorrectPath()
    {
        var lead = MakeLead();
        var expectedPath = Path.Combine(_basePath, LeadPaths.LeadFolder(lead.FullName), "Lead Profile.md");

        await _sut.SaveAsync(lead, CancellationToken.None);

        Assert.True(File.Exists(expectedPath), $"Expected file at {expectedPath}");
    }

    [Fact]
    public async Task SaveAsync_FileContainsLeadIdInFrontmatter()
    {
        var lead = MakeLead();
        var expectedPath = Path.Combine(_basePath, LeadPaths.LeadFolder(lead.FullName), "Lead Profile.md");

        await _sut.SaveAsync(lead, CancellationToken.None);

        var content = await File.ReadAllTextAsync(expectedPath);
        Assert.Contains($"leadId: {lead.Id}", content);
    }

    // ── UpdateEnrichmentAsync ────────────────────────────────────────────────

    [Fact]
    public async Task UpdateEnrichmentAsync_WritesResearchInsightsFile()
    {
        var lead = MakeLead();
        await _sut.SaveAsync(lead, CancellationToken.None);

        var enrichment = LeadEnrichment.Empty() with { MotivationCategory = "relocating" };
        var score = LeadScore.Default("no reason");
        var enrichmentPath = Path.Combine(_basePath, LeadPaths.LeadFolder(lead.FullName), "Research & Insights.md");

        await _sut.UpdateEnrichmentAsync(AgentId, lead.Id, enrichment, score, CancellationToken.None);

        Assert.True(File.Exists(enrichmentPath));
        var content = await File.ReadAllTextAsync(enrichmentPath);
        Assert.Contains("motivationCategory: relocating", content);
    }

    // ── UpdateHomeSearchIdAsync ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateHomeSearchIdAsync_UpdatesHomeSearchIdInProfileFile()
    {
        var lead = MakeLead();
        await _sut.SaveAsync(lead, CancellationToken.None);

        var homeSearchId = "search-456";
        await _sut.UpdateHomeSearchIdAsync(AgentId, lead.Id, homeSearchId, CancellationToken.None);

        var profilePath = Path.Combine(_basePath, LeadPaths.LeadFolder(lead.FullName), "Lead Profile.md");
        var content = await File.ReadAllTextAsync(profilePath);
        Assert.Contains($"homeSearchId: {homeSearchId}", content);
    }

    // ── UpdateStatusAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatusAsync_UpdatesStatusFieldInProfileFile()
    {
        var lead = MakeLead();
        await _sut.SaveAsync(lead, CancellationToken.None);

        await _sut.UpdateStatusAsync(AgentId, lead.Id, LeadStatus.Enriched, CancellationToken.None);

        var profilePath = Path.Combine(_basePath, LeadPaths.LeadFolder(lead.FullName), "Lead Profile.md");
        var content = await File.ReadAllTextAsync(profilePath);
        Assert.Contains("status: Enriched", content);
    }

    // ── GetAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_RoundTripsLeadFromFile()
    {
        var lead = MakeLead();
        await _sut.SaveAsync(lead, CancellationToken.None);

        var result = await _sut.GetAsync(AgentId, lead.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(lead.Id, result.Id);
        Assert.Equal("Jane", result.FirstName);
        Assert.Equal("Doe", result.LastName);
        Assert.Equal("jane@example.com", result.Email);
        Assert.Equal(LeadStatus.Received, result.Status);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenLeadDoesNotExist()
    {
        var result = await _sut.GetAsync(AgentId, Guid.NewGuid(), CancellationToken.None);
        Assert.Null(result);
    }

    // ── GetByNameAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByNameAsync_RoundTripsLeadFromFile()
    {
        var lead = MakeLead();
        await _sut.SaveAsync(lead, CancellationToken.None);

        var result = await _sut.GetByNameAsync(AgentId, lead.FullName, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(lead.Id, result.Id);
        Assert.Equal("jane@example.com", result.Email);
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsNull_WhenLeadNotFound()
    {
        var result = await _sut.GetByNameAsync(AgentId, "Nonexistent Person", CancellationToken.None);
        Assert.Null(result);
    }

    // ── ListByStatusAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ListByStatusAsync_ReturnsOnlyMatchingStatusLeads()
    {
        var receivedLead = MakeLead(new Guid("aaaaaaaa-0000-0000-0000-000000000001"), LeadStatus.Received);
        var enrichedLead = MakeLead(new Guid("bbbbbbbb-0000-0000-0000-000000000002"), LeadStatus.Enriched, firstName: "John", lastName: "Smith");

        await _sut.SaveAsync(receivedLead, CancellationToken.None);
        await _sut.SaveAsync(enrichedLead, CancellationToken.None);

        var results = await _sut.ListByStatusAsync(AgentId, LeadStatus.Received, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(receivedLead.Id, results[0].Id);
    }

    [Fact]
    public async Task ListByStatusAsync_ReturnsEmpty_WhenNoneMatch()
    {
        var lead = MakeLead(status: LeadStatus.Received);
        await _sut.SaveAsync(lead, CancellationToken.None);

        var results = await _sut.ListByStatusAsync(AgentId, LeadStatus.Complete, CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task ListByStatusAsync_ReturnsEmpty_WhenNoLeadsFolderExists()
    {
        var results = await _sut.ListByStatusAsync(AgentId, LeadStatus.Received, CancellationToken.None);
        Assert.Empty(results);
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesLeadProfileFile()
    {
        var lead = MakeLead();
        await _sut.SaveAsync(lead, CancellationToken.None);
        var profilePath = Path.Combine(_basePath, LeadPaths.LeadFolder(lead.FullName), "Lead Profile.md");
        Assert.True(File.Exists(profilePath));

        await _sut.DeleteAsync(AgentId, lead.Id, CancellationToken.None);

        Assert.False(File.Exists(profilePath));
    }

    [Fact]
    public async Task DeleteAsync_DoesNotThrow_WhenLeadNotFound()
    {
        var ex = await Record.ExceptionAsync(
            () => _sut.DeleteAsync(AgentId, Guid.NewGuid(), CancellationToken.None));
        Assert.Null(ex);
    }

    // ── Error paths ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatusAsync_ThrowsInvalidOperation_WhenLeadNotFound()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateStatusAsync(AgentId, Guid.NewGuid(), LeadStatus.Enriched, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateHomeSearchIdAsync_ThrowsInvalidOperation_WhenLeadNotFound()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateHomeSearchIdAsync(AgentId, Guid.NewGuid(), "search-id", CancellationToken.None));
    }

    [Fact]
    public async Task UpdateEnrichmentAsync_ThrowsInvalidOperation_WhenLeadNotFound()
    {
        var enrichment = LeadEnrichment.Empty();
        var score = LeadScore.Default("no reason");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateEnrichmentAsync(AgentId, Guid.NewGuid(), enrichment, score, CancellationToken.None));
    }

    // ── GetByEmailAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetByEmailAsync_ReturnsLead_WhenEmailMatches()
    {
        var lead = MakeLead();
        await _sut.SaveAsync(lead, CancellationToken.None);

        var result = await _sut.GetByEmailAsync(AgentId, lead.Email, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(lead.Id, result.Id);
        Assert.Equal(lead.Email, result.Email);
    }

    [Fact]
    public async Task GetByEmailAsync_ReturnsNull_WhenEmailDoesNotMatch()
    {
        var lead = MakeLead();
        await _sut.SaveAsync(lead, CancellationToken.None);

        var result = await _sut.GetByEmailAsync(AgentId, "nobody@example.com", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByEmailAsync_IsCaseInsensitive()
    {
        var lead = MakeLead();
        await _sut.SaveAsync(lead, CancellationToken.None);

        var result = await _sut.GetByEmailAsync(AgentId, lead.Email.ToUpperInvariant(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(lead.Id, result.Id);
    }

    [Fact]
    public async Task GetByEmailAsync_ReturnsNull_WhenNoLeadsFolderExists()
    {
        var result = await _sut.GetByEmailAsync(AgentId, "any@example.com", CancellationToken.None);

        Assert.Null(result);
    }

    // ── ParseLead roundtrip — optional fields ─────────────────────────────────

    [Fact]
    public async Task UpdateMarketingOptInAsync_WritesAndRoundTrips()
    {
        var lead = MakeLead();
        await _sut.SaveAsync(lead, CancellationToken.None);
        await _sut.UpdateMarketingOptInAsync(AgentId, lead.Id, true, CancellationToken.None);

        // Verify the YAML key is snake_case (consistent with LeadFileStore and ParseLead)
        var profilePath = Path.Combine(_basePath, LeadPaths.LeadFolder(lead.FullName), "Lead Profile.md");
        var content = await File.ReadAllTextAsync(profilePath);
        Assert.Contains("marketing_opted_in: true", content);

        // Round-trip: ParseLead reads marketing_opted_in and returns the value
        var result = await _sut.GetAsync(AgentId, lead.Id, CancellationToken.None);
        Assert.NotNull(result);
        Assert.True(result.MarketingOptedIn);
    }

    [Fact]
    public async Task RoundTrip_PreservesHomeSearchId_WhenSet()
    {
        var lead = MakeLead();
        await _sut.SaveAsync(lead, CancellationToken.None);
        var homeSearchId = Guid.NewGuid().ToString();
        await _sut.UpdateHomeSearchIdAsync(AgentId, lead.Id, homeSearchId, CancellationToken.None);

        var result = await _sut.GetAsync(AgentId, lead.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(Guid.Parse(homeSearchId), result.HomeSearchId);
    }

    [Fact]
    public async Task GetAsync_ReturnsLeadWithNullOptionalFields_WhenNotSet()
    {
        // Save a minimal lead (no cmaJobId, homeSearchId, marketingOptedIn)
        var lead = MakeLead();
        await _sut.SaveAsync(lead, CancellationToken.None);

        var result = await _sut.GetAsync(AgentId, lead.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result.HomeSearchId);
        Assert.Null(result.MarketingOptedIn);
    }

    [Fact]
    public async Task GetAsync_ReturnsNullOptionalIds_WhenFrontmatterContainsInvalidGuidValues()
    {
        // Write a profile file with non-empty, non-GUID values for cmaJobId / homeSearchId.
        // This exercises the false branch of `Guid.TryParse(...)` in ParseLead.
        var lead = MakeLead();
        var folder = LeadPaths.LeadFolder(lead.FullName);
        var folderPath = Path.Combine(_basePath, folder);
        Directory.CreateDirectory(folderPath);

        var content = $"""
            ---
            leadId: {lead.Id}
            status: Received
            firstName: Jane
            lastName: Doe
            email: jane@example.com
            phone: 5551234567
            timeline: 1-3months
            leadTypes: [buying]
            receivedAt: 2026-03-19T14:00:00Z
            homeSearchId: also-not-a-guid
            ---
            """;
        await File.WriteAllTextAsync(Path.Combine(folderPath, "Lead Profile.md"), content);

        var result = await _sut.GetAsync(AgentId, lead.Id, CancellationToken.None);

        Assert.NotNull(result);
        // Non-parseable GUID values should be returned as null
        Assert.Null(result.HomeSearchId);
    }

    [Fact]
    public async Task GetByEmailAsync_ReturnsNull_WhenLeadFileHasNoEmail()
    {
        // Write a profile file without an email field to exercise the null branch
        // in GetByEmailAsync's email comparison (fm does not contain "email" key).
        var lead = MakeLead();
        var folder = LeadPaths.LeadFolder(lead.FullName);
        var folderPath = Path.Combine(_basePath, folder);
        Directory.CreateDirectory(folderPath);

        var content = $"""
            ---
            leadId: {lead.Id}
            status: Received
            firstName: Jane
            lastName: Doe
            phone: 5551234567
            timeline: 1-3months
            leadTypes: [buying]
            ---
            """;
        await File.WriteAllTextAsync(Path.Combine(folderPath, "Lead Profile.md"), content);

        var result = await _sut.GetByEmailAsync(AgentId, "jane@example.com", CancellationToken.None);

        // No email field in frontmatter — should not match
        Assert.Null(result);
    }
}
