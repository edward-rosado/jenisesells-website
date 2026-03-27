namespace RealEstateStar.Domain.Leads.Models;

/// <summary>
/// Tracks completed pipeline steps with content-aware keys.
/// Used by the orchestrator to skip already-completed work on retry.
/// </summary>
public record LeadRetryState
{
    /// <summary>
    /// Key: activity name ("cma", "homeSearch", "pdf", "draftLeadEmail", "draftAgentNotification")
    /// Value: SHA256 hash of the input that produced the completed result
    /// </summary>
    public Dictionary<string, string> CompletedActivityKeys { get; init; } = new();

    /// <summary>
    /// Key: activity name
    /// Value: storage path or serialized result reference
    /// </summary>
    public Dictionary<string, string> CompletedResultPaths { get; init; } = new();

    public string? GetHash(string activityName) =>
        CompletedActivityKeys.GetValueOrDefault(activityName);

    public bool IsCompleted(string activityName, string currentInputHash) =>
        CompletedActivityKeys.TryGetValue(activityName, out var storedHash) && storedHash == currentInputHash;
}
