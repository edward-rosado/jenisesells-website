# Deployment-Ready Onboarding — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Wire all stubbed integrations (Google OAuth, Cloudflare deploy, CMA pipeline, Stripe checkout) into the onboarding flow so the demo is fully live.

**Architecture:** The onboarding feature lives as a vertical slice under `Features/Onboarding/` in the .NET 10 API (`apps/api/`). A Next.js 16 frontend (`apps/platform/`) renders the chat UI. Four integration seams are currently stubbed: (1) Google OAuth for Gmail/Drive/Docs/Sheets access, (2) Cloudflare Pages deploy via Wrangler CLI, (3) in-process CMA pipeline invocation with per-session Google tokens, and (4) Stripe Checkout for payment. Each integration gets wired into the existing state machine, tool dispatcher, and chat service. New states (ConnectGoogle) and endpoints (OAuth start/callback, Stripe webhook) are added as REPR vertical slices with auto-registration.

**Tech Stack:** .NET 10 Minimal API, Next.js 16, Google APIs (OAuth2, Gmail, Drive, Docs, Sheets), Stripe.net, Cloudflare Pages (Wrangler CLI), QuestPDF

**Design Doc:** `docs/plans/2026-03-10-deployment-ready-onboarding-design.md`

**REPR Conventions (MUST follow):**
- Every endpoint operation is a vertical slice: `Features/Onboarding/{Operation}/{Operation}Endpoint.cs`
- Endpoint classes implement `IEndpoint` (from `Infrastructure/`) — auto-discovered, no manual registration
- HTTP request DTOs live in the operation folder, NOT the same as domain models
- Domain types at feature root: `Features/Onboarding/OnboardingSession.cs`, `OnboardingState.cs`, etc.
- Feature services live inside the feature: `Features/Onboarding/Services/`
- Mappers at feature level: `Features/Onboarding/OnboardingMappers.cs`
- Endpoint class name matches folder name: `CreateSession/` → `CreateSessionEndpoint`
- CancellationToken is always REQUIRED (no `= default`)
- File-scoped namespaces, primary constructors, expression-bodied members
- Handle methods must be `internal` (not private) so tests call directly
- Tests first (TDD), 100% branch coverage

---

## Phase A: Google OAuth Integration

### Task A1: Add `ConnectGoogle` state to OnboardingState enum

**Modify:** `apps/api/RealEstateStar.Api/Features/Onboarding/OnboardingState.cs`
**Test:** `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Services/StateMachineTests.cs`

**Step 1 — Write failing test:**

Add to `StateMachineTests.cs`:

```csharp
[Fact]
public void CanAdvance_FromCollectBranding_ToConnectGoogle()
{
    var session = OnboardingSession.Create(null);
    session.CurrentState = OnboardingState.CollectBranding;
    Assert.True(_sm.CanAdvance(session, OnboardingState.ConnectGoogle));
}

[Fact]
public void CanAdvance_FromConnectGoogle_ToGenerateSite()
{
    var session = OnboardingSession.Create(null);
    session.CurrentState = OnboardingState.ConnectGoogle;
    Assert.True(_sm.CanAdvance(session, OnboardingState.GenerateSite));
}

[Fact]
public void CannotSkip_FromCollectBranding_ToGenerateSite()
{
    var session = OnboardingSession.Create(null);
    session.CurrentState = OnboardingState.CollectBranding;
    Assert.False(_sm.CanAdvance(session, OnboardingState.GenerateSite));
}

[Fact]
public void GetAllowedTools_ConnectGoogle_ReturnsGoogleAuthTool()
{
    var tools = _sm.GetAllowedTools(OnboardingState.ConnectGoogle);
    Assert.Contains("google_auth_card", tools);
    Assert.DoesNotContain("deploy_site", tools);
}
```

Update the existing `AllTransitions_AreValid` Theory to include the new ConnectGoogle state:

```csharp
[Theory]
[InlineData(OnboardingState.ScrapeProfile, OnboardingState.ConfirmIdentity)]
[InlineData(OnboardingState.ConfirmIdentity, OnboardingState.CollectBranding)]
[InlineData(OnboardingState.CollectBranding, OnboardingState.ConnectGoogle)]
[InlineData(OnboardingState.ConnectGoogle, OnboardingState.GenerateSite)]
[InlineData(OnboardingState.GenerateSite, OnboardingState.PreviewSite)]
[InlineData(OnboardingState.PreviewSite, OnboardingState.DemoCma)]
[InlineData(OnboardingState.DemoCma, OnboardingState.ShowResults)]
[InlineData(OnboardingState.ShowResults, OnboardingState.CollectPayment)]
[InlineData(OnboardingState.CollectPayment, OnboardingState.TrialActivated)]
public void AllTransitions_AreValid(OnboardingState from, OnboardingState to)
{
    var session = OnboardingSession.Create(null);
    session.CurrentState = from;
    Assert.True(_sm.CanAdvance(session, to));
}
```

**Step 2 — Run test to verify failure:**

```bash
dotnet test apps/api/RealEstateStar.Api.Tests --filter "StateMachineTests"
```

**Step 3 — Write implementation:**

Update `OnboardingState.cs`:

```csharp
using System.Text.Json.Serialization;

namespace RealEstateStar.Api.Features.Onboarding;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OnboardingState
{
    ScrapeProfile,
    ConfirmIdentity,
    CollectBranding,
    ConnectGoogle,
    GenerateSite,
    PreviewSite,
    DemoCma,
    ShowResults,
    CollectPayment,
    TrialActivated
}
```

**Step 4 — Run test to verify pass** (will still fail until Task A2 completes — state machine transitions not yet updated).

**Step 5 — Commit:** `feat(onboarding): add ConnectGoogle state to onboarding enum`

---

### Task A2: Update OnboardingStateMachine transitions + tool access

**Modify:** `apps/api/RealEstateStar.Api/Features/Onboarding/Services/OnboardingStateMachine.cs`
**Test:** `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Services/StateMachineTests.cs` (tests from A1)

**Step 1 — Tests already written in A1.**

**Step 2 — Already verified failure.**

**Step 3 — Write implementation:**

Update `OnboardingStateMachine.cs`:

```csharp
namespace RealEstateStar.Api.Features.Onboarding.Services;

public class OnboardingStateMachine
{
    private static readonly Dictionary<OnboardingState, OnboardingState[]> Transitions = new()
    {
        [OnboardingState.ScrapeProfile] = [OnboardingState.ConfirmIdentity],
        [OnboardingState.ConfirmIdentity] = [OnboardingState.CollectBranding],
        [OnboardingState.CollectBranding] = [OnboardingState.ConnectGoogle],
        [OnboardingState.ConnectGoogle] = [OnboardingState.GenerateSite],
        [OnboardingState.GenerateSite] = [OnboardingState.PreviewSite],
        [OnboardingState.PreviewSite] = [OnboardingState.DemoCma],
        [OnboardingState.DemoCma] = [OnboardingState.ShowResults],
        [OnboardingState.ShowResults] = [OnboardingState.CollectPayment],
        [OnboardingState.CollectPayment] = [OnboardingState.TrialActivated],
        [OnboardingState.TrialActivated] = [],
    };

    private static readonly Dictionary<OnboardingState, string[]> ToolsByState = new()
    {
        [OnboardingState.ScrapeProfile] = ["scrape_url", "update_profile"],
        [OnboardingState.ConfirmIdentity] = ["update_profile"],
        [OnboardingState.CollectBranding] = ["extract_colors", "set_branding"],
        [OnboardingState.ConnectGoogle] = ["google_auth_card"],
        [OnboardingState.GenerateSite] = ["deploy_site"],
        [OnboardingState.PreviewSite] = ["get_preview_url"],
        [OnboardingState.DemoCma] = ["submit_cma_form"],
        [OnboardingState.ShowResults] = [],
        [OnboardingState.CollectPayment] = ["create_stripe_session"],
        [OnboardingState.TrialActivated] = [],
    };

    public bool CanAdvance(OnboardingSession session, OnboardingState targetState)
        => Transitions.TryGetValue(session.CurrentState, out var allowed)
           && allowed.Contains(targetState);

    public void Advance(OnboardingSession session, OnboardingState targetState)
    {
        if (!CanAdvance(session, targetState))
            throw new InvalidOperationException(
                $"Cannot transition from {session.CurrentState} to {targetState}");

        session.CurrentState = targetState;
        session.UpdatedAt = DateTime.UtcNow;
    }

    public string[] GetAllowedTools(OnboardingState state)
        => ToolsByState.GetValueOrDefault(state, []);
}
```

**Step 4 — Run tests:**

```bash
dotnet test apps/api/RealEstateStar.Api.Tests --filter "StateMachineTests"
```

**Step 5 — Commit:** `feat(onboarding): add ConnectGoogle transitions and tool access to state machine`

---

### Task A3: Create GoogleTokens domain type

**Create:** `apps/api/RealEstateStar.Api/Features/Onboarding/GoogleTokens.cs`
**Test:** `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/GoogleTokensTests.cs`

**Step 1 — Write failing test:**

```csharp
using RealEstateStar.Api.Features.Onboarding;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding;

public class GoogleTokensTests
{
    [Fact]
    public void IsExpired_WhenPastExpiry_ReturnsTrue()
    {
        var tokens = new GoogleTokens
        {
            AccessToken = "access",
            RefreshToken = "refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            Scopes = ["gmail.send"],
            GoogleEmail = "test@gmail.com",
            GoogleName = "Test User",
        };

        Assert.True(tokens.IsExpired);
    }

    [Fact]
    public void IsExpired_WhenBeforeExpiry_ReturnsFalse()
    {
        var tokens = new GoogleTokens
        {
            AccessToken = "access",
            RefreshToken = "refresh",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Scopes = ["gmail.send"],
            GoogleEmail = "test@gmail.com",
            GoogleName = "Test User",
        };

        Assert.False(tokens.IsExpired);
    }

    [Fact]
    public void IsExpired_WhenWithin5MinBuffer_ReturnsTrue()
    {
        var tokens = new GoogleTokens
        {
            AccessToken = "access",
            RefreshToken = "refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(3),
            Scopes = ["gmail.send"],
            GoogleEmail = "test@gmail.com",
            GoogleName = "Test User",
        };

        Assert.True(tokens.IsExpired);
    }
}
```

