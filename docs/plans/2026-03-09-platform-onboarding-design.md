# Platform & Onboarding Chat UI - Design

**Date:** 2026-03-09
**Status:** Approved
**Author:** Eddie Rosado + Claude

## Executive Summary

A Real Estate Star marketing website (`apps/platform/`) with a bold landing page and an AI-powered onboarding chat that walks real estate agents through setup, deploys their white-label site, demos the CMA pipeline live, and collects payment — all in one conversation. The onboarding IS the demo. The demo IS the sale.

## Product Positioning

**Pricing:** $900 one-time fee. No monthly subscriptions.
**Trial:** 7-day free trial. No credit card required to start. Card captured via Stripe SetupIntent after the wow moment — not charged until day 7.
**Competitive advantage:** Every competitor charges $150–$1,800/month. Real Estate Star breaks even vs. Placester in 11 months, vs. AgentFire in 6, vs. kvCORE in 3.

See `docs/research/2026-03-09-competitive-pricing-analysis.md` for full competitive analysis.

## User Journey

### Landing Page

One screen. No scrolling. Dark background, minimal copy.

```
┌─────────────────────────────────────────────┐
│  ★ Real Estate Star              [Log In]   │
│                                             │
│                                             │
│     Stop paying monthly.                    │
│     $900. Everything.                       │
│                                             │
│     Website. CMA automation. Lead           │
│     management. One payment. Done.          │
│                                             │
│  ┌───────────────────────────────────────┐  │
│  │ Paste your Zillow or Realtor.com URL  │  │
│  └───────────────────────────────────────┘  │
│           [ Get Started Free ]              │
│                                             │
│     7-day free trial. No credit card.       │
│                                             │
└─────────────────────────────────────────────┘
```

- One input field, one button
- No features grid, no testimonials, no pricing tiers
- "Log In" link for returning agents (future)
- The onboarding chat does the selling

### Onboarding State Machine

Backend-owned. The API controls the state, determines which tools Claude can access at each step, and streams responses to the frontend. The frontend is a thin chat renderer.

```
1. scrape_profile     → AI extracts everything from profile URL
2. confirm_identity   → "Here's what I found — anything to fix?"
3. collect_branding   → "Got your colors. Want to customize?"
4. generate_site      → AI builds config + content, deploys site
5. preview_site       → "Here's your site." (live iframe in chat)
6. demo_cma           → "Let's show you what your clients experience."
7. show_results       → "Check your inbox. Check your Drive."
                        "That just happened in 2 minutes."
                        "But that's just the beginning."

                        "With Real Estate Star, you automate
                         your entire workflow:"

                        ✓ Instant CMA for every lead
                        ✓ Contract drafting and DocuSign
                        ✓ MLS listing creation
                        ✓ Lead tracking and follow-up
                        ✓ Everything organized in your Google Drive

                        "From first contact to closing — on autopilot."

8. collect_payment    → "Unlock your website and the full platform.
                         $900. One time."
                        → Stripe SetupIntent (card captured, not charged)
9. trial_activated    → "Your 7-day trial is live."
                        Site live at custom domain or
                        {agent-id}.realestatestar.com
```

If the agent provides an invalid URL or no URL, the AI gracefully pivots to manual collection: "No problem — let's build your profile together."

### Chat resumes across sessions. If an agent closes the tab and returns, they pick up where they left off.

## Architecture

### Frontend — `apps/platform/`

Next.js 16 app. Mobile-responsive (no native app). Contains:

```
apps/platform/
  app/
    page.tsx                    Landing page
    onboard/
      page.tsx                  Chat UI
    layout.tsx                  Shared layout + branding
  components/
    chat/
      ChatWindow.tsx            Full-screen chat container
      MessageBubble.tsx         Text messages
      ProfileCard.tsx           Scraped identity display
      ColorPalette.tsx          Branding colors with edit
      SitePreview.tsx           Live iframe
      EmailPreview.tsx          CMA email preview
      DrivePreview.tsx          Folder structure display
      FeatureChecklist.tsx      Platform capabilities list
      PaymentCard.tsx           Stripe Elements embed
      ProgressIndicator.tsx     "Deploying..." status
      ActionButton.tsx          "Looks right", "Approve" etc.
```

Message types the chat renders:
- Text bubbles — AI conversation
- Profile card — scraped identity (photo, name, brokerage)
- Color palette — extracted branding with edit option
- Site preview — live iframe of deployed site
- Email preview — CMA email the lead receives
- Drive preview — folder structure created
- Feature checklist — full platform capabilities
- Stripe embed — payment collection
- Progress indicators — animated status during long operations

