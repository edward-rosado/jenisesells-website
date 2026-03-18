# Pages Architecture Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate from flat `config/agents/` to account-based `config/accounts/` with pages wrapper, generic section names, profiles section, `/agents/{id}` routing, and CI/CD updates.

**Architecture:** Account-based multi-tenancy — each account has `account.json` (branding, brokerage, template) and `content.json` (pages.home.sections). Solo agents have `agent.enabled: true` inline. Company accounts have `agents/` subfolder with per-agent config + content. All pages share the account's template and branding.

**Tech Stack:** Next.js 16, TypeScript, Vitest, Cloudflare Workers (OpenNext), JSON config files, prebuild code generation.

**Spec:** `docs/superpowers/specs/2026-03-17-pages-architecture-design.md`

---

## Chunk 1: Types & Schema

### Task 1: Update TypeScript types — rename existing types

**Files:**
- Modify: `apps/agent-site/lib/types.ts`
- Test: `apps/agent-site/__tests__/lib/types.test.ts` (new)

- [ ] **Step 1: Write failing test for renamed types**

Create `apps/agent-site/__tests__/lib/types.test.ts`:

```typescript
import type {
  FeatureItem,
  GalleryItem,
  ContactFormData,
  AccountConfig,
  AccountAgent,
  AgentConfig,
  AccountBranding,
  AccountLocation,
  AccountIntegrations,
  AccountTracking,
  BrokerageInfo,
  BrokerInfo,
  ComplianceInfo,
  ContentConfig,
  PageSections,
  ProfileItem,
  ProfilesData,
  NavItem,
  SectionConfig,
  HeroData,
  StatItem,
  StepItem,
  TestimonialItem,
  AboutData,
  ThankYouData,
  ContactMethod,
  CityPageData,
} from "@/lib/types";

describe("types.ts — type smoke tests", () => {
  it("FeatureItem replaces ServiceItem", () => {
    const item: FeatureItem = { title: "A", description: "B" };
    expect(item.title).toBe("A");
  });

  it("GalleryItem replaces SoldHomeItem", () => {
    const item: GalleryItem = { address: "1 Main", city: "NY", state: "NY", price: "$1" };
    expect(item.address).toBe("1 Main");
  });

  it("ContactFormData replaces CmaFormData", () => {
    const data: ContactFormData = { title: "T", subtitle: "S" };
    expect(data.title).toBe("T");
  });

  it("AccountConfig has required fields", () => {
    const cfg: AccountConfig = {
      handle: "test",
      template: "emerald-classic",
      branding: {},
      brokerage: { name: "B", license_number: "123" },
      location: { state: "NJ", service_areas: [] },
    };
    expect(cfg.handle).toBe("test");
  });

  it("AccountAgent has enabled flag", () => {
    const agent: AccountAgent = {
      enabled: true,
      id: "a",
      name: "A",
      title: "T",
      phone: "1",
      email: "a@b.com",
    };
    expect(agent.enabled).toBe(true);
  });

  it("AgentConfig is the small agent-identity type", () => {
    const agent: AgentConfig = {
      id: "a",
      name: "A",
      title: "T",
      phone: "1",
      email: "a@b.com",
    };
    expect(agent.id).toBe("a");
  });

  it("NavItem has href and enabled", () => {
    const nav: NavItem = { label: "Home", href: "#hero", enabled: true };
    expect(nav.href).toBe("#hero");
    expect(nav.enabled).toBe(true);
  });

  it("ProfileItem has id and optional link", () => {
    const p: ProfileItem = { id: "x", name: "X", title: "Agent" };
    expect(p.link).toBeUndefined();
  });

  it("ContentConfig has pages wrapper", () => {
    const c: ContentConfig = {
      pages: {
        home: {
          sections: {
            hero: { enabled: true, data: { headline: "H", tagline: "T", cta_text: "C", cta_link: "#" } },
          },
        },
      },
    };
    expect(c.pages.home.sections.hero?.enabled).toBe(true);
  });

  it("PageSections uses new section names", () => {
    const s: PageSections = {
      features: { enabled: true, data: { items: [{ title: "A", description: "B" }] } },
      gallery: { enabled: true, data: { items: [{ address: "1", city: "C", state: "S", price: "$1" }] } },
      contact_form: { enabled: true, data: { title: "T", subtitle: "S" } },
      profiles: { enabled: false, data: { items: [] } },
    };
    expect(s.features?.enabled).toBe(true);
    expect(s.gallery?.enabled).toBe(true);
    expect(s.contact_form?.enabled).toBe(true);
    expect(s.profiles?.enabled).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/lib/types.test.ts`
Expected: FAIL — none of the new type names exist yet

- [ ] **Step 3: Rewrite types.ts with new type hierarchy**

Replace the entire contents of `apps/agent-site/lib/types.ts` with:

```typescript
// --- Account Config (config/accounts/{handle}/account.json) ---

export interface AccountConfig {
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

export interface AccountAgent {
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

export interface AgentConfig {
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

export interface AccountBranding {
  primary_color?: string;
  secondary_color?: string;
  accent_color?: string;
  font_family?: string;
  logo_url?: string;
}

export interface BrokerageInfo {
  name: string;
  license_number: string;
  office_address?: string;
  office_phone?: string;
}

export interface BrokerInfo {
  name: string;
  title?: string;
  headshot_url?: string;
  bio?: string;
}

export interface AccountLocation {
  state: string;
  service_areas: string[];
}

export interface AccountIntegrations {
  email_provider?: "gmail" | "outlook" | "smtp";
  hosting?: string;
  tracking?: AccountTracking;
}

export interface AccountTracking {
  google_analytics_id?: string;
  google_ads_id?: string;
  google_ads_conversion_label?: string;
  meta_pixel_id?: string;
  gtm_container_id?: string;
}

export interface ContactMethod {
  type: "email" | "phone";
  value: string;
  ext?: string | null;
  label: string;
  is_preferred: boolean;
}

export interface ComplianceInfo {
  state_form?: string;
  licensing_body?: string;
  disclosure_requirements?: string[];
}

// --- Content Config (content.json — account or agent level) ---

export interface ContentConfig {
  navigation?: NavigationConfig;
  pages: {
    home: { sections: PageSections };
    thank_you?: ThankYouData;
  };
}

export interface NavigationConfig {
  items: NavItem[];
}

export interface NavItem {
  label: string;
  href: string;
  enabled: boolean;
}

// --- Page Sections ---

export interface SectionConfig<T = Record<string, unknown>> {
  enabled: boolean;
  data: T;
}

export interface PageSections {
  hero?: SectionConfig<HeroData>;
  stats?: SectionConfig<StatsData>;
  features?: SectionConfig<FeaturesData>;
  steps?: SectionConfig<StepsData>;
  gallery?: SectionConfig<GalleryData>;
  testimonials?: SectionConfig<TestimonialsData>;
  profiles?: SectionConfig<ProfilesData>;
  contact_form?: SectionConfig<ContactFormData>;
  about?: SectionConfig<AboutData>;
  city_pages?: SectionConfig<CityPagesData>;
}

// --- Section Data Types ---

export interface HeroData {
  headline: string;
  highlight_word?: string;
  tagline: string;
  body?: string;
  cta_text: string;
  cta_link: string;
}

export interface StatItem {
  value: string;
  label: string;
}
export type StatsData = { items: StatItem[] };

// Renamed: ServiceItem -> FeatureItem
export interface FeatureItem {
  title: string;
  description: string;
  icon?: string;
}
export type FeaturesData = { title?: string; subtitle?: string; items: FeatureItem[] };

export interface StepItem {
  number: number;
  title: string;
  description: string;
}
export type StepsData = { title?: string; subtitle?: string; steps: StepItem[] };

// Renamed: SoldHomeItem -> GalleryItem
export interface GalleryItem {
  address: string;
  city: string;
  state: string;
  price: string;
  sold_date?: string;
  image_url?: string;
}
export type GalleryData = { title?: string; subtitle?: string; items: GalleryItem[] };

export interface TestimonialItem {
  text: string;
  reviewer: string;
  rating: number;
  source?: string;
}
export type TestimonialsData = { title?: string; subtitle?: string; items: TestimonialItem[] };

// New: Profiles section
export interface ProfileItem {
  id: string;
  name: string;
  title: string;
  headshot_url?: string;
  phone?: string;
  email?: string;
  link?: string;
}
export interface ProfilesData {
  title?: string;
  subtitle?: string;
  items: ProfileItem[];
}

// Renamed: CmaFormData -> ContactFormData
export interface ContactFormData {
  title: string;
  subtitle: string;
  description?: string;
}

export interface AboutData {
  title?: string;
  bio: string | string[];
  credentials?: string[];
  image_url?: string;
}

export interface ThankYouData {
  heading: string;
  subheading: string;
  body?: string;
  disclaimer?: string;
  cta_call?: string;
  cta_back?: string;
}

export interface CityPageData {
  slug: string;
  city: string;
  state: string;
  county: string;
  highlights: string[];
  market_snapshot: string;
}
export type CityPagesData = { cities: CityPageData[] };

// --- Backward-compatible aliases (removed in Task 16 cleanup) ---
/** @deprecated Use FeatureItem */
export type ServiceItem = FeatureItem;
/** @deprecated Use GalleryItem */
export type SoldHomeItem = GalleryItem;
/** @deprecated Use ContactFormData */
export type CmaFormData = ContactFormData;
```

