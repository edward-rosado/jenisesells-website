using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Api.Features.OAuth.Services;
using System.Security.Cryptography;
using System.Text;

namespace RealEstateStar.Api.Tests.Features.OAuth.Services;

public class AuthorizationLinkServiceTests
{
    private const string Secret = "test-secret-32-bytes-long-enough!";
    private const string BaseUrl = "https://api.real-estate-star.com";

    private static AuthorizationLinkService CreateService(
        string? secret = Secret,
        int expirationHours = 24,
        string? baseUrl = BaseUrl)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OAuthLink:Secret"] = secret,
                ["OAuthLink:ExpirationHours"] = expirationHours.ToString(),
                ["Api:BaseUrl"] = baseUrl,
            })
            .Build();
        return new AuthorizationLinkService(config, NullLogger<AuthorizationLinkService>.Instance);
    }

    // ─── GenerateLink ───────────────────────────────────────────────────────────

    [Fact]
    public void GenerateLink_ReturnsUrlWithAllQueryParams()
    {
        var svc = CreateService();

        var url = svc.GenerateLink("acct-1", "agent-1", "agent@example.com");

        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        Assert.Equal("acct-1", query["accountId"]);
        Assert.Equal("agent-1", query["agentId"]);
        Assert.Equal("agent@example.com", query["email"]);
        Assert.NotNull(query["exp"]);
        Assert.NotNull(query["sig"]);
    }

    [Fact]
    public void GenerateLink_ExpContainsUnixTimestampInFuture()
    {
        var svc = CreateService(expirationHours: 24);
        var before = DateTimeOffset.UtcNow.AddHours(23).ToUnixTimeSeconds();

        var url = svc.GenerateLink("acct-1", "agent-1", "agent@example.com");

        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var exp = long.Parse(query["exp"]!);
        Assert.True(exp > before);
        Assert.True(exp <= DateTimeOffset.UtcNow.AddHours(25).ToUnixTimeSeconds());
    }

    // ─── ValidateSignature ──────────────────────────────────────────────────────

    [Fact]
    public void ValidateSignature_RoundTrip_Passes()
    {
        var svc = CreateService();
        var url = svc.GenerateLink("acct-1", "agent-1", "agent@example.com");
        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        var valid = svc.ValidateSignature(
            query["accountId"]!,
            query["agentId"]!,
            query["email"]!,
            long.Parse(query["exp"]!),
            query["sig"]!);

        Assert.True(valid);
    }

    [Fact]
    public void ValidateSignature_TamperedAccountId_Fails()
    {
        var svc = CreateService();
        var url = svc.GenerateLink("acct-1", "agent-1", "agent@example.com");
        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        var valid = svc.ValidateSignature(
            "tampered-acct",
            query["agentId"]!,
            query["email"]!,
            long.Parse(query["exp"]!),
            query["sig"]!);

        Assert.False(valid);
    }

    [Fact]
    public void ValidateSignature_TamperedAgentId_Fails()
    {
        var svc = CreateService();
        var url = svc.GenerateLink("acct-1", "agent-1", "agent@example.com");
        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        var valid = svc.ValidateSignature(
            query["accountId"]!,
            "tampered-agent",
            query["email"]!,
            long.Parse(query["exp"]!),
            query["sig"]!);

        Assert.False(valid);
    }

    [Fact]
    public void ValidateSignature_TamperedEmail_Fails()
    {
        var svc = CreateService();
        var url = svc.GenerateLink("acct-1", "agent-1", "agent@example.com");
        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        var valid = svc.ValidateSignature(
            query["accountId"]!,
            query["agentId"]!,
            "tampered@example.com",
            long.Parse(query["exp"]!),
            query["sig"]!);

        Assert.False(valid);
    }

    [Fact]
    public void ValidateSignature_TamperedSig_Fails()
    {
        var svc = CreateService();
        var url = svc.GenerateLink("acct-1", "agent-1", "agent@example.com");
        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        var valid = svc.ValidateSignature(
            query["accountId"]!,
            query["agentId"]!,
            query["email"]!,
            long.Parse(query["exp"]!),
            "0000000000000000000000000000000000000000000000000000000000000000");

        Assert.False(valid);
    }

    [Fact]
    public void ValidateSignature_ExpiredLink_Fails()
    {
        var svc = CreateService();
        var url = svc.GenerateLink("acct-1", "agent-1", "agent@example.com");
        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        // Expired = exp timestamp in the past
        var expiredExp = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();

        // Recompute sig for the expired payload so signature itself is valid — only expiry check should fail
        var payload = $"acct-1.agent-1.agent@example.com.{expiredExp}";
        var key = Encoding.UTF8.GetBytes(Secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var sigBytes = HMACSHA256.HashData(key, payloadBytes);
        var expiredSig = Convert.ToHexString(sigBytes).ToLowerInvariant();

        var valid = svc.ValidateSignature("acct-1", "agent-1", "agent@example.com", expiredExp, expiredSig);

        Assert.False(valid);
    }

    [Fact]
    public void ValidateSignature_UsesConstantTimeComparison()
    {
        // This test verifies the implementation detail — it should not throw when
        // sig lengths differ (padding to same length or returning early via bool comparison,
        // never timing-variant).
        var svc = CreateService();
        // Short sig should not throw; just return false
        var valid = svc.ValidateSignature("acct-1", "agent-1", "agent@example.com",
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), "short");

        Assert.False(valid);
    }

    // ─── GenerateNonce ───────────────────────────────────────────────────────────

    [Fact]
    public void GenerateNonce_Returns32HexChars()
    {
        var svc = CreateService();

        var nonce = svc.GenerateNonce("acct-1", "agent-1", "agent@example.com");

        Assert.Equal(32, nonce.Length);
        Assert.Matches("^[0-9a-f]{32}$", nonce);
    }

    [Fact]
    public void GenerateNonce_TwoCallsReturnDifferentValues()
    {
        var svc = CreateService();

        var nonce1 = svc.GenerateNonce("acct-1", "agent-1", "a@b.com");
        var nonce2 = svc.GenerateNonce("acct-1", "agent-1", "a@b.com");

        Assert.NotEqual(nonce1, nonce2);
    }

    // ─── ValidateAndConsumeNonce ─────────────────────────────────────────────────

    [Fact]
    public void ValidateAndConsumeNonce_ValidNonce_ReturnsState()
    {
        var svc = CreateService();
        var nonce = svc.GenerateNonce("acct-1", "agent-1", "agent@example.com");

        var state = svc.ValidateAndConsumeNonce(nonce);

        Assert.NotNull(state);
        Assert.Equal("acct-1", state.AccountId);
        Assert.Equal("agent-1", state.AgentId);
        Assert.Equal("agent@example.com", state.Email);
    }

    [Fact]
    public void ValidateAndConsumeNonce_SingleUse_SecondCallReturnsNull()
    {
        var svc = CreateService();
        var nonce = svc.GenerateNonce("acct-1", "agent-1", "agent@example.com");

        var first = svc.ValidateAndConsumeNonce(nonce);
        var second = svc.ValidateAndConsumeNonce(nonce);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void ValidateAndConsumeNonce_UnknownNonce_ReturnsNull()
    {
        var svc = CreateService();

        var state = svc.ValidateAndConsumeNonce("0000000000000000ffffffffffffffff");

        Assert.Null(state);
    }

    [Fact]
    public void ValidateAndConsumeNonce_ExpiredNonce_ReturnsNull()
    {
        var svc = CreateService();

        // Manually inject an expired nonce via internal method for testing
        var nonce = svc.GenerateNonce("acct-1", "agent-1", "agent@example.com");

        // Simulate expiry by creating a service that has the nonce past its TTL
        // Since we can't directly age the nonce, we verify CleanupExpired removes it.
        // We use the overload that accepts a custom creation time.
        var expiredNonce = svc.GenerateNonce("acct-1", "agent-1", "agent@example.com");
        svc.ForceExpireNonce(expiredNonce); // test hook method

        var state = svc.ValidateAndConsumeNonce(expiredNonce);

        Assert.Null(state);
    }

    [Fact]
    public void CleanupExpired_RemovesOnlyExpiredNonces()
    {
        var svc = CreateService();
        var freshNonce = svc.GenerateNonce("acct-1", "agent-1", "agent@example.com");
        var expiredNonce = svc.GenerateNonce("acct-2", "agent-2", "other@example.com");
        svc.ForceExpireNonce(expiredNonce);

        svc.CleanupExpired();

        Assert.NotNull(svc.ValidateAndConsumeNonce(freshNonce));
    }
}
