# Contributor Onboarding Guide

## Quick Start

```bash
# Clone the repo
git clone https://github.com/edward-rosado/Real-Estate-Star.git
cd Real-Estate-Star

# Run the setup script
bash setup.sh        # macOS / Linux / WSL
# or
.\setup.ps1          # Windows PowerShell
```

The setup script installs dependencies, symlinks Claude Code configuration, and validates your environment.

## Repo Structure Overview

| Directory | Purpose |
|-----------|---------|
| `apps/` | Deployable applications (portal, agent sites, API) |
| `packages/` | Shared libraries and utilities across apps |
| `skills/` | Claude Code skills (contracts, comms, CMA, etc.) |
| `config/` | Agent profiles, schemas, and shared configuration |
| `prototype/` | Original prototype files (preserved for reference) |
| `infra/` | Infrastructure-as-code and deployment configs |
| `docs/` | Documentation, plans, and guides |

For the full architectural rationale, see the [design doc](plans/2026-03-09-repo-restructure-design.md).

## How Agent Profiles Work

Every real estate agent on the platform has a JSON profile at:

```
config/agents/{id}.json
```

Profiles are validated against `config/agent.schema.json`. A profile contains the agent's identity, branding, contact info, service areas, and feature flags. All skills and apps read from these profiles rather than hardcoding agent-specific data.

Example structure:

```jsonc
{
  "identity": {
    "name": "Jenise Buckalew",
    "license_state": "NJ",
    "brokerage": "RE/MAX"
  },
  "branding": {
    "primary_color": "#1a3c6e",
    "tagline": "Your home, your way"
  },
  "contact": { ... },
  "features": { ... }
}
```

## How Product Skills Reference Agent Config

Skills use variable syntax to pull data from the active agent profile at runtime:

- `{agent.identity.name}` -- agent's display name
- `{agent.branding.primary_color}` -- brand color for generated content
- `{agent.contact.email}` -- contact email
- `{agent.identity.license_state}` -- determines which contract templates apply

Skills **never** hardcode agent data. This keeps every skill multi-tenant by default.

## How to Add a New Agent

1. Copy the reference tenant profile:
   ```bash
   cp config/agents/reference.json config/agents/{new-id}.json
   ```
2. Update all fields (identity, branding, contact, service areas).
3. Validate against the schema:
   ```bash
   npx ajv validate -s config/agent.schema.json -d config/agents/{new-id}.json
   ```
4. Test with each skill to confirm variable substitution works correctly.

## How to Add a New State for Contracts

1. Create the state directory and README:
   ```bash
   mkdir -p skills/contracts/templates/{STATE}
   ```
2. Add a `README.md` documenting the state-specific form (e.g., field mappings, legal requirements, source form name).
3. Add contract templates to the directory following the existing format.
4. Update the supported states table in `skills/contracts/README.md`.

## Development Workflow

The project follows a structured pipeline:

1. **Brainstorm** -- Identify the problem and potential solutions.
2. **Plan** -- Use the planner agent to create an implementation plan with phases.
3. **TDD** -- Write tests first (RED), implement (GREEN), refactor (IMPROVE).
4. **Code Review** -- Use the code-reviewer agent to catch issues before committing.
5. **PR** -- Open a pull request with a comprehensive summary and test plan.

For full details on each step, see the rules in `.claude/rules/`.

## How Languages & Localization Work

Real Estate Star supports two locales: **English** (`en`) and **Spanish** (`es`).

### Where Locale Is Resolved

- **Agent-site middleware** (`middleware.ts`): Reads `Accept-Language` header, checks for a `locale` cookie (set by language picker), and validates against the agent's `identity.languages` config array. Defaults to `en` if no match.
- **LeadForm**: Includes a hidden `locale` field set to the page's resolved locale.
- **API endpoint**: Validates the locale against the agent's supported languages and persists it as `Lead.Locale` in YAML frontmatter.

### Testing Bilingual Agents

Use the `jenise-buckalew` config (has `languages: ["English", "Spanish"]`). The agent site will resolve locale from the browser's `Accept-Language` header or the language picker cookie.

To test Spanish lead flow end-to-end:
1. Set browser language to Spanish (or manually set the `locale` cookie to `es`)
2. Submit a lead through the agent site
3. Verify `Lead.Locale` is `es` in the saved lead file
4. Verify the drafted email uses Spanish voice/template

### Where Per-Language Skills Are Stored

Skills are stored at `real-estate-star/{agentId}/` in Google Drive and Azure Blob Storage:
- English (default): `Voice Skill.md`, `Personality Skill.md`, etc.
- Spanish: `Voice Skill.es.md`, `Personality Skill.es.md`, etc.

