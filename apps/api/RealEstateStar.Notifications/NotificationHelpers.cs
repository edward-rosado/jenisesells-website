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
}
