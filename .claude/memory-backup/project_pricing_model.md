---
name: Pricing Model
description: Current pricing structure and product positioning — 14-day free trial + $14.99/mo, no setup fee
type: project
---

# Pricing Model (as of 2026-03-13)

## Current Pricing
- **14-day free trial** — no credit card required, cancel anytime
- **$14.99/month** after trial — automation, hosting, and all new features included
- No setup fee

## Product Positioning
- **"Your business, automated by AI."** — the product is AI-powered business automation, NOT just an AI-powered website
- Lead with the free trial, price is secondary
- Messaging should emphasize: automation, workflow, lead management, CMA tools — not just website generation
- Competitors: KVCore ($499/mo), Ylopo ($395/mo) — we are dramatically cheaper with no commitment

## Previous Pricing (deprecated)
- Was $699 setup + $10/mo (changed 2026-03-13)
- Before that was $10/mo only, free setup
- Before that was $900 one-time payment (changed in PR #9)

## Production Feature Gate
- `/onboard` route shows "Coming Soon" in production via `NEXT_PUBLIC_COMING_SOON=true` env var
- Set in `.github/workflows/deploy-platform.yml` build step
- Remove the env var when onboarding is ready for production
