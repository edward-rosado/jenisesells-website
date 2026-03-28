using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.DataServices.Config;

public partial class AccountConfigService(string configDirectory, ILogger<AccountConfigService>? logger = null) : IAccountConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false
    };

    [GeneratedRegex(@"^[a-z0-9-]+$")]
    private static partial Regex AccountHandlePattern();

    public async Task<AccountConfig?> GetAccountAsync(string handle, CancellationToken ct)
    {
        ValidateHandle(handle);

        var resolvedConfigDir = Path.GetFullPath(configDirectory);

        // Account format: {handle}/account.json
        var resolvedPath = Path.GetFullPath(Path.Combine(configDirectory, handle, "account.json"));

        if (!resolvedPath.StartsWith(resolvedConfigDir, StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogWarning("Path traversal attempt detected for handle {Handle}", handle);
            throw new ArgumentException($"Invalid handle: {handle}", nameof(handle));
        }

        if (!File.Exists(resolvedPath))
        {
            logger?.LogWarning("Account config file not found: {FilePath}", resolvedPath);
            return null;
        }

        logger?.LogInformation("Loading account config from {FilePath}", resolvedPath);

        await using var stream = File.OpenRead(resolvedPath);
        return await JsonSerializer.DeserializeAsync<AccountConfig>(stream, JsonOptions, ct);
    }

    public async Task<List<AccountConfig>> ListAllAsync(CancellationToken ct)
    {
        var results = new List<AccountConfig>();

        if (!Directory.Exists(configDirectory))
            return results;

        // Account format: {configDirectory}/{handle}/account.json
        var subDirs = Directory.GetDirectories(configDirectory);
        foreach (var dir in subDirs)
        {
            var handle = Path.GetFileName(dir);
            if (!AccountHandlePattern().IsMatch(handle))
                continue;

            var configFile = Path.Combine(dir, "account.json");
            if (!File.Exists(configFile))
                continue;

            try
            {
                await using var stream = File.OpenRead(configFile);
                var config = await JsonSerializer.DeserializeAsync<AccountConfig>(stream, JsonOptions, ct);
                if (config is not null)
                    results.Add(config);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to load account config from {FilePath}", configFile);
            }
        }

        return results;
    }

    public async Task UpdateAccountAsync(string handle, AccountConfig config, CancellationToken ct)
    {
        ValidateHandle(handle);

        var resolvedConfigDir = Path.GetFullPath(configDirectory);
        var resolvedPath = Path.GetFullPath(Path.Combine(configDirectory, handle, "account.json"));

        if (!resolvedPath.StartsWith(resolvedConfigDir, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Invalid handle: {handle}", nameof(handle));

        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(resolvedPath, json, ct);
        logger?.LogInformation("[CONFIG-010] Updated account config for {Handle}", handle);
    }

    private static void ValidateHandle(string handle)
    {
        if (string.IsNullOrWhiteSpace(handle) || !AccountHandlePattern().IsMatch(handle))
            throw new ArgumentException($"Invalid handle: {handle}", nameof(handle));
    }
}