- [ ] **Step 4: Run type test to verify it passes**

Run: `cd apps/agent-site && npx vitest run __tests__/lib/types.test.ts`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/lib/types.ts apps/agent-site/__tests__/lib/types.test.ts
git commit -m "feat: rewrite types.ts with account-based type hierarchy

Rename ServiceItem->FeatureItem, SoldHomeItem->GalleryItem,
CmaFormData->ContactFormData. Add AccountConfig, AgentConfig,
ProfileItem, ContentConfig with pages wrapper, NavItem with
href+enabled. Keep backward-compatible aliases temporarily."
```

---

### Task 2: Update section prop types

**Files:**
- Modify: `apps/agent-site/components/sections/types.ts`

- [ ] **Step 1: Update section prop types to use new names**

Update `apps/agent-site/components/sections/types.ts`:

```typescript
import type {
  HeroData,
  StatItem,
  FeatureItem,
  StepItem,
  GalleryItem,
  TestimonialItem,
  AccountConfig,
  AgentConfig,
  AboutData,
  ProfileItem,
} from "@/lib/types";

export interface HeroProps {
  data: HeroData;
  agentPhotoUrl?: string;
  agentName?: string;
}

export interface StatsProps {
  items: StatItem[];
  sourceDisclaimer?: string;
}

export interface FeaturesProps {
  items: FeatureItem[];
  title?: string;
  subtitle?: string;
}

export interface StepsProps {
  steps: StepItem[];
  title?: string;
  subtitle?: string;
}

export interface GalleryProps {
  items: GalleryItem[];
  title?: string;
  subtitle?: string;
}

export interface TestimonialsProps {
  items: TestimonialItem[];
  title?: string;
}

export interface ProfilesProps {
  items: ProfileItem[];
  title?: string;
  subtitle?: string;
}

export interface AboutProps {
  agent: AccountConfig | AgentConfig;
  data: AboutData;
}

/** Extract display name from either AccountConfig or AgentConfig */
export function getDisplayName(agent: AccountConfig | AgentConfig): string {
  if ("handle" in agent) {
    return agent.agent?.name ?? agent.broker?.name ?? agent.brokerage.name;
  }
  return agent.name;
}

/** Extract headshot URL from either AccountConfig or AgentConfig */
export function getHeadshotUrl(agent: AccountConfig | AgentConfig): string | undefined {
  if ("handle" in agent) {
    return agent.agent?.headshot_url;
  }
  return agent.headshot_url;
}

export function clampRating(rating: number): number {
  return Math.min(5, Math.max(0, Math.floor(rating || 0)));
}

export const FTC_DISCLAIMER =
  "Real reviews from real clients. Unedited excerpts from verified reviews on Zillow. No compensation was provided. Individual results may vary.";

// Backward-compatible aliases
/** @deprecated Use FeaturesProps */
export type ServicesProps = FeaturesProps;
/** @deprecated Use GalleryProps */
export type SoldHomesProps = GalleryProps;
```

- [ ] **Step 2: Run existing tests to check compile status**

Run: `cd apps/agent-site && npx vitest run --reporter=verbose 2>&1 | head -50`
Expected: Many tests fail due to AgentConfig shape change. This is expected — fixed in later tasks.

- [ ] **Step 3: Commit**

```bash
git add apps/agent-site/components/sections/types.ts
git commit -m "feat: update section prop types with new names

Add FeaturesProps, GalleryProps, ProfilesProps. AboutProps accepts
AccountConfig|AgentConfig. Add getDisplayName/getHeadshotUrl helpers.
Keep ServicesProps/SoldHomesProps aliases."
```

---

### Task 3: Update test fixtures to new type shapes

**Files:**
- Modify: `apps/agent-site/__tests__/components/fixtures.ts`

- [ ] **Step 1: Rewrite fixtures to match AccountConfig + ContentConfig**

Replace `apps/agent-site/__tests__/components/fixtures.ts`:

```typescript
import type { AccountConfig, ContentConfig } from "@/lib/types";

export const ACCOUNT: AccountConfig = {
  handle: "test-agent",
  template: "emerald-classic",
  branding: {
    primary_color: "#1B5E20",
    secondary_color: "#2E7D32",
    accent_color: "#C8A951",
    font_family: "Segoe UI",
  },
  brokerage: {
    name: "Best Homes Realty",
    license_number: "123456",
    office_phone: "(732) 251-2500",
  },
  agent: {
    enabled: true,
    id: "test-agent",
    name: "Jane Smith",
    title: "REALTOR",
    phone: "555-123-4567",
    email: "jane@example.com",
    tagline: "Your Dream Home Awaits",
    languages: ["English", "Spanish"],
    headshot_url: undefined,
  },
  location: {
    state: "NJ",
    service_areas: ["Hoboken", "Jersey City"],
  },
  integrations: {},
  contact_info: [
    { type: "email", value: "jane@example.com", label: "Personal Email", is_preferred: false },
    { type: "phone", value: "555-123-4567", label: "Cell Phone", is_preferred: true },
    { type: "phone", value: "(732) 251-2500", ext: "714", label: "Office Phone", is_preferred: false },
  ],
};

export const ACCOUNT_MINIMAL: AccountConfig = {
  handle: "minimal-agent",
  template: "emerald-classic",
  branding: {},
  brokerage: { name: "Min Realty", license_number: "000" },
  agent: {
    enabled: true,
    id: "minimal-agent",
    name: "Bob Jones",
    title: "Agent",
    phone: "555-000-1234",
    email: "bob@example.com",
  },
  location: {
    state: "TX",
    service_areas: [],
  },
};

