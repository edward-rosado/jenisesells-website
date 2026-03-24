namespace RealEstateStar.Domain.Shared.Interfaces.External;

public interface IGDocsClient
{
    Task<string> CreateDocumentAsync(string accountId, string agentId, string title, string content, CancellationToken ct);
    Task<string?> ReadDocumentAsync(string accountId, string agentId, string documentId, CancellationToken ct);
    Task UpdateDocumentAsync(string accountId, string agentId, string documentId, string content, CancellationToken ct);
}
