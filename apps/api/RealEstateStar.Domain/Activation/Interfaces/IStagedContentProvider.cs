namespace RealEstateStar.Domain.Activation.Interfaces;

/// <summary>
/// Provides read/write access to staged Drive file contents during activation.
/// Contents are ephemeral — stored temporarily for synthesis, then cleaned up.
///
/// Implementations may back this with blob storage (production) or in-memory dictionaries (tests).
/// Workers depend on this interface only — they never reference Clients.Azure directly.
/// </summary>
public interface IStagedContentProvider
{
    /// <summary>Stages a single Drive file's content for later retrieval by synthesis workers.</summary>
    Task StageContentAsync(string accountId, string agentId, string driveFileId, string content, CancellationToken ct);

    /// <summary>Returns the text content for the given Drive file ID, or null if not available.</summary>
    Task<string?> GetContentAsync(string accountId, string agentId, string driveFileId, CancellationToken ct);

    /// <summary>Returns all staged file IDs and their content. Use sparingly — prefer GetContentAsync for targeted lookups.</summary>
    Task<IReadOnlyDictionary<string, string>> GetAllContentsAsync(string accountId, string agentId, CancellationToken ct);

    /// <summary>Returns the count of staged files without loading content.</summary>
    Task<int> GetCountAsync(string accountId, string agentId, CancellationToken ct);

    /// <summary>Deletes all staged content for the given agent. Called after Phase 3 persist.</summary>
    Task CleanupAsync(string accountId, string agentId, CancellationToken ct);
}
