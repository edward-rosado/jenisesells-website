using System.Security.Cryptography;
using System.Text;

namespace RealEstateStar.Domain.Shared;

/// <summary>
/// Utility for computing content-addressed SHA256 hashes from activity input fields.
/// Used by the orchestrator to determine whether an activity's inputs have changed
/// since the last run, enabling safe skip-on-retry behavior.
/// </summary>
public static class ContentHash
{
    /// <summary>
    /// Computes a deterministic SHA256 hash of the given fields joined with "|".
    /// Null fields are treated as empty strings. Field order matters.
    /// </summary>
    public static string Compute(params string?[] fields)
    {
        var input = string.Join("|", fields.Select(f => f ?? ""));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