export const CONTENT: ContentConfig = {
  navigation: {
    items: [
      { label: "Why Choose Me", href: "#features", enabled: true },
      { label: "How It Works", href: "#steps", enabled: true },
      { label: "Recent Sales", href: "#gallery", enabled: true },
      { label: "Testimonials", href: "#testimonials", enabled: true },
      { label: "Ready to Move?", href: "#contact_form", enabled: true },
      { label: "About", href: "#about", enabled: true },
    ],
  },
  pages: {
    home: {
      sections: {
        hero: {
          enabled: true,
          data: {
            headline: "Sell Your Home Fast",
            tagline: "Expert guidance every step",
            cta_text: "Get Free Report",
            cta_link: "#contact_form",
          },
        },
        stats: {
          enabled: true,
          data: {
            items: [
              { value: "150+", label: "Homes Sold" },
              { value: "$2.5M", label: "Total Volume" },
            ],
          },
        },
        features: {
          enabled: true,
          data: {
            items: [
              { title: "Market Analysis", description: "Deep market insights" },
              { title: "Photography", description: "Professional photos" },
              { title: "Negotiation", description: "Expert negotiation" },
            ],
          },
        },
        steps: {
          enabled: true,
          data: {
            steps: [
              { number: 1, title: "Submit Info", description: "Fill out the form" },
              { number: 2, title: "Get Report", description: "Receive your CMA" },
              { number: 3, title: "Meet Agent", description: "Schedule walkthrough" },
            ],
          },
        },
        gallery: {
          enabled: true,
          data: {
            items: [
              { address: "123 Main St", city: "Hoboken", state: "NJ", price: "$750,000" },
              { address: "456 Elm Ave", city: "Jersey City", state: "NJ", price: "$620,000" },
            ],
          },
        },
        testimonials: {
          enabled: true,
          data: {
            items: [
              { text: "Amazing service!", reviewer: "Alice B.", rating: 5, source: "Zillow" },
              { text: "Would recommend.", reviewer: "Tom C.", rating: 4 },
              { text: "Smooth process.", reviewer: "Sara D.", rating: 3 },
            ],
          },
        },
        contact_form: {
          enabled: true,
          data: {
            title: "What's Your Home Worth?",
            subtitle: "Get a free CMA today",
            description: "Selling? Get a **free** home value report. Buying? Tell us what you need.",
          },
        },
        about: {
          enabled: true,
          data: {
            bio: "Jane Smith is a top agent in New Jersey.",
            credentials: ["ABR", "CRS"],
          },
        },
        city_pages: {
          enabled: false,
          data: { cities: [] },
        },
      },
    },
  },
};

export const CONTENT_ALL_DISABLED: ContentConfig = {
  pages: {
    home: {
      sections: {
        hero: { enabled: false, data: { headline: "", tagline: "", cta_text: "", cta_link: "" } },
        stats: { enabled: false, data: { items: [] } },
        features: { enabled: false, data: { items: [] } },
        steps: { enabled: false, data: { steps: [] } },
        gallery: { enabled: false, data: { items: [] } },
        testimonials: { enabled: false, data: { items: [] } },
        contact_form: { enabled: false, data: { title: "", subtitle: "" } },
        about: { enabled: false, data: { bio: "", credentials: [] } },
        city_pages: { enabled: false, data: { cities: [] } },
      },
    },
  },
};

// Backward-compatible aliases for gradual test migration
export const AGENT = ACCOUNT;
export const AGENT_MINIMAL = ACCOUNT_MINIMAL;
```

- [ ] **Step 2: Commit**

```bash
git add apps/agent-site/__tests__/components/fixtures.ts
git commit -m "feat: rewrite test fixtures with AccountConfig + ContentConfig shapes

AGENT -> ACCOUNT with handle/brokerage/agent/pages structure.
CONTENT uses pages.home.sections with renamed section keys.
Backward-compatible AGENT/AGENT_MINIMAL aliases exported."
```

---

### Task 4: Update config loader

**Files:**
- Modify: `apps/agent-site/lib/config.ts`

- [ ] **Step 1: Rewrite config.ts for account-based loading**

```typescript
// apps/agent-site/lib/config.ts
import type { AccountConfig, AgentConfig, ContentConfig } from "./types";
import { accounts, accountContent, agentConfigs, agentContent, legalContent } from "./config-registry";

const VALID_HANDLE = /^[a-z0-9-]+$/;

function validateHandle(handle: string): void {
  if (!VALID_HANDLE.test(handle)) {
    throw new Error(`Invalid handle: ${handle}`);
  }
}

function assertAccountConfig(value: unknown): asserts value is AccountConfig {
  const v = value as Record<string, unknown>;
  if (typeof v?.handle !== "string") throw new Error("AccountConfig: missing handle");
  if (typeof v?.template !== "string") throw new Error("AccountConfig: missing template");
  const brokerage = v.brokerage as Record<string, unknown> | undefined;
  if (typeof brokerage?.name !== "string") throw new Error("AccountConfig: missing brokerage.name");
  const location = v.location as Record<string, unknown> | undefined;
  if (typeof location?.state !== "string") throw new Error("AccountConfig: missing location.state");
}

export function loadAccountConfig(handle: string): AccountConfig {
  validateHandle(handle);
  const config = accounts[handle];
  if (!config) {
    throw new Error(`Account not found: ${handle}`);
  }
  assertAccountConfig(config);
  return config;
}

export function loadAccountContent(
  handle: string,
  config?: AccountConfig,
): ContentConfig {
  validateHandle(handle);
  const content = accountContent[handle];
  if (content) return content;
  const resolved = config ?? loadAccountConfig(handle);
  return buildDefaultContent(resolved);
}

export function loadAgentConfig(handle: string, agentId: string): AgentConfig {
  validateHandle(handle);
  validateHandle(agentId);
  const agentMap = agentConfigs[handle];
  if (!agentMap || !agentMap[agentId]) {
    throw new Error(`Agent not found: ${handle}/${agentId}`);
  }
  return agentMap[agentId];
}

export function loadAgentContent(handle: string, agentId: string): ContentConfig | undefined {
  validateHandle(handle);
  validateHandle(agentId);
  return agentContent[handle]?.[agentId];
}

export function loadLegalContent(
  handle: string,
  page: "privacy" | "terms" | "accessibility",
): { above?: string; below?: string } {
  validateHandle(handle);
  return legalContent[handle]?.[page] ?? { above: undefined, below: undefined };
}

export function getAgentIds(handle: string): string[] {
  validateHandle(handle);
  return Object.keys(agentConfigs[handle] ?? {});
}