**Step 2 — Run test to verify failure (compilation error — type doesn't exist).**

**Step 3 — Write implementation:**

```csharp
namespace RealEstateStar.Api.Features.Onboarding;

public sealed record GoogleTokens
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; init; }
    public required DateTime ExpiresAt { get; set; }
    public required string[] Scopes { get; init; }
    public required string GoogleEmail { get; init; }
    public required string GoogleName { get; init; }

    /// <summary>
    /// Returns true if the access token is expired or will expire within 5 minutes.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt.AddMinutes(-5);
}
```

**Step 4 — Run tests:**

```bash
dotnet test apps/api/RealEstateStar.Api.Tests --filter "GoogleTokensTests"
```

**Step 5 — Commit:** `feat(onboarding): add GoogleTokens domain type`

---

### Task A4: Add GoogleTokens to OnboardingSession

**Modify:** `apps/api/RealEstateStar.Api/Features/Onboarding/OnboardingSession.cs`
**Test:** `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/OnboardingSessionTests.cs`

**Step 1 — Write failing test:**

Add to `OnboardingSessionTests.cs`:

```csharp
[Fact]
public void GoogleTokens_DefaultsToNull()
{
    var session = OnboardingSession.Create(null);
    Assert.Null(session.GoogleTokens);
}

[Fact]
public void GoogleTokens_CanBeSet()
{
    var session = OnboardingSession.Create(null);
    session.GoogleTokens = new GoogleTokens
    {
        AccessToken = "access",
        RefreshToken = "refresh",
        ExpiresAt = DateTime.UtcNow.AddHours(1),
        Scopes = ["gmail.send"],
        GoogleEmail = "test@gmail.com",
        GoogleName = "Test User",
    };

    Assert.NotNull(session.GoogleTokens);
    Assert.Equal("test@gmail.com", session.GoogleTokens.GoogleEmail);
}
```

**Step 2 — Run test to verify failure.**

**Step 3 — Write implementation:**

Update `OnboardingSession.cs`:

```csharp
namespace RealEstateStar.Api.Features.Onboarding;

public sealed class OnboardingSession
{
    public required string Id { get; init; }
    public OnboardingState CurrentState { get; set; } = OnboardingState.ScrapeProfile;
    public string? ProfileUrl { get; init; }
    public ScrapedProfile? Profile { get; set; }
    public GoogleTokens? GoogleTokens { get; set; }
    public List<ChatMessage> Messages { get; init; } = [];
    public string? AgentConfigId { get; set; }
    public string? StripeSessionId { get; set; }
    public string? StripeSetupIntentId { get; set; }
    public string? SiteUrl { get; set; }
    public string? CustomDomain { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static OnboardingSession Create(string? profileUrl) => new()
    {
        Id = Guid.NewGuid().ToString("N")[..12],
        ProfileUrl = profileUrl
    };
}
```

**Step 4 — Run tests:**

```bash
dotnet test apps/api/RealEstateStar.Api.Tests --filter "OnboardingSessionTests"
```

**Step 5 — Commit:** `feat(onboarding): add GoogleTokens and StripeSessionId to OnboardingSession`

---

### Task A5: Create GoogleOAuthService

**Create:** `apps/api/RealEstateStar.Api/Features/Onboarding/Services/GoogleOAuthService.cs`
**Test:** `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Services/GoogleOAuthServiceTests.cs`

**Step 1 — Write failing tests:**

```csharp
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class GoogleOAuthServiceTests
{
    private const string ClientId = "test-client-id";
    private const string ClientSecret = "test-client-secret";
    private const string RedirectUri = "http://localhost:5000/oauth/google/callback";

    [Fact]
    public void BuildAuthorizationUrl_ReturnsGoogleUrl_WithAllScopes()
    {
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(handler.Object);
        var service = new GoogleOAuthService(httpClient, ClientId, ClientSecret, RedirectUri, NullLogger<GoogleOAuthService>.Instance);

        var url = service.BuildAuthorizationUrl("session123");

        Assert.Contains("accounts.google.com/o/oauth2/v2/auth", url);
        Assert.Contains("client_id=test-client-id", url);
        Assert.Contains("state=session123", url);
        Assert.Contains("gmail.send", url);
        Assert.Contains("drive.file", url);
        Assert.Contains("userinfo.profile", url);
        Assert.Contains("userinfo.email", url);
        Assert.Contains("documents", url);
        Assert.Contains("spreadsheets", url);
        Assert.Contains("calendar.events", url);
        Assert.Contains("access_type=offline", url);
        Assert.Contains("prompt=consent", url);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ReturnsGoogleTokens()
    {
        var tokenResponse = new
        {
            access_token = "ya29.test-access",
            refresh_token = "1//test-refresh",
            expires_in = 3600,
            scope = "openid email profile",
            token_type = "Bearer"
        };

        var profileResponse = new
        {
            email = "agent@gmail.com",
            name = "Jane Doe",
        };

        var handler = new Mock<HttpMessageHandler>();
        var callCount = 0;
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                var json = callCount == 1
                    ? JsonSerializer.Serialize(tokenResponse)
                    : JsonSerializer.Serialize(profileResponse);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

        var httpClient = new HttpClient(handler.Object);
        var service = new GoogleOAuthService(httpClient, ClientId, ClientSecret, RedirectUri, NullLogger<GoogleOAuthService>.Instance);

        var tokens = await service.ExchangeCodeAsync("auth-code-123", CancellationToken.None);

        Assert.Equal("ya29.test-access", tokens.AccessToken);
        Assert.Equal("1//test-refresh", tokens.RefreshToken);
        Assert.Equal("agent@gmail.com", tokens.GoogleEmail);
        Assert.Equal("Jane Doe", tokens.GoogleName);
        Assert.False(tokens.IsExpired);
    }

    [Fact]
    public async Task ExchangeCodeAsync_WhenTokenEndpointFails_Throws()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"invalid_grant\"}")
            });

        var httpClient = new HttpClient(handler.Object);
        var service = new GoogleOAuthService(httpClient, ClientId, ClientSecret, RedirectUri, NullLogger<GoogleOAuthService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExchangeCodeAsync("bad-code", CancellationToken.None));
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_UpdatesTokenFields()
    {
        var refreshResponse = new
        {
            access_token = "ya29.new-access",
            expires_in = 3600,
            scope = "openid email profile",
            token_type = "Bearer"
        };

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(refreshResponse),
                    System.Text.Encoding.UTF8,
                    "application/json")
            });

        var httpClient = new HttpClient(handler.Object);
        var service = new GoogleOAuthService(httpClient, ClientId, ClientSecret, RedirectUri, NullLogger<GoogleOAuthService>.Instance);

        var tokens = new GoogleTokens
        {
            AccessToken = "ya29.old-access",
            RefreshToken = "1//test-refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            Scopes = ["gmail.send"],
            GoogleEmail = "agent@gmail.com",
            GoogleName = "Jane Doe",
        };

        await service.RefreshAccessTokenAsync(tokens, CancellationToken.None);

        Assert.Equal("ya29.new-access", tokens.AccessToken);
        Assert.False(tokens.IsExpired);
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_WhenRefreshFails_Throws()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\":\"invalid_grant\"}")
            });

        var httpClient = new HttpClient(handler.Object);
        var service = new GoogleOAuthService(httpClient, ClientId, ClientSecret, RedirectUri, NullLogger<GoogleOAuthService>.Instance);

        var tokens = new GoogleTokens
        {
            AccessToken = "ya29.old",
            RefreshToken = "1//bad-refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            Scopes = ["gmail.send"],
            GoogleEmail = "agent@gmail.com",
            GoogleName = "Jane Doe",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RefreshAccessTokenAsync(tokens, CancellationToken.None));
    }
}
```

**Step 2 — Run test to verify failure.**

**Step 3 — Write implementation:**

```csharp
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Api.Features.Onboarding.Services;

public class GoogleOAuthService(
    HttpClient httpClient,
    string clientId,
    string clientSecret,
    string redirectUri,
    ILogger<GoogleOAuthService> logger)
{
    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UserInfoEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo";

    private static readonly string[] Scopes =
    [
        "https://www.googleapis.com/auth/userinfo.profile",
        "https://www.googleapis.com/auth/userinfo.email",
        "https://www.googleapis.com/auth/gmail.send",
        "https://www.googleapis.com/auth/drive.file",
        "https://www.googleapis.com/auth/documents",
        "https://www.googleapis.com/auth/spreadsheets",
        "https://www.googleapis.com/auth/calendar.events",
    ];

    public string BuildAuthorizationUrl(string sessionId)
    {
        var scopeStr = Uri.EscapeDataString(string.Join(" ", Scopes));
        return $"{AuthEndpoint}?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope={scopeStr}&access_type=offline&prompt=consent&state={sessionId}";
    }

    public async Task<GoogleTokens> ExchangeCodeAsync(string code, CancellationToken ct)
    {
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
        });

        var tokenResponse = await httpClient.PostAsync(TokenEndpoint, tokenRequest, ct);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var errorBody = await tokenResponse.Content.ReadAsStringAsync(ct);
            logger.LogError("Google token exchange failed: {StatusCode} {Error}", tokenResponse.StatusCode, errorBody);
            throw new InvalidOperationException("Failed to exchange authorization code for tokens");
        }

        var tokenJson = await tokenResponse.Content.ReadAsStringAsync(ct);
        var tokenData = JsonDocument.Parse(tokenJson).RootElement;

        var accessToken = tokenData.GetProperty("access_token").GetString()!;
        var refreshToken = tokenData.GetProperty("refresh_token").GetString()!;
        var expiresIn = tokenData.GetProperty("expires_in").GetInt32();

        // Fetch user profile
        var profileRequest = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var profileResponse = await httpClient.SendAsync(profileRequest, ct);
        profileResponse.EnsureSuccessStatusCode();

        var profileJson = await profileResponse.Content.ReadAsStringAsync(ct);
        var profileData = JsonDocument.Parse(profileJson).RootElement;

        var email = profileData.GetProperty("email").GetString()!;
        var name = profileData.GetProperty("name").GetString()!;

        logger.LogInformation("Google OAuth completed for {Email}", email);

        return new GoogleTokens
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn),
            Scopes = Scopes,
            GoogleEmail = email,
            GoogleName = name,
        };
    }

    public async Task RefreshAccessTokenAsync(GoogleTokens tokens, CancellationToken ct)
    {
        var refreshRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["refresh_token"] = tokens.RefreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "refresh_token",
        });

        var response = await httpClient.PostAsync(TokenEndpoint, refreshRequest, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Google token refresh failed: {StatusCode} {Error}", response.StatusCode, errorBody);
            throw new InvalidOperationException("Failed to refresh Google access token");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var data = JsonDocument.Parse(json).RootElement;

        tokens.AccessToken = data.GetProperty("access_token").GetString()!;
        tokens.ExpiresAt = DateTime.UtcNow.AddSeconds(data.GetProperty("expires_in").GetInt32());

        logger.LogInformation("Refreshed Google access token for {Email}", tokens.GoogleEmail);
    }
}
```

**Step 4 — Run tests:**

```bash
dotnet test apps/api/RealEstateStar.Api.Tests --filter "GoogleOAuthServiceTests"
```

**Step 5 — Commit:** `feat(onboarding): add GoogleOAuthService for token exchange and refresh`

---

### Task A6: Create StartGoogleOAuthEndpoint

**Create:** `apps/api/RealEstateStar.Api/Features/Onboarding/ConnectGoogle/StartGoogleOAuthEndpoint.cs`
**Test:** `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/ConnectGoogle/StartGoogleOAuthEndpointTests.cs`

**Step 1 — Write failing test:**

```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.ConnectGoogle;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.ConnectGoogle;

public class StartGoogleOAuthEndpointTests
{
    private readonly Mock<ISessionStore> _mockStore = new();
    private readonly Mock<GoogleOAuthService> _mockOAuth;

    public StartGoogleOAuthEndpointTests()
    {
        // GoogleOAuthService needs mocking via an interface or virtual methods.
        // Since it's a concrete class with primary constructor, we create a test helper.
        _mockOAuth = new Mock<GoogleOAuthService>(
            new HttpClient(), "client-id", "client-secret", "http://localhost:5000/oauth/google/callback",
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleOAuthService>.Instance);
    }

    [Fact]
    public async Task Handle_WithValidSession_RedirectsToGoogle()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.ConnectGoogle;
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _mockOAuth.Setup(o => o.BuildAuthorizationUrl(session.Id))
            .Returns("https://accounts.google.com/o/oauth2/v2/auth?test=true");

        var result = await StartGoogleOAuthEndpoint.Handle(
            session.Id, _mockStore.Object, _mockOAuth.Object, CancellationToken.None);

        var redirect = Assert.IsType<RedirectHttpResult>(result);
        Assert.Contains("accounts.google.com", redirect.Url);
    }

    [Fact]
    public async Task Handle_WithMissingSession_Returns404()
    {
        _mockStore.Setup(s => s.LoadAsync("bad-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OnboardingSession?)null);

        var result = await StartGoogleOAuthEndpoint.Handle(
            "bad-id", _mockStore.Object, _mockOAuth.Object, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Handle_WhenNotInConnectGoogleState_Returns400()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.ScrapeProfile;
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await StartGoogleOAuthEndpoint.Handle(
            session.Id, _mockStore.Object, _mockOAuth.Object, CancellationToken.None);

        Assert.IsType<BadRequest<string>>(result);
    }
}
```

**Step 2 — Run test to verify failure.**

**Step 3 — Write implementation:**

```csharp
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Features.Onboarding.ConnectGoogle;

public class StartGoogleOAuthEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app)
    {
        app.MapGet("/oauth/google/start", Handle);
    }

    internal static async Task<IResult> Handle(
        string sessionId,
        ISessionStore sessionStore,
        GoogleOAuthService oAuthService,
        CancellationToken ct)
    {
        var session = await sessionStore.LoadAsync(sessionId, ct);
        if (session is null) return Results.NotFound();

        if (session.CurrentState != OnboardingState.ConnectGoogle)
            return Results.BadRequest("Session is not in ConnectGoogle state");

        var authUrl = oAuthService.BuildAuthorizationUrl(sessionId);
        return Results.Redirect(authUrl);
    }
}
```

**Step 4 — Run tests:**

```bash
dotnet test apps/api/RealEstateStar.Api.Tests --filter "StartGoogleOAuthEndpointTests"
```

**Step 5 — Commit:** `feat(onboarding): add StartGoogleOAuth endpoint`

---

### Task A7: Create GoogleOAuthCallbackEndpoint

**Create:** `apps/api/RealEstateStar.Api/Features/Onboarding/ConnectGoogle/GoogleOAuthCallbackEndpoint.cs`
**Test:** `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/ConnectGoogle/GoogleOAuthCallbackEndpointTests.cs`

**Step 1 — Write failing test:**

```csharp
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.ConnectGoogle;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.ConnectGoogle;

public class GoogleOAuthCallbackEndpointTests
{
    private readonly Mock<ISessionStore> _mockStore = new();
    private readonly Mock<GoogleOAuthService> _mockOAuth;
    private readonly OnboardingStateMachine _sm = new();

    public GoogleOAuthCallbackEndpointTests()
    {
        _mockOAuth = new Mock<GoogleOAuthService>(
            new HttpClient(), "client-id", "client-secret", "http://localhost:5000/oauth/google/callback",
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleOAuthService>.Instance);
    }

    [Fact]
    public async Task Handle_WithValidCode_StoresTokensAndAdvancesState()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.ConnectGoogle;
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockStore.Setup(s => s.SaveAsync(session, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tokens = new GoogleTokens
        {
            AccessToken = "ya29.test",
            RefreshToken = "1//test",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Scopes = ["gmail.send"],
            GoogleEmail = "agent@gmail.com",
            GoogleName = "Jane Doe",
        };
        _mockOAuth.Setup(o => o.ExchangeCodeAsync("auth-code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokens);

        var result = await GoogleOAuthCallbackEndpoint.Handle(
            "auth-code", session.Id, null,
            _mockStore.Object, _mockOAuth.Object, _sm, CancellationToken.None);

        Assert.NotNull(session.GoogleTokens);
        Assert.Equal("agent@gmail.com", session.GoogleTokens.GoogleEmail);
        Assert.Equal(OnboardingState.GenerateSite, session.CurrentState);
        _mockStore.Verify(s => s.SaveAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithMissingSession_ReturnsErrorHtml()
    {
        _mockStore.Setup(s => s.LoadAsync("bad-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OnboardingSession?)null);

        var result = await GoogleOAuthCallbackEndpoint.Handle(
            "code", "bad-id", null,
            _mockStore.Object, _mockOAuth.Object, _sm, CancellationToken.None);

        // Returns HTML that closes popup with error
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_WithErrorParam_ReturnsErrorHtml()
    {
        var result = await GoogleOAuthCallbackEndpoint.Handle(
            null, "session-id", "access_denied",
            _mockStore.Object, _mockOAuth.Object, _sm, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_WhenExchangeFails_ReturnsErrorHtml()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.ConnectGoogle;
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _mockOAuth.Setup(o => o.ExchangeCodeAsync("bad-code", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Token exchange failed"));

        var result = await GoogleOAuthCallbackEndpoint.Handle(
            "bad-code", session.Id, null,
            _mockStore.Object, _mockOAuth.Object, _sm, CancellationToken.None);

        Assert.Null(session.GoogleTokens);
    }
}
```

**Step 2 — Run test to verify failure.**

**Step 3 — Write implementation:**

```csharp
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Features.Onboarding.ConnectGoogle;

public class GoogleOAuthCallbackEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app)
    {
        app.MapGet("/oauth/google/callback", Handle);
    }

    internal static async Task<IResult> Handle(
        string? code,
        string state,
        string? error,
        ISessionStore sessionStore,
        GoogleOAuthService oAuthService,
        OnboardingStateMachine stateMachine,
        CancellationToken ct)
    {
        // The callback renders HTML that uses postMessage to notify the parent window
        if (error is not null)
            return Results.Content(BuildCallbackHtml(false, $"Google authorization denied: {error}"), "text/html");

        if (code is null)
            return Results.Content(BuildCallbackHtml(false, "No authorization code received"), "text/html");

        var session = await sessionStore.LoadAsync(state, ct);
        if (session is null)
            return Results.Content(BuildCallbackHtml(false, "Session not found"), "text/html");

        try
        {
            var tokens = await oAuthService.ExchangeCodeAsync(code, ct);
            session.GoogleTokens = tokens;
            stateMachine.Advance(session, OnboardingState.GenerateSite);
            await sessionStore.SaveAsync(session, ct);

            return Results.Content(
                BuildCallbackHtml(true, $"Connected as {tokens.GoogleName} ({tokens.GoogleEmail})"),
                "text/html");
        }
        catch (InvalidOperationException)
        {
            return Results.Content(BuildCallbackHtml(false, "Failed to connect Google account"), "text/html");
        }
    }

    private static string BuildCallbackHtml(bool success, string message) => $"""
        <!DOCTYPE html>
        <html>
        <head><title>Google OAuth</title></head>
        <body>
            <p>{(success ? "Connected!" : "Error")}: {message}</p>
            <script>
                window.opener?.postMessage({{
                    type: 'google_oauth_callback',
                    success: {success.ToString().ToLowerInvariant()},
                    message: '{message.Replace("'", "\\'")}'
                }}, '*');
                window.close();
            </script>
        </body>
        </html>
        """;
}
```

**Step 4 — Run tests:**

```bash
dotnet test apps/api/RealEstateStar.Api.Tests --filter "GoogleOAuthCallbackEndpointTests"
```

**Step 5 — Commit:** `feat(onboarding): add GoogleOAuthCallback endpoint with popup postMessage flow`

---

### Task A8: Create GoogleAuthCardTool

**Create:** `apps/api/RealEstateStar.Api/Features/Onboarding/Tools/GoogleAuthCardTool.cs`
**Test:** `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Tools/GoogleAuthCardToolTests.cs`

**Step 1 — Write failing test:**

```csharp
using System.Text.Json;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Features.Onboarding.Tools;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Tools;

public class GoogleAuthCardToolTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsOAuthUrl()
    {
        var mockOAuth = new Mock<GoogleOAuthService>(
            new HttpClient(), "client-id", "client-secret", "http://localhost:5000/oauth/google/callback",
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleOAuthService>.Instance);

        mockOAuth.Setup(o => o.BuildAuthorizationUrl(It.IsAny<string>()))
            .Returns("https://accounts.google.com/o/oauth2/v2/auth?test=true");

        var tool = new GoogleAuthCardTool(mockOAuth.Object);
        var session = OnboardingSession.Create(null);

        var result = await tool.ExecuteAsync(default, session, CancellationToken.None);

        Assert.Contains("accounts.google.com", result);
        Assert.Equal("google_auth_card", tool.Name);
    }

    [Fact]
    public async Task ExecuteAsync_IncludesSessionIdInUrl()
    {
        var mockOAuth = new Mock<GoogleOAuthService>(
            new HttpClient(), "client-id", "client-secret", "http://localhost:5000/oauth/google/callback",
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleOAuthService>.Instance);

        mockOAuth.Setup(o => o.BuildAuthorizationUrl(It.IsAny<string>()))
            .Returns((string sid) => $"https://accounts.google.com/o/oauth2/v2/auth?state={sid}");

        var tool = new GoogleAuthCardTool(mockOAuth.Object);
        var session = OnboardingSession.Create(null);

        var result = await tool.ExecuteAsync(default, session, CancellationToken.None);

        Assert.Contains(session.Id, result);
    }
}
```

**Step 2 — Run test to verify failure.**

**Step 3 — Write implementation:**

```csharp
using System.Text.Json;
using RealEstateStar.Api.Features.Onboarding.Services;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class GoogleAuthCardTool(GoogleOAuthService oAuthService) : IOnboardingTool
{
    public string Name => "google_auth_card";

    public Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct)
    {
        var authUrl = oAuthService.BuildAuthorizationUrl(session.Id);
        return Task.FromResult(
            $"Google OAuth URL: {authUrl} — " +
            "Render a google_auth card with a 'Connect Google Account' button that opens this URL in a popup window.");
    }
}
```

**Step 4 — Run tests:**

```bash
dotnet test apps/api/RealEstateStar.Api.Tests --filter "GoogleAuthCardToolTests"
```

**Step 5 — Commit:** `feat(onboarding): add GoogleAuthCardTool for chat-rendered OAuth`

---

### Task A9: Update OnboardingChatService system prompt for ConnectGoogle state

**Modify:** `apps/api/RealEstateStar.Api/Features/Onboarding/Services/OnboardingChatService.cs`
**Test:** `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Services/ChatServiceTests.cs`

**Step 1 — Write failing test:**

Add to `ChatServiceTests.cs` (or verify existing coverage handles the new state — the `BuildSystemPrompt` method is private static, but can be tested via the stream response behavior). The simplest approach: add a test verifying the `BuildToolDefinitions` includes the new tool and `BuildSystemPrompt` handles the new state.

Since `BuildSystemPrompt` and `BuildToolDefinitions` are private static, test indirectly or make them internal for testing. The safest approach: verify the chat service doesn't crash when session is in `ConnectGoogle` state.

Add to `ChatServiceTests.cs`:

```csharp
[Fact]
public void ToolDefinitions_IncludeGoogleAuthCard()
{
    // BuildToolDefinitions is private static, so we test via allowed tools
    var sm = new OnboardingStateMachine();
    var tools = sm.GetAllowedTools(OnboardingState.ConnectGoogle);
    Assert.Contains("google_auth_card", tools);
}
```

**Step 2 — Run test to verify failure (should pass if A2 is done).**

**Step 3 — Write implementation:**

In `OnboardingChatService.cs`, add the ConnectGoogle case to the `BuildSystemPrompt` switch and the `google_auth_card` tool definition to `BuildToolDefinitions`:

In `BuildSystemPrompt`, add to the switch:

```csharp
OnboardingState.ConnectGoogle =>
    "Ask the agent to connect their Google account. Explain this enables sending CMA emails from their Gmail, organizing files in their Drive, and creating lead tracking sheets. Use the google_auth_card tool to present the connection button. After they connect, their Google profile will be cross-validated against their scraped profile.",
```

In `BuildToolDefinitions`, add:

```csharp
["google_auth_card"] = new
{
    name = "google_auth_card",
    description = "Show a Google account connection card with OAuth button",
    input_schema = new { type = "object", properties = new { } }
},
```

Also add Google token info to the system prompt when tokens are present:

```csharp
if (session.GoogleTokens is not null)
{
    sb.AppendLine();
    sb.AppendLine($"Google connected: {session.GoogleTokens.GoogleName} ({session.GoogleTokens.GoogleEmail})");
}
```

**Step 4 — Run tests:**

```bash
dotnet test apps/api/RealEstateStar.Api.Tests --filter "ChatServiceTests"
```

**Step 5 — Commit:** `feat(onboarding): add ConnectGoogle system prompt and google_auth_card tool definition`

---

### Task A10: Create GoogleAuthCard.tsx frontend component

**Create:** `apps/platform/components/chat/GoogleAuthCard.tsx`
**No backend test — frontend component.**

**Implementation:**

```tsx
import { useEffect, useCallback } from "react";

interface GoogleAuthCardProps {
  oauthUrl: string;
  onConnected: (email: string) => void;
  onError?: (error: string) => void;
}

export function GoogleAuthCard({ oauthUrl, onConnected, onError }: GoogleAuthCardProps) {
  const handleMessage = useCallback(
    (event: MessageEvent) => {
      if (event.data?.type !== "google_oauth_callback") return;
      if (event.data.success) {
        onConnected(event.data.message);
      } else {
        onError?.(event.data.message);
      }
    },
    [onConnected, onError]
  );

  useEffect(() => {
    window.addEventListener("message", handleMessage);
    return () => window.removeEventListener("message", handleMessage);
  }, [handleMessage]);

  const openOAuthPopup = () => {
    const width = 500;
    const height = 600;
    const left = window.screenX + (window.outerWidth - width) / 2;
    const top = window.screenY + (window.outerHeight - height) / 2;
    window.open(
      oauthUrl,
      "google-oauth",
      `width=${width},height=${height},left=${left},top=${top}`
    );
  };

  return (
    <div className="bg-gray-800 rounded-xl p-5 max-w-sm space-y-3 text-center">
      <div className="flex justify-center">
        <svg className="w-10 h-10" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
          <path
            d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 01-2.2 3.32v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.1z"
            fill="#4285F4"
          />
          <path
            d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"
            fill="#34A853"
          />
          <path
            d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"
            fill="#FBBC05"
          />
          <path
            d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"
            fill="#EA4335"
          />
        </svg>
      </div>
      <h3 className="text-lg font-semibold text-white">Connect Google Account</h3>
      <p className="text-sm text-gray-400">
        Connect your Google account to enable CMA emails from your Gmail, file organization in Drive,
        and automated lead tracking in Sheets.
      </p>
      <button
        onClick={openOAuthPopup}
        className="w-full px-4 py-2 rounded-lg bg-white hover:bg-gray-100 text-gray-800 font-semibold transition-colors flex items-center justify-center gap-2"
      >
        <svg className="w-5 h-5" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
          <path
            d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 01-2.2 3.32v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.1z"
            fill="#4285F4"
          />
          <path
            d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"
            fill="#34A853"
          />
          <path
            d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"
            fill="#FBBC05"
          />
          <path
            d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"
            fill="#EA4335"
          />
        </svg>
        Connect with Google
      </button>
      <p className="text-xs text-gray-500">
        We request access to Gmail, Drive, Docs, Sheets, and Calendar.
        We only access files we create.
      </p>
    </div>
  );
}
```

**Commit:** `feat(platform): add GoogleAuthCard component with popup OAuth flow`

---

### Task A11: Update MessageRenderer.tsx to handle google_auth type

**Modify:** `apps/platform/components/chat/MessageRenderer.tsx`

**Implementation:**

```tsx
import { MessageBubble } from "./MessageBubble";
import { ProfileCard } from "./ProfileCard";
import { ColorPalette } from "./ColorPalette";
import { SitePreview } from "./SitePreview";
import { FeatureChecklist } from "./FeatureChecklist";
import { PaymentCard } from "./PaymentCard";
import { GoogleAuthCard } from "./GoogleAuthCard";

export interface ChatMessageData {
  role: "user" | "assistant";
  content: string;
  type?: "text" | "profile_card" | "color_palette" | "site_preview" | "feature_checklist" | "payment_card" | "google_auth";
  metadata?: Record<string, unknown>;
}

interface MessageRendererProps {
  message: ChatMessageData;
  onAction?: (action: string, data?: unknown) => void;
}

export function MessageRenderer({ message, onAction }: MessageRendererProps) {
  const meta = message.metadata ?? {};
  const act = onAction ?? (() => {});

  switch (message.type) {
    case "profile_card":
      return (
        <ProfileCard
          name={(meta.name as string) ?? ""}
          brokerage={meta.brokerage as string}
          state={meta.state as string}
          photoUrl={meta.photoUrl as string}
          homesSold={meta.homesSold as number}
          avgRating={meta.avgRating as number}
          onConfirm={() => act("confirm_profile")}
        />
      );
    case "color_palette":
      return (
        <ColorPalette
          primaryColor={(meta.primaryColor as string) ?? "#000000"}
          accentColor={(meta.accentColor as string) ?? "#000000"}
          onConfirm={(colors) => act("confirm_colors", colors)}
        />
      );
    case "google_auth":
      return (
        <GoogleAuthCard
          oauthUrl={(meta.oauthUrl as string) ?? ""}
          onConnected={(email) => act("google_connected", { email })}
          onError={(error) => act("google_auth_error", { error })}
        />
      );
    case "site_preview":
      return (
        <SitePreview
          siteUrl={(meta.siteUrl as string) ?? ""}
          onApprove={() => act("approve_site")}
        />
      );
    case "feature_checklist":
      return <FeatureChecklist />;
    case "payment_card":
      return <PaymentCard onPaymentComplete={() => act("payment_complete")} />;
    default:
      return <MessageBubble role={message.role} content={message.content} />;
  }
}
```

**Commit:** `feat(platform): add google_auth type to MessageRenderer`

---

### Task A12: Register GoogleOAuthService and GoogleAuthCardTool in Program.cs

**Modify:** `apps/api/RealEstateStar.Api/Program.cs`

**Implementation:**

Add after the existing config key validations:

```csharp
var googleClientId = builder.Configuration["Google:ClientId"]
    ?? throw new InvalidOperationException("Google:ClientId configuration is required");
var googleClientSecret = builder.Configuration["Google:ClientSecret"]
    ?? throw new InvalidOperationException("Google:ClientSecret configuration is required");
var googleRedirectUri = builder.Configuration["Google:RedirectUri"]
    ?? "http://localhost:5000/oauth/google/callback";
```

Add to the onboarding services section:

```csharp
builder.Services.AddHttpClient<GoogleOAuthService>();
builder.Services.AddSingleton(sp =>
    new GoogleOAuthService(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(GoogleOAuthService)),
        googleClientId,
        googleClientSecret,
        googleRedirectUri,
        sp.GetRequiredService<ILogger<GoogleOAuthService>>()));
builder.Services.AddSingleton<IOnboardingTool, GoogleAuthCardTool>();
```

Also update the `OnboardingChatService` registration to accept the `GoogleOAuthService` if needed (currently the chat service doesn't need it directly — the tool handles it).

**Commit:** `feat(onboarding): register GoogleOAuthService and GoogleAuthCardTool in DI`

---

## Phase B: Real Site Deployment

### Task B1: Update SiteDeployService to call Wrangler CLI

**Modify:** `apps/api/RealEstateStar.Api/Features/Onboarding/Services/SiteDeployService.cs`
**Test:** `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Services/SiteDeployTests.cs`

**Step 1 — Write failing test:**

Replace `SiteDeployTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class SiteDeployTests : IDisposable
{
    private readonly string _testDir;

    public SiteDeployTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"res-deploy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task GenerateAgentConfigAsync_WritesConfigFile()
    {
        var service = new SiteDeployService(
            NullLogger<SiteDeployService>.Instance,
            "test-api-token",
            "test-account-id");
        var session = OnboardingSession.Create(null);
        session.Profile = new ScrapedProfile
        {
            Name = "Jane Doe",
            Brokerage = "RE/MAX",
            State = "NJ",
            PrimaryColor = "#1e40af",
        };

        var slug = service.GenerateAgentSlug(session);

        Assert.Equal("jane-doe", slug);
    }

    [Fact]
    public void GenerateAgentSlug_WithoutProfile_Throws()
    {
        var service = new SiteDeployService(
            NullLogger<SiteDeployService>.Instance,
            "test-api-token",
            "test-account-id");
        var session = OnboardingSession.Create(null);

        Assert.Throws<InvalidOperationException>(() => service.GenerateAgentSlug(session));
    }

    [Fact]
    public async Task GenerateAgentConfigAsync_SetsSessionAgentConfigId()
    {
        var service = new SiteDeployService(
            NullLogger<SiteDeployService>.Instance,
            "test-api-token",
            "test-account-id");
        var session = OnboardingSession.Create(null);
        session.Profile = new ScrapedProfile
        {
            Name = "Jane Doe",
            Brokerage = "RE/MAX",
            State = "NJ",
            Email = "jane@email.com",
        };

        await service.GenerateAgentConfigAsync(session, CancellationToken.None);

        Assert.Equal("jane-doe", session.AgentConfigId);
    }

    [Fact]
    public void BuildDeployUrl_ReturnsCorrectUrl()
    {
        var url = SiteDeployService.BuildDeployUrl("jane-doe");
        Assert.Equal("https://jane-doe.real-estate-star-agents.pages.dev", url);
    }

    [Fact]
    public async Task DeployAsync_WithoutProfile_Throws()
    {
        var service = new SiteDeployService(
            NullLogger<SiteDeployService>.Instance,
            "test-api-token",
            "test-account-id");
        var session = OnboardingSession.Create(null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeployAsync(session, CancellationToken.None));
    }
}
```

**Step 2 — Run test to verify failure.**

**Step 3 — Write implementation:**

```csharp
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Api.Features.Onboarding.Services;

public class SiteDeployService(
    ILogger<SiteDeployService> logger,
    string cloudflareApiToken,
    string cloudflareAccountId)
{
    private const string ProjectName = "real-estate-star-agents";
    private static readonly TimeSpan DeployTimeout = TimeSpan.FromSeconds(60);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string GenerateAgentSlug(OnboardingSession session)
    {
        var profile = session.Profile
            ?? throw new InvalidOperationException("Cannot deploy site without a scraped profile");

        return (profile.Name ?? "agent").ToLowerInvariant().Replace(" ", "-");
    }

    public static string BuildDeployUrl(string agentSlug) =>
        $"https://{agentSlug}.{ProjectName}.pages.dev";

    public async Task<string> GenerateAgentConfigAsync(OnboardingSession session, CancellationToken ct)
    {
        var profile = session.Profile
            ?? throw new InvalidOperationException("Cannot deploy site without a scraped profile");

        var agentSlug = GenerateAgentSlug(session);
        var configDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "config", "agents");
        Directory.CreateDirectory(configDir);

        var agentConfig = new
        {
            identity = new
            {
                name = profile.Name,
                phone = profile.Phone,
                email = profile.Email,
                brokerage = profile.Brokerage,
                licenseId = profile.LicenseId,
            },
            location = new
            {
                state = profile.State,
                serviceAreas = profile.ServiceAreas ?? [],
                officeAddress = profile.OfficeAddress,
            },
            branding = new
            {
                primaryColor = profile.PrimaryColor ?? "#1e40af",
                accentColor = profile.AccentColor ?? "#10b981",
                logoUrl = profile.LogoUrl,
            },
        };

        var configPath = Path.Combine(configDir, $"{agentSlug}.json");
        var json = JsonSerializer.Serialize(agentConfig, JsonOptions);
        await File.WriteAllTextAsync(configPath, json, ct);

        session.AgentConfigId = agentSlug;
        logger.LogInformation("Generated agent config for {AgentSlug} at {ConfigPath}", agentSlug, configPath);

        return configPath;
    }

    public async Task<string> DeployAsync(OnboardingSession session, CancellationToken ct)
    {
        var profile = session.Profile
            ?? throw new InvalidOperationException("Cannot deploy site without a scraped profile");

        var agentSlug = GenerateAgentSlug(session);

        // Generate agent config first
        await GenerateAgentConfigAsync(session, ct);

        // Deploy via Wrangler CLI
        var agentSitePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "apps", "agent-site", ".next");

        var psi = new ProcessStartInfo("npx")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("wrangler");
        psi.ArgumentList.Add("pages");
        psi.ArgumentList.Add("deploy");
        psi.ArgumentList.Add(agentSitePath);
        psi.ArgumentList.Add("--project-name");
        psi.ArgumentList.Add(ProjectName);
        psi.ArgumentList.Add("--branch");
        psi.ArgumentList.Add(agentSlug);
        psi.ArgumentList.Add("--commit-message");
        psi.ArgumentList.Add($"Deploy {profile.Name}'s site");
        psi.Environment["CLOUDFLARE_API_TOKEN"] = cloudflareApiToken;
        psi.Environment["CLOUDFLARE_ACCOUNT_ID"] = cloudflareAccountId;

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(DeployTimeout);
        await process.WaitForExitAsync(timeoutCts.Token);

        if (process.ExitCode != 0)
        {
            logger.LogError("Wrangler deploy failed (exit {ExitCode}): {Stderr}", process.ExitCode, stderr);
            throw new InvalidOperationException("Site deployment failed");
        }

        var siteUrl = BuildDeployUrl(agentSlug);
        session.SiteUrl = siteUrl;

        logger.LogInformation("Deployed site for {AgentSlug} at {SiteUrl}", agentSlug, siteUrl);
        return siteUrl;
    }
}
```

**Step 4 — Run tests:**

```bash
dotnet test apps/api/RealEstateStar.Api.Tests --filter "SiteDeployTests"
```

**Step 5 — Commit:** `feat(onboarding): wire SiteDeployService to Cloudflare Pages via Wrangler CLI`

---

### Task B2: Update DeploySiteTool to use real deployment

**Modify:** `apps/api/RealEstateStar.Api/Features/Onboarding/Tools/DeploySiteTool.cs`
**Test:** `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Tools/DeploySiteToolTests.cs` (new)

**Step 1 — Write failing test:**

```csharp
using System.Text.Json;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Features.Onboarding.Tools;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Tools;

public class DeploySiteToolTests
{
    [Fact]
    public async Task ExecuteAsync_CallsDeployService_ReturnsSiteUrl()
    {
        var mockDeploy = new Mock<SiteDeployService>(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SiteDeployService>.Instance,
            "token", "account-id");

        var session = OnboardingSession.Create(null);
        session.Profile = new ScrapedProfile { Name = "Jane Doe" };

        mockDeploy.Setup(d => d.DeployAsync(session, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://jane-doe.real-estate-star-agents.pages.dev");

        var tool = new DeploySiteTool(mockDeploy.Object);

        var result = await tool.ExecuteAsync(default, session, CancellationToken.None);

        Assert.Contains("jane-doe.real-estate-star-agents.pages.dev", result);
        Assert.Equal("deploy_site", tool.Name);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeployFails_ReturnsErrorMessage()
    {
        var mockDeploy = new Mock<SiteDeployService>(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SiteDeployService>.Instance,
            "token", "account-id");

        var session = OnboardingSession.Create(null);
        session.Profile = new ScrapedProfile { Name = "Jane Doe" };

        mockDeploy.Setup(d => d.DeployAsync(session, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Site deployment failed"));

        var tool = new DeploySiteTool(mockDeploy.Object);

        var result = await tool.ExecuteAsync(default, session, CancellationToken.None);

        Assert.Contains("failed", result, StringComparison.OrdinalIgnoreCase);
    }
}
```

**Step 2 — Run test to verify failure.**

**Step 3 — Write implementation:**

```csharp
using System.Text.Json;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class DeploySiteTool(Services.SiteDeployService siteDeployService) : IOnboardingTool
{
    public string Name => "deploy_site";

    public async Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct)
    {
        try
        {
            var siteUrl = await siteDeployService.DeployAsync(session, ct);
            return $"Site deployed successfully at {siteUrl}. Render a site_preview card with this URL.";
        }
        catch (InvalidOperationException ex)
        {
            return $"Site deployment failed: {ex.Message}. Ask the agent to try again.";
        }
    }
}
```

**Step 4 — Run tests:**

```bash
dotnet test apps/api/RealEstateStar.Api.Tests --filter "DeploySiteToolTests"
```

**Step 5 — Commit:** `feat(onboarding): wire DeploySiteTool to real Cloudflare deployment`

---

### Task B3: Update Program.cs with Cloudflare config keys

**Modify:** `apps/api/RealEstateStar.Api/Program.cs`

**Implementation:**

Add config key validation:

```csharp
var cloudflareApiToken = builder.Configuration["Cloudflare:ApiToken"]
    ?? throw new InvalidOperationException("Cloudflare:ApiToken configuration is required");
var cloudflareAccountId = builder.Configuration["Cloudflare:AccountId"]
    ?? throw new InvalidOperationException("Cloudflare:AccountId configuration is required");
```

Update the SiteDeployService registration:

```csharp
builder.Services.AddSingleton(sp =>
    new SiteDeployService(
        sp.GetRequiredService<ILogger<SiteDeployService>>(),
        cloudflareApiToken,
        cloudflareAccountId));
```

**Commit:** `feat(onboarding): register SiteDeployService with Cloudflare config`

---

## Phase C: Real CMA Pipeline Integration

### Task C1: Update SubmitCmaFormTool to invoke CmaPipeline directly

**Modify:** `apps/api/RealEstateStar.Api/Features/Onboarding/Tools/SubmitCmaFormTool.cs`
**Test:** `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Tools/CmaToolTests.cs`

**Step 1 — Write failing test:**

Replace `CmaToolTests.cs`:

```csharp
using System.Text.Json;
using Moq;
using RealEstateStar.Api.Features.Cma;
using RealEstateStar.Api.Features.Cma.Services;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Tools;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Tools;

public class CmaToolTests
{
    [Fact]
    public async Task SubmitCmaFormTool_WithGoogleTokens_InvokesPipeline()
    {
        var mockPipeline = new Mock<CmaPipeline>(
            null!, null!, null!, null!, null!, null!, null);

        mockPipeline.Setup(p => p.ExecuteAsync(
                It.IsAny<CmaJob>(),
                It.IsAny<string>(),
                It.IsAny<Lead>(),
                It.IsAny<Func<CmaJobStatus, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tool = new SubmitCmaFormTool(mockPipeline.Object);
        var session = OnboardingSession.Create(null);
        session.AgentConfigId = "jane-doe";
        session.GoogleTokens = new GoogleTokens
        {
            AccessToken = "ya29.test",
            RefreshToken = "1//test",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Scopes = ["gmail.send"],
            GoogleEmail = "agent@gmail.com",
            GoogleName = "Jane Doe",
        };

        var json = JsonSerializer.Deserialize<JsonElement>("""{"address":"456 Oak Ave, Newark NJ 07102"}""");
        var result = await tool.ExecuteAsync(json, session, CancellationToken.None);

        Assert.Contains("CMA pipeline completed", result);
        mockPipeline.Verify(p => p.ExecuteAsync(
            It.IsAny<CmaJob>(),
            "jane-doe",
            It.Is<Lead>(l => l.Address == "456 Oak Ave"),
            It.IsAny<Func<CmaJobStatus, Task>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitCmaFormTool_WithoutGoogleTokens_ReturnsFallbackMessage()
    {
        var mockPipeline = new Mock<CmaPipeline>(
            null!, null!, null!, null!, null!, null!, null);

        var tool = new SubmitCmaFormTool(mockPipeline.Object);
        var session = OnboardingSession.Create(null);

        var result = await tool.ExecuteAsync(default, session, CancellationToken.None);

        Assert.Contains("Google account", result);
        mockPipeline.Verify(p => p.ExecuteAsync(
            It.IsAny<CmaJob>(),
            It.IsAny<string>(),
            It.IsAny<Lead>(),
            It.IsAny<Func<CmaJobStatus, Task>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitCmaFormTool_WhenPipelineFails_ReturnsError()
    {
        var mockPipeline = new Mock<CmaPipeline>(
            null!, null!, null!, null!, null!, null!, null);

        mockPipeline.Setup(p => p.ExecuteAsync(
                It.IsAny<CmaJob>(),
                It.IsAny<string>(),
                It.IsAny<Lead>(),
                It.IsAny<Func<CmaJobStatus, Task>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Pipeline step failed"));

        var tool = new SubmitCmaFormTool(mockPipeline.Object);
        var session = OnboardingSession.Create(null);
        session.AgentConfigId = "jane-doe";
        session.GoogleTokens = new GoogleTokens
        {
            AccessToken = "ya29.test",
            RefreshToken = "1//test",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Scopes = ["gmail.send"],
            GoogleEmail = "agent@gmail.com",
            GoogleName = "Jane Doe",
        };

        var json = JsonSerializer.Deserialize<JsonElement>("""{"address":"456 Oak Ave, Newark NJ 07102"}""");
        var result = await tool.ExecuteAsync(json, session, CancellationToken.None);

        Assert.Contains("failed", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitCmaFormTool_UsesDefaultAddress_WhenNotProvided()
    {
        var mockPipeline = new Mock<CmaPipeline>(
            null!, null!, null!, null!, null!, null!, null);
        mockPipeline.Setup(p => p.ExecuteAsync(
                It.IsAny<CmaJob>(),
                It.IsAny<string>(),
                It.IsAny<Lead>(),
                It.IsAny<Func<CmaJobStatus, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tool = new SubmitCmaFormTool(mockPipeline.Object);
        var session = OnboardingSession.Create(null);
        session.AgentConfigId = "jane-doe";
        session.GoogleTokens = new GoogleTokens
        {
            AccessToken = "ya29.test",
            RefreshToken = "1//test",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Scopes = ["gmail.send"],
            GoogleEmail = "agent@gmail.com",
            GoogleName = "Jane Doe",
        };

        var result = await tool.ExecuteAsync(default, session, CancellationToken.None);

        mockPipeline.Verify(p => p.ExecuteAsync(
            It.IsAny<CmaJob>(),
            "jane-doe",
            It.Is<Lead>(l => l.Address == "123 Main St"),
            It.IsAny<Func<CmaJobStatus, Task>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

**Step 2 — Run test to verify failure.**

**Step 3 — Write implementation:**

```csharp
using System.Text.Json;
using RealEstateStar.Api.Features.Cma;
using RealEstateStar.Api.Features.Cma.Services;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class SubmitCmaFormTool(CmaPipeline cmaPipeline) : IOnboardingTool
{
    public string Name => "submit_cma_form";

    public async Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct)
    {
        if (session.GoogleTokens is null)
            return "Cannot run CMA demo — Google account not connected. Please connect your Google account first.";

        var address = parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("address", out var a)
            ? a.GetString() ?? "123 Main St, Anytown NJ 07001"
            : "123 Main St, Anytown NJ 07001";

        var parts = ParseAddress(address);
        var agentId = session.AgentConfigId ?? "demo-agent";

        // Demo mode: email goes to the agent themselves
        var lead = new Lead
        {
            FirstName = "Demo",
            LastName = "Lead",
            Email = session.GoogleTokens.GoogleEmail,
            Phone = "(555) 555-0100",
            Address = parts.Street,
            City = parts.City,
            State = parts.State,
            Zip = parts.Zip,
            Beds = 3,
            Baths = 2,
            Sqft = 1800,
            Timeline = "Just curious",
        };

        var job = CmaJob.Create(lead);

        try
        {
            await cmaPipeline.ExecuteAsync(
                job,
                agentId,
                lead,
                _ => Task.CompletedTask,
                ct);

            return $"CMA pipeline completed for {address}. " +
                   "Check your email inbox — that's what your sellers receive. " +
                   "Check your Google Drive — I've created your lead management folder structure. " +
                   "Check your Sheets — every lead is tracked automatically. " +
                   "Tell the agent to check all three right now!";
        }
        catch (Exception)
        {
            return $"CMA demo failed for {address}. The pipeline encountered an error. " +
                   "Ask the agent to try again or proceed to the next step.";
        }
    }

    private static (string Street, string City, string State, string Zip) ParseAddress(string address)
    {
        // Simple parser: "123 Main St, City ST 12345"
        var commaParts = address.Split(',', 2, StringSplitOptions.TrimEntries);
        var street = commaParts[0];

        if (commaParts.Length < 2)
            return (street, "Anytown", "NJ", "07001");

        var cityStateZip = commaParts[1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var city = cityStateZip.Length > 0 ? string.Join(" ", cityStateZip[..^2]) : "Anytown";
        var state = cityStateZip.Length >= 2 ? cityStateZip[^2] : "NJ";
        var zip = cityStateZip.Length >= 1 ? cityStateZip[^1] : "07001";

        // If zip doesn't look like a zip, it's probably the state and there's no zip
        if (zip.Length != 5 || !zip.All(char.IsDigit))
        {
            state = zip;
            zip = "07001";
            city = cityStateZip.Length > 1 ? string.Join(" ", cityStateZip[..^1]) : "Anytown";
        }

        return (street, city, state, zip);
    }
}
```

**Step 4 — Run tests:**

```bash
dotnet test apps/api/RealEstateStar.Api.Tests --filter "CmaToolTests"
```

**Step 5 — Commit:** `feat(onboarding): wire SubmitCmaFormTool to real CMA pipeline in demo mode`

---

### Task C2: Update OnboardingChatService system prompt for DemoCma state

**Modify:** `apps/api/RealEstateStar.Api/Features/Onboarding/Services/OnboardingChatService.cs`

Update the DemoCma case in `BuildSystemPrompt`:

```csharp
OnboardingState.DemoCma =>
    "Run a CMA demo using a sample address to show the agent what the platform can do. " +
    "Use submit_cma_form with a real address in their service area. " +
    "The demo sends the CMA email to the AGENT's own Gmail (not a lead), " +
    "creates their Drive folder structure, and logs the demo lead in Sheets. " +
    "Tell the agent to check their inbox, Drive, and Sheets after the demo completes. " +
    "Narrate each step as it happens — this is the big reveal moment.",
```

**Commit:** `feat(onboarding): update DemoCma system prompt for live pipeline narration`

---

## Phase D: Real Stripe Checkout

### Task D1: Add Stripe.net NuGet package

**Run:**

```bash
cd apps/api/RealEstateStar.Api && dotnet add package Stripe.net
```

**Commit:** `chore(api): add Stripe.net NuGet package`

---

### Task D2: Replace StripeService stub with real Checkout Session creation

**Modify:** `apps/api/RealEstateStar.Api/Features/Onboarding/Services/StripeService.cs`
**Test:** `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Services/StripeServiceTests.cs`

**Step 1 — Write failing test:**

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class StripeServiceTests
{
    [Fact]
    public void Constructor_WithMissingSecretKey_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new StripeService("", "whsec_test", "price_test", "http://localhost:3000",
                NullLogger<StripeService>.Instance));
    }

    [Fact]
    public void Constructor_WithMissingWebhookSecret_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new StripeService("sk_test_123", "", "price_test", "http://localhost:3000",
                NullLogger<StripeService>.Instance));
    }

    [Fact]
    public void Constructor_WithMissingPriceId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new StripeService("sk_test_123", "whsec_test", "", "http://localhost:3000",
                NullLogger<StripeService>.Instance));
    }

    [Fact]
    public void Constructor_WithValidConfig_DoesNotThrow()
    {
        var service = new StripeService(
            "sk_test_123", "whsec_test", "price_test", "http://localhost:3000",
            NullLogger<StripeService>.Instance);

        Assert.NotNull(service);
    }

    [Fact]
    public void WebhookSecret_IsAccessible()
    {
        var service = new StripeService(
            "sk_test_123", "whsec_test", "price_test", "http://localhost:3000",
            NullLogger<StripeService>.Instance);

        Assert.Equal("whsec_test", service.WebhookSecret);
    }
}
```

**Step 2 — Run test to verify failure.**

**Step 3 — Write implementation:**

```csharp
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;

namespace RealEstateStar.Api.Features.Onboarding.Services;

public class StripeService
{
    private readonly string _priceId;
    private readonly string _frontendBaseUrl;
    private readonly SessionService _sessionService;
    private readonly ILogger<StripeService> _logger;

    public string WebhookSecret { get; }

    public StripeService(
        string secretKey,
        string webhookSecret,
        string priceId,
        string frontendBaseUrl,
        ILogger<StripeService> logger)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new ArgumentException("Stripe secret key is required", nameof(secretKey));
        if (string.IsNullOrWhiteSpace(webhookSecret))
            throw new ArgumentException("Stripe webhook secret is required", nameof(webhookSecret));
        if (string.IsNullOrWhiteSpace(priceId))
            throw new ArgumentException("Stripe price ID is required", nameof(priceId));

        StripeConfiguration.ApiKey = secretKey;
        WebhookSecret = webhookSecret;
        _priceId = priceId;
        _frontendBaseUrl = frontendBaseUrl;
        _sessionService = new SessionService();
        _logger = logger;
    }

    public async Task<string> CreateCheckoutSessionAsync(OnboardingSession session, CancellationToken ct)
    {
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Price = _priceId,
                    Quantity = 1,
                },
            ],
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                SetupFutureUsage = "off_session",
            },
            SuccessUrl = $"{_frontendBaseUrl}/onboard?payment=success&session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{_frontendBaseUrl}/onboard?payment=cancelled",
            ClientReferenceId = session.Id,
            Metadata = new Dictionary<string, string>
            {
                ["onboarding_session_id"] = session.Id,
                ["agent_name"] = session.Profile?.Name ?? "unknown",
            },
        };

        var checkoutSession = await _sessionService.CreateAsync(options, cancellationToken: ct);
        session.StripeSessionId = checkoutSession.Id;

        _logger.LogInformation("Created Stripe Checkout Session {CheckoutSessionId} for onboarding session {SessionId}",
            checkoutSession.Id, session.Id);

        return checkoutSession.Url;
    }

    public bool ValidateWebhookEvent(string json, string signature, out Event stripeEvent)
    {
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, signature, WebhookSecret);
            return true;
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature validation failed");
            stripeEvent = null!;
            return false;
        }
    }
}
```

**Step 4 — Run tests:**

```bash
dotnet test apps/api/RealEstateStar.Api.Tests --filter "StripeServiceTests"
```

**Step 5 — Commit:** `feat(onboarding): replace StripeService stub with real Stripe.net Checkout Session`

---

### Task D3: Create StripeWebhookEndpoint

**Create:** `apps/api/RealEstateStar.Api/Features/Onboarding/Webhooks/StripeWebhookEndpoint.cs`
**Test:** `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Webhooks/StripeWebhookEndpointTests.cs`

**Step 1 — Write failing test:**

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Features.Onboarding.Webhooks;
using Stripe;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Webhooks;

public class StripeWebhookEndpointTests
{
    private readonly Mock<ISessionStore> _mockStore = new();
    private readonly Mock<StripeService> _mockStripe;
    private readonly OnboardingStateMachine _sm = new();

    public StripeWebhookEndpointTests()
    {
        _mockStripe = new Mock<StripeService>(
            "sk_test_123", "whsec_test", "price_test", "http://localhost:3000",
            Microsoft.Extensions.Logging.Abstractions.NullLogger<StripeService>.Instance);
    }

    [Fact]
    public async Task Handle_WithInvalidSignature_Returns400()
    {
        Event? stripeEvent = null;
        _mockStripe.Setup(s => s.ValidateWebhookEvent(
                It.IsAny<string>(), It.IsAny<string>(), out stripeEvent!))
            .Returns(false);

        var result = await StripeWebhookEndpoint.Handle(
            "body", "bad-sig", _mockStripe.Object, _mockStore.Object, _sm, CancellationToken.None);

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task Handle_WithNonCheckoutEvent_Returns200()
    {
        var evt = new Event { Type = "payment_intent.succeeded" };
        _mockStripe.Setup(s => s.ValidateWebhookEvent(
                It.IsAny<string>(), It.IsAny<string>(), out evt))
            .Returns(true);

        var result = await StripeWebhookEndpoint.Handle(
            "body", "sig", _mockStripe.Object, _mockStore.Object, _sm, CancellationToken.None);

        Assert.IsType<Ok>(result);
    }
}
```

**Step 2 — Run test to verify failure.**

**Step 3 — Write implementation:**

```csharp
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Infrastructure;
using Stripe;
using Stripe.Checkout;

namespace RealEstateStar.Api.Features.Onboarding.Webhooks;

public class StripeWebhookEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app)
    {
        app.MapPost("/webhooks/stripe", Handle);
    }

    internal static async Task<IResult> Handle(
        string body,
        string stripeSignature,
        StripeService stripeService,
        ISessionStore sessionStore,
        OnboardingStateMachine stateMachine,
        CancellationToken ct)
    {
        if (!stripeService.ValidateWebhookEvent(body, stripeSignature, out var stripeEvent))
            return Results.BadRequest("Invalid Stripe webhook signature");

        if (stripeEvent.Type != EventTypes.CheckoutSessionCompleted)
            return Results.Ok();

        var checkoutSession = stripeEvent.Data.Object as Session;
        if (checkoutSession is null)
            return Results.Ok();

        var onboardingSessionId = checkoutSession.ClientReferenceId
            ?? checkoutSession.Metadata?.GetValueOrDefault("onboarding_session_id");

        if (onboardingSessionId is null)
            return Results.Ok();

        var session = await sessionStore.LoadAsync(onboardingSessionId, ct);
        if (session is null)
            return Results.Ok();

        if (session.CurrentState == OnboardingState.CollectPayment)
        {
            stateMachine.Advance(session, OnboardingState.TrialActivated);
            await sessionStore.SaveAsync(session, ct);
        }

        return Results.Ok();
    }
}
```

Note: The raw body and Stripe-Signature header need special handling in `MapEndpoint`. Update the endpoint mapping to read raw body:

```csharp
public void MapEndpoint(WebApplication app)
{
    app.MapPost("/webhooks/stripe", async (
        HttpContext context,
        StripeService stripeService,
        ISessionStore sessionStore,
        OnboardingStateMachine stateMachine,
        CancellationToken ct) =>
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        var signature = context.Request.Headers["Stripe-Signature"].FirstOrDefault() ?? "";

        return await Handle(body, signature, stripeService, sessionStore, stateMachine, ct);
    });
}
```

**Step 4 — Run tests:**

```bash
dotnet test apps/api/RealEstateStar.Api.Tests --filter "StripeWebhookEndpointTests"
```

**Step 5 — Commit:** `feat(onboarding): add StripeWebhookEndpoint for checkout.session.completed`

---

### Task D4: Update CreateStripeSessionTool to return real checkout URL

**Modify:** `apps/api/RealEstateStar.Api/Features/Onboarding/Tools/CreateStripeSessionTool.cs`
**Test:** `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Tools/StripeToolTests.cs` (new)

**Step 1 — Write failing test:**

```csharp
using System.Text.Json;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Features.Onboarding.Tools;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Tools;