Spanish skills are only extracted during activation if the agent's tagged Spanish corpus has >= 10 items. Below this threshold, the email drafter falls back to the English skill with a Spanish translation system prompt.

### How to Add a New Language

1. Add the language name to `identity.languages` in the agent's `account.json`
2. Create `content-{locale}.json` with localized UI strings for the agent site
3. Add the locale code to `LanguageDetector` in `Domain/Shared/Services/` (stop-word list + character-set rules)
4. Add localized strings to the email template and CMA PDF generator
5. Update the `LeadForm` locale validation to accept the new code

## Using the Typed API Client

The `@real-estate-star/api-client` package provides a typed API client generated from the OpenAPI specification. Every request automatically includes an `X-Correlation-ID` header for end-to-end tracing in Grafana.

### Regenerating Types Locally

After making API changes, regenerate the types:

```bash
# Start the API (runs on port 5135)
dotnet run --project apps/api/RealEstateStar.Api

# In another terminal, regenerate types
npm run generate --workspace=packages/api-client
```

This pulls the latest OpenAPI spec from the running API and regenerates `packages/api-client/src/types.ts`.

### Two Usage Patterns

**Platform (shared instance):**
```typescript
import { api } from "@/lib/api";
const { data, error } = await api.GET("/health/ready");
```

**Agent-site (per-request HMAC headers):**
```typescript
import { createApiClient } from "@real-estate-star/api-client";
const client = createApiClient(baseUrl);
const { data, error } = await client.POST("/path" as never, {
  headers: { "X-Signature": sig, "X-Timestamp": ts, "X-API-Key": key },
  body: payload,
});
```

### CI Auto-Updates

The `api-client` GitHub Actions workflow automatically regenerates types whenever the API build produces a new OpenAPI spec. No manual action needed on main.

## Key Tools

| Tool | Purpose |
|------|---------|
| **Claude Code** | AI pair programmer -- used for planning, coding, reviewing, and committing |
| **GitHub CLI (`gh`)** | Issues, PRs, searches, and CI interaction from the terminal |
| **PM Skills** | Optional product management skills for Claude Code (see [PM Skills Setup](pm-skills-setup.md)) |

## Configuring a New Agent for Lead Submission

When adding a new agent, configure the following for the lead submission feature:

### 1. Google Drive Folder Structure

Create a folder hierarchy in the agent's Google Drive:

```
{Agent's Shared Drive or My Drive}/
├── 1_leads/                    # Lead submissions (auto-saved by API)
├── 2_cma/                      # Comparative Market Analysis reports
└── 3_contracts/                # Generated state-specific contracts
```

Copy the folder IDs into the agent profile:

```json
{
  "integrations": {
    "google_drive": {
      "folder_id_leads": "0ADxxxxxxxxxxx_1-0",
      "folder_id_cma": "0ADxxxxxxxxxxx_2-0",
      "folder_id_contracts": "0ADxxxxxxxxxxx_3-0"
    }
  }
}
```

### 2. API Key and HMAC Secret Generation

Generate credentials for the agent's white-label website to submit leads:

```bash
# Generate a 32-byte API key (base64-encoded)
openssl rand -base64 32

# Example output:
# AbCdEfGhIjKlMnOpQrStUvWxYzAbCdEfGhIjKl=

# Generate a 64-byte HMAC secret (base64-encoded)
openssl rand -base64 64

# Example output:
# AbCdEfGhIjKlMnOpQrStUvWxYzAbCdEfGhIjKlMnOpQrStUvWxYzAbCdEfGhIjKl=
```

Store in the agent profile:

```json
{
  "integrations": {
    "lead_submission": {
      "api_key": "AbCdEfGhIjKlMnOpQrStUvWxYzAbCdEfGhIjKl=",
      "hmac_secret": "AbCdEfGhIjKlMnOpQrStUvWxYzAbCdEfGhIjKlMnOpQrStUvWxYzAbCdEfGhIjKl="
    }
  }
}
```

**Security Note:** These credentials are stored per-agent in the config file. In production, move to Azure Key Vault or equivalent secret manager.

### 3. Google Chat Webhook URL (Optional)

To receive notifications when leads arrive, configure a Google Chat webhook:

1. Go to the agent's Google Chat space
2. Create an incoming webhook at **Space settings > Apps & integrations > Manage webhooks**
3. Copy the webhook URL and add to agent profile:

