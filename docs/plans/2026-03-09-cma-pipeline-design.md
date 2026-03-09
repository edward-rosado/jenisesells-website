# CMA Pipeline - Design

**Date:** 2026-03-09
**Status:** Approved
**Author:** Eddie Rosado + Claude

## Executive Summary

A hybrid AI + deterministic pipeline that generates Comparative Market Analyses
from lead form submissions. The .NET 10 API orchestrates the pipeline, Claude AI
handles comp analysis, lead research, and market narrative, QuestPDF generates
the PDF, and Google Workspace CLI (`gws`) handles email delivery, Drive
organization, lead tracking, and agent notification via Lead Briefs. The pipeline
adapts its depth based on lead intent and delivers results in under 2 minutes.

## Pipeline Flow

```
Lead submits CMA form on agent site
  → API receives POST /agents/{id}/cma
    → Step 1: Parse lead data
    → Step 2: Fetch comps (MLS → API → scrape Zillow/Realtor/Redfin) [parallel]
    → Step 3: Research lead (public records, LinkedIn, social) [parallel with Step 2]
    → Step 4: Claude API analyzes comps + lead data, generates value estimate,
              market narrative, lead insights, and conversation starters
    → Step 5: QuestPDF generates branded PDF (adaptive depth based on intent)
    → Step 6: gws drive — create lead folder structure, upload CMA PDF
    → Step 7: gws docs — create Lead Brief with AI-researched lead intelligence
              (Google Drive sends native notification to agent)
    → Step 8: gws gmail — send branded email with PDF to lead
    → Step 9: gws sheets — log lead in tracking spreadsheet
  → Lead receives CMA email (~2 minutes)
  → Agent receives Drive notification with Lead Brief
```

## Hybrid AI Architecture

```
.NET API (deterministic, fast, orchestrator):
├── Receives form data
├── Queries comp sources in parallel (MLS, API, scraping)
├── Queries lead research sources in parallel
├── Deduplicates and normalizes all data
├── Calls Claude API with structured comp data + lead research
├── Generates PDF with QuestPDF
├── Orchestrates gws calls (drive, docs, gmail, sheets)
└── Pushes real-time status updates via WebSocket

Claude API (AI, analytical):
├── Receives: subject property + comp data + lead research
├── Returns:
│   ├── Value estimate range (low / mid / high)
│   ├── Market narrative (adapted to lead intent)
│   ├── Pricing recommendation (for serious sellers)
│   ├── Lead insights (life events, motivations, equity estimate)
│   └── Conversation starters (personalized for agent's first call)
└── Adapts tone/depth based on timeline field
```

## Comp Data Sourcing

Priority order — all sources queried in parallel, results merged:

```
1. Agent's MLS feed (authoritative)
   → NJMLS, GSMLS, Bright MLS, CRMLS, Stellar, etc.
   → Most accurate, includes off-market data
   → Agent trusts it — it's what they use daily

2. Real estate data API (ATTOM Data or RealtyMole)
   → Structured, reliable, fills MLS gaps
   → Cost: $0.01-0.10 per query

3. Web scraping (Zillow, Realtor.com, Redfin)
   → Supplementary, widest public coverage
   → All three scraped for maximum data
```

### Merge Strategy

- Deduplicate by address + sale date
- MLS data wins on conflicts
- Flag each comp with its source for transparency in the PDF
- Target: 5-8 comps within 0.5 miles, sold in last 6 months (prefer 3 months)
- If fewer than 5 comps found, expand radius to 1 mile, then 3 miles

## Lead Research

When a lead submits their name, email, phone, and property address, the AI
proactively researches them across public sources:

### Data Sources

