using System.Security.Cryptography;
using System.Text;

namespace RealEstateStar.Notifications;

internal static class NotificationHelpers
{
    /// <summary>
    /// Returns a 12-character lowercase hex prefix of the SHA-256 hash of the email address.
    /// Trims and lowercases the input so casing differences hash identically.
    /// Used for structured logging — never log PII in plain text.
    /// </summary>
    internal static string HashEmail(string email)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }

    /// <summary>
    /// Escapes a string for safe embedding inside a double-quoted YAML scalar.
    /// Escapes backslashes first, then double quotes, preventing key injection.
    /// </summary>
    internal static string EscapeYaml(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>
    /// Resolves the accountId from an already-loaded AccountConfig.
    /// Falls back to agentId if config is null or has no AccountId set.
    /// Use this overload when the caller has already loaded the config for other purposes.
    /// </summary>
    internal static string ResolveAccountId(AccountConfig? config, string agentId)
        => config?.AccountId ?? agentId;

    /// <summary>
    /// Resolves the accountId for a given agentId by loading the account config.
    /// Falls back to agentId if config is unavailable or has no AccountId set.
    /// Use this overload when accountId is the only value needed from the config.
    /// </summary>
    internal static async Task<string> ResolveAccountIdAsync(
        IAccountConfigService accountConfigService,
        string agentId,
        CancellationToken ct)
    {
        var config = await accountConfigService.GetAccountAsync(agentId, ct);
        return config?.AccountId ?? agentId;
    }
}
