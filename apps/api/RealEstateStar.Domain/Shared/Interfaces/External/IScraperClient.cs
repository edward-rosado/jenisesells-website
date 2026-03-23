namespace RealEstateStar.Domain.Shared.Interfaces.External;

public interface IScraperClient
{
    Task<string?> FetchAsync(string targetUrl, string source, string agentId, CancellationToken ct);
    bool IsAvailable { get; }
}