function buildDefaultContent(config: AccountConfig): ContentConfig {
  const agentName = config.agent?.name ?? config.broker?.name ?? config.brokerage.name;
  const tagline = config.agent?.tagline ?? "Your Trusted Real Estate Professional";

  return {
    pages: {
      home: {
        sections: {
          hero: {
            enabled: true,
            data: {
              headline: "Sell Your Home with Confidence",
              tagline,
              cta_text: "Get Your Free Home Value",
              cta_link: "#contact_form",
            },
          },
          stats: { enabled: false, data: { items: [] } },
          features: {
            enabled: true,
            data: {
              items: [
                { title: "Expert Market Analysis", description: `${agentName} provides a detailed analysis of your local market to price your home right.` },
                { title: "Strategic Marketing Plan", description: "Professional photography, virtual tours, and targeted online advertising." },
                { title: "Negotiation & Closing", description: "Skilled negotiation to get you the best possible price and smooth closing." },
              ],
            },
          },
          steps: {
            enabled: true,
            data: {
              steps: [
                { number: 1, title: "Submit Your Info", description: "Fill out the quick form below with your property details." },
                { number: 2, title: "Get Your Free Report", description: "Receive a professional Comparative Market Analysis within minutes." },
                { number: 3, title: "Schedule a Walkthrough", description: `Meet with ${agentName} to discuss your selling strategy.` },
              ],
            },
          },
          gallery: { enabled: false, data: { items: [] } },
          testimonials: { enabled: false, data: { items: [] } },
          contact_form: {
            enabled: true,
            data: {
              title: "What's Your Home Worth?",
              subtitle: "Get a free, professional Comparative Market Analysis",
            },
          },
          about: {
            enabled: true,
            data: {
              bio: `${agentName} is a dedicated real estate professional serving ${config.location.service_areas?.join(", ") || config.location.state}. Contact ${agentName} today to learn how they can help you achieve your real estate goals.`,
              credentials: [],
            },
          },
          city_pages: { enabled: false, data: { cities: [] } },
        },
      },
    },
  };
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/agent-site/lib/config.ts
git commit -m "feat: rewrite config loader for account-based multi-tenancy

loadAccountConfig/loadAccountContent replace old loader functions.
Separate loadAgentConfig(handle, agentId) for team members.
buildDefaultContent uses new section names and pages wrapper."
```

---

## Chunk 2: Config Migration & Prebuild

### Task 5: Migrate config files from agents/ to accounts/

**Files:**
- Move: `config/agents/*` -> `config/accounts/*`
- Modify: Each `config.json` -> `account.json` (new shape)
- Modify: Each `content.json` (add pages wrapper, rename sections)

This task requires a migration script to avoid manual editing of 4+ config files (11 when branding-test-agents merges).

- [ ] **Step 1: Write migration script**

Create `scripts/migrate-to-accounts.mjs`:

```javascript
#!/usr/bin/env node
// One-time migration: config/agents/ -> config/accounts/
import fs from "fs";
import path from "path";

const AGENTS_DIR = path.resolve("config/agents");
const ACCOUNTS_DIR = path.resolve("config/accounts");

const SECTION_RENAMES = {
  services: "features",
  how_it_works: "steps",
  sold_homes: "gallery",
  cma_form: "contact_form",
};

const NAV_SECTION_RENAMES = {
  services: "#features",
  "how-it-works": "#steps",
  sold: "#gallery",
  "cma-form": "#contact_form",
  testimonials: "#testimonials",
  about: "#about",
  hero: "#hero",
};

function loadJson(p) { return JSON.parse(fs.readFileSync(p, "utf-8")); }
function writeJson(p, data) {
  fs.mkdirSync(path.dirname(p), { recursive: true });
  fs.writeFileSync(p, JSON.stringify(data, null, 2) + "\n", "utf-8");
}

function migrateConfig(config) {
  const id = config.id;
  const identity = config.identity || {};
  return {
    handle: id,
    template: "emerald-classic",
    branding: config.branding || {},
    brokerage: {
      name: identity.brokerage || "Unknown Brokerage",
      license_number: identity.brokerage_id || "",
      ...(identity.office_phone && { office_phone: identity.office_phone }),
      ...(config.location?.office_address && { office_address: config.location.office_address }),
    },
    agent: {
      enabled: true,
      id,
      name: identity.name,
      title: identity.title || "Real Estate Agent",
      phone: identity.phone,
      email: identity.email,
      ...(identity.headshot_url && { headshot_url: identity.headshot_url }),
      ...(identity.license_id && { license_number: identity.license_id }),
      ...(identity.languages && { languages: identity.languages }),
      ...(identity.tagline && { tagline: identity.tagline }),
    },
    location: {
      state: config.location?.state || "US",
      service_areas: config.location?.service_areas || [],
    },
    ...(config.integrations && { integrations: config.integrations }),
    ...(config.compliance && { compliance: config.compliance }),
  };
}

function migrateContent(content, account) {
  if (content.template) {
    account.template = content.template;
  }

  if (content.contact_info) {
    account.contact_info = content.contact_info;
  }

  const oldSections = content.sections || {};
  const newSections = {};
  for (const [key, val] of Object.entries(oldSections)) {
    const newKey = SECTION_RENAMES[key] || key;
    newSections[newKey] = val;
  }

  const navItems = (content.navigation?.items || []).map(item => ({
    label: item.label,
    href: NAV_SECTION_RENAMES[item.section] || `#${item.section}`,
    enabled: true,
  }));

  return {
    ...(navItems.length > 0 && { navigation: { items: navItems } }),
    pages: {
      home: { sections: newSections },
      ...(content.pages?.thank_you && { thank_you: content.pages.thank_you }),
    },
  };
}

function main() {
  if (!fs.existsSync(AGENTS_DIR)) {
    console.error("config/agents/ not found");
    process.exit(1);
  }

  fs.mkdirSync(ACCOUNTS_DIR, { recursive: true });

  for (const entry of fs.readdirSync(AGENTS_DIR, { withFileTypes: true })) {
    if (!entry.isDirectory()) continue;
    const agentDir = path.join(AGENTS_DIR, entry.name);
    const configPath = path.join(agentDir, "config.json");
    if (!fs.existsSync(configPath)) continue;

    console.log(`Migrating ${entry.name}...`);
    const config = loadJson(configPath);
    const account = migrateConfig(config);

    const contentPath = path.join(agentDir, "content.json");
    let newContent = null;
    if (fs.existsSync(contentPath)) {
      const content = loadJson(contentPath);
      newContent = migrateContent(content, account);
    }

    const accountDir = path.join(ACCOUNTS_DIR, entry.name);
    writeJson(path.join(accountDir, "account.json"), account);
    if (newContent) {
      writeJson(path.join(accountDir, "content.json"), newContent);
    }

    const legalDir = path.join(agentDir, "legal");
    if (fs.existsSync(legalDir)) {
      const destLegal = path.join(accountDir, "legal");
      fs.mkdirSync(destLegal, { recursive: true });
      for (const file of fs.readdirSync(legalDir)) {
        fs.copyFileSync(path.join(legalDir, file), path.join(destLegal, file));
      }
    }
  }

  console.log("Migration complete. Review config/accounts/ then delete config/agents/.");
}

main();
```

- [ ] **Step 2: Run migration**

Run: `node scripts/migrate-to-accounts.mjs`
Expected: Creates `config/accounts/` with migrated files for each agent.

- [ ] **Step 3: Manually review migrated files**

Verify each `account.json` has correct data: `handle`, `template` (from content.json), `brokerage.name`, `agent.enabled: true`.

- [ ] **Step 4: Create test-brokerage account (company mode)**

Create `config/accounts/test-brokerage/account.json`:
```json
{
  "handle": "test-brokerage",
  "template": "emerald-classic",
  "branding": {
    "primary_color": "#1a1a2e",
    "secondary_color": "#16213e",
    "accent_color": "#d4af37",
    "font_family": "Georgia"
  },
  "brokerage": {
    "name": "Sterling & Associates Real Estate",
    "license_number": "NJ-2024-12345",
    "office_address": "100 Summit Ave, Summit, NJ 07901",
    "office_phone": "(908) 555-0100"
  },
  "broker": {
    "name": "Victoria Sterling",
    "title": "Managing Broker",
    "bio": "30 years of luxury real estate experience."
  },
  "location": {
    "state": "NJ",
    "service_areas": ["Summit", "Short Hills", "Chatham"]
  }
}
```

Create `config/accounts/test-brokerage/content.json` with profiles section enabled, nav items linking to #profiles, and standard sections.

Create `config/accounts/test-brokerage/agents/agent-a/config.json` and `content.json`.
Create `config/accounts/test-brokerage/agents/agent-b/config.json` and `content.json`.

Create `config/accounts/test-brokerage/agents/agent-a/content.json`:
```json
{
  "pages": {
    "home": {
      "sections": {
        "hero": { "enabled": true, "data": { "headline": "James Whitfield — Senior Associate", "tagline": "Your land expert.", "cta_text": "Work With James", "cta_link": "#contact_form" } },
        "stats": { "enabled": false, "data": { "items": [] } },
        "features": { "enabled": true, "data": { "title": "My Specialties", "items": [{ "title": "Land Sales", "description": "Expert in vacant land and development parcels." }] } },
        "steps": { "enabled": false, "data": { "steps": [] } },
        "gallery": { "enabled": false, "data": { "items": [] } },
        "testimonials": { "enabled": false, "data": { "items": [] } },
        "profiles": { "enabled": false, "data": { "items": [] } },
        "contact_form": { "enabled": true, "data": { "title": "Work With James", "subtitle": "Let's find your next property." } },
        "about": { "enabled": true, "data": { "bio": "James specializes in vacant land across northern NJ.", "credentials": ["REALTOR\u00ae"] } },
        "city_pages": { "enabled": false, "data": { "cities": [] } }
      }
    }
  }
}
```

Create `config/accounts/test-brokerage/agents/agent-b/content.json`:
```json
{
  "pages": {
    "home": {
      "sections": {
        "hero": { "enabled": true, "data": { "headline": "Sarah Chen — Luxury Specialist", "tagline": "Connecting you with the perfect home.", "cta_text": "Work With Sarah", "cta_link": "#contact_form" } },
        "stats": { "enabled": false, "data": { "items": [] } },
        "features": { "enabled": false, "data": { "items": [] } },
        "steps": { "enabled": false, "data": { "steps": [] } },
        "gallery": { "enabled": false, "data": { "items": [] } },
        "testimonials": { "enabled": false, "data": { "items": [] } },
        "profiles": { "enabled": false, "data": { "items": [] } },
        "contact_form": { "enabled": true, "data": { "title": "Work With Sarah", "subtitle": "Let me find your dream home." } },
        "about": { "enabled": true, "data": { "bio": "Sarah Chen brings luxury property expertise and fluent Mandarin.", "credentials": ["REALTOR\u00ae", "ABR"] } },
        "city_pages": { "enabled": false, "data": { "cities": [] } }
      }
    }
  }
}
```

- [ ] **Step 5: Create test-broker-agent account (both mode — broker with personal site + team)**

Create `config/accounts/test-broker-agent/account.json`:
```json
{
  "handle": "test-broker-agent",
  "template": "emerald-classic",
  "branding": { "primary_color": "#2c3e50", "accent_color": "#e67e22" },
  "brokerage": { "name": "Broker Direct Realty", "license_number": "NJ-2024-99999" },
  "agent": {
    "enabled": true,
    "id": "test-broker-agent",
    "name": "Diana Reyes",
    "title": "Broker/Owner",
    "phone": "(973) 555-0300",
    "email": "diana@brokerdirect.com",
    "tagline": "Broker and mentor."
  },
  "location": { "state": "NJ", "service_areas": ["Newark", "Montclair"] }
}
```

Create `config/accounts/test-broker-agent/content.json` with hero, features, profiles (enabled), contact_form, and about sections. Profiles items list includes `agent-c`.

Create `config/accounts/test-broker-agent/agents/agent-c/config.json`:
```json
{
  "id": "agent-c",
  "name": "Marcus Lee",
  "title": "Associate Agent",
  "phone": "(973) 555-0301",
  "email": "marcus@brokerdirect.com"
}
```

Create `config/accounts/test-broker-agent/agents/agent-c/content.json` with hero, contact_form, about sections enabled.

- [ ] **Step 6: Delete old config/agents/ directory**

After verifying migration: `rm -rf config/agents/`

Keep `config/agent.schema.json` — update to `config/account.schema.json` later.

- [ ] **Step 6: Commit**

```bash
git add config/accounts/ scripts/migrate-to-accounts.mjs
git rm -r config/agents/
git commit -m "feat: migrate config/agents/ to config/accounts/

Run migration script to convert flat agent configs to account-based
structure. Each account has account.json + content.json with pages
wrapper and renamed sections. Added test-brokerage with 2 sub-agents."
```

---

### Task 6: Update config registry prebuild script

**Files:**
- Modify: `apps/agent-site/scripts/generate-config-registry.mjs`

- [ ] **Step 1: Rewrite prebuild script to read from config/accounts/**

Key changes:
- `AGENTS_DIR` -> `ACCOUNTS_DIR` pointing to `config/accounts`
- `discoverAccounts()` reads directories with `account.json`
- `discoverAgents(accountDir)` reads `agents/` subdirectory
- Output exports: `accounts`, `accountContent`, `agentConfigs`, `agentContent`, `legalContent`, `customDomains`, `accountHandles`
- Import types: `AccountConfig`, `AgentConfig`, `ContentConfig`

```javascript
// apps/agent-site/scripts/generate-config-registry.mjs
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ACCOUNTS_DIR = path.resolve(__dirname, "../../../config/accounts");
const OUTPUT = path.resolve(__dirname, "../lib/config-registry.ts");

const SKIP_PATTERN = /^bad-|\.schema\.json$/;

function loadJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, "utf-8"));
}

function tryReadFile(filePath) {
  try { return fs.readFileSync(filePath, "utf-8"); }
  catch { return undefined; }
}

function discoverAccounts() {
  const result = [];
  for (const entry of fs.readdirSync(ACCOUNTS_DIR, { withFileTypes: true })) {
    if (!entry.isDirectory() || SKIP_PATTERN.test(entry.name)) continue;
    const accountPath = path.join(ACCOUNTS_DIR, entry.name, "account.json");
    if (fs.existsSync(accountPath)) {
      result.push(entry.name);
    }
  }
  return result;
}

function discoverAgents(accountDir) {
  const agentsDir = path.join(accountDir, "agents");
  if (!fs.existsSync(agentsDir)) return [];
  const result = [];
  for (const entry of fs.readdirSync(agentsDir, { withFileTypes: true })) {
    if (!entry.isDirectory()) continue;
    const configPath = path.join(agentsDir, entry.name, "config.json");
    if (fs.existsSync(configPath)) {
      result.push(entry.name);
    }
  }
  return result;
}

function main() {
  const handles = discoverAccounts();
  console.log(`[prebuild] Found ${handles.length} account(s): ${handles.join(", ")}`);

  const accountsMap = {};
  const accountContentMap = {};
  const agentConfigsMap = {};
  const agentContentMap = {};
  const legalContentMap = {};
  const customDomains = {};

  for (const handle of handles) {
    const accountDir = path.join(ACCOUNTS_DIR, handle);
    const account = loadJson(path.join(accountDir, "account.json"));
    accountsMap[handle] = account;

    const contentPath = path.join(accountDir, "content.json");
    if (fs.existsSync(contentPath)) {
      accountContentMap[handle] = loadJson(contentPath);
    }

    const legalDir = path.join(accountDir, "legal");
    if (fs.existsSync(legalDir)) {
      const legalPages = {};
      for (const page of ["privacy", "terms", "accessibility"]) {
        const above = tryReadFile(path.join(legalDir, `${page}-above.md`));
        const below = tryReadFile(path.join(legalDir, `${page}-below.md`));
        if (above !== undefined || below !== undefined) {
          legalPages[page] = { above, below };
        }
      }
      if (Object.keys(legalPages).length > 0) {
        legalContentMap[handle] = legalPages;
      }
    }

    if (account.agent?.id && account.integrations?.hosting) {
      customDomains[account.integrations.hosting] = handle;
    }

    const agentIds = discoverAgents(accountDir);
    if (agentIds.length > 0) {
      agentConfigsMap[handle] = {};
      agentContentMap[handle] = {};
      for (const agentId of agentIds) {
        const agentDir = path.join(accountDir, "agents", agentId);
        agentConfigsMap[handle][agentId] = loadJson(path.join(agentDir, "config.json"));
        const agentContentPath = path.join(agentDir, "content.json");
        if (fs.existsSync(agentContentPath)) {
          agentContentMap[handle][agentId] = loadJson(agentContentPath);
        }
      }
      console.log(`  ${handle}: ${agentIds.length} agent(s): ${agentIds.join(", ")}`);
    }
  }

  const output = `// AUTO-GENERATED by scripts/generate-config-registry.mjs -- DO NOT EDIT
import type { AccountConfig, AgentConfig, ContentConfig } from "./types";

export const accounts: Record<string, AccountConfig> = ${JSON.stringify(accountsMap, null, 2)} as unknown as Record<string, AccountConfig>;

export const accountContent: Record<string, ContentConfig> = ${JSON.stringify(accountContentMap, null, 2)} as unknown as Record<string, ContentConfig>;

export const agentConfigs: Record<string, Record<string, AgentConfig>> = ${JSON.stringify(agentConfigsMap, null, 2)} as unknown as Record<string, Record<string, AgentConfig>>;

export const agentContent: Record<string, Record<string, ContentConfig>> = ${JSON.stringify(agentContentMap, null, 2)} as unknown as Record<string, Record<string, ContentConfig>>;

export const legalContent: Record<string, Record<string, { above?: string; below?: string }>> = ${JSON.stringify(legalContentMap, null, 2)};

export const customDomains: Record<string, string> = ${JSON.stringify(customDomains, null, 2)};

export const accountHandles: Set<string> = new Set(${JSON.stringify(handles)});
`;

  fs.writeFileSync(OUTPUT, output, "utf-8");
  console.log(`[prebuild] Wrote ${OUTPUT}`);
}

main();
```

- [ ] **Step 2: Run prebuild to generate new registry**

Run: `node apps/agent-site/scripts/generate-config-registry.mjs`
Expected: Generates `config-registry.ts` with new export names.

- [ ] **Step 3: Commit**

```bash
git add apps/agent-site/scripts/generate-config-registry.mjs apps/agent-site/lib/config-registry.ts
git commit -m "feat: update config registry prebuild for accounts structure

Read from config/accounts/ instead of config/agents/.
Generate accounts, accountContent, agentConfigs, agentContent maps.
Support nested agents/ subdirectories."
```

---

## Chunk 3: Template & Rendering Pipeline Updates

### Task 7: Update template props and compositions

**Files:**
- Modify: `apps/agent-site/templates/types.ts`
- Modify: `apps/agent-site/templates/emerald-classic.tsx`
- Modify: `apps/agent-site/templates/modern-minimal.tsx`
- Modify: `apps/agent-site/templates/warm-community.tsx`

- [ ] **Step 1: Update TemplateProps**

`apps/agent-site/templates/types.ts`:
```typescript
import type { AccountConfig, AgentConfig, ContentConfig } from "@/lib/types";

export interface TemplateProps {
  account: AccountConfig;
  content: ContentConfig;
  /** When rendering an agent sub-page, this is the agent's identity */
  agent?: AgentConfig;
}
```

- [ ] **Step 2: Update all three templates**

Each template changes:
- Props: `{ agent, content }` -> `{ account, content, agent }`
- Sections: `content.sections.X` -> `content.pages.home.sections.X`
- Section keys: `services` -> `features`, `how_it_works` -> `steps`, `sold_homes` -> `gallery`, `cma_form` -> `contact_form`
- Identity resolution: `agent ?? account.agent ?? fallback-to-broker/brokerage`
- Optional chaining on sections: `s.hero?.enabled` (sections are optional in PageSections)
- Nav: `<Nav account={account} navigation={content.navigation} />`
- CmaSection: `agentId={identity.id}`, `defaultState={account.location.state}`, `tracking={account.integrations?.tracking}`
- About: `agent={account}` (or the resolved identity)
- Footer: `agent={account}` `agentId={identity.id}`

- [ ] **Step 3: Commit**

```bash
git add apps/agent-site/templates/
git commit -m "feat: update templates for pages architecture

