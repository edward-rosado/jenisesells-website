---
name: cma
description: |
  **Instant CMA Generator**: Create a professional Comparative Market Analysis (CMA) PDF report for a residential property. Use this skill whenever an agent wants to generate a CMA, home valuation, market analysis, or comp report for a potential seller.
  - MANDATORY TRIGGERS: CMA, comparative market analysis, home valuation, home value, what's my home worth, comps, comparable sales, market analysis, property value, seller lead, price opinion
  - Also trigger when an agent pastes a form submission from their website containing a seller's name, property address, and contact info
  - Even casual mentions like "run comps on 123 Main St" or "what's this house worth" or "new lead just came in" should trigger this skill
---

# Instant CMA Generator

You are generating a professional Comparative Market Analysis on behalf of a licensed real estate agent. The agent's identity, branding, location, and contact information are loaded from their config profile. This CMA will be sent directly to a potential seller, so it needs to look polished and professional.

## Agent Config

Before executing any step, load the agent's profile from:

```
config/agents/{agent-id}.json
```

The profile conforms to `config/agent.schema.json` and provides all tenant-specific values used throughout this skill. The following variable namespaces are referenced below:

| Namespace | Fields Used |
|-----------|-------------|
| `{agent.identity.*}` | name, title, brokerage, phone, email, website, languages, tagline |
| `{agent.location.*}` | state, service_areas |
| `{agent.branding.*}` | primary_color, secondary_color, accent_color, font_family |
| `{agent.integrations.*}` | email_provider |

**How to resolve the agent ID:**
1. If the conversation is scoped to a known agent (e.g., via CLI flag, environment variable, or session context), use that ID.
2. If ambiguous, ask: "Which agent profile should I use?" and list available profiles from `config/agents/`.
3. Never assume a default agent.

<details>
<summary>Example: loading jenise-buckalew</summary>

```json
{
  "id": "jenise-buckalew",
  "identity": {
    "name": "Jenise Buckalew",
    "title": "REALTOR\u00ae",
    "brokerage": "Green Light Realty LLC",
    "phone": "(347) 393-5993",
    "email": "jenisesellsnj@gmail.com",
    "website": "jenisesellsnj.com",
    "languages": ["English", "Spanish"],
    "tagline": "Forward. Moving."
  },
  "location": {
    "state": "NJ",
    "service_areas": ["Middlesex County", "Monmouth County", "Ocean County"]
  },
  "branding": {
    "primary_color": "#1B5E20",
    "secondary_color": "#2E7D32",
    "accent_color": "#C8A951",
    "font_family": "Segoe UI"
  }
}
```

</details>

## AUTOMATED WORKFLOW

The .NET API drives the full CMA pipeline. A single `POST /agents/{id}/cma` request triggers the entire automation:

1. Load the agent config
2. Parse the lead info
3. Research comparable sales
4. Generate the CMA PDF (QuestPDF)
5. Create a Lead Brief in Google Drive
6. Draft and send a personalized email with the CMA attached
7. Report progress via real-time WebSocket updates

**This is fully automated.** The API handles the entire pipeline end-to-end. WebSocket clients receive step-by-step progress events throughout execution.

## What You Need

At minimum: **Property address** (street, city, state, zip)

Nice to have (DO NOT ask if a form submission was pasted):
- Beds / Baths / Approx sqft
- Seller's name and email
- Any known details about the home

## Step-by-Step Workflow

### 1. Parse the Seller Info

Extract property address, seller name, email, phone, timeline, and contact preferences from the pasted lead data.

### 2. Research Comparable Sales

Use web search to find 3-5 recent comparable sales near the subject property:
- Recently sold homes within ~0.5 miles
- Similar bed/bath count and square footage
- Sold within the last 6 months (prefer 3 months)
- **Scope the search to `{agent.location.state}` and the relevant area(s) within `{agent.location.service_areas}`**

For each comp, find:
| Field | Description |
|-------|-------------|
| Address | Full street address |
| Sale price | Final sold price |
| Sale date | Date of closing |
| Beds/Baths/Sqft | Property specs |
| Price per sqft | Calculated from sale price and sqft |
| Days on market | DOM from list to close |

### 3. Estimate the Value Range

- Calculate average and median price per sqft from comps
- Apply to subject property sqft for baseline estimate
- Provide a value range (low to high)
- Note any adjustments needed (condition, lot size, upgrades, location premium)

### 4. Generate the CMA PDF

The .NET API uses QuestPDF to create a polished, branded PDF.

**Page 1 — Cover Page**
- Title: "Comparative Market Analysis"
- Property address
- "Prepared for: [Seller Name]"
- "Prepared by: {agent.identity.name}, {agent.identity.title}"
- "{agent.identity.brokerage}"
- Date of report
- Agent contact: {agent.identity.phone} | {agent.identity.email} | {agent.identity.website}

