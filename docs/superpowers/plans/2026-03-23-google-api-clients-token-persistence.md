# Google + Anthropic API Clients, Token Persistence — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace gws CLI with proper Google API clients (Gmail, Drive, Docs, Sheets) using per-agent OAuth tokens stored durably in Azure Table Storage. Consolidate 6 duplicate Claude callers into a shared AnthropicClient. Fan-out all document writes to Agent + Account + Platform Drive.

**Architecture:** Every Google API call resolves an agent's OAuth token from `ITokenStore` (Azure Table, DPAPI encrypted, ETag optimistic locking). A `FanOutStorageProvider` implements `IFileStorageProvider` and writes documents to 3 Drive tiers (best-effort). A shared `AnthropicClient` replaces 6 inline Claude HTTP callers with built-in diagnostics. Every new component has OTel counters for Grafana visibility.

**Tech Stack:** .NET 10, Google.Apis.Gmail.v1, Google.Apis.Drive.v3, Google.Apis.Docs.v1, Google.Apis.Sheets.v4, Azure.Data.Tables, ASP.NET Data Protection API, MimeKit, OpenTelemetry

**Design Spec:** `docs/superpowers/specs/2026-03-23-google-api-clients-token-persistence-design.md`

---

## File Map

### New Files

| File | Responsibility |
|------|---------------|
| `Domain/Shared/Models/OAuthCredential.cs` | Immutable OAuth credential record |
| `Domain/Shared/OAuthProviders.cs` | Provider name constants |
| `Domain/Shared/Interfaces/Storage/ITokenStore.cs` | Token store contract |
| `Domain/Shared/Interfaces/External/IGmailSender.cs` | Gmail send contract |
| `Domain/Shared/Interfaces/External/IGDriveClient.cs` | Drive operations contract |
| `Domain/Shared/Interfaces/External/IGDocsClient.cs` | Docs operations contract |
| `Domain/Shared/Interfaces/External/IGSheetsClient.cs` | Sheets operations contract |
| `Domain/Shared/Interfaces/External/IAnthropicClient.cs` | Claude API client contract |
| `Domain/Shared/Models/AnthropicResponse.cs` | Claude API response record |
| `Domain/Shared/GmailDiagnostics.cs` | Gmail OTel counters |
| `Domain/Shared/GDriveDiagnostics.cs` | Drive OTel counters |
| `Domain/Shared/GDocsDiagnostics.cs` | Docs OTel counters |
| `Domain/Shared/GSheetsDiagnostics.cs` | Sheets OTel counters |
| `Domain/Shared/TokenStoreDiagnostics.cs` | Token store OTel counters |
| `Domain/Shared/FanOutDiagnostics.cs` | Fan-out write OTel counters |
| `Clients.Gmail/GmailApiClient.cs` | Gmail API client |
| `Clients.GDrive/GDriveApiClient.cs` | Drive API client |
| `Clients.GDocs/GDocsApiClient.cs` | Docs API client |
| `Clients.GSheets/GSheetsApiClient.cs` | Sheets API client |
| `Clients.Anthropic/AnthropicClient.cs` | Shared Claude API client |
| `Clients.Azure/AzureTableTokenStore.cs` | Encrypted token store |
| `TestUtilities/InMemoryTokenStore.cs` | Test double for token store |
| `DataServices/Storage/FanOutStorageProvider.cs` | Three-tier fan-out writes |
| `Api/Features/Agents/Transfer/TransferAgentEndpoint.cs` | 501 placeholder |
| `docs/architecture/google-api-token-flow.md` | Token flow architecture diagram |
| `docs/architecture/fan-out-storage.md` | Fan-out storage architecture diagram |

### Modified Files

