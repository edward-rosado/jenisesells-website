# Migrate Existing Accounts Runbook

## Overview
This runbook walks through re-triggering activation for existing test accounts after deploying Phase 2.75 + Phase 3 changes.

## Prerequisites
- Local dev environment with API running on port 5135
- PowerShell 5.1+ (Windows) or pwsh (cross-platform)
- `scripts/migrate-account.ps1` in the repo root

## Test Accounts to Migrate
1. `jenise-buckalew` — primary test account (prod reference)
2. `safari-homes` — secondary test account
3. `glr` — tertiary test account

## Step 1: Start Local Services

**Terminal 1 — API**
```bash
cd apps/api
dotnet run --launch-profile RealEstateStar.Api
# API should start on http://localhost:5135
```

**Terminal 2 — Agent Site (optional, for manual verification)**
```bash
cd apps/agent-site
npm run dev
# Site should start on http://localhost:3000
```

## Step 2: Trigger Activation for Each Account

Run the migration script for each test account:

```powershell
# Terminal 3 — PowerShell
.\scripts\migrate-account.ps1 -AccountHandle "jenise-buckalew"
.\scripts\migrate-account.ps1 -AccountHandle "safari-homes"
.\scripts\migrate-account.ps1 -AccountHandle "glr"
```

Each script will output:
```
Migrating account: jenise-buckalew
Sending activation request to http://localhost:5135/activation/trigger...
✓ Activation triggered successfully
Instance ID: activation-jenise-buckalew-<uuid>
Monitor progress at: http://localhost:5135/activation/status?instanceId=...
```

## Step 3: Monitor Activation Progress

For each account, check the status endpoint:

```bash
curl http://localhost:5135/activation/status?instanceId=activation-jenise-buckalew-<uuid>
```

Expected JSON response:
```json
{
  "instanceId": "activation-jenise-buckalew-<uuid>",
  "runtimeStatus": "Completed",
  "output": {
    "resultType": "Full",
    "contentByLocale": {
      "en": { "html": "...", "assets": [...] },
      "es": { "html": "...", "assets": [...] }
    }
  }
}
```

Watch for these statuses:
- `Pending` — waiting for orchestrator to start
- `Running` — orchestrator in progress
- `Completed` — success
- `Failed` — check logs in Grafana (see **Troubleshooting** below)

## Step 4: Verify Content Generation

Once activation completes:

### Verify KV Content
Check Cloudflare KV for generated site HTML and metadata:

```bash
# Local dev (uses in-memory KV)
curl http://localhost:5135/accounts/jenise-buckalew/site-content/en
curl http://localhost:5135/accounts/jenise-buckalew/site-content/es
```

### Verify Browser Rendering
If running the agent site locally:

```bash
# In a browser
http://localhost:3000  # Should show site with generated content
```

### Verify R2 Asset Hosting
Assets should be hosted on R2 (Cloudflare object storage):

```bash
# Check asset URLs in the generated HTML
# Should see URLs like: https://{accountId}.r2.cloudflarestorage.com/hero-image-<hash>.jpg
```

## Step 5: Check Logs

If activation fails, check structured logs in Grafana or local logs:

```bash
# Find activation logs by instance ID
# Search in Grafana: span.instance_id = "activation-jenise-buckalew-<uuid>"
# Or check API logs: docker logs <container-id> | grep "activation-jenise-buckalew"
```

Common failure patterns:

| Error | Cause | Fix |
|-------|-------|-----|
| `[VOICE-001] Failed to extract voice` | Email not found or too short | Ensure account has recent emails (>100 chars) |
| `[FACTS-001] Extraction failed` | Metadata incomplete | Verify SiteFacts generation in Phase 2.75 |
| `[CONTENT-001] Build failed` | Template render error | Check template syntax; re-read template from config |
| `[R2-001] Asset rehost failed` | Network timeout | Retry manually or wait for backoff; check R2 quota |
| `[PUBLISH-001] Persist failed` | KV write failed | Check KV quota; Cloudflare status page |

## Step 6: Promote to Live (Manual)

Once you've verified the generated content looks good:

```bash
# Call the publish endpoint to promote draft → live
curl -X POST http://localhost:5135/accounts/jenise-buckalew/site/publish \
  -H "Content-Type: application/json" \
  -d '{"locale": "en"}'

curl -X POST http://localhost:5135/accounts/jenise-buckalew/site/publish \
  -H "Content-Type: application/json" \
  -d '{"locale": "es"}'
```

Each call should return:
```json
{
  "locale": "en",
  "url": "https://jenise-buckalew.real-estate-star.com",
  "publishedAt": "2026-04-16T10:30:00Z"
}
```

## Rollback (If Needed)

If something goes wrong, unpublish and re-trigger:

```bash
# Unpublish the live site (draft remains in KV for re-trigger)
curl -X POST http://localhost:5135/accounts/jenise-buckalew/site/unpublish \
  -H "Content-Type: application/json"

# Re-trigger activation for a fresh build
.\scripts\migrate-account.ps1 -AccountHandle "jenise-buckalew"
```

## Observability Checklist

- [ ] All 3 accounts re-triggered without errors
- [ ] Activation runtimeStatus = "Completed" for each
- [ ] Generated HTML contains localized content (not fallback)
- [ ] Asset URLs are R2-hosted (*.r2.cloudflarestorage.com)
- [ ] Browser renders correctly at http://localhost:3000 (or custom domain)
- [ ] Telemetry spans recorded (ActivitySource: RealEstateStar.Activation)
- [ ] No errors in Grafana or API logs
- [ ] Content cache hit (second re-trigger of same account should be faster)

## Next Steps

After migration completes:
1. **QA team**: Manually verify sites look correct and respond to leads
2. **Product**: Announce to agents (in-app notification, email)
3. **Ops**: Monitor for failures in production over 24 hours
4. **Archive logs**: Copy instance outputs to docs/migrations/2026-04-16-phase-3-activation.json for audit trail