TemplateProps takes account+content+agent. Templates read from
content.pages.home.sections with new section key names. Identity
resolved from agent prop, account.agent, or account.broker."
```

---

### Task 8: Update Nav component

**Files:**
- Modify: `apps/agent-site/components/Nav.tsx`

- [ ] **Step 1: Update Nav props and rendering**

Key changes:
- `NavProps.agent: AgentConfig` -> `NavProps.account: AccountConfig`
- Remove `contactInfo` prop (read from `account.contact_info`)
- Filter nav items: `items.filter(item => item.enabled)`
- Use `item.href` directly (already has `#` prefix)
- Logo link: always `href="/"`
- Agent name/phone/email: `account.agent?.name ?? account.brokerage.name`

- [ ] **Step 2: Commit**

```bash
git add apps/agent-site/components/Nav.tsx
git commit -m "feat: update Nav for account-based props

Nav takes AccountConfig. Filters nav items by enabled flag.
Logo links to /. Contact info from account.contact_info."
```

---

### Task 9: Update app/page.tsx (home page)

**Files:**
- Modify: `apps/agent-site/app/page.tsx`

- [ ] **Step 1: Update page to use account loading**

Key changes:
- `loadAgentConfig` -> `loadAccountConfig`, `loadAgentContent` -> `loadAccountContent`
- `agent.identity.name` -> `account.agent?.name ?? account.brokerage.name`
- `agent.branding` -> `account.branding`
- `content.template` -> `account.template`
- `<Template agent={agent} content={content} />` -> `<Template account={account} content={content} />`
- JSON-LD: build from `account.agent` or broker/brokerage fields
- Rename `resolveAgentId` -> `resolveHandle`