**Page 2 — Subject Property Overview**
- Property details (beds, baths, sqft, lot size, year built)
- Neighborhood description
- Location context within `{agent.location.service_areas}`

**Page 3 — Comparable Sales**
- Clean table with all comp data (address, price, date, specs, price/sqft, DOM)
- Map or proximity note for each comp relative to subject

**Page 4 — Estimated Value & Recommendation**
- Value range with methodology explanation
- Average and median price per sqft from comps
- Market context (buyer's market, seller's market, balanced)
- Strategic pricing recommendation
- Call to action: schedule a walkthrough / listing consultation

**Page 5 — About the Agent**
- Agent bio built from `{agent.identity.*}`
- Service areas from `{agent.location.service_areas}`
- Languages from `{agent.identity.languages}`
- Contact info: phone, email, website
- Tagline: `{agent.identity.tagline}`

### Design Guidelines

| Element | Value |
|---------|-------|
| Primary color | `{agent.branding.primary_color}` |
| Secondary color | `{agent.branding.secondary_color}` |
| Accent color | `{agent.branding.accent_color}` |
| Font family | `{agent.branding.font_family}` |
| Style | Clean, modern, professional |
| Page numbers | Every page |

All colors, fonts, and styling MUST come from the agent's branding config. Never hardcode color values.

### 5. Save the CMA PDF

Save to workspace: `CMA_[Address]_[Date].pdf`

### 6. Draft the Response Email

- Professional but warm tone
- Address lead by first name
- Highlight key findings: estimated value range, market conditions
- Reference attached CMA report
- Include call to action to schedule a walkthrough or listing consultation

**Subject:** `"Your Home Value Report — [Property Address]"`

**Email signature** — built dynamically from the agent config:

```
Warm regards,
{agent.identity.name}, {agent.identity.title}
{agent.identity.brokerage}
{agent.identity.phone}
{agent.identity.email}
{agent.identity.website}
```

If `{agent.identity.languages}` contains more than one language, append a line indicating multilingual support. For example, if languages include Spanish: "Se Habla Espanol". Adapt phrasing to the additional language(s).

If `{agent.identity.tagline}` is set, append it as the final line of the signature.

### 7. Send via Email Provider

The API uses `gws gmail` (Google Workspace Gmail API) to send the email with the CMA PDF attached:

| Provider | Method |
|----------|--------|
| `gmail` | `gws gmail` — sends directly via the Google Workspace API with the PDF attachment |
| `outlook` | Outlook integration (when available) |

If the provider is unsupported or not configured, fall back to creating the draft text and instructing the agent to send manually.

### 8. Lead Brief

The pipeline also creates a Lead Brief document in Google Drive containing the lead's info, CMA summary, and next steps. This gives the agent a quick-reference doc for follow-up.

## State-Specific Notes

CMA generation is largely state-agnostic, but some nuances apply depending on `{agent.location.state}`:

| Consideration | Details |
|---------------|---------|
| **Disclosure language** | Some states require specific disclaimers on CMAs (e.g., "This is not an appraisal"). Always include a disclaimer footer: _"This Comparative Market Analysis is an estimate of value based on comparable sales data and is not a formal appraisal. For an official valuation, consult a licensed appraiser."_ |
| **MLS data availability** | Comp accuracy depends on public data access. In states with limited public records, note that comps are sourced from publicly available data and may not reflect all market activity. |
| **Licensing display** | If `{agent.identity.license_id}` is present, include it on the cover page and about page. Some states require license numbers on all marketing materials. |
| **Service area scope** | Always restrict comp searches to the agent's `{agent.location.service_areas}` first. Expand the radius only if fewer than 3 comps are found within the service area. |
| **Property tax context** | Property tax rates vary significantly by state and locality. When available, include local tax rate context in the valuation section to help sellers understand carrying costs. |
| **Market seasonality** | Reference state or regional seasonal trends when providing market context (e.g., shore communities in coastal states have different cycles than inland markets). |

## Error Handling

| Scenario | Action |
|----------|--------|
| Fewer than 3 comps found | Expand search radius to 1 mile, then 2 miles. If still insufficient, note limited data and provide a wider value range with caveats. |
| No seller email provided | Generate the CMA PDF and present it to the agent. Skip Steps 6-7. |
| Agent config missing fields | Warn the agent about missing config fields. Use reasonable defaults for non-critical fields (e.g., default font "Helvetica" if font_family is missing). Halt if identity fields (name, email) are missing. |
| Email provider not configured | Generate the CMA PDF and email draft text. Instruct the agent to send manually. |
| Property address is ambiguous | Ask the agent to confirm the full address including city, state, and zip before proceeding. This is the ONE exception to the no-questions rule. |
