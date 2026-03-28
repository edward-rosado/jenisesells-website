using Microsoft.Extensions.Logging;

namespace RealEstateStar.DataServices.Privacy;

public class DriveChangeMonitor(
    IGwsService gws,
    ILeadStore leadStore,
    ILogger<DriveChangeMonitor> logger)
{
    // Maps the leading number of a destination folder name to a LeadStatus value.
    private static readonly Dictionary<string, LeadStatus> FolderNumberToStatus = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1"] = LeadStatus.Received,
        ["2"] = LeadStatus.ActiveClient,
        ["3"] = LeadStatus.UnderContract,
        ["4"] = LeadStatus.Closed,
        ["5"] = LeadStatus.Inactive,
    };

    /// <summary>
    /// Polls Google Drive activity for the given agent since <paramref name="since"/>,
    /// parses folder Move events, and updates lead statuses accordingly.
    /// </summary>
    public virtual async Task<DriveChangeResult> PollAsync(
        string agentId,
        string agentEmail,
        DateTime since,
        CancellationToken ct)
    {
        var processed = 0;
        var statusUpdated = 0;
        var errors = 0;
        var errorDetails = new List<string>();

        string rawJson;
        try
        {
            rawJson = await gws.QueryDriveActivityAsync(agentEmail, LeadPaths.Root, since, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[LEAD-040] Failed to query Drive activity for agent {AgentId}", agentId);
            errorDetails.Add($"[LEAD-040] Drive query failed for agent {agentId}: {ex.Message}");
            return new DriveChangeResult(0, 0, 1, errorDetails);
        }

        var events = DriveActivityParser.Parse(rawJson);

        foreach (var evt in events)
        {
            processed++;

            try
            {
                if (evt.Action == "Delete")
                {
                    logger.LogWarning(
                        "[LEAD-041] File deleted in Drive folder for agent {AgentId}: {FileName} in {FolderPath}",
                        agentId, evt.FileName, evt.FolderPath);
                    continue;
                }

                if (evt.Action != "Move" || evt.DestinationParent is null)
                    continue;

                var status = ResolveStatus(evt.DestinationParent);
                if (status is null)
                    continue;

                // Extract lead name from the folder path (last path segment of source folder).
                var leadName = ExtractLeadName(evt.FolderPath);
                if (string.IsNullOrWhiteSpace(leadName))
                    continue;

                var lead = await leadStore.GetByNameAsync(agentId, leadName, ct);
                if (lead is null)
                {
                    logger.LogWarning(
                        "[LEAD-042] Lead not found for Drive folder move: agent {AgentId}, name {LeadName}",
                        agentId, leadName);
                    continue;
                }

                await leadStore.UpdateStatusAsync(lead, status.Value, ct);
                statusUpdated++;

                logger.LogInformation(
                    "[LEAD-043] Updated lead {LeadId} status to {Status} for agent {AgentId}",
                    lead.Id, status.Value, agentId);
            }
            catch (Exception ex)
            {
                errors++;
                var detail = $"[LEAD-044] Error processing event {evt.Action}/{evt.FileName} for agent {agentId}: {ex.Message}";
                errorDetails.Add(detail);
                logger.LogError(ex, detail);
            }
        }

        return new DriveChangeResult(processed, statusUpdated, errors, errorDetails);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a LeadStatus from a destination folder path like
    /// "Real Estate Star/2 - Active Clients/Jane Doe".
    /// Extracts the status folder segment and maps its leading number.
    /// </summary>
    private static LeadStatus? ResolveStatus(string destinationParent)
    {
        // Split by '/' and find the segment that matches a known status folder pattern (e.g. "2 - Active Clients").
        var segments = destinationParent.Split('/');
        foreach (var segment in segments)
        {
            var trimmed = segment.Trim();
            var dashIndex = trimmed.IndexOf(" - ", StringComparison.Ordinal);
            if (dashIndex <= 0) continue;

            var number = trimmed[..dashIndex].Trim();
            if (FolderNumberToStatus.TryGetValue(number, out var status))
                return status;
        }

        return null;
    }

    /// <summary>
    /// Extracts the lead name from a source folder path.
    /// E.g. "Real Estate Star/1 - Leads/Jane Doe" → "Jane Doe".
    /// </summary>
    private static string ExtractLeadName(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return "";

        var lastSlash = folderPath.LastIndexOf('/');
        return lastSlash >= 0 ? folderPath[(lastSlash + 1)..].Trim() : folderPath.Trim();
    }
}
