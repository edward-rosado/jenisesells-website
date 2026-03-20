using RealEstateStar.Api.Services.Storage;

namespace RealEstateStar.Api.Features.Leads.Services;

/// <summary>
/// ILeadStore implementation backed by the local filesystem via LocalStorageProvider.
/// Used in development and CI environments where GDrive is unavailable.
/// </summary>
public class FileLeadStore(LocalStorageProvider storage, string basePath) : ILeadStore
{
    private const string LeadProfileFile = "Lead Profile.md";
    private const string ResearchInsightsFile = "Research & Insights.md";

    // ── Write operations ───────────────────────────────────────────────────────

    public async Task SaveAsync(Lead lead, CancellationToken ct)
    {
        var folder = LeadPaths.LeadFolder(lead.FullName);
        await storage.EnsureFolderExistsAsync(folder, ct);
        var content = LeadMarkdownRenderer.RenderLeadProfile(lead);
        await storage.WriteDocumentAsync(folder, LeadProfileFile, content, ct);
    }

    public async Task UpdateEnrichmentAsync(string agentId, Guid leadId, LeadEnrichment enrichment, LeadScore score, CancellationToken ct)
    {
        var lead = await GetAsync(agentId, leadId, ct)
            ?? throw new InvalidOperationException($"[FLS-001] Lead {leadId} not found for agent {agentId}.");

        lead.Enrichment = enrichment;
        lead.Score = score;

        var folder = LeadPaths.LeadFolder(lead.FullName);
        var content = LeadMarkdownRenderer.RenderResearchInsights(lead);
        await storage.WriteDocumentAsync(folder, ResearchInsightsFile, content, ct);
    }

    public async Task UpdateCmaJobIdAsync(string agentId, Guid leadId, string cmaJobId, CancellationToken ct)
    {
        var (folder, doc) = await ReadLeadDocAsync(agentId, leadId, ct);
        var updated = YamlFrontmatterParser.UpdateField(doc, "cmaJobId", cmaJobId);
        await storage.UpdateDocumentAsync(folder, LeadProfileFile, updated, ct);
    }

    public async Task UpdateHomeSearchIdAsync(string agentId, Guid leadId, string homeSearchId, CancellationToken ct)
    {
        var (folder, doc) = await ReadLeadDocAsync(agentId, leadId, ct);
        var updated = YamlFrontmatterParser.UpdateField(doc, "homeSearchId", homeSearchId);
        await storage.UpdateDocumentAsync(folder, LeadProfileFile, updated, ct);
    }

    public async Task UpdateStatusAsync(string agentId, Guid leadId, LeadStatus status, CancellationToken ct)
    {
        var (folder, doc) = await ReadLeadDocAsync(agentId, leadId, ct);
        var updated = YamlFrontmatterParser.UpdateField(doc, "status", status.ToString());
        await storage.UpdateDocumentAsync(folder, LeadProfileFile, updated, ct);
    }

    // ── Read operations ────────────────────────────────────────────────────────

    public async Task<Lead?> GetAsync(string agentId, Guid leadId, CancellationToken ct)
    {
        foreach (var name in GetLeadFolderNames())
        {
            var folder = LeadPaths.LeadFolder(name);
            var doc = await storage.ReadDocumentAsync(folder, LeadProfileFile, ct);
            if (doc is null) continue;

            var fm = YamlFrontmatterParser.Parse(doc);
            if (fm.TryGetValue("leadId", out var storedId) &&
                Guid.TryParse(storedId, out var parsedId) &&
                parsedId == leadId)
            {
                return ParseLead(agentId, fm);
            }
        }
        return null;
    }

    public async Task<Lead?> GetByNameAsync(string agentId, string leadName, CancellationToken ct)
    {
        var folder = LeadPaths.LeadFolder(leadName);
        var doc = await storage.ReadDocumentAsync(folder, LeadProfileFile, ct);
        if (doc is null) return null;

        var fm = YamlFrontmatterParser.Parse(doc);
        return ParseLead(agentId, fm);
    }

