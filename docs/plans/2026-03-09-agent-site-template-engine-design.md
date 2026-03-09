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

### Data-First Onboarding

The AI proactively scrapes public real estate profiles BEFORE asking questions.
When an agent provides a Zillow, Realtor.com, or similar profile URL, the AI
extracts everything available:

**Extractable from Zillow/Realtor.com profiles:**
- Name, title, brokerage, license number
- Phone, email, website
- Headshot photo
- Service areas and office address
- Years of experience
- Homes sold count and active/sold listings (address, price, photos)
- Reviews/testimonials (text, rating, reviewer name)
- Specialties, designations, languages
- Bio text

This can fill 80-90% of both Pass 1 and Pass 2 from a single URL.
The AI only asks for what it couldn't find.

```
1.  Agent receives invite link
2.  Opens branded chat UI in portal (apps/portal)
3.  PASS 1 — GO LIVE + WOW MOMENT (~5 minutes):
    a. AI asks for profile URLs: Zillow, Realtor.com, MLS site, or any combo
    b. AI scrapes all provided profiles — extracts identity, location, stats,
       reviews, sold homes, listings, bio, headshot, and everything available
       (merges data across sources, MLS provides richest listing data)
    c. AI presents: "Here's what I found" — agent confirms or corrects
    d. AI asks for logo upload (for brand color extraction)
       - If agent has existing website, AI scrapes colors from there too
    e. AI extracts colors/fonts, confirms palette with agent
    f. AI sets up Google Workspace integration (single OAuth flow):
       - Gmail (send CMA emails, auto-replies)
       - Google Drive (store CMA PDFs, documents)
       - Google Docs (generate CMA reports as Docs)
       - Google Sheets (lead tracking spreadsheet)
       - Google Calendar (scheduling, future use)
       Fallback: Outlook/SMTP for non-Google agents
    g. AI generates agent config + content (mostly from scraped data)
    h. PR created → AI code review → preview URL generated
    i. Agent sees preview in chat, approves
    j. PR merges → production deploy → subdomain live
    k. AI embeds agent's live site in chat with CMA form pre-filled
       (sample property from agent's service area)
    l. AI instructs agent: "Click submit to see the magic"
    m. Agent clicks submit
    n. CMA generates → PDF email arrives in agent's inbox instantly
    o. WOW MOMENT — agent is hooked

    Minimum agent input: profile URL + logo upload + email connect + one click

4.  PASS 2 — ENRICH (only what the scraper couldn't find):
    a. AI identifies gaps from Pass 1 scrape
    b. Only asks for missing data — e.g.:
       - Additional testimonials not on Zillow
       - Sold homes not in public records
       - Custom services beyond defaults
       - City-specific landing page content for SEO
       - Lead capture preferences (CMA form, contact form, both)
    c. For many agents, Pass 2 may be minimal or unnecessary
    d. Each update → PR → AI review → preview → approve → merge
```

### Supported Profile Sources

The AI agent speaks real estate fluently. It knows the industry ecosystem —
brokerages, MLS systems, portals, review sites — and can extract data from
any of them. It should never ask "what's your Zillow URL?" generically. It
should ask smart questions based on what it already knows:

- "Which brokerage are you with?" → scrapes the brokerage team page
- "Which MLS do you belong to?" → knows NJMLS, GSMLS, Bright MLS, etc.
- "Do you have a Zillow or Realtor.com profile?" → scrapes both

**The AI accepts ANY URL an agent provides and extracts what it can.**
It is not limited to a predefined list. If an agent pastes a link, the AI
scrapes it and pulls whatever structured data is available.

