# Pages Architecture — Account-Based Multi-Tenancy

## Goal

Restructure the agent site from a flat agent-based config model to an account-based model that supports both solo agents and brokerages with teams. Introduce a `pages` wrapper in content.json, rename sections to be generic, add a `profiles` section type, and support `/agents/{id}` routing for team member pages.

## Background

Today each agent has a flat config at `config/agents/{agent-id}/`. This works for solo agents but can't represent a brokerage with multiple agents sharing branding and a template. The site also uses real-estate-specific section names (`sold_homes`, `services`, `cma_form`) that limit reusability.

### Current State

```
config/agents/{agent-id}/
├── config.json    ← identity + branding + brokerage + location + compliance
├── content.json   ← template + navigation + sections (flat)
└── legal/         ← accessibility, privacy, terms markdown
```

- 4 agent configs on main (jenise-buckalew, test-emerald, test-modern, test-warm) + 7 more on `feat/branding-test-agents` branch (test-luxury, test-loft, test-beginnings, test-light-luxury, test-coastal, test-country, test-commercial)
- 10 templates (3 on main, 7 on feat/branding-test-agents)
- Sections: hero, stats, services, how_it_works, sold_homes, testimonials, cma_form, about, city_pages (disabled)
- Additional fields in current schema: `contact_info` (ContactMethod[]), `integrations` (email_provider, tracking/analytics), `city_pages` section
- Config registry prebuilt at build time for Cloudflare Workers (no fs at runtime)
- URL model: `{handle}.real-estate-star.com` renders the agent's single-scroll page
- **Starting point**: This migration applies on top of the `feat/branding-test-agents` branch (or after it merges to main), which has all 11 agent configs

## Architecture

### Account Hierarchy

```
Account (handle = subdomain)
├── account.json        ← branding, brokerage, template, broker?, agent?
├── content.json        ← navigation + pages (home, thank_you)
├── legal/              ← compliance markdown files
└── agents/             ← optional, absent for solo agents
    └── {agent-id}/
        ├── config.json ← agent identity
        └── content.json ← agent page content
```

Key concepts:

- **Account** = the business entity. This is the subdomain. It holds branding, brokerage details, template choice, and compliance info.
- **Agent** = a person. Agents either live inline in `account.json` (solo) or in the `agents/` folder (team).
- **Solo mode**: `account.json.agent.enabled: true`, no `agents/` folder. The home page uses the inline agent's identity.
- **Company mode**: `agents/` folder present with sub-directories. Home page uses brokerage/broker identity. Each agent gets a page at `/agents/{id}`.
- **Both**: An account can have `agent.enabled: true` AND an `agents/` folder — a broker who also has a personal presence plus a team.

### Folder Structure

```
config/accounts/
├── jenise-buckalew/              ← production, solo agent
│   ├── account.json
│   ├── content.json
│   └── legal/
├── test-emerald/                 ← test, solo (emerald-classic)
│   ├── account.json
│   └── content.json
├── test-modern/                  ← test, solo (modern-minimal)
│   ├── account.json
│   └── content.json
├── test-warm/                    ← test, solo (warm-community)
│   ├── account.json
│   └── content.json
├── test-luxury/                  ← test, solo (luxury-estate)
│   ├── account.json
│   └── content.json
├── test-loft/                    ← test, solo (urban-loft)
│   ├── account.json
│   └── content.json
├── test-beginnings/              ← test, solo (new-beginnings)
│   ├── account.json
│   └── content.json
├── test-light-luxury/            ← test, solo (light-luxury)
│   ├── account.json
│   └── content.json
├── test-coastal/                 ← test, solo (coastal-living)
│   ├── account.json
│   └── content.json
├── test-country/                 ← test, solo (country-estate)
│   ├── account.json
│   └── content.json
├── test-commercial/              ← test, solo (commercial)
│   ├── account.json
│   └── content.json
└── test-brokerage/               ← NEW test, company with agents
    ├── account.json
    ├── content.json
    └── agents/
        ├── agent-a/
        │   ├── config.json
        │   └── content.json
        └── agent-b/
            ├── config.json
            └── content.json
```

## Data Model

### account.json