- [ ] **Step 2: Commit**

```bash
git add apps/agent-site/app/page.tsx
git commit -m "feat: update home page for account-based loading

Use loadAccountConfig/loadAccountContent. Template from account.template.
Branding from account.branding. Pass account+content to template."
```

---

### Task 10: Add /agents/[id] dynamic route

**Files:**
- Create: `apps/agent-site/app/agents/[id]/page.tsx`

- [ ] **Step 1: Create agent sub-page**

This page:
1. Resolves `handle` from env/query params (same as home page)
2. Gets `id` from the URL path param
3. Loads `loadAccountConfig(handle)` for template + branding
4. Loads `loadAgentConfig(handle, id)` for agent identity
5. Loads `loadAgentContent(handle, id)` for agent content (falls back to account content)
6. Renders `<Template account={account} content={agentContent} agent={agentConfig} />`
7. JSON-LD: RealEstateAgent with `worksFor` referencing the brokerage

- [ ] **Step 2: Commit**

```bash
git add apps/agent-site/app/agents/
git commit -m "feat: add /agents/[id] dynamic route for team member pages

Agent sub-pages load agent config + content from accounts/{handle}/agents/{id}.
Uses account template and branding. Falls back to account content if no agent content."
```

---

### Task 10b: Add /agents/[id]/thank-you route

**Files:**
- Create: `apps/agent-site/app/agents/[id]/thank-you/page.tsx`

- [ ] **Step 1: Create agent thank-you page**

This page resolves handle + agent ID, loads account + agent config, then renders the thank-you page content. Falls back to account's thank_you data if agent content has none.

```typescript
// Key logic:
// 1. Load account config (for branding, template)
// 2. Load agent config (for name, phone)
// 3. Load agent content, fallback to account content
// 4. Render content.pages.thank_you with agent identity interpolation
```

- [ ] **Step 2: Commit**

```bash
git add apps/agent-site/app/agents/[id]/thank-you/
git commit -m "feat: add /agents/[id]/thank-you route

Agent thank-you pages fall back to account thank_you content."
```

---

### Task 10c: Add /agents/[id] route tests

**Files:**
- Create: `apps/agent-site/__tests__/pages/agents-id.test.tsx`

- [ ] **Step 1: Write route tests**

Test cases:
1. Valid agent renders with agent identity and account branding
2. Invalid agent ID returns 404 (notFound)
3. Agent page falls back to account content when agent has no content.json
4. Agent thank-you page renders with agent identity
5. Agent thank-you falls back to account thank-you when agent has none

Mock `loadAccountConfig`, `loadAgentConfig`, `loadAgentContent`, `loadAccountContent`.

- [ ] **Step 2: Run tests**

Run: `cd apps/agent-site && npx vitest run __tests__/pages/agents-id.test.tsx`
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add apps/agent-site/__tests__/pages/agents-id.test.tsx
git commit -m "test: add /agents/[id] and /agents/[id]/thank-you route tests

Covers valid agent, invalid agent 404, content fallback, thank-you fallback."
```

---

### Task 10d: Create Profiles section components (Spec Phase 5)

**Files:**
- Create: `apps/agent-site/components/sections/profiles/ProfilesGrid.tsx`
- Create: `apps/agent-site/components/sections/profiles/ProfilesCards.tsx`
- Create: `apps/agent-site/components/sections/profiles/ProfilesClean.tsx`
- Test: `apps/agent-site/__tests__/components/profiles/ProfilesGrid.test.tsx`
- Test: `apps/agent-site/__tests__/components/profiles/ProfilesCards.test.tsx`
- Test: `apps/agent-site/__tests__/components/profiles/ProfilesClean.test.tsx`
- Modify: `apps/agent-site/components/sections/index.ts` (add exports)

One variant per existing template style:
- `ProfilesGrid` — for Emerald Classic (card grid with headshots)
- `ProfilesCards` — for Warm Community (rounded card layout)
- `ProfilesClean` — for Modern Minimal (minimal list layout)

Each component:
- Accepts `ProfilesProps` (items, title?, subtitle?)
- Each profile card shows: headshot (optional), name, title
- Links to `/agents/{item.id}` (or `item.link` if provided)

- [ ] **Step 1: Write failing tests for ProfilesGrid**

```typescript
import { render, screen } from "@testing-library/react";
import { ProfilesGrid } from "@/components/sections/profiles/ProfilesGrid";

