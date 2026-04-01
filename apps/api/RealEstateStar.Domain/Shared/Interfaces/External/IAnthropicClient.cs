using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Domain.Shared.Interfaces.External;

public interface IAnthropicClient
{
    Task<AnthropicResponse> SendAsync(string model, string systemPrompt,
        string userMessage, int maxTokens, string pipeline, CancellationToken ct);

    Task<AnthropicResponse> SendWithImagesAsync(string model, string systemPrompt,
        string userMessage, IReadOnlyList<(byte[] Data, string MimeType)> images,
        int maxTokens, string pipeline, CancellationToken ct);
}