```json
{
  "handle": "jenise-buckalew",
  "template": "emerald-classic",
  "branding": {
    "primary_color": "#1B5E20",
    "secondary_color": "#2E7D32",
    "accent_color": "#C8A951",
    "font_family": "Segoe UI",
    "logo_url": "/agents/jenise-buckalew/logo.png"
  },
  "brokerage": {
    "name": "Green Light Realty LLC",
    "license_number": "1751390",
    "office_address": "1109 Englishtown Rd, Old Bridge, NJ 08857",
    "office_phone": "(732) 251-2500 ext 714"
  },
  "broker": {
    "name": "Optional Broker Name",
    "title": "Managing Broker",
    "headshot_url": "/path/to/broker-headshot.jpg",
    "bio": "Optional broker bio..."
  },
  "agent": {
    "enabled": true,
    "id": "jenise-buckalew",
    "name": "Jenise Buckalew",
    "title": "REALTOR®",
    "phone": "(347) 393-5993",
    "email": "jenisesellsnj@gmail.com",
    "headshot_url": "/agents/jenise-buckalew/headshot.jpg",
    "license_number": "0676823",
    "languages": ["English", "Spanish"],
    "tagline": "Forward. Moving.",
    "credentials": ["REALTOR®"]
  },
  "location": {
    "state": "NJ",
    "service_areas": ["Middlesex County", "Monmouth County", "Ocean County"]
  },
  "location": {
    "state": "NJ",
    "service_areas": ["Middlesex County", "Monmouth County", "Ocean County"]
  },
  "integrations": {
    "email_provider": "gmail",
    "tracking": {
      "google_analytics_id": "G-XXXXXXXX",
      "meta_pixel_id": "1234567890",
      "gtm_container_id": "GTM-XXXXXXXX"
    }
  },
  "contact_info": [
    { "type": "phone", "value": "(347) 393-5993", "label": "Cell", "is_preferred": true },
    { "type": "email", "value": "jenisesellsnj@gmail.com", "label": "Email", "is_preferred": false }
  ],
  "compliance": {
    "state_form": "NJ-REALTORS-118",
    "licensing_body": "NJ Real Estate Commission",
    "disclosure_requirements": [
      "Lead-based paint disclosure (pre-1978)",
      "Seller property condition disclosure"
    ]
  }
}
```

Fields:

| Field | Required | Description |
|---|---|---|
| `handle` | yes | Subdomain identifier. Must be URL-safe. |
| `template` | yes | Template name. All pages under this account use it. |
| `branding` | yes | Colors, font, logo. Inherited by all pages. |
| `brokerage` | yes | Brokerage name and license. Required even for solo agents (regulatory). |
| `broker` | no | The brokerage owner/managing broker. Optional. |
| `agent` | no | Inline agent for solo accounts. Has `enabled` flag. |
| `agent.enabled` | yes (if agent present) | Whether this agent is the active face of the home page. |
| `location` | yes | State and service areas. Used for compliance and SEO. |
| `integrations` | no | Email provider, analytics tracking (GA4, Meta Pixel, GTM). Migrated from current config. |
| `contact_info` | no | Structured contact methods (phone, email) with preferred flag. Migrated from current content. |
| `compliance` | no | State-specific regulatory info. |

### agents/{id}/config.json (team member)

```json
{
  "id": "james-whitfield",
  "name": "James Whitfield",
  "title": "Senior Associate",
  "phone": "(908) 555-0101",
  "email": "james@sterling.com",
  "headshot_url": "/agents/sterling-associates/agents/james-whitfield/headshot.jpg",
  "license_number": "NJ-2024-67890",
  "languages": ["English"],
  "tagline": "Your land expert.",
  "credentials": ["REALTOR®", "CRS", "ABR"]
}
```

All fields match the shape of `account.json.agent` minus the `enabled` flag (team agents are always enabled if their folder exists).

### content.json (account level)