| Data Point | Source |
|------------|--------|
| Social media profiles | LinkedIn, Facebook, Instagram (name + location) |
| Occupation / employer | LinkedIn |
| Property ownership history | County tax records, public deed records |
| How long they've owned the home | Purchase date from public records |
| What they paid for it | Public sale records |
| Current tax assessment | County assessor |
| Mortgage info (if public) | County recorder |
| Estimated equity | Purchase price + appreciation vs. remaining mortgage |
| Life events (job change, etc.) | LinkedIn activity, public social |
| Neighborhood context | School ratings, walkability, local amenities |

### Privacy Considerations

- Only public data is used — no paid background checks, no private databases
- Lead Brief is stored in agent's private Google Drive, not shared
- No sensitive financial data beyond what's in public records
- Conversation starters are suggestions, not scripts — agent uses judgment

## Lead Brief (Google Doc)

Created automatically via `gws docs` in the lead's folder. The agent's
Google Drive sends a native notification when the file is created — no
custom notification infrastructure needed.

### Lead Brief Template

```
📋 New Lead Brief — {Lead Name}
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Property: {Address}, {City}, {State} {Zip}
Timeline: {Timeline from form}
Submitted: {Date} at {Time}

About {First Name}:
• {Occupation} at {Employer} (LinkedIn)
• Purchased {Address} in {Month Year} for {Purchase Price}
• Owned for {Duration}
• Estimated equity: {Equity Range}
• {Life event insight if found — job change, growing family, etc.}

Property Details (public records):
• {Beds} bed / {Baths} bath / {Sqft} sqft, built {Year}
• Lot: {Lot Size}
• Current tax assessment: {Assessment}
• Annual property taxes: {Tax Amount}
• {Liens/judgments status}

Market Context:
• {N} comparable sales found in {radius}
• Estimated current value: {Value Range}
• Median days on market: {DOM}
• Market trending: {Buyer's/Seller's/Balanced} market

Conversation Starters:
• "{Personalized opener based on life event or equity}"
• "{Market-based opener connecting their property to trends}"
• "{Neighborhood-based opener — schools, amenities, demand}"

CMA Status: ✅ Sent to {lead email}
CMA Report: [Link to PDF in this folder]

Recommended Next Steps:
1. {Priority action based on timeline — e.g. "Call within 1 hour"}
2. {Contextual tip — e.g. "Reference the job change naturally"}
3. Schedule walkthrough
4. Prepare listing agreement

Contact:
📞 {Phone}
📧 {Email}
```

### What Makes This Powerful

The AI connects dots between the lead's life events and property data:
- Job change in LinkedIn → possible relocation trigger
- Owned 7+ years → significant equity built, ready to upgrade
- Growing family signals → may need more space
- Tax assessment rising → good time to sell at peak

The agent walks into every call with 30 minutes of research done in 30 seconds.

## Real-Time Progress (Thank You Page)

After form submit, the lead sees a progress indicator via WebSocket:

```
✓ Received your property details
✓ Searching MLS databases...
✓ Found {N} comparable sales
✓ Analyzing market trends...
✓ Generating your personalized report...
✓ Your report has been sent to your email!
```

This builds trust — the lead sees thorough research happening, making the
analysis feel comprehensive and credible. Important for listing agents who
need sellers to trust their pricing recommendations.

### Implementation

- WebSocket connection opened when thank-you page loads
- API pushes status events as each pipeline step completes
- Frontend renders checkmarks progressively
- If WebSocket fails, falls back to polling GET /agents/{id}/cma/{jobId}/status

## Adaptive Report Depth

Based on the "When are you looking to sell?" form field:

| Timeline | Report Type | Pages | Content |
|----------|------------|-------|---------|
| "Just curious" | Lean | 3-4 | Cover, comp table, value range, agent CTA |
| "6-12 months" | Standard | 5 | + property overview, AI market narrative |
| "3-6 months" | Standard | 5 | Same as above |
| "1-3 months" | Comprehensive | 7-8 | + market trends, price/sqft, neighborhood, DOM, listing strategy |
| "ASAP" | Comprehensive | 7-8 | + urgency language, recommended pricing, equity analysis |