Key UX decisions:
- Full screen — no sidebar, no distractions
- Inline rich cards — everything renders inside the chat flow
- Action buttons on cards — "Looks right", "Change colors", "Approve site" so agents don't have to type
- SSE streaming — AI responses stream in real-time

### Backend — `apps/api/`

```
apps/api/
  Onboarding/
    Models/
      OnboardingSession.cs        Session state (persisted)
      OnboardingState.cs          Enum of 9 states
      ScrapedProfile.cs           Extracted profile data
    Services/
      OnboardingStateMachine.cs   State transitions + validation
      ProfileScraperService.cs    Scrapes Zillow/Realtor.com/brokerage
      OnboardingChatService.cs    Claude API with state-aware tools
      SiteDeployService.cs        Generates config, creates PR, deploys
    Endpoints/
      OnboardingEndpoints.cs      SSE streaming chat + actions
```

### Chat Message Flow

```
Agent sends message or clicks action button
  → POST /onboard/{sessionId}/chat
    → StateMachine checks current state
      → Determines which tools Claude can use:
          scrape_profile:   [scrape_url, parse_profile]
          confirm_identity: [update_profile]
          collect_branding: [extract_colors, set_branding]
          generate_site:    [create_config, deploy_site]
          preview_site:     [get_preview_url]
          demo_cma:         [submit_cma_form]
          show_results:     [check_inbox, check_drive]
          collect_payment:  [create_stripe_session]
          trial_activated:  [get_site_url]
      → Calls Claude API with message history + allowed tools
        → Streams response via SSE
          → If Claude calls a tool, execute + feed result back
            → If state transition condition met, advance state
```

Claude only gets tools relevant to the current step. It cannot skip ahead because future-state tools are not available.

### Session Persistence

JSON files on disk (one per session). Move to a database when needed. Sessions include:
- Current state
- Message history
- Scraped profile data
- Agent config (built incrementally)
- Stripe SetupIntent ID (after payment step)

## Profile Scraping

Two-tier approach:

1. **Structured scraper** — Zillow and Realtor.com have predictable page structures. Parse HTML for known selectors. Fast, reliable.
2. **AI scraper** — for brokerage sites or unknown URLs, fetch page content and send to Claude for extraction. Slower but handles anything.

Try structured first, fall back to AI.

### Data extracted:

```
identity:     name, title, phone, email, photo, brokerage, license
location:     state, office address, service areas
content:      bio, reviews, sold homes, active listings
branding:     logo URL, dominant colors from photo/page
stats:        years experience, homes sold count, avg rating
```

This feeds directly into the agent config + content JSON files that the agent-site template engine already consumes.

## Stripe Integration

```
Stripe Configuration:
  Product:    Real Estate Star Platform
  Price:      $900 one-time
  Flow:       SetupIntent (no charge) → scheduled PaymentIntent on day 7
```

Flow:
1. Agent enters card in chat → Stripe SetupIntent validates card, no charge
2. 7-day trial starts → site is live
3. Day 7 → scheduled job charges $900 via PaymentIntent
4. If agent cancels before day 7 → site deactivated, card never charged

No Stripe account needed during development. Stub with a "payment collected" button and wire Stripe when ready to sell.

## Scope — What's In and Out

### In v1

- Landing page (dark, bold, one CTA)
- Onboarding chat with 9-state machine
- Profile scraping (Zillow, Realtor.com, AI fallback)
- Site generation and deployment
- Live site preview in chat
- CMA demo pipeline
- Feature checklist pitch
- Stripe SetupIntent payment capture
- 7-day trial with deferred charge
- Chat history / resume session
- Custom domain support
- Mobile-responsive (all pages)

### NOT in v1

- Agent dashboard / login experience (future)
- Multiple templates (emerald-classic only)
- IDX/MLS listing integration (core to product vision, post-launch)
- Admin panel (manage via CLI + config files)
- Email sequences / drip campaigns (single CMA email only)
- Native mobile app (responsive web covers mobile)
- Team / brokerage plans (single agent only)
- Analytics dashboard (Google Drive tracking is enough)

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Platform frontend | Next.js 16, React 19, Tailwind CSS 4 |
| API | .NET Minimal API |
| AI | Claude API (streaming, tool use) |
| Payments | Stripe (SetupIntent + PaymentIntent) |
| Scraping | Structured parsers + Claude AI fallback |
| Chat streaming | Server-Sent Events (SSE) |
| Session storage | JSON files on disk (DB later) |
| Hosting | TBD |