```json
{
  "navigation": {
    "items": [
      { "label": "Home", "href": "#hero", "enabled": true },
      { "label": "Services", "href": "#features", "enabled": true },
      { "label": "Team", "href": "#profiles", "enabled": true },
      { "label": "Portfolio", "href": "#gallery", "enabled": true },
      { "label": "Reviews", "href": "#testimonials", "enabled": true },
      { "label": "Contact", "href": "#contact_form", "enabled": true },
      { "label": "About", "href": "#about", "enabled": true }
    ]
  },
  "pages": {
    "home": {
      "sections": {
        "hero":          { "enabled": true, "data": { "headline": "...", "tagline": "...", "cta_text": "...", "cta_link": "#contact_form" } },
        "stats":         { "enabled": true, "data": { "items": [] } },
        "features":      { "enabled": true, "data": { "title": "Our Services", "items": [] } },
        "steps":         { "enabled": true, "data": { "title": "How It Works", "steps": [] } },
        "gallery":       { "enabled": true, "data": { "title": "Recent Sales", "items": [] } },
        "testimonials":  { "enabled": true, "data": { "title": "Client Reviews", "items": [] } },
        "profiles":      { "enabled": false, "data": { "title": "Meet Our Team", "items": [] } },
        "contact_form":  { "enabled": true, "data": { "title": "Get In Touch" } },
        "about":         { "enabled": true, "data": { "title": "About Us", "bio": "..." } }
      }
    },
    "thank_you": {
      "heading": "Thank You!",
      "subheading": "Your Free Home Value Report Is Being Prepared Now!",
      "body": "{firstName} will send your personalized Comparative Market Analysis...",
      "disclaimer": "This home value report is a CMA and is not an appraisal...",
      "cta_call": "Call {firstName}: {phone}",
      "cta_back": "Back to {firstName}'s Site"
    }
  }
}
```

### content.json (agent level — under agents/{id}/)

Same structure but without `navigation` (inherited from account):

```json
{
  "pages": {
    "home": {
      "sections": {
        "hero":          { "enabled": true, "data": { "headline": "...", "tagline": "..." } },
        "stats":         { "enabled": false, "data": {} },
        "features":      { "enabled": true, "data": { "title": "My Specialties", "items": [] } },
        "steps":         { "enabled": false, "data": {} },
        "gallery":       { "enabled": true, "data": { "title": "My Recent Sales", "items": [] } },
        "testimonials":  { "enabled": true, "data": { "title": "What Clients Say", "items": [] } },
        "profiles":      { "enabled": false, "data": {} },
        "contact_form":  { "enabled": true, "data": { "title": "Work With Me" } },
        "about":         { "enabled": true, "data": { "bio": "..." } }
      }
    },
    "thank_you": {
      "heading": "Thank You!",
      "subheading": "...",
      "body": "...",
      "cta_call": "Call {firstName}: {phone}",
      "cta_back": "Back to {firstName}'s Site"
    }
  }
}
```

## Section Naming

Generic names replace real-estate-specific names:

| Old Name | New Name | Rationale |
|---|---|---|
| `services` | `features` | Could be capabilities, offerings, programs — not just services |
| `how_it_works` | `steps` | Matches component naming, generic process steps |
| `sold_homes` | `gallery` | Image card grid — sold homes, properties, projects, anything |
| `cma_form` | `contact_form` | Lead capture form, not necessarily CMA-specific |
| `hero` | `hero` | Already generic |
| `stats` | `stats` | Already generic |
| `testimonials` | `testimonials` | Already generic |
| `about` | `about` | Already generic |
| *(new)* | `profiles` | People cards — staff, agents, partners |

### Renamed TypeScript Types

| Old Type | New Type |
|---|---|
| `ServiceItem` | `FeatureItem` |
| `SoldHomeItem` | `GalleryItem` |
| `CmaFormData` | `ContactFormData` |
| `AgentConfig` (top-level) | `AccountConfig` |

`AgentConfig` is reused as the smaller agent-identity-only type for team members.

## New Types

### ProfileItem

```typescript
interface ProfileItem {
  id: string;              // agent ID, used for routing
  name: string;
  title: string;           // "Senior Associate", "REALTOR®"
  headshot_url?: string;
  phone?: string;
  email?: string;
  link?: string;           // defaults to "/agents/{id}" if absent
}

interface ProfilesData {
  title?: string;
  subtitle?: string;
  items: ProfileItem[];
}
```

### NavItem (updated)

The current `NavItem` uses `section: string`. This renames to `href: string` and adds `enabled`:

```typescript
// BEFORE:
interface NavItem { label: string; section: string; }

// AFTER:
interface NavItem { label: string; href: string; enabled: boolean; }
```

Migration: `{ label: "Home", section: "hero" }` → `{ label: "Home", href: "#hero", enabled: true }`.