| File | Change |
|------|--------|
| `Domain/Onboarding/Models/GoogleTokens.cs` | Delete — replaced by OAuthCredential |
| `Domain/Shared/Models/AccountConfig.cs` | Add `AccountId` property |
| `Notifications/MultiChannelLeadNotifier.cs` | gws → IGmailSender + FanOutStorageProvider |
| `Notifications/CmaSellerNotifier.cs` | gws → IGmailSender + IGDriveClient |
| `Notifications/HomeSearchBuyerNotifier.cs` | gws → IGmailSender + FanOutStorageProvider |
| `Api/Features/Onboarding/Services/GoogleOAuthService.cs` | Return OAuthCredential |
| `Api/Features/Onboarding/Endpoints/GoogleOAuthCallbackEndpoint.cs` | Save tokens to ITokenStore |
| `DataServices/Onboarding/EncryptingSessionStoreDecorator.cs` | Encrypt/decrypt OAuthCredential |
| `Workers.Leads/ScraperLeadEnricher.cs` | Inline Claude → IAnthropicClient |
| `Workers.Cma/ClaudeCmaAnalyzer.cs` | Inline Claude → IAnthropicClient |
| `Workers.Cma/ScraperCompSource.cs` | Inline Claude → IAnthropicClient |
| `Workers.HomeSearch/ScraperHomeSearchProvider.cs` | Inline Claude → IAnthropicClient |
| `Api/Features/Onboarding/Services/OnboardingChatService.cs` | Inline Claude → IAnthropicClient |
| `DataServices/Onboarding/ProfileScraperService.cs` | Inline Claude → IAnthropicClient |
| `Api/Program.cs` | Register all new services |
| `Api/Diagnostics/OpenTelemetryExtensions.cs` | Register 6 new diagnostics |
| `Api/appsettings.json` | Add TokenStore config |
| `config/agent.schema.json` | Add accountId field |
| `config/accounts/*/account.json` | Add accountId to each account |
| `infra/grafana/real-estate-star-api-dashboard.json` | Add Row 8 |
| `docs/architecture/system-overview.md` | Add Google + Anthropic clients |
| `docs/architecture/lead-processing-pipeline.md` | Update notification + fan-out |
| `docs/architecture/observability-flow.md` | Add backend OTel section |
| `docs/architecture/cma-pipeline.md` | Update CMA notification flow |
| `CLAUDE.md` | Update infrastructure table |
| `.claude/CLAUDE.md` | Update monorepo structure |
| `docs/onboarding.md` | Update OAuth token persistence |

---

## Phase 1: Foundation (Tasks 1-4) — All independent, max parallel

### Task 1: OAuthCredential model + OAuthProviders + ITokenStore interface

**Files:**
- Create: `apps/api/RealEstateStar.Domain/Shared/Models/OAuthCredential.cs`
- Create: `apps/api/RealEstateStar.Domain/Shared/OAuthProviders.cs`
- Create: `apps/api/RealEstateStar.Domain/Shared/Interfaces/Storage/ITokenStore.cs`
- Delete: `apps/api/RealEstateStar.Domain/Onboarding/Models/GoogleTokens.cs`

- [ ] **Step 1: Create OAuthCredential**

```csharp
// Domain/Shared/Models/OAuthCredential.cs
namespace RealEstateStar.Domain.Shared.Models;

public sealed record OAuthCredential
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required string[] Scopes { get; init; }
    public required string Email { get; init; }
    public required string Name { get; init; }
    public string? AccountId { get; init; }
    public string? AgentId { get; init; }
    public string? ETag { get; init; }
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt.AddMinutes(-5);
}
```

- [ ] **Step 2: Create OAuthProviders**

```csharp
// Domain/Shared/OAuthProviders.cs
namespace RealEstateStar.Domain.Shared;

public static class OAuthProviders
{
    public const string Google = "google";
}
```

- [ ] **Step 3: Create ITokenStore**

```csharp
// Domain/Shared/Interfaces/Storage/ITokenStore.cs
namespace RealEstateStar.Domain.Shared.Interfaces.Storage;

public interface ITokenStore
{
    Task<OAuthCredential?> GetAsync(string accountId, string agentId, string provider, CancellationToken ct);
    Task SaveAsync(OAuthCredential credential, CancellationToken ct);
    Task<bool> SaveIfUnchangedAsync(OAuthCredential credential, string etag, CancellationToken ct);
    Task DeleteAsync(string accountId, string agentId, string provider, CancellationToken ct);
}
```

- [ ] **Step 4: Delete GoogleTokens, fix all compilation errors**

Delete `Domain/Onboarding/Models/GoogleTokens.cs`. Find all references (OnboardingSession, GoogleOAuthService, EncryptingSessionStoreDecorator, tests) and replace `GoogleTokens` with `OAuthCredential`. Update property names: `GoogleEmail` → `Email`, `GoogleName` → `Name`. Build the solution and fix all errors.

- [ ] **Step 5: Build and verify**

```bash
dotnet build apps/api/RealEstateStar.Api.sln
```

- [ ] **Step 6: Commit**

```
feat: add OAuthCredential model + ITokenStore interface, replace GoogleTokens
```