### PDF Structure — Lean (3-4 pages)

1. **Cover** — property address, prepared for/by, agent branding, date
2. **Comparable Sales Table** — address, price, beds/baths/sqft, $/sqft, sale date, source
3. **Estimated Value** — range (low-mid-high), calculation method, brief market context
4. **About Agent + CTA** — bio, contact, "schedule a walkthrough"

### PDF Structure — Comprehensive (7-8 pages)

1. **Cover**
2. **Subject Property Overview** — details, neighborhood, school district
3. **Comparable Sales Table** — full details with source attribution
4. **Market Analysis** — AI narrative on local trends, DOM, inventory levels
5. **Price Per Square Foot Analysis** — subject vs. comps breakdown
6. **Estimated Value + Pricing Strategy** — range, recommended list price, AI rationale
7. **Neighborhood Overview** — schools, amenities, walkability, buyer demand
8. **About Agent + CTA + Next Steps**

All pages use agent's branding (colors from agent.branding.*, logo, contact in footer).

## Google Drive Organization

Auto-created on first CMA for each agent. Maintained by `gws` throughout
the client lifecycle. Nothing is ever deleted — inactive items move to
folder 5.

```
Real Estate Star/
├── 1 - Leads/
│   └── {Client Name}/
│       └── {Property Address}/
│           ├── CMA_{date}.pdf
│           ├── Lead-Brief.gdoc
│           └── Communications/
│               └── {date}_{description}.pdf
│
├── 2 - Active Clients/
│   └── {Client Name}/
│       ├── Agreements/
│       │   └── {document}_v{n}.pdf (all versions preserved)
│       ├── Documents Sent/
│       │   └── {date}_{document}.pdf
│       └── Communications/
│           └── {date}_{description}.pdf
│
├── 3 - Under Contract/
│   └── {Client Name}/
│       └── {Address} Transaction/
│           ├── Contracts/
│           │   └── {contract}_v{n}.pdf (all revisions tracked)
│           ├── Documents Sent/
│           ├── Inspection/
│           ├── Appraisal/
│           └── Communications/
│
├── 4 - Closed/
│   └── {Client Name}/
│       ├── Audit Log/
│       ├── Reports/
│       └── Communications/
│
├── 5 - Inactive/
│   ├── Dead Leads/
│   │   └── {Client Name}/
│   │       └── Communications/
│   └── Expired Clients/
│       └── {Client Name}/
│           ├── Documents Sent/
│           └── Communications/
│
└── 6 - Referral Network/
    ├── Agents/
    │   └── {Agent Name} - {Brokerage}/
    │       ├── Referral-Agreement.pdf
    │       └── Transactions/
    │           └── {date}_{client}_{property}.txt
    ├── Brokerages/
    │   └── {Brokerage Name}/
    │       ├── Master-Referral-Agreement.pdf
    │       └── Agents/
    └── Summary/
        └── Referral-Fees-Tracker.gsheet
```

### Key Principles

