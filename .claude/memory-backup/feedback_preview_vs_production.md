---
name: preview-vs-production-domains
description: Preview deploys use *.workers.dev which needs separate config in Turnstile, Google Maps API key, and CORS
type: feedback
---

Preview deploys (`*.misteredr.workers.dev`) and production (`*.real-estate-star.com`) are different domains. Every external service that validates by domain needs BOTH configured:

**Why:** The OpenNext migration moved from Cloudflare Pages to Workers. Every external service that was configured for `*.real-estate-star.com` also needs `*.workers.dev` for previews.

**Checklist when adding any domain-restricted external service:**
1. **Turnstile** — Cloudflare dashboard → Turnstile widget → Allowed Hostnames → add `misteredr.workers.dev`
2. **Google Maps API key** — Google Cloud Console → Credentials → HTTP referrers → add `*.workers.dev/*`
3. **CORS** — `Program.cs` `SetIsOriginAllowed` → `*.workers.dev`
4. **CSP** — `middleware.ts` connect-src/frame-src as needed

**These are NOT code changes** (except CORS). Turnstile and Google Maps are dashboard-only. Claude cannot change them.