---

### Task 2: All OTel diagnostics classes (6 files)

**Files:**
- Create: `apps/api/RealEstateStar.Domain/Shared/GmailDiagnostics.cs`
- Create: `apps/api/RealEstateStar.Domain/Shared/GDriveDiagnostics.cs`
- Create: `apps/api/RealEstateStar.Domain/Shared/GDocsDiagnostics.cs`
- Create: `apps/api/RealEstateStar.Domain/Shared/GSheetsDiagnostics.cs`
- Create: `apps/api/RealEstateStar.Domain/Shared/TokenStoreDiagnostics.cs`
- Create: `apps/api/RealEstateStar.Domain/Shared/FanOutDiagnostics.cs`
- Modify: `apps/api/RealEstateStar.Api/Diagnostics/OpenTelemetryExtensions.cs`

- [ ] **Step 1: Create all 6 diagnostics files**

Follow the exact pattern from existing `CmaDiagnostics.cs`: static class, `ActivitySource`, `Meter`, counters, histograms. Each file has:
- `ServiceName` constant (e.g., `"RealEstateStar.Gmail"`)
- Counters and histograms as specified in the design spec Section 12

- [ ] **Step 2: Register all 6 in OpenTelemetryExtensions.cs**

Add `.AddSource(...)` and `.AddMeter(...)` for each.

- [ ] **Step 3: Build and verify**
- [ ] **Step 4: Commit**

```
feat: add OTel diagnostics for Gmail, Drive, Docs, Sheets, TokenStore, FanOut
```

---

### Task 3: Shared AnthropicClient

**Files:**
- Create: `apps/api/RealEstateStar.Domain/Shared/Interfaces/External/IAnthropicClient.cs`
- Create: `apps/api/RealEstateStar.Domain/Shared/Models/AnthropicResponse.cs`
- Create: `apps/api/RealEstateStar.Clients.Anthropic/AnthropicClient.cs`
- Create: `apps/api/tests/RealEstateStar.Clients.Anthropic.Tests/AnthropicClientTests.cs`

- [ ] **Step 1: Create IAnthropicClient + AnthropicResponse in Domain**

```csharp
// Domain/Shared/Interfaces/External/IAnthropicClient.cs
public interface IAnthropicClient
{
    Task<AnthropicResponse> SendAsync(string model, string systemPrompt,
        string userMessage, int maxTokens, string pipeline, CancellationToken ct);
}

// Domain/Shared/Models/AnthropicResponse.cs
public sealed record AnthropicResponse(
    string Content, int InputTokens, int OutputTokens, double DurationMs);
```

- [ ] **Step 2: Implement AnthropicClient**

`Clients.Anthropic/AnthropicClient.cs`:
- Constructor: `(IHttpClientFactory httpClientFactory, string apiKey, ILogger<AnthropicClient> logger)`
- Builds JSON request body: `{ model, max_tokens, system, messages: [{ role: "user", content }] }`
- Sets headers: `x-api-key`, `anthropic-version: 2023-06-01`
- Parses `content[0].text` from response
- Strips code fences (port `StripCodeFences` from `ScraperLeadEnricher`)
- Parses `usage.input_tokens` / `usage.output_tokens`
- Calls `ClaudeDiagnostics.RecordUsage(pipeline, model, input, output, duration)` automatically
- Tracks elapsed time with `Stopwatch.GetTimestamp()`
- Log codes: `[CLAUDE-001]` success, `[CLAUDE-010]` API error, `[CLAUDE-020]` timeout, `[CLAUDE-030]` parse error

The Clients.Anthropic.csproj already exists and references Domain. Add package reference for `Microsoft.Extensions.Http` and `Microsoft.Extensions.Logging.Abstractions`.

- [ ] **Step 3: Write tests**