#### Portal & Listing Sites
| Source | What We Can Extract |
|--------|-------------------|
| Zillow Agent Profile | Name, brokerage, phone, email, headshot, reviews, sold homes, bio, stats, service areas |
| Realtor.com Profile | Name, brokerage, phone, email, headshot, reviews, sold/active listings, bio, specialties |
| Homes.com Profile | Name, brokerage, listings, reviews, contact info |
| Redfin Agent Page | Name, brokerage, reviews, sold history, stats |
| Trulia Agent Profile | Name, reviews, listings (powered by Zillow data) |
| FastExpert Profile | Name, reviews, stats, specialties |
| RateMyAgent Profile | Reviews, ratings, sold history |

#### MLS Systems (by region)
| Source | Coverage |
|--------|----------|
| NJMLS | Northern NJ |
| GSMLS (Garden State MLS) | Central NJ |
| Bright MLS | NJ, PA, DE, MD, DC, VA, WV |
| CRMLS (California) | Southern CA |
| NWMLS (Northwest) | WA, OR |
| Stellar MLS | FL |
| HAR (Houston) | TX (Houston metro) |
| MRED (Midwest) | IL, IN, WI |
| Any other MLS | AI attempts to scrape agent/listing data from any MLS URL provided |

The AI should know which MLS systems serve the agent's state/region and
ask specifically: "Are you on GSMLS or NJMLS?" not "what's your MLS?"

#### Brokerage Sites
| Source | What We Can Extract |
|--------|-------------------|
| Keller Williams agent page | Name, headshot, bio, contact, listings, team info |
| RE/MAX agent page | Name, headshot, bio, designations, listings |
| Coldwell Banker agent page | Name, headshot, bio, sold history, contact |
| Century 21 agent page | Name, headshot, bio, listings, reviews |
| eXp Realty agent page | Name, headshot, bio, contact, listings |
| Compass agent page | Name, headshot, bio, listings, sold data |
| Sotheby's agent page | Name, headshot, bio, luxury listings |
| Independent brokerage site | AI scrapes whatever structured data is available |

When an agent says "I'm with Green Light Realty" or "I'm at Keller Williams
in Edison," the AI should know to look for their profile on that brokerage's
site and scrape it proactively.

#### Review & Social
| Source | What We Can Extract |
|--------|-------------------|
| Google Business Profile | Reviews, rating, phone, address, hours, photos |
| Yelp Business Page | Reviews, rating, contact |
| Facebook Business Page | Reviews, contact, photos, about |
| LinkedIn Profile | Bio, experience, credentials, headshot |
| Instagram Business | Photos, follower count, branding style |

#### Uploads
| Source | What We Can Extract |
|--------|-------------------|
| Business card (image) | Name, phone, email, brokerage, logo, brand colors |
| Logo (image) | Brand colors, visual identity |
| Agent's existing website | Colors, fonts, logo, bio, contact, testimonials, listings |

### Data Merge Strategy

The AI collects from all available sources and merges intelligently:

1. **Identity fields** (name, phone, email): prefer the agent's own input,
   then brokerage site, then portal sites
2. **Reviews/testimonials**: aggregate from all sources, deduplicate,
   prefer most recent
3. **Listings/sold homes**: MLS is authoritative, supplement with portal data
4. **Bio text**: prefer agent's own website or brokerage page (most curated),
   fall back to portal profiles
