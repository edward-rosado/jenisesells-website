---
name: preview-deploy-query-params
description: "Cloudflare Workers preview deploys need PREVIEW env var to allow ?agentId query params"
user-invocable: false
origin: auto-extracted
---

# Preview Deploy Query Param Routing

**Extracted:** 2026-03-17
**Context:** Agent-site preview deploys on Cloudflare Workers serve all agents from one Worker, using ?agentId to switch tenants. But NODE_ENV=production blocks query params.

## Problem
Cloudflare Workers always run with `NODE_ENV=production`. The agent-site's `resolveAgentId()` ignores `?agentId` query params in production to prevent tenant confusion on live subdomain-routed sites. This means preview deploy URLs like `?agentId=test-modern` silently fall back to the default agent.

## Solution

### Step 1: Add PREVIEW env var to preview wrangler config
In `.github/workflows/agent-site.yml`, the preview deploy generates a `wrangler.preview.jsonc`. Add `"PREVIEW": "true"` to the `vars` section:
```jsonc
"vars": {
  "NEXT_PUBLIC_API_URL": "https://api.real-estate-star.com",
  "PREVIEW": "true"
}
```

### Step 2: Check PREVIEW in resolveAgentId
```typescript
function resolveAgentId(agentId?: string): string {
  // Production ignores query params (tenant isolation via subdomains)
  // Preview deploys set PREVIEW=true so ?agentId works for QA
  if (process.env.NODE_ENV === "production" && !process.env.PREVIEW) {
    return process.env.DEFAULT_AGENT_ID || "jenise-buckalew";
  }
  return agentId || process.env.DEFAULT_AGENT_ID || "jenise-buckalew";
}
```

Apply the same pattern to `resolveTemplateOverride()` if template switching via `?template` is needed in previews.

### Note
Other pages (thank-you, privacy, terms, accessibility) read `?agentId` directly without a production guard — they don't need this fix. Only `app/page.tsx` had the guard.

## When to Use
- Adding new query-param-based routing to agent-site pages
- Debugging preview deploy URLs that show the wrong agent
- Setting up new preview deploy environments (Cloudflare, Vercel, etc.)