public class StripeToolTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsCheckoutUrl()
    {
        var mockStripe = new Mock<StripeService>(
            "sk_test_123", "whsec_test", "price_test", "http://localhost:3000",
            Microsoft.Extensions.Logging.Abstractions.NullLogger<StripeService>.Instance);

        var session = OnboardingSession.Create(null);
        mockStripe.Setup(s => s.CreateCheckoutSessionAsync(session, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://checkout.stripe.com/pay/cs_test_123");

        var tool = new CreateStripeSessionTool(mockStripe.Object);

        var result = await tool.ExecuteAsync(default, session, CancellationToken.None);

        Assert.Contains("checkout.stripe.com", result);
        Assert.Equal("create_stripe_session", tool.Name);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStripeFails_ReturnsError()
    {
        var mockStripe = new Mock<StripeService>(
            "sk_test_123", "whsec_test", "price_test", "http://localhost:3000",
            Microsoft.Extensions.Logging.Abstractions.NullLogger<StripeService>.Instance);

        var session = OnboardingSession.Create(null);
        mockStripe.Setup(s => s.CreateCheckoutSessionAsync(session, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Stripe.StripeException("API error"));

        var tool = new CreateStripeSessionTool(mockStripe.Object);

        var result = await tool.ExecuteAsync(default, session, CancellationToken.None);

        Assert.Contains("failed", result, StringComparison.OrdinalIgnoreCase);
    }
}
```

**Step 2 — Run test to verify failure.**

**Step 3 — Write implementation:**

```csharp
using System.Text.Json;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class CreateStripeSessionTool(Services.StripeService stripeService) : IOnboardingTool
{
    public string Name => "create_stripe_session";

    public async Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct)
    {
        try
        {
            var checkoutUrl = await stripeService.CreateCheckoutSessionAsync(session, ct);
            return $"Stripe Checkout URL: {checkoutUrl} — " +
                   "Render a payment_card with a 'Start Free Trial' button that redirects to this URL.";
        }
        catch (Exception)
        {
            return "Failed to create Stripe checkout session. Please try again.";
        }
    }
}
```

**Step 4 — Run tests:**

```bash
dotnet test apps/api/RealEstateStar.Api.Tests --filter "StripeToolTests"
```

**Step 5 — Commit:** `feat(onboarding): wire CreateStripeSessionTool to real Stripe Checkout`

---

### Task D5: Update PaymentCard.tsx to redirect to Stripe Checkout

**Modify:** `apps/platform/components/chat/PaymentCard.tsx`

**Implementation:**

```tsx
interface PaymentCardProps {
  checkoutUrl?: string;
  onPaymentComplete: () => void;
}

export function PaymentCard({ checkoutUrl, onPaymentComplete }: PaymentCardProps) {
  const handleClick = () => {
    if (checkoutUrl) {
      window.location.href = checkoutUrl;
    } else {
      onPaymentComplete();
    }
  };

  return (
    <div className="bg-gray-800 rounded-xl p-5 max-w-sm space-y-3 text-center">
      <h3 className="text-2xl font-bold text-white">$900</h3>
      <p className="text-gray-400">One-time payment. Everything included.</p>
      <button
        onClick={handleClick}
        className="w-full px-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-semibold transition-colors"
      >
        Start Free Trial
      </button>
      <p className="text-xs text-gray-500">
        7-day trial. No charge until trial ends.
      </p>
    </div>
  );
}
```

Update `MessageRenderer.tsx` to pass `checkoutUrl`:

```tsx
case "payment_card":
  return (
    <PaymentCard
      checkoutUrl={meta.checkoutUrl as string}
      onPaymentComplete={() => act("payment_complete")}
    />
  );
```

**Commit:** `feat(platform): wire PaymentCard to redirect to Stripe Checkout URL`

---

### Task D6: Update Program.cs with Stripe config keys

**Modify:** `apps/api/RealEstateStar.Api/Program.cs`

**Implementation:**

Add config key validation:

```csharp
var stripeSecretKey = builder.Configuration["Stripe:SecretKey"]
    ?? throw new InvalidOperationException("Stripe:SecretKey configuration is required");
var stripeWebhookSecret = builder.Configuration["Stripe:WebhookSecret"]
    ?? throw new InvalidOperationException("Stripe:WebhookSecret configuration is required");
var stripePriceId = builder.Configuration["Stripe:PriceId"]
    ?? throw new InvalidOperationException("Stripe:PriceId configuration is required");
var frontendBaseUrl = builder.Configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
```

Update StripeService registration:

```csharp
builder.Services.AddSingleton(sp =>
    new StripeService(
        stripeSecretKey,
        stripeWebhookSecret,
        stripePriceId,
        frontendBaseUrl,
        sp.GetRequiredService<ILogger<StripeService>>()));
```

**Commit:** `feat(onboarding): register StripeService with real Stripe config`

---

## Phase E: Configuration & Local Dev

### Task E1: Update appsettings.Development.json

**Modify:** `apps/api/RealEstateStar.Api/appsettings.Development.json`

**Implementation:**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "AllowedHosts": "localhost",
  "Otel": {
    "Endpoint": "http://localhost:4317"
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000"]
  },
  "Anthropic": { "ApiKey": "dev-placeholder" },
  "Attom": { "ApiKey": "dev-placeholder" },
  "Google": {
    "ClientId": "dev-placeholder",
    "ClientSecret": "dev-placeholder",
    "RedirectUri": "http://localhost:5000/oauth/google/callback"
  },
  "Cloudflare": {
    "ApiToken": "dev-placeholder",
    "AccountId": "dev-placeholder"
  },
  "Stripe": {
    "SecretKey": "dev-placeholder",
    "WebhookSecret": "dev-placeholder",
    "PriceId": "dev-placeholder"
  },
  "Frontend": {
    "BaseUrl": "http://localhost:3000"
  }
}
```

**Commit:** `chore(api): add all integration config placeholders to appsettings.Development.json`

---

### Task E2: Update .env.local.example

**Modify:** `apps/platform/.env.local.example`

**Implementation:**

```env
# API URL — point to your .NET API server
NEXT_PUBLIC_API_URL=http://localhost:5000

# Stripe publishable key (pk_test_...) — for frontend Checkout redirect
NEXT_PUBLIC_STRIPE_KEY=pk_test_placeholder
```

**Commit:** `chore(platform): add NEXT_PUBLIC_STRIPE_KEY to .env.local.example`

---

### Task E3: Update Program.cs DI registrations for all new services

**Modify:** `apps/api/RealEstateStar.Api/Program.cs`

This task consolidates all DI changes from Phases A-D. The final onboarding services section in `Program.cs` should look like:

```csharp
// Configuration keys
var anthropicKey = builder.Configuration["Anthropic:ApiKey"]
    ?? throw new InvalidOperationException("Anthropic:ApiKey configuration is required");
var attomKey = builder.Configuration["Attom:ApiKey"]
    ?? throw new InvalidOperationException("Attom:ApiKey configuration is required");
var googleClientId = builder.Configuration["Google:ClientId"]
    ?? throw new InvalidOperationException("Google:ClientId configuration is required");
var googleClientSecret = builder.Configuration["Google:ClientSecret"]
    ?? throw new InvalidOperationException("Google:ClientSecret configuration is required");
var googleRedirectUri = builder.Configuration["Google:RedirectUri"]
    ?? "http://localhost:5000/oauth/google/callback";
var cloudflareApiToken = builder.Configuration["Cloudflare:ApiToken"]
    ?? throw new InvalidOperationException("Cloudflare:ApiToken configuration is required");
var cloudflareAccountId = builder.Configuration["Cloudflare:AccountId"]
    ?? throw new InvalidOperationException("Cloudflare:AccountId configuration is required");
var stripeSecretKey = builder.Configuration["Stripe:SecretKey"]
    ?? throw new InvalidOperationException("Stripe:SecretKey configuration is required");
var stripeWebhookSecret = builder.Configuration["Stripe:WebhookSecret"]
    ?? throw new InvalidOperationException("Stripe:WebhookSecret configuration is required");
var stripePriceId = builder.Configuration["Stripe:PriceId"]
    ?? throw new InvalidOperationException("Stripe:PriceId configuration is required");
var frontendBaseUrl = builder.Configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";

// Onboarding services
builder.Services.AddSingleton<ISessionStore, JsonFileSessionStore>();
builder.Services.AddSingleton<OnboardingStateMachine>();

builder.Services.AddHttpClient<ProfileScraperService>();
builder.Services.AddSingleton<IProfileScraper>(sp =>
    new ProfileScraperService(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(ProfileScraperService)),
        anthropicKey,
        sp.GetRequiredService<ILogger<ProfileScraperService>>()));

// Google OAuth
builder.Services.AddHttpClient<GoogleOAuthService>();
builder.Services.AddSingleton(sp =>
    new GoogleOAuthService(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(GoogleOAuthService)),
        googleClientId,
        googleClientSecret,
        googleRedirectUri,
        sp.GetRequiredService<ILogger<GoogleOAuthService>>()));

// Site deploy (Cloudflare Pages)
builder.Services.AddSingleton(sp =>
    new SiteDeployService(
        sp.GetRequiredService<ILogger<SiteDeployService>>(),
        cloudflareApiToken,
        cloudflareAccountId));

// Stripe
builder.Services.AddSingleton(sp =>
    new StripeService(
        stripeSecretKey,
        stripeWebhookSecret,
        stripePriceId,
        frontendBaseUrl,
        sp.GetRequiredService<ILogger<StripeService>>()));

// Onboarding tools
builder.Services.AddSingleton<IOnboardingTool, ScrapeUrlTool>();
builder.Services.AddSingleton<IOnboardingTool, UpdateProfileTool>();
builder.Services.AddSingleton<IOnboardingTool, SetBrandingTool>();
builder.Services.AddSingleton<IOnboardingTool, GoogleAuthCardTool>();
builder.Services.AddSingleton<IOnboardingTool, DeploySiteTool>();
builder.Services.AddSingleton<IOnboardingTool, SubmitCmaFormTool>();
builder.Services.AddSingleton<IOnboardingTool, CreateStripeSessionTool>();
builder.Services.AddSingleton<ToolDispatcher>();
builder.Services.AddSingleton<DomainService>();

builder.Services.AddHttpClient<OnboardingChatService>();
builder.Services.AddSingleton(sp =>
    new OnboardingChatService(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OnboardingChatService)),
        anthropicKey,
        sp.GetRequiredService<OnboardingStateMachine>(),
        sp.GetRequiredService<ToolDispatcher>(),
        sp.GetRequiredService<ILogger<OnboardingChatService>>()));
builder.Services.AddHostedService<TrialExpiryService>();
```

**Commit:** `feat(onboarding): consolidate all DI registrations for live integrations`

---

### Task E4: CORS update (if needed)

The existing CORS config already allows `localhost:3000`. For production deployment (Railway + Cloudflare Pages), add the production origins:

**Modify:** `apps/api/RealEstateStar.Api/appsettings.json`

Add production CORS origins when deploying:

```json
"Cors": {
    "AllowedOrigins": [
        "http://localhost:3000",
        "https://real-estate-star.pages.dev"
    ]
}
```

**Commit:** `chore(api): add production CORS origin for Cloudflare Pages`

---

### Task E5: Smoke test — full flow end-to-end locally

**Manual verification checklist:**

1. Start API: `cd apps/api/RealEstateStar.Api && dotnet run`
2. Start frontend: `cd apps/platform && npm run dev`
3. Open `http://localhost:3000`
4. Create onboarding session
5. Paste a Zillow/Realtor profile URL
6. Confirm identity, set branding
7. Click "Connect Google Account" — verify popup opens, OAuth flow completes
8. Verify site deploys to Cloudflare Pages (real URL loads)
9. Verify CMA demo runs — check Gmail inbox, Google Drive, Google Sheets
10. Click "Start Free Trial" — verify Stripe Checkout opens (test mode)
11. Complete payment with test card `4242 4242 4242 4242`
12. Verify redirect back to `/onboard?payment=success`
13. Verify session state is `TrialActivated`

---

## File Summary

### New Files

| File | Phase |
|------|-------|
| `Features/Onboarding/GoogleTokens.cs` | A |
| `Features/Onboarding/ConnectGoogle/StartGoogleOAuthEndpoint.cs` | A |
| `Features/Onboarding/ConnectGoogle/GoogleOAuthCallbackEndpoint.cs` | A |
| `Features/Onboarding/Services/GoogleOAuthService.cs` | A |
| `Features/Onboarding/Tools/GoogleAuthCardTool.cs` | A |
| `Features/Onboarding/Webhooks/StripeWebhookEndpoint.cs` | D |
| `apps/platform/components/chat/GoogleAuthCard.tsx` | A |
| **Tests:** | |
| `Tests/Features/Onboarding/GoogleTokensTests.cs` | A |
| `Tests/Features/Onboarding/ConnectGoogle/StartGoogleOAuthEndpointTests.cs` | A |
| `Tests/Features/Onboarding/ConnectGoogle/GoogleOAuthCallbackEndpointTests.cs` | A |
| `Tests/Features/Onboarding/Services/GoogleOAuthServiceTests.cs` | A |
| `Tests/Features/Onboarding/Tools/GoogleAuthCardToolTests.cs` | A |
| `Tests/Features/Onboarding/Tools/DeploySiteToolTests.cs` | B |
| `Tests/Features/Onboarding/Tools/StripeToolTests.cs` | D |
| `Tests/Features/Onboarding/Webhooks/StripeWebhookEndpointTests.cs` | D |

### Modified Files

| File | Phase |
|------|-------|
| `Features/Onboarding/OnboardingState.cs` | A |
| `Features/Onboarding/OnboardingSession.cs` | A |
| `Features/Onboarding/Services/OnboardingStateMachine.cs` | A |
| `Features/Onboarding/Services/OnboardingChatService.cs` | A, C |
| `Features/Onboarding/Services/SiteDeployService.cs` | B |
| `Features/Onboarding/Services/StripeService.cs` | D |
| `Features/Onboarding/Tools/DeploySiteTool.cs` | B |
| `Features/Onboarding/Tools/SubmitCmaFormTool.cs` | C |
| `Features/Onboarding/Tools/CreateStripeSessionTool.cs` | D |
| `Program.cs` | A, B, D, E |
| `appsettings.Development.json` | E |
| `apps/platform/components/chat/MessageRenderer.tsx` | A |
| `apps/platform/components/chat/PaymentCard.tsx` | D |
| `apps/platform/.env.local.example` | E |
| **Tests:** | |
| `Tests/Features/Onboarding/Services/StateMachineTests.cs` | A |
| `Tests/Features/Onboarding/OnboardingSessionTests.cs` | A |
| `Tests/Features/Onboarding/Services/StripeServiceTests.cs` | D |
| `Tests/Features/Onboarding/Services/SiteDeployTests.cs` | B |
| `Tests/Features/Onboarding/Tools/CmaToolTests.cs` | C |
