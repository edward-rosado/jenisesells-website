# Google API Token Flow

Describes how OAuth tokens are acquired, stored, refreshed, and consumed by Google API clients.

## Three-Tier Identity Model

```
Platform                Account               Agent
─────────────────────   ─────────────────────  ─────────────────────
Platform GWS account    Shared GWS account     Per-agent Google OAuth
(admin/ops access)      (account-level Drive)  (agent's Gmail/Drive)
IGwsService             IGDriveClient          IGmailSender
                        (account tier)         IGDriveClient (agent tier)
                                               IGDocsClient
                                               IGSheetsClient
```

## Token Persistence (ITokenStore)

Tokens are saved to Azure Table Storage via `AzureTableTokenStore` (implements `ITokenStore` from Domain).

- **Partition key**: agent handle
- **Row key**: token type (e.g., `gmail`, `drive`)
- **ETag**: used for optimistic concurrency (OCC) on updates

```
ITokenStore (Domain interface)
    └── AzureTableTokenStore (Clients.Azure)
            ├── GetAsync(handle, type) → OAuthToken?
            ├── SaveAsync(handle, type, token)
            └── SaveIfUnchangedAsync(handle, type, token, etag) → bool
```

## Token Resolution Flow

```
API call (e.g., send Gmail)
    │
    ▼
IOAuthRefresher.GetValidTokenAsync(handle, type)
    │
    ├─ ITokenStore.GetAsync()
    │       └── token not found → throw (agent not onboarded)
    │
    ├─ token.ExpiresAt > UtcNow + 5min?
    │       └── YES → return token as-is
    │
    ├─ token.ExpiresAt ≤ UtcNow + 5min (expired / expiring soon)
    │       │
    │       ▼
    │   GoogleOAuthRefresher.RefreshAsync(refreshToken)
    │       └── POST https://oauth2.googleapis.com/token
    │
    ├─ ITokenStore.SaveIfUnchangedAsync(token, etag)
    │       ├── OK → return new token
    │       └── Conflict (ETag mismatch — concurrent refresh won)
    │               │
    │               ▼
    │           ITokenStore.GetAsync() → re-read winner's token → return
    │
    └── return valid access token
```

## Client Consumption

```
IGmailSender     (Domain) → GmailClient     (Clients.Gmail)
IGDriveClient    (Domain) → GDriveClient    (Clients.GDrive)
IGDocsClient     (Domain) → GDocsClient     (Clients.GDocs)
IGSheetsClient   (Domain) → GSheetsClient   (Clients.GSheets)
```

All four clients:
1. Receive `IOAuthRefresher` via constructor injection
2. Call `GetValidTokenAsync(handle, type)` before each request
3. Add `Authorization: Bearer {accessToken}` to the outbound HTTP call
4. Never store tokens in memory — always resolve fresh from `ITokenStore`

## Sequence Diagram

```
Client (e.g., GmailClient)
    │
    │  GetValidTokenAsync(handle, "gmail")
    ▼
IOAuthRefresher
    │
    │  GetAsync(handle, "gmail")
    ▼
ITokenStore (Azure Table)
    │
    │  OAuthToken { AccessToken, RefreshToken, ExpiresAt, ETag }
    ▼
IOAuthRefresher
    │
    ├─ [not expired] ──────────────────────────────→ return AccessToken
    │
    └─ [expired]
            │
            │  RefreshAsync(refreshToken)
            ▼
        Google OAuth2 endpoint
            │
            │  new AccessToken + ExpiresAt
            ▼
        IOAuthRefresher
            │
            │  SaveIfUnchangedAsync(newToken, originalETag)
            ▼
        ITokenStore
            ├─ [ETag match]    → saved → return new AccessToken
            └─ [ETag conflict] → GetAsync() re-read → return winner's AccessToken
```

## Key Design Decisions

- **Optimistic concurrency**: `SaveIfUnchangedAsync` uses Azure Table ETag to detect concurrent refreshes. The second writer re-reads instead of overwriting — no lost tokens.
- **5-minute buffer**: Tokens are refreshed when `ExpiresAt < UtcNow + 5min` to avoid mid-request expiry.
- **Domain owns interfaces**: `ITokenStore`, `IOAuthRefresher`, `IGmailSender`, etc. live in `RealEstateStar.Domain`. Implementations live in their respective `Clients.*` projects.
- **No cross-client references**: `Clients.Gmail`, `Clients.GDrive`, `Clients.GDocs`, `Clients.GSheets`, and `Clients.GoogleOAuth` all depend only on `Domain`. They never reference each other.