Tests:
- `SendAsync_ReturnsContent_OnSuccess` — mock HttpClient returns valid Claude response JSON
- `SendAsync_StripsCodeFences` — response wrapped in ```json fences → stripped
- `SendAsync_ParsesTokenUsage` — verify InputTokens/OutputTokens in response
- `SendAsync_ThrowsOnApiError` — mock returns 500 → HttpRequestException
- `SendAsync_ThrowsOnTimeout` — mock throws TaskCanceledException
- `StripCodeFences_HandlesNoFences` — passthrough
- `StripCodeFences_HandlesJsonFences` — strips ```json...```

- [ ] **Step 4: Run tests**
- [ ] **Step 5: Commit**

```
feat: add shared AnthropicClient with built-in diagnostics and code fence stripping
```

---

### Task 4: AccountConfig changes + agent.schema.json + account.json files

**Files:**
- Modify: `apps/api/RealEstateStar.Domain/Shared/Models/AccountConfig.cs`
- Modify: `config/agent.schema.json`
- Modify: `config/accounts/*/account.json` (all account files)

- [ ] **Step 1: Add AccountId to AccountConfig**

Add property with fallback:
```csharp
public string AccountId => RawAccountId ?? Handle;
public string? RawAccountId { get; init; }
```

Read `RawAccountId` from the `accountId` JSON field.

- [ ] **Step 2: Update agent.schema.json**

Add `"accountId"` as an optional field (not required — existing accounts use handle as fallback).

- [ ] **Step 3: Add accountId to each account.json**

For `jenise-buckalew`: `"accountId": "jenise-buckalew"` (single-agent, matches handle).

- [ ] **Step 4: Build, verify**
- [ ] **Step 5: Commit**

```
feat: add accountId to AccountConfig for multi-agent brokerage support
```

---

## Phase 2: Token Store + API Clients (Tasks 5-9) — 5+6+7+8 parallel after Task 1

### Task 5: AzureTableTokenStore + InMemoryTokenStore

**Files:**
- Create: `apps/api/RealEstateStar.Clients.Azure/AzureTableTokenStore.cs`
- Create: `apps/api/tests/RealEstateStar.TestUtilities/InMemoryTokenStore.cs`
- Create: `apps/api/tests/RealEstateStar.Clients.Azure.Tests/AzureTableTokenStoreTests.cs`

- [ ] **Step 1: Create InMemoryTokenStore (test double)**

`ConcurrentDictionary<string, (OAuthCredential, string etag)>`. Key = `{accountId}:{agentId}:{provider}`. `SaveIfUnchangedAsync` checks etag match, returns false on mismatch.

- [ ] **Step 2: Create AzureTableTokenStore**

- Table: `oauthtokens`, PK = `{accountId}`, RK = `{agentId}:{provider}`
- Encrypt `AccessToken` + `RefreshToken` with `IDataProtector` (purpose `"OAuthTokenStore.v1"`)
- Graceful plaintext fallback on decryption failure
- `GetAsync` populates `ETag` from table entity
- `SaveIfUnchangedAsync` uses `UpdateEntityAsync` with ETag → returns false on 412
- Dev fallback: `UseDevelopmentStorage=true` when connection string is empty
- Emit `TokenStoreDiagnostics` counters on every operation

- [ ] **Step 3: Write tests**

Tests using InMemoryTokenStore:
- `GetAsync_ReturnsNull_WhenNotFound`
- `SaveAsync_StoresCredential`
- `GetAsync_ReturnsStoredCredential_WithETag`
- `SaveIfUnchangedAsync_ReturnsFalse_OnETagMismatch`
- `SaveIfUnchangedAsync_ReturnsTrue_OnETagMatch`
- `DeleteAsync_RemovesCredential`
- `ConcurrentRefresh_OnlyOneWins` — two threads save with same etag, one gets false

- [ ] **Step 4: Run tests**
- [ ] **Step 5: Commit**

```
feat: add AzureTableTokenStore with DPAPI encryption and ETag optimistic locking
```

---

### Task 6: GmailApiClient

**Files:**
- Create: `apps/api/RealEstateStar.Clients.Gmail/GmailApiClient.cs`
- Create: `apps/api/tests/RealEstateStar.Clients.Gmail.Tests/GmailApiClientTests.cs`

- [ ] **Step 1: Add NuGet packages to Clients.Gmail.csproj**

```xml
<PackageReference Include="Google.Apis.Gmail.v1" Version="1.68.0.3662" />
<PackageReference Include="MimeKit" Version="4.9.0" />
<PackageReference Include="Microsoft.Extensions.Options" Version="10.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
```

- [ ] **Step 2: Implement GmailApiClient**

Implements `IGmailSender`:
- Constructor: `(ITokenStore tokenStore, ILogger<GmailApiClient> logger, GoogleOAuthRefresher refresher)`
- `SendAsync`: resolve token → check expired → refresh if needed (optimistic locking) → build MIME → `GmailService.Users.Messages.Send("me", message)`
- `SendWithAttachmentAsync`: same + attachment bytes as MIME part
- **Bypass GoogleWebAuthorizationBroker**: build `UserCredential` from `TokenResponse` + no-op `IDataStore`
- Emit `GmailDiagnostics` counters
- Missing token → log `[GMAIL-010]`, increment `gmail.token_missing`, return (no-op, no throw)
- Refresh fail → log `[GMAIL-020]`, increment `gmail.token_missing`, return (no-op)
- Send fail → log `[GMAIL-030]`, increment `gmail.failed`, throw

- [ ] **Step 3: Extract GoogleOAuthRefresher (shared token refresh logic)**

Since Gmail, Drive, Docs, Sheets all need the same refresh logic, extract a shared helper:

```csharp
// Could live in Clients.GoogleOAuth or a shared location
public class GoogleOAuthRefresher(ITokenStore tokenStore, string clientId, string clientSecret, ILogger logger)
{
    public async Task<OAuthCredential> GetValidCredentialAsync(
        string accountId, string agentId, CancellationToken ct)
    {
        // 1. Get from store
        // 2. If not expired, return
        // 3. If expired, refresh via Google token endpoint
        // 4. SaveIfUnchangedAsync with etag
        // 5. If etag conflict, re-read (someone else refreshed)
        // 6. Return valid credential
    }
}
```

This prevents duplicating the refresh + optimistic locking logic in 4 clients.

- [ ] **Step 4: Write tests**

Tests (using InMemoryTokenStore + mock HTTP):
- `SendAsync_SendsEmail_WhenTokenValid`
- `SendAsync_RefreshesToken_WhenExpired`
- `SendAsync_NoOp_WhenTokenMissing` (no throw)
- `SendAsync_NoOp_WhenRefreshFails` (no throw)
- `SendAsync_Throws_WhenSendFails` (API error)
- `SendWithAttachmentAsync_IncludesAttachment`

- [ ] **Step 5: Run tests**
- [ ] **Step 6: Commit**

```
feat: add GmailApiClient with per-agent OAuth, token refresh, and MimeKit
```

---

### Task 7: GDriveApiClient

**Files:**
- Create: `apps/api/RealEstateStar.Clients.GDrive/GDriveApiClient.cs`
- Create: `apps/api/tests/RealEstateStar.Clients.GDrive.Tests/GDriveApiClientTests.cs`

- [ ] **Step 1: Add NuGet packages to Clients.GDrive.csproj**

```xml
<PackageReference Include="Google.Apis.Drive.v3" Version="1.68.0.3662" />
```

- [ ] **Step 2: Implement GDriveApiClient**

Implements `IGDriveClient`:
- Uses same `GoogleOAuthRefresher` from Task 6
- `CreateFolderAsync`, `UploadFileAsync`, `UploadBinaryAsync`, `DownloadFileAsync`, `DeleteFileAsync`, `ListFilesAsync`
- Emit `GDriveDiagnostics` counters
- Missing token → log + return null/empty (best-effort)

- [ ] **Step 3: Write tests**
- [ ] **Step 4: Commit**

```
feat: add GDriveApiClient with per-agent OAuth and Drive v3 SDK
```

---

### Task 8: GDocsApiClient

**Files:**
- Create: `apps/api/RealEstateStar.Clients.GDocs/GDocsApiClient.cs`

- [ ] **Step 1: Add NuGet packages, implement GDocsApiClient**

Same pattern as Drive. Implements `IGDocsClient`. Uses `GoogleOAuthRefresher`. Emits `GDocsDiagnostics`.

- [ ] **Step 2: Write tests**
- [ ] **Step 3: Commit**

```
feat: add GDocsApiClient with per-agent OAuth and Docs v1 SDK
```

---

### Task 9: GSheetsApiClient

**Files:**
- Create: `apps/api/RealEstateStar.Clients.GSheets/GSheetsApiClient.cs`

- [ ] **Step 1: Add NuGet packages, implement GSheetsApiClient**

Same pattern. Implements `IGSheetsClient`. Uses `GoogleOAuthRefresher`. Emits `GSheetsDiagnostics`.

- [ ] **Step 2: Write tests**
- [ ] **Step 3: Commit**

```
feat: add GSheetsApiClient with per-agent OAuth and Sheets v4 SDK
```

---

## Phase 3: Integration (Tasks 10-13) — 10+11 parallel, then 12+13 parallel

### Task 10: FanOutStorageProvider

**Files:**
- Create: `apps/api/RealEstateStar.DataServices/Storage/FanOutStorageProvider.cs`
- Create: `apps/api/tests/RealEstateStar.DataServices.Tests/Storage/FanOutStorageProviderTests.cs`

- [ ] **Step 1: Implement FanOutStorageProvider**

Implements `IFileStorageProvider`:
- Constructor: `(IGDriveClient driveClient, IGSheetsClient sheetsClient, IGwsService gwsService, string accountId, string agentId, ILogger logger)`
- Document methods (Write/Read/Update/Delete/List) → fan-out to Agent Drive + Account Drive + Platform Drive via `IGDriveClient` (agent/account) and `IGwsService` (platform). All three tiers best-effort.
- Sheets methods (Append/Read/Redact) → passthrough to `IGSheetsClient` (per-agent) or `IGwsService` (migration fallback)
- `EnsureFolderExistsAsync` → fan-out to all three
- Emit `FanOutDiagnostics` counters per tier

- [ ] **Step 2: Write tests**

Tests (using InMemoryTokenStore + mock clients):
- `WriteDocumentAsync_WritesToAllThreeTiers`
- `WriteDocumentAsync_ContinuesOnAgentDriveFailure`
- `WriteDocumentAsync_ContinuesOnAccountDriveFailure`
- `WriteDocumentAsync_ContinuesOnPlatformDriveFailure`
- `AppendRowAsync_PassesToSheetsClient`

- [ ] **Step 3: Commit**

```
feat: add FanOutStorageProvider with three-tier best-effort document writes
```

---

### Task 11: Migrate 6 Claude callers to IAnthropicClient

**Files:**
- Modify: `apps/api/RealEstateStar.Workers.Leads/ScraperLeadEnricher.cs`
- Modify: `apps/api/RealEstateStar.Workers.Cma/ClaudeCmaAnalyzer.cs`
- Modify: `apps/api/RealEstateStar.Workers.Cma/ScraperCompSource.cs`
- Modify: `apps/api/RealEstateStar.Workers.HomeSearch/ScraperHomeSearchProvider.cs`
- Modify: `apps/api/RealEstateStar.Api/Features/Onboarding/Services/OnboardingChatService.cs`
- Modify: `apps/api/RealEstateStar.DataServices/Onboarding/ProfileScraperService.cs`

- [ ] **Step 1: Migrate ScraperLeadEnricher**

Replace `CallClaudeAsync` method: remove inline HTTP request construction, headers, response parsing, code fence stripping. Replace with:
```csharp
var response = await anthropicClient.SendAsync(ClaudeModel, SystemPrompt, xmlPayload, MaxTokens, "lead", ct);
return ParseClaudeResponse(response.Content);
```
Keep `ParseClaudeResponse` (JSON → domain model) and `BuildXmlPayload`. Remove `StripCodeFences` (now in AnthropicClient). Add `IAnthropicClient` to constructor, remove `IHttpClientFactory` for Claude calls (keep for scraper).

- [ ] **Step 2: Migrate ClaudeCmaAnalyzer**

Same pattern. Replace inline Claude HTTP with:
```csharp
var response = await anthropicClient.SendAsync(Model, SystemPrompt, prompt, MaxTokens, "cma-analysis", ct);
return ParseAnalysis(response.Content);
```

- [ ] **Step 3: Migrate ScraperCompSource**

Same pattern. Pipeline tag: `"cma-comps"`.

- [ ] **Step 4: Migrate ScraperHomeSearchProvider**

Same pattern. Pipeline tag: `"home-search"`. Replace `CurateWithClaudeAsync` internals.

- [ ] **Step 5: Migrate OnboardingChatService**

This is the most complex — it uses streaming. For now, keep the streaming implementation but add `IAnthropicClient` for non-streaming tool result calls if any exist. If ALL calls are streaming, defer this migration (noted in out-of-scope as "Streaming Anthropic client").

Read the file to determine if there are non-streaming Claude calls that can use `SendAsync`.

- [ ] **Step 6: Migrate ProfileScraperService**

Same pattern. Uses `claude-haiku-4-5-20251001`. Pipeline tag: `"profile-scraper"`.

- [ ] **Step 7: Update DI registrations in Program.cs**

Register `IAnthropicClient` → `AnthropicClient` singleton. Update all caller registrations to accept `IAnthropicClient` instead of building their own HTTP requests.

- [ ] **Step 8: Build and run all tests**
- [ ] **Step 9: Commit**

```
refactor: migrate 6 Claude callers to shared IAnthropicClient
```

---

### Task 12: Migrate notification layer to Gmail + Fan-Out

**Files:**
- Modify: `apps/api/RealEstateStar.Notifications/MultiChannelLeadNotifier.cs`
- Modify: `apps/api/RealEstateStar.Notifications/CmaSellerNotifier.cs`
- Modify: `apps/api/RealEstateStar.Notifications/HomeSearchBuyerNotifier.cs`

- [ ] **Step 1: Migrate MultiChannelLeadNotifier**

Replace `gwsService.SendEmailAsync(agentEmail, to, subject, body, null, ct)` with:
```csharp
await gmailSender.SendAsync(accountId, agentId, to, subject, body, ct);
```

After send attempt, write email record:
```csharp
var record = BuildEmailRecord(subject, body, sent: true/false, error);
await fanOutStorage.WriteDocumentAsync(leadFolder, "Communications", fileName, record, ct);
```

Add `IGmailSender` and resolve `accountId` from `IAccountConfigService`.

- [ ] **Step 2: Migrate CmaSellerNotifier**

Replace `gwsService.SendEmailAsync` with `gmailSender.SendWithAttachmentAsync`. Replace `gwsService.UploadFileAsync` with `fanOutStorage.WriteDocumentAsync` for the PDF. Write email record after send.

- [ ] **Step 3: Migrate HomeSearchBuyerNotifier**

Same pattern as MultiChannelLeadNotifier.

- [ ] **Step 4: Update tests**
- [ ] **Step 5: Commit**

```
refactor: migrate notifications from gws CLI to Gmail API + fan-out storage
```

---

### Task 13: Onboarding OAuth migration — persist tokens to ITokenStore

**Files:**
- Modify: `apps/api/RealEstateStar.Api/Features/Onboarding/Services/GoogleOAuthService.cs`
- Modify: `apps/api/RealEstateStar.Api/Features/Onboarding/Endpoints/GoogleOAuthCallbackEndpoint.cs`
- Modify: `apps/api/RealEstateStar.DataServices/Onboarding/EncryptingSessionStoreDecorator.cs`

- [ ] **Step 1: Update GoogleOAuthService.ExchangeCodeAsync**

Return `OAuthCredential` instead of `GoogleTokens` (already renamed in Task 1). Verify field mapping is correct.

- [ ] **Step 2: Update GoogleOAuthCallbackEndpoint**

After exchanging code for tokens, also save to `ITokenStore`:
```csharp
var credential = await oauthService.ExchangeCodeAsync(code, ct);
// Save to durable store
var accountId = session.AccountId ?? session.AgentId;
await tokenStore.SaveAsync(credential with { AccountId = accountId, AgentId = session.AgentId }, ct);
// If first agent, also save as account token
if (isFirstAgent)
    await tokenStore.SaveAsync(credential with { AccountId = accountId, AgentId = "__account__" }, ct);
```

- [ ] **Step 3: Update EncryptingSessionStoreDecorator**

Encrypt/decrypt `OAuthCredential` fields (same pattern, different type name). Add backward compatibility for old `GoogleTokens` JSON format.

- [ ] **Step 4: Update tests**
- [ ] **Step 5: Commit**

```
feat: persist OAuth tokens to ITokenStore on onboarding completion
```

---

## Phase 4: Wiring + Cleanup (Tasks 14-16) — After all integration tasks

### Task 14: Program.cs DI registration + appsettings

**Files:**
- Modify: `apps/api/RealEstateStar.Api/Program.cs`
- Modify: `apps/api/RealEstateStar.Api/appsettings.json`

- [ ] **Step 1: Add TokenStore config to appsettings.json**

```json
"TokenStore": {
    "TableName": "oauthtokens",
    "ConnectionString": ""
}
```

- [ ] **Step 2: Register all new services in Program.cs**

```csharp
// Token store
builder.Services.AddSingleton<ITokenStore>(sp => { ... AzureTableTokenStore ... });

// OAuth refresher
builder.Services.AddSingleton<GoogleOAuthRefresher>();

// Google API clients
builder.Services.AddSingleton<IGmailSender, GmailApiClient>();
builder.Services.AddSingleton<IGDriveClient, GDriveApiClient>();
builder.Services.AddSingleton<IGDocsClient, GDocsApiClient>();
builder.Services.AddSingleton<IGSheetsClient, GSheetsApiClient>();

// Anthropic client
builder.Services.AddSingleton<IAnthropicClient>(sp =>
    new AnthropicClient(factory, anthropicKey, logger));

// Agent transfer placeholder
app.MapPost("/internal/agents/{agentId}/transfer", () => Results.StatusCode(501));
```

- [ ] **Step 3: Build full solution**
- [ ] **Step 4: Commit**

```
feat: wire all Google + Anthropic clients in DI + TokenStore config
```

---

### Task 15: Update Grafana dashboard + full test run

**Files:**
- Modify: `infra/grafana/real-estate-star-api-dashboard.json`

- [ ] **Step 1: Add Row 8: Google API + Token Store**

9 panels as defined in design spec Section 12:
- Gmail sent/failed/skipped
- Gmail duration
- Drive operations by type
- Docs operations
- Sheets operations
- Token refreshes / conflicts
- Fan-out writes by tier
- Fan-out failures by tier
- Token missing (all APIs)

- [ ] **Step 2: Run full test suite**

```bash
dotnet test apps/api/RealEstateStar.Api.sln --verbosity minimal
```

All tests must pass.

- [ ] **Step 3: Commit**

```
feat: add Google API + Token Store panels to Grafana dashboard (Row 8)
```

---

### Task 16: Documentation updates

**Files:**
- Modify: `docs/architecture/system-overview.md`
- Modify: `docs/architecture/lead-processing-pipeline.md`
- Modify: `docs/architecture/observability-flow.md`
- Modify: `docs/architecture/cma-pipeline.md`
- Create: `docs/architecture/google-api-token-flow.md`
- Create: `docs/architecture/fan-out-storage.md`
- Modify: `CLAUDE.md`
- Modify: `.claude/CLAUDE.md`
- Modify: `docs/onboarding.md`

- [ ] **Step 1: Create google-api-token-flow.md**

Mermaid diagram: Three-tier identity → ITokenStore → Gmail/Drive/Docs/Sheets clients. Show token refresh + optimistic locking.

- [ ] **Step 2: Create fan-out-storage.md**

Mermaid diagram: FanOutStorageProvider → Agent/Account/Platform Drive with best-effort semantics.

- [ ] **Step 3: Update system-overview.md**

Add Google API Clients + Anthropic Client to the API subgraph. Add ITokenStore.

- [ ] **Step 4: Update lead-processing-pipeline.md**

Update notification step: gws → IGmailSender. Add fan-out. Update to PipelineWorker.

- [ ] **Step 5: Update observability-flow.md**

Add backend OTel section with all new diagnostics.

- [ ] **Step 6: Update cma-pipeline.md**

Update CMA notification: gws → IGmailSender + IGDriveClient. Analysis: inline → IAnthropicClient.

- [ ] **Step 7: Update CLAUDE.md + .claude/CLAUDE.md**

Infrastructure table, monorepo structure, client descriptions.

- [ ] **Step 8: Update docs/onboarding.md**

OAuth tokens now persist to ITokenStore after onboarding.

- [ ] **Step 9: Commit**

```
docs: update architecture diagrams, CLAUDE.md, onboarding for Google API clients
```

---

## Summary

| Phase | Tasks | What | Parallel? |
|-------|-------|------|-----------|
| **Phase 1** | 1, 2, 3, 4 | Foundation: models, interfaces, diagnostics, Anthropic client, accountId | All 4 independent |
| **Phase 2** | 5, 6, 7, 8, 9 | Token store + 4 Google API clients | 5 depends on 1; 6-9 depend on 1+5 (refresher); 6+7+8+9 parallel |
| **Phase 3** | 10, 11, 12, 13 | Fan-out, Claude migration, notification migration, OAuth migration | 10+11 parallel; 12 depends on 6+10; 13 depends on 1+5 |
| **Phase 4** | 14, 15, 16 | DI wiring, Grafana, docs | 14 after all; 15+16 parallel after 14 |

**Max parallelization:**
- Wave 1: Tasks 1 + 2 + 3 + 4 (zero overlap)
- Wave 2: Tasks 5 + 6 + 7 + 8 + 9 (all depend on Task 1; share GoogleOAuthRefresher from Task 6)
- Wave 3: Tasks 10 + 11 + 13 (10 depends on 7; 11 depends on 3; 13 depends on 5)
- Wave 4: Task 12 (depends on 6 + 10)
- Wave 5: Tasks 14 + 15 + 16 (after all implementation)
