# Agent Site Template Engine - Design

**Date:** 2026-03-09
**Status:** Approved
**Author:** Eddie Rosado + Claude

## Executive Summary

An AI-powered agent builder that onboards real estate agents through conversation,
generates a white-label website from their existing branding materials, and deploys
it through a PR pipeline with AI code review and preview URLs. The onboarding
culminates in a live demo where the agent clicks one button and receives an instant
CMA market analysis email — proving the platform's value in seconds.

## User Journey

```
1.  Agent receives invite link
2.  Opens branded chat UI in portal (apps/portal)
3.  PASS 1 — GO LIVE + WOW MOMENT (~10 minutes):
    a. AI collects identity (name, license, brokerage, phone, email)
    b. AI collects branding (agent uploads logo, business card, or existing site URL)
    c. AI extracts colors/fonts from uploaded materials, confirms with agent
    d. AI collects location (state, office address, service areas)
    e. AI sets up email integration (Gmail/Outlook connection)
    f. AI generates agent config + minimal site content
    g. PR created → AI code review → preview URL generated
    h. Agent sees preview in chat, approves
    i. PR merges → production deploy → subdomain live
    j. AI embeds agent's live site in chat with CMA form pre-filled
       (sample property from agent's service area)
    k. AI instructs agent: "Click submit to see the magic"
    l. Agent clicks submit
    m. CMA generates → PDF email arrives in agent's inbox instantly
    n. WOW MOMENT — agent is hooked

4.  PASS 2 — ENRICH (at agent's pace, days/weeks later):
    a. AI follows up for full bio / about text
    b. Testimonials (paste from Zillow/Google or manual entry)
    c. Sold homes (MLS numbers or manual)
    d. Custom services list
    e. Stats (years experience, homes sold, rating)
    f. Lead capture preferences (CMA form, contact form, both)
    g. City-specific landing pages for SEO
    h. Each update → PR → AI review → preview → approve → merge
```

## Architecture

### Multi-Tenant Routing (Next.js 16)

- Middleware reads subdomain from request hostname
- Loads agent config + content files
- Renders the selected template with agent data
- ISR (Incremental Static Regeneration) with ~60s revalidation
- Custom domains: agent CNAMEs to realestatestar.com, middleware maps
  domain to agent ID

### Domain Strategy

- Default: `{agent-id}.realestatestar.com` (free, immediate)
- Upgrade: custom domain (e.g. `jenisesellsnj.com`) as paid feature
- Routing: subdomain middleware + custom domain lookup table

### Template System

- Templates are Next.js 16 page layouts with named section slots
- Each template = different visual style (e.g. "Modern", "Classic", "Luxury")
- Sections are modular components — agents toggle on/off
- All templates render from the same data model (agent config + content)
- Branding (colors, fonts) applied as CSS variables from agent config

### Rendering Pipeline

```
Request hits {agent-id}.realestatestar.com
  → Middleware extracts agent-id from subdomain (or custom domain lookup)
    → Load config/agents/{agent-id}.json (identity, branding, location)
    → Load config/agents/{agent-id}.content.json (sections, content)
      → Select template from content.template field
        → Render enabled sections with agent data + branding
          → ISR caches, revalidates on config change
```

## Data Model

### Agent Config (config/agents/{agent-id}.json)

Already defined in repo-restructure design. Contains identity, location,
branding, integrations, compliance.

### Agent Content (config/agents/{agent-id}.content.json)

Controls what sections are enabled and their data:

```json
{
  "template": "modern-green",
  "sections": {
    "hero": {
      "enabled": true,
      "headline": "Sell Your Home with Confidence",
      "tagline": "Forward. Moving.",
      "cta_text": "Get Your Free Home Value",
      "cta_link": "#cma-form"
    },
    "stats": {
      "enabled": false,
      "items": []
    },
    "services": {
      "enabled": true,
      "items": [
        { "title": "Expert Market Analysis", "description": "..." },
        { "title": "Strategic Marketing Plan", "description": "..." }
      ]
    },
    "how_it_works": {
      "enabled": true,
      "steps": [
        { "number": 1, "title": "Submit Your Info", "description": "..." },
        { "number": 2, "title": "Get Your Report", "description": "..." },
        { "number": 3, "title": "Schedule a Walkthrough", "description": "..." }
      ]
    },
    "sold_homes": {
      "enabled": false,
      "items": []
    },
    "testimonials": {
      "enabled": false,
      "items": []
    },
    "cma_form": {
      "enabled": true,
      "title": "What's Your Home Worth?",
      "subtitle": "Get a free, professional Comparative Market Analysis"
    },
    "about": {
      "enabled": true,
      "bio": "AI-generated placeholder based on agent info...",
      "credentials": []
    },
    "city_pages": {
      "enabled": false,
      "cities": []
    }
  }
}
```

