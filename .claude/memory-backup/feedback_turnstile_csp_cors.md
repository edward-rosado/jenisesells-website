---
name: turnstile-requires-csp-and-cors
description: Cloudflare Turnstile needs frame-src in CSP, and /telemetry needs CORS for agent site subdomains
type: feedback
---

When adding Turnstile CAPTCHA or any iframe-based widget, CSP must include `frame-src` for the widget's domain.

**Why:** Turnstile loads in an iframe from `challenges.cloudflare.com`. Without `frame-src https://challenges.cloudflare.com` in CSP, the browser blocks it with Error 300030. This was missed during the form-path-hardening PR and required a production hotfix.

**How to apply:**
- Any iframe widget (Turnstile, reCAPTCHA, payment embeds) needs `frame-src` in CSP middleware
- Browser-side fetch calls (like `/telemetry` fire-and-forget) need CORS configured on the API
- The API CORS `SetIsOriginAllowed` must include `*.real-estate-star.com` for agent site subdomains AND `*.workers.dev` for Cloudflare Workers preview deploys
- Turnstile widget domain allowlist in Cloudflare dashboard must include `*.workers.dev` for previews (Error 110200 = domain mismatch)
- Test CSP changes by checking browser console for "blocked" errors after deploy
- When deploy infra changes (Pages → Workers), update BOTH the CORS origin check in Program.cs AND the Turnstile widget allowed domains in the CF dashboard
