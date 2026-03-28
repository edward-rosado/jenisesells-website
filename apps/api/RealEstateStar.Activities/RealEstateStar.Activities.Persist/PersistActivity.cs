using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Activities.Persist;

/// <summary>
/// Writes all pipeline artifacts to the lead's storage folder in a single batch.
/// Idempotent upsert with content-hash deduplication for communication records.
/// Called as the final step of the lead orchestrator pipeline.
/// </summary>
/// <remarks>
/// Handles the final status write (Complete). Only Scored and Analyzing are written
/// inline by the orchestrator as concurrency gates. All result data persists here.
/// Does NOT write the PDF — <c>PdfActivity</c> handles PDF storage.
/// Writing:
/// <list type="bullet">
///   <item>Lead Profile.md — status (Complete), score, bucket</item>
///   <item>CMA Summary.md — estimated value, comps, market analysis</item>
///   <item>HomeSearch Summary.md — listings, area summary</item>
///   <item>Lead Email Draft.md — subject, body, sent status</item>
///   <item>Agent Notification Draft.md — subject, body, sent status</item>
///   <item>Retry State.json — content hashes per activity</item>
/// </list>
/// </remarks>
public sealed class PersistActivity(
    IDocumentStorageProvider storage,
    ILeadStore leadStore,
    ILogger<PersistActivity> logger)
{
    internal const string CmaSummaryFile = "CMA Summary.md";
    internal const string HomeSearchSummaryFile = "HomeSearch Summary.md";
    internal const string LeadEmailDraftFile = "Lead Email Draft.md";
    internal const string AgentNotificationDraftFile = "Agent Notification Draft.md";
    internal const string RetryStateFile = "Retry State.json";

    // ── Inline persistence (called mid-pipeline as concurrency gates) ──────────

    /// <summary>
    /// Persists lead status as a concurrency gate. Called inline by the orchestrator
    /// after scoring and before dispatch to prevent duplicate orchestrator instances.
    /// </summary>
    public Task PersistStatusAsync(Domain.Leads.Models.Lead lead, LeadStatus status, CancellationToken ct)
    {
        lead.Status = status;
        return leadStore.UpdateStatusAsync(lead, status, ct);
    }

    // ── Batch persistence (called once at end of pipeline) ──────────────────

    /// <summary>
    /// Persists all available pipeline artifacts for the given context.
    /// Skips any artifact whose context field is null (partial pipeline result).
    /// Sets final status to Complete before writing.
    /// </summary>
    public async Task ExecuteAsync(LeadPipelineContext ctx, CancellationToken ct)
    {
        var lead = ctx.Lead;
        var folder = LeadPaths.LeadFolder(lead.FullName);

        logger.LogInformation(
            "[PERSIST-010] Persisting artifacts for lead {LeadId} to {Folder}. CorrelationId: {CorrelationId}",
            lead.Id, folder, ctx.CorrelationId);

        await storage.EnsureFolderExistsAsync(folder, ct);

        // Final status → Complete (persisted via ILeadStore)
        lead.Status = LeadStatus.Complete;
        await leadStore.UpdateStatusAsync(lead, LeadStatus.Complete, ct);

        // Lead Profile — score, bucket, submission count
        await PersistLeadProfileAsync(ctx, folder, ct);

        // CMA Summary — only when CMA succeeded
        if (ctx.CmaResult?.Success == true)
            await PersistCmaSummaryAsync(ctx.CmaResult, folder, ct);

        // HomeSearch Summary — only when HS succeeded
        if (ctx.HsResult?.Success == true)
            await PersistHomeSearchSummaryAsync(ctx.HsResult, folder, ct);

        // Lead Email Draft — dedup by content hash
        if (ctx.LeadEmail is not null)
            await PersistCommunicationAsync(ctx.LeadEmail, folder, LeadEmailDraftFile, ct);

        // Agent Notification Draft — dedup by content hash
        if (ctx.AgentNotification is not null)
            await PersistCommunicationAsync(ctx.AgentNotification, folder, AgentNotificationDraftFile, ct);

        // Retry State — always write so orchestrator can resume on crash
        await PersistRetryStateAsync(ctx.RetryState, folder, ct);

        logger.LogInformation(
            "[PERSIST-090] Artifacts persisted for lead {LeadId}. CorrelationId: {CorrelationId}",
            lead.Id, ctx.CorrelationId);
    }

    private async Task PersistLeadProfileAsync(LeadPipelineContext ctx, string folder, CancellationToken ct)
    {
        var lead = ctx.Lead;
        var score = ctx.Score;

        var lines = new List<string>
        {
            "---",
            $"id: {lead.Id}",
            $"status: {lead.Status}",
            $"submission_count: {lead.SubmissionCount}",
        };

        if (score is not null)
        {
            lines.Add($"score: {score.OverallScore}");
            lines.Add($"score_bucket: {score.Bucket}");
            lines.Add($"score_explanation: \"{EscapeYaml(score.Explanation)}\"");
        }

        lines.Add("---");
        lines.Add("");
        lines.Add($"# {lead.FullName}");

        var content = string.Join("\n", lines);
        await WriteOrUpdateAsync(folder, "Lead Profile.md", content, ct);
    }

    private async Task PersistCmaSummaryAsync(CmaWorkerResult cma, string folder, CancellationToken ct)
    {
        var lines = new List<string>
        {
            "---",
            $"estimated_value: {cma.EstimatedValue}",
            $"price_range_low: {cma.PriceRangeLow}",
            $"price_range_high: {cma.PriceRangeHigh}",
            $"comp_count: {cma.Comps?.Count ?? 0}",
            "---",
            "",
            "## CMA Summary",
            "",
        };

        if (!string.IsNullOrWhiteSpace(cma.MarketAnalysis))
        {
            lines.Add("### Market Analysis");
            lines.Add("");
            lines.Add(cma.MarketAnalysis);
            lines.Add("");
        }

        if (cma.Comps?.Count > 0)
        {
            lines.Add("### Comparable Sales");
            lines.Add("");
            lines.Add("| Address | Price | Beds | Baths | Sqft | Days |");
            lines.Add("|---------|-------|------|-------|------|------|");
            foreach (var comp in cma.Comps)
            {
                lines.Add($"| {comp.Address} | {comp.Price:C0} | {comp.Beds} | {comp.Baths} | {comp.Sqft} | {comp.DaysOnMarket} |");
            }
        }

        var content = string.Join("\n", lines);
        await WriteOrUpdateAsync(folder, CmaSummaryFile, content, ct);
    }

    private async Task PersistHomeSearchSummaryAsync(HomeSearchWorkerResult hs, string folder, CancellationToken ct)
    {
        var lines = new List<string>
        {
            "---",
            $"listing_count: {hs.Listings?.Count ?? 0}",
            "---",
            "",
            "## HomeSearch Summary",
            "",
        };

        if (!string.IsNullOrWhiteSpace(hs.AreaSummary))
        {
            lines.Add("### Area Summary");
            lines.Add("");
            lines.Add(hs.AreaSummary);
            lines.Add("");
        }

        if (hs.Listings?.Count > 0)
        {
            lines.Add("### Available Listings");
            lines.Add("");
            lines.Add("| Address | Price | Beds | Baths | Sqft | Status |");
            lines.Add("|---------|-------|------|-------|------|--------|");
            foreach (var listing in hs.Listings)
            {
                lines.Add($"| {listing.Address} | {listing.Price:C0} | {listing.Beds} | {listing.Baths} | {listing.Sqft} | {listing.Status} |");
            }
        }

        var content = string.Join("\n", lines);
        await WriteOrUpdateAsync(folder, HomeSearchSummaryFile, content, ct);
    }

    /// <summary>
    /// Writes a communication record unless the same content was already sent.
    /// Dedup rule: existing file has same <c>content_hash</c> AND <c>sent: true</c> → skip.
    /// </summary>
    internal async Task PersistCommunicationAsync(
        CommunicationRecord record,
        string folder,
        string fileName,
        CancellationToken ct)
    {
        // Check existing file for content-hash dedup
        var existing = await storage.ReadDocumentAsync(folder, fileName, ct);
        if (existing is not null && ShouldSkipCommunication(existing, record.ContentHash))
        {
            logger.LogInformation(
                "[PERSIST-020] Skipping {File} — same content hash and already sent.",
                fileName);
            return;
        }

        var lines = new List<string>
        {
            "---",
            $"subject: \"{EscapeYaml(record.Subject)}\"",
            $"channel: {record.Channel}",
            $"drafted_at: {record.DraftedAt:O}",
            $"sent: {record.Sent.ToString().ToLowerInvariant()}",
            $"content_hash: {record.ContentHash}",
        };

        if (record.SentAt.HasValue)
            lines.Add($"sent_at: {record.SentAt.Value:O}");
        if (!string.IsNullOrEmpty(record.Error))
            lines.Add($"error: \"{EscapeYaml(record.Error)}\"");

        lines.Add("---");
        lines.Add("");
        lines.Add(record.HtmlBody);

        var content = string.Join("\n", lines);
        await WriteOrUpdateAsync(folder, fileName, content, ct);
    }

    private async Task PersistRetryStateAsync(LeadRetryState retryState, string folder, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(retryState, new JsonSerializerOptions { WriteIndented = true });
        await WriteOrUpdateAsync(folder, RetryStateFile, json, ct);
    }

    private async Task WriteOrUpdateAsync(string folder, string fileName, string content, CancellationToken ct)
    {
        var existing = await storage.ReadDocumentAsync(folder, fileName, ct);
        if (existing is null)
            await storage.WriteDocumentAsync(folder, fileName, content, ct);
        else
            await storage.UpdateDocumentAsync(folder, fileName, content, ct);
    }

    /// <summary>
    /// Returns true when the existing file has the same content hash AND was already sent.
    /// In that case, re-writing would be a no-op and could cause duplicate sends on retry.
    /// </summary>
    internal static bool ShouldSkipCommunication(string existingContent, string newHash)
    {
        var lines = existingContent.Split('\n');
        bool hashMatches = false;
        bool alreadySent = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("content_hash:"))
            {
                var storedHash = trimmed["content_hash:".Length..].Trim();
                hashMatches = storedHash == newHash;
            }
            else if (trimmed == "sent: true")
            {
                alreadySent = true;
            }
        }

        return hashMatches && alreadySent;
    }

    private static string EscapeYaml(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
}