Pass 1 creates this with hero, services, how_it_works, cma_form, and about
enabled. Other sections are disabled with empty data. Pass 2 fills them in.

## Branding Extraction

When agent uploads logo, business card, or provides existing website URL:

1. **Image upload (logo/business card):**
   - Extract dominant colors using color quantization
   - Map to primary (most dominant), secondary, accent (contrast color)
   - Detect if light or dark theme is appropriate

2. **Website URL:**
   - Fetch the page, extract CSS color values
   - Identify brand colors from headers, buttons, links
   - Extract font families from computed styles

3. **Confirmation:**
   - Present extracted palette to agent in chat
   - Show color swatches: "I found these from your materials — look right?"
   - Agent confirms or says "make it more blue" etc.
   - AI adjusts and confirms again

4. **Fallback:**
   - If extraction fails or agent has no materials
   - Fall back to template preset colors
   - Agent can pick from curated palettes

## AI Agent Builder (Chat UI)

### Portal Chat Interface

Built into apps/portal as a conversational onboarding flow:

- Branded Real Estate Star chat UI
- AI agent guides the conversation (collect info, extract branding, etc.)
- Supports file uploads (logo, business card images)
- Can embed live site preview (iframe) after deploy
- Can pre-populate and interact with the agent's CMA form
- Shows progress indicators ("Setting up your site..." "Deploying...")

### AI Capabilities During Onboarding

The AI agent in the chat must be able to:
- Parse uploaded images for color extraction
- Generate placeholder content (bio, service descriptions)
- Create agent config + content JSON files
- Create a git branch and PR via API
- Monitor CI pipeline status (build, review, preview URL)
- Embed the preview URL in chat for agent review
- Pre-fill the CMA form with a sample property address
- Trigger/monitor the CMA pipeline after form submission

## Deploy Pipeline

```
AI generates config + content files
  → Creates branch: onboard/{agent-id}
    → Opens PR against main
      → AI code review runs automatically
        → Checks: valid JSON schema, no secrets, branding values valid,
          required sections present, template exists
        → CI builds the site for this agent
          → Generates preview URL
            → AI presents preview to agent in chat
              → Agent reviews and approves
                → PR merges to main
                  → Production deploy triggers
                    → {agent-id}.realestatestar.com is live
```

### Enrichment Updates (Pass 2+)

Same pipeline for every content update:
- AI collects new content (testimonials, sold homes, etc.)
- Updates {agent-id}.content.json
- New PR → AI review → preview → agent approves → merge → deploy

## Wow Moment Flow (Technical)

After the site deploys in Pass 1:

```
1. AI selects a real property address in agent's service area
   (web search for recently listed/sold property nearby)
2. AI renders agent's live site in an iframe in the chat
3. AI pre-fills the CMA form fields via postMessage or URL params:
   - First name: "Test"
   - Last name: "Lead"
   - Email: agent's own email (so they receive the result)
   - Phone: agent's phone
   - Address: the selected sample property
   - Timeline: "Just curious about my home's value"
4. AI instructs: "Click submit to see the magic"
5. Agent clicks submit
6. CMA skill triggers:
   a. Parses the lead submission
   b. Web searches for comparable sales near the property
   c. Generates branded CMA PDF (agent's colors, logo, signature)
   d. Drafts email with PDF attached
   e. Sends to the "lead" email (which is the agent's own email)
7. Agent receives the CMA email in their inbox within seconds
8. AI in chat: "Check your inbox — that's what your leads will experience"
```

## Templates (Initial Set)

### Template 1: "Emerald Classic" (based on Jenise prototype)
- Green/gold color scheme (customizable via branding)
- Circular headshot in hero
- Card-based services grid
- Clean stats bar

### Template 2: "Navy Modern" (future)
### Template 3: "Warm Neutral" (future)
### Template 4: "Luxury Dark" (future)

Start with one template derived from the prototype. Add more as we grow.

## Integration Requirements

### For Pass 1 to work end-to-end:
- Email provider connection (Gmail OAuth or Outlook) during onboarding
- CMA form submission handler (receives form data, triggers CMA skill)
- CMA skill running as a backend service (not just a Claude skill)
- PDF generation capability
- Email sending capability

### API Endpoints Needed (apps/api):
- POST /agents — create agent config
- PUT /agents/{id}/content — update content
- POST /agents/{id}/cma — trigger CMA generation
- POST /agents/{id}/deploy — trigger PR + deploy pipeline
- GET /agents/{id}/preview — get preview URL status

## Decisions Deferred

- Hosting provider for agent sites (analysis pending)
- OAuth flow details for Gmail/Outlook connection
- PDF generation library selection (.NET or external service)
- Chat UI framework (custom vs. library)
- Template builder for creating new templates
- Pricing for custom domain upgrade