### SectionConfig (unchanged)

Carried forward from the current codebase:

```typescript
interface SectionConfig<T = Record<string, unknown>> {
  enabled: boolean;
  data: T;
}
```

### Section Data Types

Renamed and new data types used in `PageSections`:

```typescript
// Renamed: ServiceItem → FeatureItem
interface FeatureItem {
  title: string;
  description: string;
  icon?: string;
}

// Renamed: SoldHomeItem → GalleryItem
interface GalleryItem {
  address: string;
  city: string;
  state: string;
  price: string;
  sold_date?: string;
  image_url?: string;
}

// Renamed: CmaFormData → ContactFormData
interface ContactFormData {
  title: string;
  subtitle: string;
  description?: string;
}

// Unchanged types carried forward:
interface HeroData {
  headline: string;
  highlight_word?: string;  // Rendered in accent color
  tagline: string;
  body?: string;
  cta_text: string;
  cta_link: string;
}

interface StatItem { value: string; label: string; }
interface StepItem { number: number; title: string; description: string; }
interface TestimonialItem { text: string; reviewer: string; rating: number; source?: string; }

interface AboutData {
  title?: string;
  bio: string | string[];    // Supports both single string and array of paragraphs
  credentials?: string[];
  image_url?: string;
}

interface ThankYouData {
  heading: string;
  subheading: string;
  body?: string;
  disclaimer?: string;
  cta_call?: string;
  cta_back?: string;
}

// Convenience type aliases for section data wrappers:
type StatsData = { items: StatItem[] };
type FeaturesData = { title?: string; subtitle?: string; items: FeatureItem[] };
type StepsData = { title?: string; subtitle?: string; steps: StepItem[] };
type GalleryData = { title?: string; subtitle?: string; items: GalleryItem[] };
type TestimonialsData = { title?: string; subtitle?: string; items: TestimonialItem[] };

// city_pages — carried forward as disabled, available for future use
interface CityPageData {
  slug: string;
  city: string;
  state: string;
  county: string;
  highlights: string[];
  market_snapshot: string;
}
type CityPagesData = { cities: CityPageData[] };
```

### Full Type Hierarchy

```typescript
interface AccountConfig {
  handle: string;
  template: string;
  branding: AccountBranding;
  brokerage: BrokerageInfo;
  broker?: BrokerInfo;
  agent?: AccountAgent;
  location: AccountLocation;
  integrations?: AccountIntegrations;
  contact_info?: ContactMethod[];
  compliance?: ComplianceInfo;
}

interface AccountAgent {
  enabled: boolean;
  id: string;
  name: string;
  title: string;
  phone: string;
  email: string;
  headshot_url?: string;
  license_number?: string;
  languages?: string[];
  tagline?: string;
  credentials?: string[];
}

interface AgentConfig {
  id: string;
  name: string;
  title: string;
  phone: string;
  email: string;
  headshot_url?: string;
  license_number?: string;
  languages?: string[];
  tagline?: string;
  credentials?: string[];
}

interface AccountBranding {
  primary_color?: string;    // Optional — templates provide defaults
  secondary_color?: string;
  accent_color?: string;
  font_family?: string;
  logo_url?: string;
}

interface AccountIntegrations {
  email_provider?: "gmail" | "outlook" | "smtp";
  hosting?: string;
  tracking?: AccountTracking;
}

interface AccountTracking {
  google_analytics_id?: string;
  google_ads_id?: string;
  google_ads_conversion_label?: string;
  meta_pixel_id?: string;
  gtm_container_id?: string;
}

interface ContactMethod {
  type: "email" | "phone";
  value: string;
  ext?: string | null;
  label: string;
  is_preferred: boolean;
}

interface BrokerageInfo {
  name: string;
  license_number: string;
  office_address?: string;
  office_phone?: string;
}

interface BrokerInfo {
  name: string;
  title?: string;
  headshot_url?: string;
  bio?: string;
}

interface AccountLocation {
  state: string;
  service_areas: string[];
}

interface ComplianceInfo {
  state_form?: string;
  licensing_body?: string;
  disclosure_requirements?: string[];
}

interface ContentConfig {
  navigation?: NavigationConfig;
  pages: {
    home: { sections: PageSections };
    thank_you?: ThankYouPage;
  };
}

interface NavigationConfig {
  items: NavItem[];
}

interface PageSections {
  hero?:          SectionConfig<HeroData>;
  stats?:         SectionConfig<StatsData>;
  features?:      SectionConfig<FeaturesData>;
  steps?:         SectionConfig<StepsData>;
  gallery?:       SectionConfig<GalleryData>;
  testimonials?:  SectionConfig<TestimonialsData>;
  profiles?:      SectionConfig<ProfilesData>;
  contact_form?:  SectionConfig<ContactFormData>;
  about?:         SectionConfig<AboutData>;
  city_pages?:    SectionConfig<CityPagesData>;  // Carried forward, typically disabled
}
```

