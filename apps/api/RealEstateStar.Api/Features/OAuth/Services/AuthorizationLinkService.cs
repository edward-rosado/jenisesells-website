using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace RealEstateStar.Api.Features.OAuth.Services;

/// <summary>
/// State associated with a CSRF nonce — captures who the nonce was issued for.
/// </summary>
public sealed record AuthorizationLinkState(
    string AccountId,
    string AgentId,
    string Email,
    DateTime CreatedAt);

/// <summary>
/// Generates HMAC-signed OAuth authorization links and manages single-use CSRF nonces.
/// </summary>
public class AuthorizationLinkService : IDisposable
{
    private const int NonceTtlMinutes = 10;
    private const int CleanupIntervalMinutes = 5;

    private readonly string _secret;
    private readonly int _expirationHours;
    private readonly string _baseUrl;
    private readonly ILogger<AuthorizationLinkService> _logger;

    private readonly ConcurrentDictionary<string, NonceEntry> _nonces = new();
    private readonly Timer _cleanupTimer;

    public AuthorizationLinkService(IConfiguration configuration, ILogger<AuthorizationLinkService> logger)
    {
        _secret = configuration["OAuthLink:Secret"]
            ?? throw new InvalidOperationException("OAuthLink:Secret configuration is required");
        _expirationHours = int.TryParse(configuration["OAuthLink:ExpirationHours"], out var h) ? h : 24;
        _baseUrl = (configuration["Api:BaseUrl"] ?? "").TrimEnd('/');
        _logger = logger;

        _cleanupTimer = new Timer(
            _ => CleanupExpired(),
            null,
            TimeSpan.FromMinutes(CleanupIntervalMinutes),
            TimeSpan.FromMinutes(CleanupIntervalMinutes));
    }

    /// <summary>
    /// Generates an HMAC-signed authorization URL.
    /// </summary>
    public string GenerateLink(string accountId, string agentId, string email)
    {
        var exp = DateTimeOffset.UtcNow.AddHours(_expirationHours).ToUnixTimeSeconds();
        var sig = ComputeSignature(accountId, agentId, email, exp);

        var qs = HttpUtility.ParseQueryString(string.Empty);
        qs["accountId"] = accountId;
        qs["agentId"] = agentId;
        qs["email"] = email;
        qs["exp"] = exp.ToString();
        qs["sig"] = sig;

        return $"{_baseUrl}/oauth/google/authorize?{qs}";
    }

    /// <summary>
    /// Validates the HMAC signature and checks that the link has not expired.
    /// Uses constant-time comparison to prevent timing attacks.
    /// </summary>
    public bool ValidateSignature(string accountId, string agentId, string email, long exp, string sig)
    {
        // Check expiry first (avoids unnecessary crypto work on clearly expired links)
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp)
        {
            _logger.LogDebug("[OAUTH-LINK-001] Link expired. AccountId={AccountId}, AgentId={AgentId}", accountId, agentId);
            return false;
        }

        var expected = ComputeSignature(accountId, agentId, email, exp);

        // Normalize lengths for constant-time comparison — pad short sigs to prevent length leak
        // If sig is invalid length, pad with zeros so FixedTimeEquals always compares equal-length arrays
        var expectedBytes = Encoding.ASCII.GetBytes(expected);
        var actualBytes = Encoding.ASCII.GetBytes(sig.PadRight(expected.Length, '0'));

        // If lengths still differ after padding (sig is longer), truncate to expected length
        // This still causes them to be different, just avoids ArgumentException in FixedTimeEquals
        if (actualBytes.Length != expectedBytes.Length)
        {
            // Lengths differ — sig is definitely wrong, but still do constant-time compare to avoid oracle
            var normalized = new byte[expectedBytes.Length];
            actualBytes[..Math.Min(actualBytes.Length, normalized.Length)].CopyTo(normalized, 0);
            return CryptographicOperations.FixedTimeEquals(expectedBytes, normalized) &&
                   actualBytes.Length == expectedBytes.Length; // always false since lengths differ
        }

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    /// <summary>
    /// Generates a single-use 32-char hex CSRF nonce, stores it with the authorization link state.
    /// </summary>
    public string GenerateNonce(string accountId, string agentId, string email)
    {
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        _nonces[nonce] = new NonceEntry(
            new AuthorizationLinkState(accountId, agentId, email, DateTime.UtcNow),
            DateTime.UtcNow.AddMinutes(NonceTtlMinutes));
        return nonce;
    }

    /// <summary>
    /// Validates and atomically consumes a nonce. Returns the associated state or null if invalid/expired/already-used.
    /// </summary>
    public AuthorizationLinkState? ValidateAndConsumeNonce(string nonce)
    {
        if (!_nonces.TryRemove(nonce, out var entry))
        {
            _logger.LogDebug("[OAUTH-LINK-002] Nonce not found or already consumed.");
            return null;
        }

        if (DateTime.UtcNow > entry.ExpiresAt)
        {
            _logger.LogDebug("[OAUTH-LINK-003] Nonce expired.");
            return null;
        }

        return entry.State;
    }

    /// <summary>
    /// Removes all expired nonces. Called by the cleanup timer and exposed for testing.
    /// </summary>
    public void CleanupExpired()
    {
        var now = DateTime.UtcNow;
        var expired = _nonces
            .Where(kvp => now > kvp.Value.ExpiresAt)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expired)
            _nonces.TryRemove(key, out _);

        if (expired.Count > 0)
            _logger.LogDebug("[OAUTH-LINK-004] Cleaned up {Count} expired nonces.", expired.Count);
    }

    /// <summary>
    /// Test hook: forces the nonce to appear expired so cleanup and validate tests can exercise the expiry path.
    /// </summary>
    internal void ForceExpireNonce(string nonce)
    {
        if (_nonces.TryGetValue(nonce, out var entry))
            _nonces[nonce] = entry with { ExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
    }

    private string ComputeSignature(string accountId, string agentId, string email, long exp)
    {
        var payload = $"{accountId}.{agentId}.{email}.{exp}";
        var key = Encoding.UTF8.GetBytes(_secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hashBytes = HMACSHA256.HashData(key, payloadBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record NonceEntry(AuthorizationLinkState State, DateTime ExpiresAt);
}
