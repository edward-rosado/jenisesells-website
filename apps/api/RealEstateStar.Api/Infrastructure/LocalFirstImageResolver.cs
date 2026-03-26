using RealEstateStar.Domain.Cma.Interfaces;

namespace RealEstateStar.Api.Infrastructure;

/// <summary>
/// Resolves agent images (headshots, logos) for PDF generation using a local-first strategy:
/// 1. Try reading the file from the local agent-site public directory.
/// 2. Fall back to downloading from the agent's live site.
/// 3. Return null if neither source has the image — PDF renders gracefully without it.
/// </summary>
public sealed class LocalFirstImageResolver : IImageResolver
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LocalFirstImageResolver> _logger;

    public LocalFirstImageResolver(
        IHttpClientFactory httpClientFactory,
        IWebHostEnvironment env,
        ILogger<LocalFirstImageResolver> logger)
    {
        _httpClientFactory = httpClientFactory;
        _env = env;
        _logger = logger;
    }

    public async Task<byte[]?> ResolveAsync(string handle, string relativePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        // Normalize to forward slashes and strip leading slash
        var normalized = relativePath.TrimStart('/').Replace('\\', '/');

        // Local path candidates:
        // Docker: /app/config/accounts/{handle}/{relativePath}
        // Local dev: apps/agent-site/public/{relativePath}
        var dockerPath = Path.Combine(_env.ContentRootPath, "config", "accounts", handle, normalized);
        var localDevPath = Path.Combine(_env.ContentRootPath, "..", "..", "..", "apps", "agent-site", "public", normalized);

        foreach (var candidate in new[] { dockerPath, localDevPath })
        {
            if (File.Exists(candidate))
            {
                var bytes = await File.ReadAllBytesAsync(candidate, ct);
                _logger.LogInformation(
                    "[IMG-001] Resolved image for {Handle} from local file: {Path}",
                    handle, candidate);
                return bytes;
            }
        }

        // Fall back to HTTP download from the agent's live site
        var url = $"https://{handle}.real-estate-star.com/{normalized}";
        _logger.LogInformation(
            "[IMG-010] Local image not found for {Handle}, falling back to HTTP: {Url}",
            handle, url);

        try
        {
            var client = _httpClientFactory.CreateClient("image-resolver");
            using var response = await client.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                _logger.LogInformation(
                    "[IMG-001] Resolved image for {Handle} via HTTP: {Url}",
                    handle, url);
                return bytes;
            }

            _logger.LogWarning(
                "[IMG-020] Image not found for {Handle} at {Url}. HTTP {StatusCode}",
                handle, url, (int)response.StatusCode);
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _logger.LogWarning(
                "[IMG-020] Image not found for {Handle} at {Url}. Error: {Error}",
                handle, url, ex.Message);
            return null;
        }
    }
}