const ITEMS = [
  { id: "agent-a", name: "James W.", title: "Senior Associate" },
  { id: "agent-b", name: "Sarah C.", title: "Associate", headshot_url: "/headshot.jpg" },
];

describe("ProfilesGrid", () => {
  it("renders all profile items", () => {
    render(<ProfilesGrid items={ITEMS} title="Our Team" />);
    expect(screen.getByText("Our Team")).toBeInTheDocument();
    expect(screen.getByText("James W.")).toBeInTheDocument();
    expect(screen.getByText("Sarah C.")).toBeInTheDocument();
  });

  it("links each profile to /agents/{id}", () => {
    render(<ProfilesGrid items={ITEMS} />);
    const links = screen.getAllByRole("link");
    expect(links[0]).toHaveAttribute("href", "/agents/agent-a");
    expect(links[1]).toHaveAttribute("href", "/agents/agent-b");
  });

  it("uses item.link when provided", () => {
    const items = [{ id: "x", name: "X", title: "T", link: "/custom" }];
    render(<ProfilesGrid items={items} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "/custom");
  });

  it("renders headshot image when provided", () => {
    render(<ProfilesGrid items={ITEMS} />);
    const img = screen.getByAltText("Sarah C.");
    expect(img).toBeInTheDocument();
  });

  it("renders placeholder when no headshot", () => {
    render(<ProfilesGrid items={[ITEMS[0]]} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Implement ProfilesGrid**

```typescript
// apps/agent-site/components/sections/profiles/ProfilesGrid.tsx
import Image from "next/image";
import Link from "next/link";
import type { ProfilesProps } from "../types";

export function ProfilesGrid({ items, title, subtitle }: ProfilesProps) {
  return (
    <section id="profiles" style={{ padding: "4rem 1.5rem", maxWidth: 1200, margin: "0 auto" }}>
      {title && <h2 style={{ textAlign: "center", fontSize: "2rem", marginBottom: 8 }}>{title}</h2>}
      {subtitle && <p style={{ textAlign: "center", color: "#666", marginBottom: 32 }}>{subtitle}</p>}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))", gap: 24 }}>
        {items.map((item) => (
          <Link
            key={item.id}
            href={item.link ?? `/agents/${item.id}`}
            style={{ textDecoration: "none", color: "inherit" }}
          >
            <div style={{ border: "1px solid #e5e7eb", borderRadius: 12, overflow: "hidden", textAlign: "center", padding: 24 }}>
              {item.headshot_url ? (
                <Image src={item.headshot_url} alt={item.name} width={120} height={120} style={{ borderRadius: "50%", objectFit: "cover", margin: "0 auto 16px" }} />
              ) : (
                <div style={{ width: 120, height: 120, borderRadius: "50%", background: "#e5e7eb", margin: "0 auto 16px", display: "flex", alignItems: "center", justifyContent: "center", fontSize: 36, color: "#9ca3af" }}>
                  {item.name.charAt(0)}
                </div>
              )}
              <h3 style={{ fontSize: "1.1rem", marginBottom: 4 }}>{item.name}</h3>
              <p style={{ color: "#666", fontSize: "0.9rem" }}>{item.title}</p>
            </div>
          </Link>
        ))}
      </div>
    </section>
  );
}
```

- [ ] **Step 3: Run ProfilesGrid test**

Run: `cd apps/agent-site && npx vitest run __tests__/components/profiles/ProfilesGrid.test.tsx`
Expected: PASS

- [ ] **Step 4: Implement ProfilesCards and ProfilesClean** (same pattern, different styling)

- [ ] **Step 5: Add exports to sections/index.ts**

```typescript
// Profiles variants
export { ProfilesGrid } from "./profiles/ProfilesGrid";
export { ProfilesCards } from "./profiles/ProfilesCards";
export { ProfilesClean } from "./profiles/ProfilesClean";
```

- [ ] **Step 6: Wire profiles into template compositions**

Each template checks `s.profiles?.enabled && s.profiles.data.items.length > 0` and renders the appropriate variant.

- [ ] **Step 7: Run all profile tests**

Run: `cd apps/agent-site && npx vitest run __tests__/components/profiles/`
Expected: PASS

- [ ] **Step 8: Commit**

```bash
git add apps/agent-site/components/sections/profiles/ apps/agent-site/__tests__/components/profiles/ apps/agent-site/components/sections/index.ts apps/agent-site/templates/
git commit -m "feat: add Profiles section components (Phase 5)

ProfilesGrid (Emerald), ProfilesCards (Warm), ProfilesClean (Modern).
Each card links to /agents/{id}. Wired into template compositions."
```

---

## Chunk 4: Section Component & About/Footer Updates

### Task 11: Update section component imports and About/Footer

**Files:**
- Modify: All section components that import old type names
- Modify: About components (AboutSplit, AboutMinimal, AboutCard)
- Modify: Footer component

**Note on file/folder renames:** Component files keep their current names (`ServicesGrid.tsx`, `SoldGrid.tsx`, etc.) because renaming files would break 30+ import paths and test files with no behavioral change. The type imports change, but the component names stay. This is intentional — file renames can be done in a follow-up PR if desired.

- [ ] **Step 1: Update type imports across section components**

Find-and-replace in each section file:
- `ServiceItem` -> `FeatureItem`
- `SoldHomeItem` -> `GalleryItem`
- `CmaFormData` -> `ContactFormData`
- `ServicesProps` -> `FeaturesProps`
- `SoldHomesProps` -> `GalleryProps`

These are import-only changes — component logic stays the same since the type shapes are identical.

- [ ] **Step 2: Update About components to use getDisplayName helper**

Each About component (AboutSplit, AboutMinimal, AboutCard) currently does:
- `agent.identity.name` — change to `getDisplayName(agent)`
- `agent.identity.title` — change to helper or inline check
- `agent.identity.headshot_url` — change to `getHeadshotUrl(agent)`

Import `getDisplayName`, `getHeadshotUrl` from `./types`.

- [ ] **Step 3: Update Footer component**

Footer currently accesses `agent.identity.name`, `agent.identity.phone`, etc. Update to accept `AccountConfig` and read from `account.agent?.name ?? account.brokerage.name`.

- [ ] **Step 4: Update backward-compatible component aliases in sections/index.ts**

The aliases (`Hero`, `Services`, `HowItWorks`, `SoldHomes`, etc.) stay for now — they're used in the templates until Task 16.

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/
git commit -m "feat: update section components for renamed types

Import FeatureItem, GalleryItem, ContactFormData. About and Footer
use getDisplayName/getHeadshotUrl helpers for AccountConfig|AgentConfig."
```

---

## Chunk 5: Test Updates

### Task 12: Update all existing test files

**Files:**
- Modify: All `apps/agent-site/__tests__/**/*.test.tsx` files

This is the largest task. Every test that imports AGENT/CONTENT must be updated:
- `AGENT` -> `ACCOUNT` (or use the alias)
- `agent.identity.name` -> `account.agent?.name`
- `content.sections.services` -> `content.pages.home.sections.features`
- Template renders: `<Template agent={AGENT} content={CONTENT} />` -> `<Template account={ACCOUNT} content={CONTENT} />`
- Nav: `<Nav agent={AGENT} ...>` -> `<Nav account={ACCOUNT} ...>`
- About: assertions on agent name

- [ ] **Step 1: Update template integration tests**

Files:
- `__tests__/templates/emerald-classic.test.tsx`
- `__tests__/templates/modern-minimal.test.tsx`
- `__tests__/templates/warm-community.test.tsx`

- [ ] **Step 2: Update Nav tests**

File: `__tests__/components/Nav.test.tsx`

Add test for `enabled` filtering: nav item with `enabled: false` should not render.

- [ ] **Step 3: Update section component tests**

For each section test (heroes, stats, services, steps, sold, testimonials, about, shared):
- Update imports and fixture references
- About tests: pass `agent={ACCOUNT}` and update name assertions

- [ ] **Step 4: Update page/layout tests**

Update mocked imports from old loader names to new ones.

- [ ] **Step 5: Run full test suite**

Run: `cd apps/agent-site && npx vitest run`
Expected: All tests pass.

- [ ] **Step 6: Run coverage**

Run: `cd apps/agent-site && npx vitest run --coverage`
Expected: 100% branch coverage on all modified files.

- [ ] **Step 7: Commit**

```bash
git add apps/agent-site/__tests__/
git commit -m "test: update all tests for account-based types

Migrate AGENT->ACCOUNT, CONTENT uses pages wrapper, section key renames.
Template tests pass account+content. Nav tests verify enabled filtering."
```

---

## Chunk 6: Validation, CI/CD, Cleanup

### Task 13: Add config validation test

**Files:**
- Create: `apps/agent-site/__tests__/config/validation.test.ts`

- [ ] **Step 1: Write nav-to-section validation test**

```typescript
import { accounts, accountContent, agentContent } from "@/lib/config-registry";

describe("Config validation", () => {
  const handles = Object.keys(accounts);

  describe.each(handles)("account: %s", (handle) => {
    const content = accountContent[handle];
    if (!content) return;

    const nav = content.navigation?.items ?? [];
    const sections = content.pages.home.sections;

    it("every enabled nav item maps to an enabled section", () => {
      const enabledNavHrefs = nav
        .filter((item) => item.enabled)
        .map((item) => item.href.replace("#", ""));

      for (const href of enabledNavHrefs) {
        const section = sections[href as keyof typeof sections];
        expect(section, `nav links to #${href} but section "${href}" is missing`).toBeDefined();
        if (section) {
          expect(section.enabled, `nav links to #${href} but section is disabled`).toBe(true);
        }
      }
    });
  });

  // Validate agent content: sections referenced by account nav must exist on agent page
  for (const handle of handles) {
    const entries = agentContent[handle];
    if (!entries) continue;
    const accountNav = accountContent[handle]?.navigation?.items ?? [];

    for (const [agentId, content] of Object.entries(entries)) {
      describe(`agent: ${handle}/${agentId}`, () => {
        it("has valid section structure", () => {
          expect(content.pages.home.sections).toBeDefined();
        });

        it("enabled sections in agent content have valid data", () => {
          const sections = content.pages.home.sections;
          for (const [key, section] of Object.entries(sections)) {
            if (section && (section as { enabled: boolean }).enabled) {
              expect((section as { data: unknown }).data,
                `agent ${agentId} section "${key}" is enabled but has no data`
              ).toBeDefined();
            }
          }
        });
      });
    }
  }
});
```

- [ ] **Step 2: Run validation test**

Run: `cd apps/agent-site && npx vitest run __tests__/config/validation.test.ts`
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add apps/agent-site/__tests__/config/validation.test.ts
git commit -m "test: add config validation -- nav items must map to enabled sections

Build-time test catches config mismatches before deployment."
```

---

### Task 14: Update CI/CD pipeline

**Files:**
- Modify: `.github/workflows/agent-site.yml`

- [ ] **Step 1: Update path triggers**

Lines 8, 14: `'config/agents/**'` -> `'config/accounts/**'`

- [ ] **Step 2: Update preview deploy agent discovery**

Lines 181-208: Change `for dir in config/agents/*/` to `for dir in config/accounts/*/`, read from `account.json` instead of `config.json`, extract name from `account.agent.name` or `brokerage.name`.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/agent-site.yml
git commit -m "ci: update agent-site workflow for config/accounts/ path

Path triggers, preview deploy discovery read from config/accounts/."
```

---

### Task 15: Update infra scripts

**Files:**
- Check and modify: All files in `infra/` referencing `config/agents`

- [ ] **Step 1: Search and update**

Run: `grep -r "config/agents" infra/ --include="*.ps1" --include="*.sh"`
Update any matches to `config/accounts`.

- [ ] **Step 2: Commit if needed**

```bash
git add infra/
git commit -m "ci: update infra scripts for config/accounts/ path"
```

---

### Task 16: Remove backward-compatible aliases

**Files:**
- Modify: `apps/agent-site/lib/types.ts`
- Modify: `apps/agent-site/components/sections/types.ts`
- Modify: `apps/agent-site/components/sections/index.ts`
- Modify: `apps/agent-site/templates/*.tsx`

- [ ] **Step 1: Remove deprecated type aliases from types.ts**

Remove: `ServiceItem`, `SoldHomeItem`, `CmaFormData` aliases.

- [ ] **Step 2: Remove deprecated prop aliases from sections/types.ts**

Remove: `ServicesProps`, `SoldHomesProps` aliases.

- [ ] **Step 3: Remove backward-compatible component aliases from sections/index.ts**

Remove: `Hero`, `Services`, `HowItWorks`, `SoldHomes`, `Testimonials`, `About` aliases.
Update template imports to use direct component names.

- [ ] **Step 4: Remove AGENT/AGENT_MINIMAL aliases from fixtures**

Remove the `export const AGENT = ACCOUNT` lines.

- [ ] **Step 5: Run full test suite**

Run: `cd apps/agent-site && npx vitest run`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add apps/agent-site/
git commit -m "refactor: remove all backward-compatible aliases

Types, prop types, component aliases, and fixture aliases removed.
All imports use new names directly."
```

---

### Task 17: Final verification

- [ ] **Step 1: Regenerate config registry**

Run: `node apps/agent-site/scripts/generate-config-registry.mjs`

- [ ] **Step 2: Run full test suite with coverage**

Run: `cd apps/agent-site && npx vitest run --coverage`
Expected: All tests pass, 100% branch coverage.

- [ ] **Step 3: Build Next.js**

Run: `cd apps/agent-site && npm run build`
Expected: Build succeeds with no TypeScript errors.

- [ ] **Step 4: Verify no old references remain**

Run: `grep -r "config/agents" apps/agent-site/ .github/ infra/ --include="*.ts" --include="*.tsx" --include="*.mjs" --include="*.yml" --include="*.ps1" --include="*.sh"`
Expected: No matches.

Run: `grep -r "ServiceItem\|SoldHomeItem\|CmaFormData\|AgentIdentity\|AgentContent" apps/agent-site/lib/ apps/agent-site/components/ apps/agent-site/templates/ --include="*.ts" --include="*.tsx"`
Expected: No matches (old type names fully removed).

- [ ] **Step 5: Commit any remaining fixes**

```bash
git add -A
git commit -m "chore: final cleanup -- no stale references to old config paths or types"
```

---

## Task Dependency Graph

```
PARALLEL TRACK A (code):
  Task 1 (types) --> Task 2 (section props) --> Task 3 (fixtures) --> Task 4 (config loader)

PARALLEL TRACK B (config files — independent of code):
  Task 5 (migrate files) --> Task 6 (prebuild script)

MERGE POINT — both tracks must complete before continuing:

Task 2 + Task 6 --> Task 7 (templates) --> Task 8 (Nav)
                                       --> Task 9 (page.tsx)
                                       --> Task 10 (agents route)
                                       --> Task 10b (agents thank-you route)
                                       --> Task 10c (agents route tests)
                                       --> Task 10d (profiles components)
                                       --> Task 11 (section component imports)

Task 7-11 --> Task 12 (test updates) --> Task 13 (validation test)
                                     --> Task 14 (CI/CD)
                                     --> Task 15 (infra scripts)

Task 12-15 --> Task 16 (remove aliases) --> Task 17 (final verify)
```

**Parallelism:** Tasks 1-4 and Tasks 5-6 can execute simultaneously — config files don't import TypeScript types and vice versa. Tasks 7-11 can also be partially parallelized since they touch different files, though they all depend on Tasks 1-6 completing.

**New tasks added by review:**
- Task 10b: `/agents/[id]/thank-you` route (spec routing table)
- Task 10c: Agent route tests (spec Phase 6)
- Task 10d: Profiles section components (spec Phase 5)