```json
{
  "integrations": {
    "notifications": {
      "google_chat_webhook_url": "https://chat.googleapis.com/v1/spaces/SPACE_ID/messages?key=KEY&token=TOKEN"
    }
  }
}
```

The API will POST lead summaries to this webhook when submissions arrive.

### 4. Turnstile Site Key Configuration

Real Estate Star uses Cloudflare Turnstile to prevent bot lead submissions:

1. Go to Cloudflare dashboard > Turnstile
2. Create a site for `{agent-handle}.real-estate-star.com`
3. Copy the site key (public) and secret key (private)
4. Add to agent profile:

```json
{
  "branding": {
    "turnstile_site_key": "1x00000000000000000000BB"
  },
  "integrations": {
    "turnstile_secret_key": "2x0000000000000000000000000000000AA"
  }
}
```

The `turnstile_site_key` is public and embedded in the agent site; the `turnstile_secret_key` is kept private on the API and used during lead submission validation.

### 5. OAuth Token Persistence

During the onboarding flow, agents authorize Real Estate Star to access their Gmail and Google Drive. After authorization:

- Tokens are saved to `ITokenStore` (backed by Azure Table Storage via `AzureTableTokenStore` in `Clients.Azure`).
- Tokens survive session cleanup — they are **not** stored in-memory or in the onboarding session. The agent does not need to re-authorize on the next login.
- Tokens are stored with **DPAPI encryption** at rest.

When a Google API client (Gmail, Drive, Docs, Sheets) needs to make a call on behalf of an agent:

1. It calls `IOAuthRefresher.GetValidTokenAsync(handle, type)`.
2. The refresher reads the token from `ITokenStore` (Azure Table).
3. If the token is expired (or expiring within 5 minutes), `GoogleOAuthRefresher` automatically calls the Google OAuth2 endpoint to exchange the refresh token for a new access token.
4. The new token is saved back with **optimistic locking** (`SaveIfUnchangedAsync` checks the Azure Table ETag). If two concurrent requests both refresh at the same time, the second writer re-reads the winner's token rather than overwriting it.

This means once an agent connects their Google account during onboarding, all subsequent API operations are fully automatic — no re-auth prompts, no token management needed in feature code.

#### Local Development Setup (Azurite)

In development, `ITokenStore` uses `NullTokenStore` (silent no-op) when `AzureStorage:ConnectionString` is absent. To test token persistence locally:

1. **Install Azurite** (Azure Storage emulator):
   ```bash
   npm install -g azurite
   ```

2. **Start Azurite**:
   ```bash
   azurite --silent --location .azurite --debug .azurite/debug.log
   ```

3. **Set the connection string** in `appsettings.Development.json` or user secrets:
   ```json
   {
     "AzureStorage": {
       "ConnectionString": "UseDevelopmentStorage=true"
     }
   }
   ```

4. **Verify** — on startup, the API logs `ITokenStore → AzureTableTokenStore (table: oauthtokens)`. If you see `ITokenStore → NullTokenStore`, the connection string is missing.

#### Usage Examples

**Sending email as an agent** (in a notifier or worker):
```csharp
// IGmailSender injected via DI — tokens resolved automatically
await gmailSender.SendAsync(accountId, agentId, recipientEmail, subject, htmlBody, ct);
// No-ops gracefully if agent hasn't connected Google (gmail.token_missing counter increments)
```

**Uploading to agent's Drive** (in a worker or storage provider):
```csharp
// IGDriveClient injected via DI
var fileId = await driveClient.UploadFileAsync(accountId, agentId, folderId, "report.md", content, ct);
// Returns empty string if no token — best-effort, never throws on missing auth
```

**Reading tokens directly** (for diagnostics or admin):
```csharp
// ITokenStore injected via DI
var credential = await tokenStore.GetAsync(accountId, agentId, OAuthProviders.Google, ct);
if (credential is null) { /* agent hasn't onboarded OAuth */ }
else if (credential.IsExpired) { /* token needs refresh — IOAuthRefresher handles this */ }
```

**Token store key structure**:
| Identity | PartitionKey | RowKey | Example |
|----------|-------------|--------|---------|
| Agent | `{accountId}` | `{agentId}:google` | PK=`jenise-buckalew`, RK=`jenise-buckalew:google` |
| Account | `{accountId}` | `__account__:google` | PK=`jenise-buckalew`, RK=`__account__:google` |

### 6. Validate the Agent Profile

After configuring all fields, validate the profile against the schema:

```bash
npx ajv validate -s config/agent.schema.json -d config/agents/{agent-id}.json
```

All required fields should pass. Missing folder IDs or API keys will cause lead submission to fail silently, so double-check before going live.