    public async Task<List<Lead>> ListByStatusAsync(string agentId, LeadStatus status, CancellationToken ct)
    {
        var results = new List<Lead>();

        foreach (var name in GetLeadFolderNames())
        {
            var folder = LeadPaths.LeadFolder(name);
            var doc = await storage.ReadDocumentAsync(folder, LeadProfileFile, ct);
            if (doc is null) continue;

            var fm = YamlFrontmatterParser.Parse(doc);
            if (!fm.TryGetValue("status", out var statusStr)) continue;
            if (!Enum.TryParse<LeadStatus>(statusStr, ignoreCase: true, out var leadStatus)) continue;
            if (leadStatus != status) continue;

            results.Add(ParseLead(agentId, fm));
        }

        return results;
    }

    // ── Delete operations ──────────────────────────────────────────────────────

    public async Task DeleteAsync(string agentId, Guid leadId, CancellationToken ct)
    {
        var lead = await GetAsync(agentId, leadId, ct);
        if (lead is null) return;

        var folder = LeadPaths.LeadFolder(lead.FullName);
        await storage.DeleteDocumentAsync(folder, LeadProfileFile, ct);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Enumerates lead subfolder names directly from the filesystem.
    /// LocalStorageProvider.ListDocumentsAsync only returns files; leads are subdirectories.
    /// </summary>
    private IEnumerable<string> GetLeadFolderNames()
    {
        var leadsPath = Path.Combine(basePath, LeadPaths.LeadsFolder);
        if (!Directory.Exists(leadsPath)) yield break;

        foreach (var dir in Directory.GetDirectories(leadsPath))
            yield return Path.GetFileName(dir);
    }

    private async Task<(string folder, string doc)> ReadLeadDocAsync(string agentId, Guid leadId, CancellationToken ct)
    {
        foreach (var name in GetLeadFolderNames())
        {
            var folder = LeadPaths.LeadFolder(name);
            var doc = await storage.ReadDocumentAsync(folder, LeadProfileFile, ct);
            if (doc is null) continue;

            var fm = YamlFrontmatterParser.Parse(doc);
            if (fm.TryGetValue("leadId", out var storedId) &&
                Guid.TryParse(storedId, out var parsedId) &&
                parsedId == leadId)
            {
                return (folder, doc);
            }
        }

        throw new InvalidOperationException($"[FLS-002] Lead {leadId} not found for agent {agentId}.");
    }

    private static Lead ParseLead(string agentId, Dictionary<string, string> fm)
    {
        fm.TryGetValue("leadId", out var leadIdStr);
        fm.TryGetValue("status", out var statusStr);
        fm.TryGetValue("receivedAt", out var receivedAtStr);
        fm.TryGetValue("cmaJobId", out var cmaJobIdStr);
        fm.TryGetValue("homeSearchId", out var homeSearchIdStr);
        fm.TryGetValue("firstName", out var firstName);
        fm.TryGetValue("lastName", out var lastName);
        fm.TryGetValue("email", out var email);
        fm.TryGetValue("phone", out var phone);
        fm.TryGetValue("timeline", out var timeline);
        fm.TryGetValue("leadTypes", out var leadTypesRaw);

        Guid.TryParse(leadIdStr, out var leadId);
        Enum.TryParse<LeadStatus>(statusStr, ignoreCase: true, out var status);
        DateTime.TryParse(receivedAtStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var receivedAt);

        Guid? cmaJobId = !string.IsNullOrWhiteSpace(cmaJobIdStr) && Guid.TryParse(cmaJobIdStr, out var cj) ? cj : null;
        Guid? homeSearchId = !string.IsNullOrWhiteSpace(homeSearchIdStr) && Guid.TryParse(homeSearchIdStr, out var hs) ? hs : null;

        var leadTypes = ParseYamlList(leadTypesRaw ?? "");

        return new Lead
        {
            Id = leadId,
            AgentId = agentId,
            LeadTypes = leadTypes,
            FirstName = firstName ?? "",
            LastName = lastName ?? "",
            Email = email ?? "",
            Phone = phone ?? "",
            Timeline = timeline ?? "",
            Status = status,
            ReceivedAt = receivedAt,
            CmaJobId = cmaJobId,
            HomeSearchId = homeSearchId,
        };
    }

    private static List<string> ParseYamlList(string raw)
    {
        var trimmed = raw.Trim('[', ']').Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return [];
        return [..trimmed.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0)];
    }
}