## Routing

### URL Structure

| URL | Resolves To |
|---|---|
| `{handle}.real-estate-star.com` | Account `content.json → pages.home` |
| `{handle}.real-estate-star.com/agents/{id}` | Agent `content.json → pages.home` |
| `{handle}.real-estate-star.com/thank-you` | Account `pages.thank_you` |
| `{handle}.real-estate-star.com/agents/{id}/thank-you` | Agent `pages.thank_you` (fallback to account's) |
| `{handle}.real-estate-star.com/privacy` | Existing legal pages (unchanged) |

### Page Resolution Logic

1. Subdomain → determine `handle` (existing logic)
2. Path determines page:
   - `/` → load account content, render `pages.home`
   - `/agents/{id}` → load `agents/{id}/content.json`, render `pages.home`
3. Template → always from `account.json.template`
4. Branding → always from `account.json.branding`
5. Agent identity for rendering:
   - Account home: `account.json.agent` (if enabled) or `account.json.broker`
   - Agent page: `agents/{id}/config.json`

### Navigation Behavior

- **Navigation config** lives only in the account's content.json. All pages share it.
- **Nav items** have an `enabled` flag. Only enabled items render.
- **Logo / brokerage name** in the nav always links to `/` (company home).
- **All other nav items** are `#anchor` links — relative to the current page.
- On agent pages, nav items link to sections on the agent page (e.g., `#features` scrolls to the agent's features section).
- If a nav item is enabled but the target section doesn't exist on the current page, the link does nothing (no error, no crash).

### Config Validation Rule

A build-time test validates: for every nav item with `enabled: true`, the corresponding section must exist and be `enabled: true` in `pages.home.sections`. This runs against all account and agent content.json files. This catches config mismatches before deployment.

## Config Registry Prebuild

The existing config registry prebuild (generates TypeScript from JSON for Cloudflare Workers) expands:

```typescript
// Generated at build time
export const ACCOUNTS: Record<string, AccountConfig> = { ... };
export const ACCOUNT_CONTENT: Record<string, ContentConfig> = { ... };
export const AGENT_CONFIGS: Record<string, Record<string, AgentConfig>> = { ... };
export const AGENT_CONTENT: Record<string, Record<string, ContentConfig>> = { ... };
```

- `ACCOUNTS` keyed by handle
- `ACCOUNT_CONTENT` keyed by handle
- `AGENT_CONFIGS` keyed by handle, then agent ID
- `AGENT_CONTENT` keyed by handle, then agent ID

Solo accounts with no `agents/` folder will have empty entries in `AGENT_CONFIGS` and `AGENT_CONTENT`.

## Migration

### Phase 1 — Schema & Types

- Create `account.schema.json` replacing `agent.schema.json`
- Update all TypeScript types in `apps/agent-site/lib/types.ts`
- Rename types: `ServiceItem` → `FeatureItem`, `SoldHomeItem` → `GalleryItem`, `CmaFormData` → `ContactFormData`
- Add `AccountConfig`, `AgentConfig` (small), `ProfileItem`, `ProfilesData`, `NavItem.enabled`
- Add `ContentConfig` with `pages` wrapper and `PageSections`

### Phase 2 — Migrate Config Files

- Move `config/agents/` → `config/accounts/`
- For each agent, split `config.json` into `account.json`:
  - `identity.*` → `account.json.agent` (with `enabled: true`)
  - `identity.brokerage` + `identity.brokerage_id` → `account.json.brokerage`
  - `branding.*` → `account.json.branding`
  - `location.*` → `account.json.location`
  - `compliance.*` → `account.json.compliance`
  - `template` moves from content.json to account.json
- For each content.json:
  - Wrap `sections` under `pages.home.sections`
  - Move existing `thank_you` page data under `pages.thank_you`
  - Rename section keys: `services` → `features`, `how_it_works` → `steps`, `sold_homes` → `gallery`, `cma_form` → `contact_form`
  - Rename nav item `section` field to `href` (e.g., `"section": "hero"` → `"href": "#hero"`)
  - Add `enabled: true` flag to all nav items
  - Migrate `contact_info` from content.json to account.json
  - Migrate `integrations` from config.json to account.json
  - Carry forward `city_pages` section (typically `enabled: false`)
- Branding fields remain optional (templates provide defaults) — no breaking change for configs that omit colors
- Create `test-brokerage/` account with 2 sub-agents to test company flow
- Delete old `config/agents/` directory

### Phase 3 — Update Config Registry Prebuild

- Update the prebuild script (`scripts/generate-config-registry.ts` or similar) to:
  - Read from `config/accounts/` instead of `config/agents/`
  - Generate `ACCOUNTS`, `ACCOUNT_CONTENT`, `AGENT_CONFIGS`, `AGENT_CONTENT` exports
  - Support nested `agents/` subdirectories
- Update all imports of the generated registry throughout the app

### Phase 4 — Update Rendering Pipeline

- Update template compositions to read from `content.pages.home.sections` instead of `content.sections`
- Update section component prop types with renamed types
- Nav component: filter items by `enabled` flag
- Nav component: logo always links to `/`
- Add `/agents/[id]` dynamic route in Next.js `app/` directory
- Agent page loads agent config + agent content, renders with account template + branding
- Update the thank-you page to resolve under `/agents/{id}/thank-you` as well

### Phase 5 — Profiles Section

- Create `ProfilesData` and `ProfileItem` types
- Create section variants: at least one `Profiles*` component per existing template style
- Each profile card shows: headshot, name, title, and links to `/agents/{id}`
- Wire into template compositions (enabled via `profiles` section key)

### Phase 6 — Validation & Tests

- Config validation test: for every content.json, verify enabled nav items map to enabled sections
- Update all existing component tests to use renamed types and new content structure
- Add routing tests for `/agents/{id}` resolution
- Add tests for the profiles section variants
- Update config registry generation tests
- Ensure 100% branch coverage on all new and modified code

### Phase 7 — CI/CD Pipeline Updates

- Update GitHub Actions workflows that reference `config/agents/` to read from `config/accounts/`
- Update the prebuild step in CI that generates `config-registry.ts` (currently runs before `vitest` and `next build`)
- Update any deploy scripts (Cloudflare wrangler, `infra/` scripts) that enumerate agent configs
- Update `infra/cloudflare/add-agent-domain.ps1` to read handles from `config/accounts/` instead of `config/agents/`
- Verify the Cloudflare Workers deploy still bundles the generated registry correctly with the new nested structure
- Update any test fixtures or CI-specific config references

## Risks & Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Large breaking change across all config files | High — every test, every import, every config reference changes | Execute as atomic migration: types + configs + rendering in one pass. No intermediate state. |
| Config registry prebuild breaks Cloudflare deploy | High — site goes down | Validate generated registry in CI before deploy. Keep old registry generation as fallback until migration is verified. |
| Section rename breaks existing content | Medium — wrong keys silently ignored | JSON Schema validation catches unknown keys. Build-time test enumerates all expected section keys. |
| Agent page routing conflicts with existing routes | Low — `/agents/` is a new path | Test that existing routes (`/privacy`, `/terms`, `/thank-you`) still work. |
| Nav links dead on agent pages | Low — UX annoyance | Config validation test catches nav-to-section mismatches at build time. |
| Static asset paths break after folder rename | Medium — broken images | Update public asset paths from `/agents/{id}/` to `/accounts/{id}/` or keep existing paths and add redirects. |

## Out of Scope

- Premium components (marquee, parallax hero, feature cards, scroll animations) — separate spec
- Database-backed config (stays as JSON files for now)
- Agent self-service config editing (future platform feature)
- Per-agent template overrides (all pages use account template)
- Multi-page routing beyond home + agents + thank-you (no `/about` or `/services` standalone pages)