5. **Photos/headshot**: prefer highest resolution source
6. **Stats**: cross-reference across sources, use highest verified numbers
7. **Conflicts**: present to agent for resolution ("Zillow says 100 homes
   sold, Realtor.com says 85 — which is accurate?")

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
- Google Workspace OAuth during onboarding (single consent for Gmail, Drive, Docs, Sheets, Calendar)
- Google Workspace CLI (`gws`) installed on backend — `npm install -g @googleworkspace/cli`
- CMA form submission handler (receives form data, triggers CMA skill)
- CMA skill running as a backend service (not just a Claude skill)
- PDF generation capability
- Email sending via `gws gmail` (replaces direct Gmail API / Formspree)
- Lead logging via `gws sheets` (auto-creates tracking spreadsheet)
- Document storage via `gws drive` (organized per-agent folder)
- Fallback: Outlook/SMTP for non-Google agents

### Google Workspace CLI (`gws`) Integration

The `gws` CLI (https://github.com/googleworkspace/cli) provides a unified
interface to all Google Workspace APIs. Installed via npm, authenticated via
OAuth. We use it for:

| Service | Use Case |
|---------|----------|
| `gws gmail` | Send CMA emails with PDF attachments, auto-replies to leads |
| `gws drive` | Store CMA PDFs in agent's Drive, organized by lead/date |
| `gws docs` | Generate Google Doc version of CMA for easy sharing |
| `gws sheets` | Log leads in tracking spreadsheet (auto-created per agent) |
| `gws calendar` | Future: schedule walkthroughs, photographer appointments |

Authentication: During onboarding, agent completes a single Google OAuth
consent screen requesting scopes for Gmail, Drive, Docs, Sheets, and Calendar.
Credentials stored securely per-agent (never in git — see config/agents/*.credentials.json
in .gitignore). The `gws` CLI reads credentials via `GOOGLE_WORKSPACE_CLI_TOKEN`
or local encrypted storage.

### CMA Pipeline (with `gws`)

```
Lead submits CMA form on agent site
  → API receives form data (POST /agents/{id}/cma)
    → CMA skill generates branded PDF
      → gws drive: upload PDF to agent's "CMA Reports" folder
      → gws gmail: send email to lead with PDF attached
      → gws sheets: log lead in agent's tracking spreadsheet
      → gws docs: (optional) create Google Doc version for agent
  → Lead receives email with CMA within seconds
  → Agent sees new row in their tracking spreadsheet
```

### API Endpoints Needed (apps/api):
- POST /agents — create agent config
- PUT /agents/{id}/content — update content
- POST /agents/{id}/cma — trigger CMA generation + gws pipeline
- POST /agents/{id}/deploy — trigger PR + deploy pipeline
- GET /agents/{id}/preview — get preview URL status
- POST /agents/{id}/auth/google — handle Google OAuth callback, store credentials

## Decisions Made

### Hosting: Cloudflare Pages
- Zero egress fees — critical for multi-tenant bandwidth
- Unlimited bandwidth on free tier
- Best edge performance (300+ data centers)
- Next.js support via OpenNext adapter (mature in 2026)
- Free → $5/mo pro tier

### PDF Generation: QuestPDF (.NET)
- MIT license, free for companies under $1M revenue
- Fluent C# API — fits .NET 10 backend
- Purpose-built for reports, invoices, data-driven documents
- No HTML rendering dependency — clean programmatic layout
- NuGet: `QuestPDF`

### Chat UI: assistant-ui (React)
- TypeScript/React library built specifically for AI chat
- YC-backed, actively maintained
- Supports streaming responses, tool call visualization, file uploads
- File uploads needed for logo/business card during onboarding
- npm: `@assistant-ui/react`

### Google Cloud OAuth Setup
- Google Cloud project: "Real Estate Star"
- OAuth consent screen: external, production
- OAuth 2.0 client ID: web application type
- Scopes requested in single consent flow:
  - `https://www.googleapis.com/auth/gmail.send`
  - `https://www.googleapis.com/auth/drive.file`
  - `https://www.googleapis.com/auth/documents`
  - `https://www.googleapis.com/auth/spreadsheets`
  - `https://www.googleapis.com/auth/calendar.events`

### Credential Storage: Encrypted Local Files → Secret Manager
- MVP: Encrypted local files (matches `gws` CLI native storage)
- Production: Google Secret Manager (already in GCP for OAuth)
- Agent credentials stored per-agent, never in git
- `.gitignore` already excludes `config/agents/*.credentials.json`

## Decisions Deferred

- Template builder for creating new templates (YAGNI — one template for now)
- Pricing for custom domain upgrade (needs market research with real agents)
