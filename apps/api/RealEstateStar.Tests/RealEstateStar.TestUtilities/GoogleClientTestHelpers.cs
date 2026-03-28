using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.TestUtilities;

public static class GoogleClientTestHelpers
{
    public const string TestAccountId = "test-account";
    public const string TestAgentId = "test-agent";
    public const string TestClientId = "test-client-id";
    public const string TestClientSecret = "test-client-secret";

    public static OAuthCredential ValidCredential() => new()
    {
        AccessToken = "test-access-token",
        RefreshToken = "test-refresh-token",
        ExpiresAt = DateTime.UtcNow.AddHours(1),
        Scopes = ["https://www.googleapis.com/auth/gmail.send"],
        Email = "test@example.com",
        Name = "Test User",
        AccountId = TestAccountId,
        AgentId = TestAgentId,
        ETag = "etag-123"
    };

    public static OAuthCredential ExpiredCredential() => ValidCredential() with
    {
        ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
        ETag = "old-etag"
    };

    public static System.Net.Http.HttpResponseMessage UnauthorizedResponse() =>
        new(System.Net.HttpStatusCode.Unauthorized)
        {
            Content = new System.Net.Http.StringContent(
                "{\"error\": \"invalid_grant\"}",
                System.Text.Encoding.UTF8,
                "application/json")
        };
}
