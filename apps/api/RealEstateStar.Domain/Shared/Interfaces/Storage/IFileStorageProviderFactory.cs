namespace RealEstateStar.Domain.Shared.Interfaces.Storage;

/// <summary>
/// Creates per-agent IFileStorageProvider instances with the correct accountId/agentId
/// context for Drive writes. Used by activation persist activities that need to write
/// to the agent's Google Drive, not the platform-level singleton.
/// </summary>
public interface IFileStorageProviderFactory
{
    IFileStorageProvider CreateForAgent(string accountId, string agentId);
}