- **Communications/** in every stage — all inbound/outbound tracked, timestamped as PDFs
- **Contracts versioned** — _v1, _v2, _v3 — every revision preserved, never overwritten
- **Documents Sent/** — everything ever given to a client, timestamped
- **Nothing deleted** — inactive items moved to 5 - Inactive/ with a reason noted
- **Referral Network** feeds into contract skill for commission calculations
- **Referral-Fees-Tracker.gsheet** auto-updated via `gws sheets` on each closed transaction

## Onboarding Demo vs Production

| Aspect | Production (Seller) | Onboarding Demo (Agent) |
|--------|-------------------|----------------------|
| Who submits form | Real lead / homeowner | AI pre-fills, agent clicks submit |
| Email recipient | Lead's email | Agent's own email (sees seller experience) |
| Lead Brief | Created silently in Drive | AI walks agent through it in chat |
| Drive organization | Auto-files silently | AI shows the folder structure created |
| Sheets logging | Row added silently | AI shows the tracking spreadsheet |
| Progress indicator | Lead sees on thank-you page | Agent sees in portal chat + thank-you page |

During the demo, the AI says: "Check your Drive — I've created your lead
management system and filed the first CMA. Check your inbox — that's what
your sellers will receive. And check your Sheets — every lead is tracked
automatically."

## Agent Notification Flow (No Custom Infrastructure)

```
CMA pipeline completes
  → gws docs creates Lead Brief in 1 - Leads/{Name}/{Address}/
    → Google Drive sends native notification to agent
      → Agent opens Lead Brief on phone/desktop
        → Sees: lead details, property research, equity estimate,
          conversation starters, recommended next steps
          → Agent makes the call, fully prepared
```

No push notification service. No custom mobile app. No email notification
system to build. Google Drive's built-in notifications do the work.

For agents who want more aggressive notifications, they can:
- Turn on Google Drive mobile notifications (most already have this)
- Use a Google Apps Script (we auto-deploy during onboarding) that
  watches the Leads folder and sends a formatted email summary
- Future: integrate with Google Chat for real-time alerts

## API Endpoints

```
POST /agents/{id}/cma
  Body: { firstName, lastName, email, phone, address, city, state, zip,
          timeline, beds, baths, sqft, notes }
  Returns: { jobId, status: "processing" }

GET /agents/{id}/cma/{jobId}/status
  Returns: { status, step, totalSteps, message }
  Status values: "parsing" | "searching_comps" | "researching_lead" |
                 "analyzing" | "generating_pdf" | "organizing_drive" |
                 "sending_email" | "logging" | "complete"
  WebSocket upgrade available for real-time push

GET /agents/{id}/leads
  Returns: [{ id, name, address, timeline, cmaStatus, submittedAt, driveLink }]

POST /agents/{id}/referrals
  Body: { agentName, brokerage, splitOurs, splitTheirs, agreementUrl }
  Returns: { id }

GET /agents/{id}/referrals
  Returns: [{ id, agentName, brokerage, split, transactions }]
```

## Error Handling

| Failure | Recovery |
|---------|----------|
| MLS unavailable | Continue with API + scrape sources, note in PDF |
| All comp sources fail | Alert agent, ask to manually provide comps |
| Lead research finds nothing | Lead Brief shows property data only, skips "About" section |
| Claude API timeout | Retry once, fall back to template-based narrative |
| Claude API error | Use simpler statistical analysis (avg price/sqft from comps) |
| QuestPDF generation fails | Retry, alert agent if persistent |
| gws gmail send fails | Queue for retry (3 attempts), alert agent |
| gws drive unavailable | Store PDF locally, sync to Drive when available |
| gws sheets fails | Queue for retry, don't block pipeline |
| WebSocket connection lost | Client falls back to polling status endpoint |

## Performance Budget

| Step | Target Time | Notes |
|------|------------|-------|
| Parse lead data | <100ms | Validation only |
| Fetch comps (all sources) | <30s | Parallel queries, timeout at 30s |
| Research lead | <15s | Parallel with comps, timeout at 15s |
| Claude API analysis | <30s | Single call with all data |
| QuestPDF generation | <5s | CPU-bound, fast |
| gws drive + docs | <10s | Create folder + Lead Brief |
| gws gmail | <5s | Send with attachment |
| gws sheets | <3s | Append row |
| **Total** | **<90s** | **Target: under 2 minutes** |

## Dependencies

- .NET 10 API scaffolded at `apps/api`
- QuestPDF NuGet package
- Google Workspace CLI (`gws`) installed on API server
- Google OAuth credentials per agent (from onboarding)
- Claude API key (Anthropic account)
- At least one comp data source configured (MLS, API, or scraping)
- WebSocket support in API for real-time status updates
