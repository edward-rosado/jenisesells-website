# Agent Site Template Engine Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build the agent site template engine — a Next.js 16 app that renders white-label agent websites from config files, with multi-tenant subdomain routing and ISR caching.

**Architecture:** Next.js 16 app at `apps/agent-site` renders templates from `config/agents/{id}.json` (identity/branding) and `config/agents/{id}.content.json` (sections/content). Middleware routes subdomains to agent IDs. CSS variables apply per-agent branding. Sections are modular and toggleable.

**Tech Stack:** Next.js 16, TypeScript, Tailwind CSS 4, React 19

**Prerequisites:**
- Node.js 20+
- Repo restructure complete (config/agents/jenise-buckalew.json exists)
- Read design: `docs/plans/2026-03-09-agent-site-template-engine-design.md`

---

## Phase 1: Scaffold Agent Site App

### Task 1: Initialize Next.js 16 App

**Files:**
- Create: `apps/agent-site/package.json`
- Create: `apps/agent-site/tsconfig.json`
- Create: `apps/agent-site/next.config.ts`
- Create: `apps/agent-site/tailwind.config.ts`
- Create: `apps/agent-site/app/layout.tsx`
- Create: `apps/agent-site/app/page.tsx`

**Step 1: Scaffold the Next.js app**

```bash
cd apps/agent-site
npx create-next-app@latest . --typescript --tailwind --eslint --app --src-dir=false --import-alias="@/*" --turbopack
```

Accept defaults. This creates the full Next.js 16 project with App Router, TypeScript, Tailwind CSS, and ESLint.

**Step 2: Verify it runs**

```bash
cd apps/agent-site
npm run dev
```

Expected: Dev server starts on http://localhost:3000, default Next.js page renders.

**Step 3: Clean up default boilerplate**

Remove the default page content from `app/page.tsx`. Replace with a minimal placeholder:

```tsx
// apps/agent-site/app/page.tsx
export default function Home() {
  return (
    <main>
      <h1>Real Estate Star — Agent Site</h1>
      <p>Template engine loading...</p>
    </main>
  );
}
```

Remove default global CSS styles from `app/globals.css` except for the Tailwind directives:

```css
/* apps/agent-site/app/globals.css */
@import "tailwindcss";
```

**Step 4: Commit**

```bash
git add apps/agent-site/
git commit -m "feat: scaffold Next.js 16 agent-site app"
```

---

### Task 2: Create Agent Config Types

**Files:**
- Create: `apps/agent-site/lib/types.ts`
- Test: `apps/agent-site/lib/__tests__/types.test.ts`

**Step 1: Write the failing test**

```typescript
// apps/agent-site/lib/__tests__/types.test.ts
import { describe, it, expect } from "vitest";
import type { AgentConfig, AgentContent, SectionConfig } from "../types";

describe("AgentConfig type", () => {
  it("should accept a valid agent config", () => {
    const config: AgentConfig = {
      id: "jenise-buckalew",
      identity: {
        name: "Jenise Buckalew",
        email: "jenisesellsnj@gmail.com",
        phone: "(347) 393-5993",
      },
      location: {
        state: "NJ",
      },
      branding: {},
    };
    expect(config.id).toBe("jenise-buckalew");
  });
});

describe("AgentContent type", () => {
  it("should accept a valid content config with enabled/disabled sections", () => {
    const content: AgentContent = {
      template: "emerald-classic",
      sections: {
        hero: {
          enabled: true,
          data: {
            headline: "Sell Your Home with Confidence",
            tagline: "Forward. Moving.",
            cta_text: "Get Your Free Home Value",
            cta_link: "#cma-form",
          },
        },
        stats: {
          enabled: false,
          data: {},
        },
      },
    };
    expect(content.sections.hero.enabled).toBe(true);
    expect(content.sections.stats.enabled).toBe(false);
  });
});
```

**Step 2: Install vitest and run test to verify it fails**

```bash
cd apps/agent-site
npm install -D vitest @vitejs/plugin-react
npx vitest run lib/__tests__/types.test.ts
```

Expected: FAIL — module `../types` not found.

**Step 3: Write the types**

```typescript
// apps/agent-site/lib/types.ts

// --- Agent Config (config/agents/{id}.json) ---

export interface AgentIdentity {
  name: string;
  title?: string;
  license_id?: string;
  brokerage?: string;
  brokerage_id?: string;
  phone: string;
  email: string;
  website?: string;
  languages?: string[];
  tagline?: string;
}

export interface AgentLocation {
  state: string;
  office_address?: string;
  service_areas?: string[];
}

export interface AgentBranding {
  primary_color?: string;
  secondary_color?: string;
  accent_color?: string;
  font_family?: string;
}

export interface AgentIntegrations {
  email_provider?: "gmail" | "outlook" | "smtp";
  hosting?: string;
  form_handler?: "formspree" | "custom";
  form_handler_id?: string;
}

export interface AgentCompliance {
  state_form?: string;
  licensing_body?: string;
  disclosure_requirements?: string[];
}

export interface AgentConfig {
  id: string;
  identity: AgentIdentity;
  location: AgentLocation;
  branding: AgentBranding;
  integrations?: AgentIntegrations;
  compliance?: AgentCompliance;
}

// --- Agent Content (config/agents/{id}.content.json) ---

export interface SectionConfig<T = Record<string, unknown>> {
  enabled: boolean;
  data: T;
}

export interface HeroData {
  headline: string;
  tagline: string;
  cta_text: string;
  cta_link: string;
}

export interface StatItem {
  value: string;
  label: string;
}

export interface ServiceItem {
  title: string;
  description: string;
}

export interface StepItem {
  number: number;
  title: string;
  description: string;
}

export interface SoldHomeItem {
  address: string;
  city: string;
  state: string;
  price: string;
  sold_date?: string;
}

export interface TestimonialItem {
  text: string;
  reviewer: string;
  rating: number;
  source?: string;
}

export interface CmaFormData {
  title: string;
  subtitle: string;
}

export interface AboutData {
  bio: string;
  credentials?: string[];
}

export interface CityPageData {
  slug: string;
  city: string;
  state: string;
  county: string;
  highlights: string[];
  market_snapshot: string;
}

export interface AgentContent {
  template: string;
  sections: {
    hero: SectionConfig<HeroData>;
    stats: SectionConfig<{ items: StatItem[] }>;
    services: SectionConfig<{ items: ServiceItem[] }>;
    how_it_works: SectionConfig<{ steps: StepItem[] }>;
    sold_homes: SectionConfig<{ items: SoldHomeItem[] }>;
    testimonials: SectionConfig<{ items: TestimonialItem[] }>;
    cma_form: SectionConfig<CmaFormData>;
    about: SectionConfig<AboutData>;
    city_pages: SectionConfig<{ cities: CityPageData[] }>;
  };
}
```

**Step 4: Run test to verify it passes**

```bash
cd apps/agent-site
npx vitest run lib/__tests__/types.test.ts
```

Expected: PASS

**Step 5: Commit**

```bash
git add apps/agent-site/lib/
git commit -m "feat: add agent config and content TypeScript types"
```

---

### Task 3: Create Agent Config Loader

**Files:**
- Create: `apps/agent-site/lib/config.ts`
- Test: `apps/agent-site/lib/__tests__/config.test.ts`

**Step 1: Write the failing test**

```typescript
// apps/agent-site/lib/__tests__/config.test.ts
import { describe, it, expect } from "vitest";
import { loadAgentConfig, loadAgentContent } from "../config";

describe("loadAgentConfig", () => {
  it("should load jenise-buckalew config from config/agents/", async () => {
    const config = await loadAgentConfig("jenise-buckalew");
    expect(config).toBeDefined();
    expect(config.id).toBe("jenise-buckalew");
    expect(config.identity.name).toBe("Jenise Buckalew");
    expect(config.location.state).toBe("NJ");
    expect(config.branding.primary_color).toBe("#1B5E20");
  });

  it("should throw for non-existent agent", async () => {
    await expect(loadAgentConfig("nobody")).rejects.toThrow();
  });
});

describe("loadAgentContent", () => {
  it("should return default content when no content file exists", async () => {
    const content = await loadAgentContent("jenise-buckalew");
    expect(content).toBeDefined();
    expect(content.template).toBe("emerald-classic");
    expect(content.sections.hero.enabled).toBe(true);
    expect(content.sections.cma_form.enabled).toBe(true);
  });
});
```

**Step 2: Run test to verify it fails**

```bash
cd apps/agent-site
npx vitest run lib/__tests__/config.test.ts
```

Expected: FAIL — `loadAgentConfig` not found.

**Step 3: Write the config loader**

```typescript
// apps/agent-site/lib/config.ts
import { readFile } from "fs/promises";
import path from "path";
import type { AgentConfig, AgentContent } from "./types";

const CONFIG_DIR = path.resolve(process.cwd(), "../../config/agents");

export async function loadAgentConfig(agentId: string): Promise<AgentConfig> {
  const filePath = path.join(CONFIG_DIR, `${agentId}.json`);
  const raw = await readFile(filePath, "utf-8");
  return JSON.parse(raw) as AgentConfig;
}

export async function loadAgentContent(agentId: string): Promise<AgentContent> {
  const filePath = path.join(CONFIG_DIR, `${agentId}.content.json`);
  try {
    const raw = await readFile(filePath, "utf-8");
    return JSON.parse(raw) as AgentContent;
  } catch {
    return buildDefaultContent(agentId);
  }
}

async function buildDefaultContent(agentId: string): Promise<AgentContent> {
  const config = await loadAgentConfig(agentId);
  const name = config.identity.name;
  const tagline = config.identity.tagline || "Your Trusted Real Estate Professional";

  return {
    template: "emerald-classic",
    sections: {
      hero: {
        enabled: true,
        data: {
          headline: `Sell Your Home with Confidence`,
          tagline,
          cta_text: "Get Your Free Home Value",
          cta_link: "#cma-form",
        },
      },
      stats: { enabled: false, data: { items: [] } },
      services: {
        enabled: true,
        data: {
          items: [
            { title: "Expert Market Analysis", description: `${name} provides a detailed analysis of your local market to price your home right.` },
            { title: "Strategic Marketing Plan", description: "Professional photography, virtual tours, and targeted online advertising." },
            { title: "Negotiation & Closing", description: "Skilled negotiation to get you the best possible price and smooth closing." },
          ],
        },
      },
      how_it_works: {
        enabled: true,
        data: {
          steps: [
            { number: 1, title: "Submit Your Info", description: "Fill out the quick form below with your property details." },
            { number: 2, title: "Get Your Free Report", description: "Receive a professional Comparative Market Analysis within minutes." },
            { number: 3, title: "Schedule a Walkthrough", description: `Meet with ${name} to discuss your selling strategy.` },
          ],
        },
      },
      sold_homes: { enabled: false, data: { items: [] } },
      testimonials: { enabled: false, data: { items: [] } },
      cma_form: {
        enabled: true,
        data: {
          title: "What's Your Home Worth?",
          subtitle: "Get a free, professional Comparative Market Analysis",
        },
      },
      about: {
        enabled: true,
        data: {
          bio: `${name} is a dedicated real estate professional serving ${config.location.service_areas?.join(", ") || config.location.state}. Contact ${name} today to learn how they can help you achieve your real estate goals.`,
          credentials: [],
        },
      },
      city_pages: { enabled: false, data: { cities: [] } },
    },
  };
}
```

**Step 4: Run test to verify it passes**

```bash
cd apps/agent-site
npx vitest run lib/__tests__/config.test.ts
```

Expected: PASS

**Step 5: Commit**

```bash
git add apps/agent-site/lib/
git commit -m "feat: add agent config and content loader with defaults"
```

---

## Phase 2: Multi-Tenant Middleware & Branding

### Task 4: Subdomain Routing Middleware

**Files:**
- Create: `apps/agent-site/middleware.ts`
- Test: `apps/agent-site/lib/__tests__/routing.test.ts`

**Step 1: Write the failing test**

```typescript
// apps/agent-site/lib/__tests__/routing.test.ts
import { describe, it, expect } from "vitest";
import { extractAgentId } from "../routing";

describe("extractAgentId", () => {
  it("should extract agent-id from subdomain", () => {
    expect(extractAgentId("jenise-buckalew.realestatestar.com")).toBe("jenise-buckalew");
  });

  it("should return null for bare domain", () => {
    expect(extractAgentId("realestatestar.com")).toBeNull();
  });

  it("should return null for www subdomain", () => {
    expect(extractAgentId("www.realestatestar.com")).toBeNull();
  });

  it("should handle localhost with port for dev", () => {
    expect(extractAgentId("jenise-buckalew.localhost:3000")).toBe("jenise-buckalew");
  });

  it("should return null for plain localhost", () => {
    expect(extractAgentId("localhost:3000")).toBeNull();
  });
});
```

**Step 2: Run test to verify it fails**

```bash
cd apps/agent-site
npx vitest run lib/__tests__/routing.test.ts
```

Expected: FAIL — `extractAgentId` not found.

**Step 3: Write the routing utility**

```typescript
// apps/agent-site/lib/routing.ts
const BASE_DOMAINS = ["realestatestar.com", "localhost"];
const RESERVED_SUBDOMAINS = ["www", "api", "portal", "app", "admin"];

export function extractAgentId(hostname: string): string | null {
  const host = hostname.split(":")[0]; // strip port

  for (const base of BASE_DOMAINS) {
    if (host === base) return null;
    if (host.endsWith(`.${base}`)) {
      const subdomain = host.slice(0, -(base.length + 1));
      if (RESERVED_SUBDOMAINS.includes(subdomain)) return null;
      if (subdomain.includes(".")) return null; // nested subdomain
      return subdomain;
    }
  }

  // Custom domain — look up in domain map (future)
  // For now, return null
  return null;
}
```

**Step 4: Run test to verify it passes**

```bash
cd apps/agent-site
npx vitest run lib/__tests__/routing.test.ts
```

Expected: PASS

**Step 5: Write the Next.js middleware**

```typescript
// apps/agent-site/middleware.ts
import { NextRequest, NextResponse } from "next/server";
import { extractAgentId } from "./lib/routing";

export function middleware(request: NextRequest) {
  const hostname = request.headers.get("host") || "localhost:3000";
  const agentId = extractAgentId(hostname);

  if (agentId) {
    // Set agent ID as a header for downstream pages to read
    const response = NextResponse.next();
    response.headers.set("x-agent-id", agentId);

    // Also rewrite the URL to include agent ID for the page to access
    const url = request.nextUrl.clone();
    url.searchParams.set("agentId", agentId);
    return NextResponse.rewrite(url);
  }

  // No agent subdomain — show landing/404
  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next|favicon.ico|api).*)"],
};
```

**Step 6: Commit**

```bash
git add apps/agent-site/lib/routing.ts apps/agent-site/lib/__tests__/routing.test.ts apps/agent-site/middleware.ts
git commit -m "feat: add subdomain routing middleware"
```

---

### Task 5: CSS Variable Branding System

**Files:**
- Create: `apps/agent-site/lib/branding.ts`
- Test: `apps/agent-site/lib/__tests__/branding.test.ts`

**Step 1: Write the failing test**

```typescript
// apps/agent-site/lib/__tests__/branding.test.ts
import { describe, it, expect } from "vitest";
import { buildCssVariables } from "../branding";
import type { AgentBranding } from "../types";

describe("buildCssVariables", () => {
  it("should generate CSS variables from branding config", () => {
    const branding: AgentBranding = {
      primary_color: "#1B5E20",
      secondary_color: "#2E7D32",
      accent_color: "#C8A951",
      font_family: "Segoe UI",
    };
    const css = buildCssVariables(branding);
    expect(css).toContain("--color-primary: #1B5E20");
    expect(css).toContain("--color-secondary: #2E7D32");
    expect(css).toContain("--color-accent: #C8A951");
    expect(css).toContain("--font-family: 'Segoe UI'");
  });

  it("should use defaults for missing values", () => {
    const css = buildCssVariables({});
    expect(css).toContain("--color-primary: #1B5E20");
    expect(css).toContain("--font-family: 'Segoe UI'");
  });
});
```

**Step 2: Run test to verify it fails**

```bash
cd apps/agent-site
npx vitest run lib/__tests__/branding.test.ts
```

Expected: FAIL

**Step 3: Write the branding utility**

```typescript
// apps/agent-site/lib/branding.ts
import type { AgentBranding } from "./types";

const DEFAULTS: Required<AgentBranding> = {
  primary_color: "#1B5E20",
  secondary_color: "#2E7D32",
  accent_color: "#C8A951",
  font_family: "Segoe UI",
};

export function buildCssVariables(branding: AgentBranding): string {
  const merged = { ...DEFAULTS, ...branding };
  return [
    `--color-primary: ${merged.primary_color}`,
    `--color-secondary: ${merged.secondary_color}`,
    `--color-accent: ${merged.accent_color}`,
    `--font-family: '${merged.font_family}'`,
  ].join("; ");
}
```

**Step 4: Run test to verify it passes**

```bash
cd apps/agent-site
npx vitest run lib/__tests__/branding.test.ts
```

Expected: PASS

**Step 5: Commit**

```bash
git add apps/agent-site/lib/branding.ts apps/agent-site/lib/__tests__/branding.test.ts
git commit -m "feat: add CSS variable branding system"
```

---

## Phase 3: Template Sections

### Task 6: Create Section Components

**Files:**
- Create: `apps/agent-site/components/sections/Hero.tsx`
- Create: `apps/agent-site/components/sections/StatsBar.tsx`
- Create: `apps/agent-site/components/sections/Services.tsx`
- Create: `apps/agent-site/components/sections/HowItWorks.tsx`
- Create: `apps/agent-site/components/sections/SoldHomes.tsx`
- Create: `apps/agent-site/components/sections/Testimonials.tsx`
- Create: `apps/agent-site/components/sections/CmaForm.tsx`
- Create: `apps/agent-site/components/sections/About.tsx`
- Create: `apps/agent-site/components/sections/Footer.tsx`
- Create: `apps/agent-site/components/sections/index.ts`

Each section component receives typed props and renders using Tailwind CSS
with CSS variable references for branding. Sections use `var(--color-primary)`,
`var(--color-accent)`, etc. so they automatically adapt to each agent's brand.

**Step 1: Create the Hero section**

```tsx
// apps/agent-site/components/sections/Hero.tsx
import type { AgentConfig } from "@/lib/types";
import type { HeroData } from "@/lib/types";

interface HeroProps {
  agent: AgentConfig;
  data: HeroData;
}

export function Hero({ agent, data }: HeroProps) {
  return (
    <section
      className="min-h-[500px] flex items-center justify-center gap-16 flex-wrap px-10 py-20"
      style={{ background: "linear-gradient(135deg, var(--color-primary) 0%, var(--color-secondary) 60%)" }}
    >
      <div className="max-w-xl text-white">
        <h1 className="text-5xl font-extrabold leading-tight mb-3">
          {data.headline.split(agent.identity.name).map((part, i, arr) =>
            i < arr.length - 1 ? (
              <span key={i}>
                {part}
                <span style={{ color: "var(--color-accent)" }}>{agent.identity.name}</span>
              </span>
            ) : (
              <span key={i}>{part}</span>
            )
          )}
        </h1>
        <p className="text-xl italic opacity-80 mb-6">{data.tagline}</p>
        <a
          href={data.cta_link}
          className="inline-block px-9 py-4 rounded-full text-lg font-bold transition-transform hover:-translate-y-0.5"
          style={{ backgroundColor: "var(--color-accent)", color: "var(--color-primary)" }}
        >
          {data.cta_text} &rarr;
        </a>
      </div>
    </section>
  );
}
```

**Step 2: Create the StatsBar section**

```tsx
// apps/agent-site/components/sections/StatsBar.tsx
import type { StatItem } from "@/lib/types";

interface StatsBarProps {
  items: StatItem[];
}

export function StatsBar({ items }: StatsBarProps) {
  return (
    <section
      className="py-8 px-10 flex justify-center gap-12 flex-wrap"
      style={{ backgroundColor: "var(--color-primary)" }}
    >
      {items.map((item, i) => (
        <div key={i} className="text-center text-white">
          <div className="text-3xl font-extrabold" style={{ color: "var(--color-accent)" }}>
            {item.value}
          </div>
          <div className="text-xs uppercase tracking-widest mt-1">{item.label}</div>
        </div>
      ))}
    </section>
  );
}
```

**Step 3: Create the Services section**

```tsx
// apps/agent-site/components/sections/Services.tsx
import type { ServiceItem } from "@/lib/types";

interface ServicesProps {
  items: ServiceItem[];
}

export function Services({ items }: ServicesProps) {
  return (
    <section className="py-16 px-10 max-w-6xl mx-auto">
      <h2 className="text-3xl font-bold text-center mb-10" style={{ color: "var(--color-primary)" }}>
        What I Do for You
      </h2>
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {items.map((item, i) => (
          <div
            key={i}
            className="bg-gray-50 rounded-xl p-7 border-l-4 transition-transform hover:-translate-y-1 hover:shadow-lg"
            style={{ borderLeftColor: "var(--color-secondary)" }}
          >
            <h3 className="text-lg font-bold mb-2" style={{ color: "var(--color-primary)" }}>
              {item.title}
            </h3>
            <p className="text-gray-600 text-sm">{item.description}</p>
          </div>
        ))}
      </div>
    </section>
  );
}
```

**Step 4: Create the HowItWorks section**

```tsx
// apps/agent-site/components/sections/HowItWorks.tsx
import type { StepItem } from "@/lib/types";

interface HowItWorksProps {
  steps: StepItem[];
}

export function HowItWorks({ steps }: HowItWorksProps) {
  return (
    <section className="py-16 px-10 max-w-6xl mx-auto">
      <h2 className="text-3xl font-bold text-center mb-10" style={{ color: "var(--color-primary)" }}>
        How It Works
      </h2>
      <div className="flex justify-center gap-10 flex-wrap">
        {steps.map((step) => (
          <div key={step.number} className="text-center max-w-[250px]">
            <div
              className="w-14 h-14 rounded-full flex items-center justify-center text-2xl font-bold text-white mx-auto mb-4"
              style={{ backgroundColor: "var(--color-secondary)" }}
            >
              {step.number}
            </div>
            <h3 className="font-bold mb-2" style={{ color: "var(--color-primary)" }}>
              {step.title}
            </h3>
            <p className="text-gray-500 text-sm">{step.description}</p>
          </div>
        ))}
      </div>
    </section>
  );
}
```

**Step 5: Create the SoldHomes section**

```tsx
// apps/agent-site/components/sections/SoldHomes.tsx
import type { SoldHomeItem } from "@/lib/types";

interface SoldHomesProps {
  items: SoldHomeItem[];
}

export function SoldHomes({ items }: SoldHomesProps) {
  return (
    <section className="py-16 px-10 max-w-6xl mx-auto">
      <h2 className="text-3xl font-bold text-center mb-10" style={{ color: "var(--color-primary)" }}>
        Recently Sold
      </h2>
      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-5 gap-5">
        {items.map((item, i) => (
          <div key={i} className="bg-gray-50 rounded-lg p-5 text-center border border-gray-200">
            <span
              className="inline-block text-xs font-bold px-3 py-1 rounded-full mb-3"
              style={{ backgroundColor: "var(--color-accent)", color: "var(--color-primary)" }}
            >
              SOLD
            </span>
            <div className="text-xl font-extrabold" style={{ color: "var(--color-primary)" }}>
              {item.price}
            </div>
            <div className="text-xs text-gray-500 mt-1">
              {item.address}, {item.city}, {item.state}
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
```

**Step 6: Create the Testimonials section**

```tsx
// apps/agent-site/components/sections/Testimonials.tsx
import type { TestimonialItem } from "@/lib/types";

interface TestimonialsProps {
  items: TestimonialItem[];
}

export function Testimonials({ items }: TestimonialsProps) {
  return (
    <section className="py-16 px-10 max-w-6xl mx-auto">
      <h2 className="text-3xl font-bold text-center mb-10" style={{ color: "var(--color-primary)" }}>
        What My Clients Say
      </h2>
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {items.map((item, i) => (
          <div key={i} className="bg-gray-50 rounded-xl p-7">
            <div className="text-lg mb-3" style={{ color: "var(--color-accent)" }}>
              {"★".repeat(item.rating)}{"☆".repeat(5 - item.rating)}
            </div>
            <p className="italic text-gray-600 text-sm leading-relaxed">{item.text}</p>
            <div className="mt-4 font-bold text-sm" style={{ color: "var(--color-primary)" }}>
              — {item.reviewer}
              {item.source && <span className="font-normal text-gray-400"> via {item.source}</span>}
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
```

**Step 7: Create the CmaForm section**

```tsx
// apps/agent-site/components/sections/CmaForm.tsx
"use client";

import { useState } from "react";
import type { AgentConfig, CmaFormData } from "@/lib/types";

interface CmaFormProps {
  agent: AgentConfig;
  data: CmaFormData;
}

export function CmaForm({ agent, data }: CmaFormProps) {
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setSubmitting(true);
    const formData = new FormData(e.currentTarget);
    // Submit to form handler (formspree or custom API)
    const endpoint = agent.integrations?.form_handler === "formspree"
      ? `https://formspree.io/f/${agent.integrations.form_handler_id}`
      : `/api/leads`;

    try {
      await fetch(endpoint, { method: "POST", body: formData });
      window.location.href = "/thank-you";
    } catch {
      setSubmitting(false);
    }
  }

  return (
    <section id="cma-form" className="py-16 px-10 max-w-2xl mx-auto">
      <h2 className="text-3xl font-bold text-center mb-2" style={{ color: "var(--color-primary)" }}>
        {data.title}
      </h2>
      <p className="text-center text-gray-500 mb-10">{data.subtitle}</p>
      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="grid grid-cols-2 gap-4">
          <input name="firstName" placeholder="First Name" required className="border rounded-lg px-4 py-3 w-full" />
          <input name="lastName" placeholder="Last Name" required className="border rounded-lg px-4 py-3 w-full" />
        </div>
        <input name="email" type="email" placeholder="Email Address" required className="border rounded-lg px-4 py-3 w-full" />
        <input name="phone" type="tel" placeholder="Phone Number" required className="border rounded-lg px-4 py-3 w-full" />
        <input name="address" placeholder="Property Address" required className="border rounded-lg px-4 py-3 w-full" />
        <div className="grid grid-cols-3 gap-4">
          <input name="city" placeholder="City" required className="border rounded-lg px-4 py-3 w-full" />
          <input name="state" placeholder="State" defaultValue={agent.location.state} required className="border rounded-lg px-4 py-3 w-full" />
          <input name="zip" placeholder="Zip" required className="border rounded-lg px-4 py-3 w-full" />
        </div>
        <select name="timeline" required className="border rounded-lg px-4 py-3 w-full">
          <option value="">When are you looking to sell?</option>
          <option value="asap">As soon as possible</option>
          <option value="1-3m">1-3 months</option>
          <option value="3-6m">3-6 months</option>
          <option value="6-12m">6-12 months</option>
          <option value="curious">Just curious about my home&apos;s value</option>
        </select>
        <textarea name="notes" placeholder="Anything else I should know?" rows={3} className="border rounded-lg px-4 py-3 w-full" />
        <input type="hidden" name="_subject" value={`New CMA Request — ${agent.identity.name}`} />
        <button
          type="submit"
          disabled={submitting}
          className="w-full py-4 rounded-full text-lg font-bold transition-transform hover:-translate-y-0.5 disabled:opacity-50"
          style={{ backgroundColor: "var(--color-accent)", color: "var(--color-primary)" }}
        >
          {submitting ? "Submitting..." : "Get My Free Home Value Report →"}
        </button>
      </form>
    </section>
  );
}
```

**Step 8: Create the About section**

```tsx
// apps/agent-site/components/sections/About.tsx
import type { AgentConfig, AboutData } from "@/lib/types";

interface AboutProps {
  agent: AgentConfig;
  data: AboutData;
}

export function About({ agent, data }: AboutProps) {
  return (
    <section className="py-16 px-10 max-w-4xl mx-auto">
      <h2 className="text-3xl font-bold text-center mb-10" style={{ color: "var(--color-primary)" }}>
        About {agent.identity.name}
      </h2>
      <div className="text-gray-600 leading-relaxed text-center max-w-2xl mx-auto">
        <p>{data.bio}</p>
        {data.credentials && data.credentials.length > 0 && (
          <div className="flex justify-center gap-4 mt-6 flex-wrap">
            {data.credentials.map((cred, i) => (
              <span
                key={i}
                className="px-4 py-2 rounded-full text-sm font-semibold"
                style={{ backgroundColor: "var(--color-primary)", color: "white" }}
              >
                {cred}
              </span>
            ))}
          </div>
        )}
      </div>
    </section>
  );
}
```

**Step 9: Create the Footer**

```tsx
// apps/agent-site/components/sections/Footer.tsx
import type { AgentConfig } from "@/lib/types";

interface FooterProps {
  agent: AgentConfig;
}

export function Footer({ agent }: FooterProps) {
  const { identity, location } = agent;
  return (
    <footer className="py-10 px-10 text-center text-white" style={{ backgroundColor: "var(--color-primary)" }}>
      <p className="text-lg font-bold">
        {identity.name}{identity.title ? `, ${identity.title}` : ""}
      </p>
      {identity.brokerage && <p className="text-sm opacity-80">{identity.brokerage}</p>}
      <p className="mt-3 text-sm">
        <a href={`tel:${identity.phone}`} style={{ color: "var(--color-accent)" }}>{identity.phone}</a>
        {" | "}
        <a href={`mailto:${identity.email}`} style={{ color: "var(--color-accent)" }}>{identity.email}</a>
      </p>
      {location.service_areas && (
        <p className="mt-2 text-xs opacity-60">
          Serving {location.service_areas.join(" · ")}
        </p>
      )}
      {identity.languages && identity.languages.length > 1 && (
        <p className="mt-1 text-xs opacity-60">
          {identity.languages.join(" · ")}
        </p>
      )}
      <p className="mt-6 text-xs opacity-40">
        &copy; {new Date().getFullYear()} {identity.name}. All rights reserved.
      </p>
    </footer>
  );
}
```

**Step 10: Create the barrel export**

```typescript
// apps/agent-site/components/sections/index.ts
export { Hero } from "./Hero";
export { StatsBar } from "./StatsBar";
export { Services } from "./Services";
export { HowItWorks } from "./HowItWorks";
export { SoldHomes } from "./SoldHomes";
export { Testimonials } from "./Testimonials";
export { CmaForm } from "./CmaForm";
export { About } from "./About";
export { Footer } from "./Footer";
```

**Step 11: Commit**

```bash
git add apps/agent-site/components/
git commit -m "feat: add all template section components"
```

---

## Phase 4: Template Rendering & Content File

### Task 7: Create Emerald Classic Template

**Files:**
- Create: `apps/agent-site/templates/emerald-classic.tsx`
- Create: `apps/agent-site/templates/index.ts`

**Step 1: Create the template**

```tsx
// apps/agent-site/templates/emerald-classic.tsx
import type { AgentConfig, AgentContent } from "@/lib/types";
import { Hero, StatsBar, Services, HowItWorks, SoldHomes, Testimonials, CmaForm, About, Footer } from "@/components/sections";

interface TemplateProps {
  agent: AgentConfig;
  content: AgentContent;
}

export function EmeraldClassic({ agent, content }: TemplateProps) {
  const s = content.sections;
  return (
    <>
      {s.hero.enabled && <Hero agent={agent} data={s.hero.data} />}
      {s.stats.enabled && s.stats.data.items.length > 0 && <StatsBar items={s.stats.data.items} />}
      {s.services.enabled && <Services items={s.services.data.items} />}
      {s.how_it_works.enabled && <HowItWorks steps={s.how_it_works.data.steps} />}
      {s.sold_homes.enabled && s.sold_homes.data.items.length > 0 && <SoldHomes items={s.sold_homes.data.items} />}
      {s.testimonials.enabled && s.testimonials.data.items.length > 0 && <Testimonials items={s.testimonials.data.items} />}
      {s.cma_form.enabled && <CmaForm agent={agent} data={s.cma_form.data} />}
      {s.about.enabled && <About agent={agent} data={s.about.data} />}
      <Footer agent={agent} />
    </>
  );
}
```

**Step 2: Create template registry**

```typescript
// apps/agent-site/templates/index.ts
import { EmeraldClassic } from "./emerald-classic";

export const TEMPLATES: Record<string, typeof EmeraldClassic> = {
  "emerald-classic": EmeraldClassic,
};

export function getTemplate(name: string) {
  return TEMPLATES[name] || EmeraldClassic;
}
```

**Step 3: Commit**

```bash
git add apps/agent-site/templates/
git commit -m "feat: add Emerald Classic template with section registry"
```

---

### Task 8: Create Jenise Content File

**Files:**
- Create: `config/agents/jenise-buckalew.content.json`

**Step 1: Create the content file with data from the prototype**

Extract the real content from `prototype/index.html` — testimonials, sold homes,
stats, services, bio. This is the reference implementation.

```json
{
  "template": "emerald-classic",
  "sections": {
    "hero": {
      "enabled": true,
      "data": {
        "headline": "Sell Your Home with Confidence",
        "tagline": "Forward. Moving.",
        "cta_text": "Get Your Free Home Value",
        "cta_link": "#cma-form"
      }
    },
    "stats": {
      "enabled": true,
      "data": {
        "items": [
          { "value": "100+", "label": "Homes Sold" },
          { "value": "5.0", "label": "Zillow Rating (24 Reviews)" },
          { "value": "Gold", "label": "NJ REALTORS Circle of Excellence" },
          { "value": "20+", "label": "Years of Experience" },
          { "value": "$500K–$1.5M", "label": "Sale Price Range" }
        ]
      }
    },
    "services": {
      "enabled": true,
      "data": {
        "items": [
          { "title": "Expert Market Analysis", "description": "I study local trends, recent sales, and neighborhood data to determine the true value of your home — so you never leave money on the table." },
          { "title": "Strategic Marketing Plan", "description": "From professional photography to targeted online campaigns, I create a custom marketing plan that puts your home in front of serious buyers." },
          { "title": "Professional Photography", "description": "First impressions matter. I use high-quality photos and virtual tours to showcase your home beautifully online." },
          { "title": "Strategic Pricing", "description": "Price it right from day one. I use deep market analysis to position your home competitively — attracting more offers, faster." },
          { "title": "Home Prep Guidance", "description": "Small improvements can mean big returns. I walk through your home and recommend exactly what to fix, stage, or skip." },
          { "title": "Maximum Exposure", "description": "Your listing goes on MLS, Zillow, Realtor.com, social media, and my personal network — reaching thousands of potential buyers." },
          { "title": "Se Habla Español", "description": "I proudly serve both English- and Spanish-speaking homeowners with the same care and expertise." }
        ]
      }
    },
    "how_it_works": {
      "enabled": true,
      "data": {
        "steps": [
          { "number": 1, "title": "Submit Your Info", "description": "Fill out the quick form below with your property details." },
          { "number": 2, "title": "Get Your Free Home Value Report", "description": "I'll prepare a professional Comparative Market Analysis showing what your home is worth today." },
          { "number": 3, "title": "Schedule a Walkthrough", "description": "We'll meet at your home to discuss strategy, timing, and next steps — no pressure, just expert guidance." }
        ]
      }
    },
    "sold_homes": {
      "enabled": true,
      "data": {
        "items": [
          { "address": "123 Main St", "city": "East Brunswick", "state": "NJ", "price": "$585,000" },
          { "address": "456 Oak Ave", "city": "Edison", "state": "NJ", "price": "$520,000" },
          { "address": "789 Elm Dr", "city": "Sayreville", "state": "NJ", "price": "$475,000" },
          { "address": "12 Pine Rd", "city": "Hazlet", "state": "NJ", "price": "$610,000" },
          { "address": "34 Cedar Ln", "city": "Cliffwood", "state": "NJ", "price": "$390,000" }
        ]
      }
    },
    "testimonials": {
      "enabled": true,
      "data": {
        "items": [
          { "text": "Jenise was absolutely incredible throughout the entire selling process. She knew the local market inside and out and helped us price our home perfectly. We received multiple offers within the first week!", "reviewer": "The Martinez Family", "rating": 5, "source": "Zillow" },
          { "text": "As first-time sellers, we were nervous about everything. Jenise walked us through each step with patience and professionalism. She handled every detail so we didn't have to stress.", "reviewer": "David & Sarah K.", "rating": 5, "source": "Zillow" },
          { "text": "I interviewed three agents before choosing Jenise, and I'm so glad I did. Her marketing strategy was next-level — professional photos, social media ads, the works. My home sold above asking!", "reviewer": "Linda P.", "rating": 5, "source": "Zillow" },
          { "text": "Jenise helped us sell our home in Middlesex County in under 30 days. She's responsive, knowledgeable, and genuinely cares about her clients. Highly recommend!", "reviewer": "Carlos & Maria G.", "rating": 5, "source": "Zillow" }
        ]
      }
    },
    "cma_form": {
      "enabled": true,
      "data": {
        "title": "What's Your Home Worth?",
        "subtitle": "Get a free, professional Comparative Market Analysis — delivered within minutes."
      }
    },
    "about": {
      "enabled": true,
      "data": {
        "bio": "With over 20 years of experience in New Jersey real estate, I've helped more than 100 families buy and sell homes across Middlesex, Monmouth, and Ocean Counties. I know these neighborhoods, school districts, and market trends — and I use that knowledge to get you the best possible result. Whether you're selling your first home or your fifth, I'm here to guide you every step of the way.",
        "credentials": ["NJ Licensed REALTOR®", "Gold Circle of Excellence", "Bilingual: English & Spanish"]
      }
    },
    "city_pages": {
      "enabled": false,
      "data": {
        "cities": []
      }
    }
  }
}
```

**Step 2: Commit**

```bash
git add config/agents/jenise-buckalew.content.json
git commit -m "feat: add Jenise content file extracted from prototype"
```

---

### Task 9: Wire Up the Main Page

**Files:**
- Modify: `apps/agent-site/app/page.tsx`
- Modify: `apps/agent-site/app/layout.tsx`

**Step 1: Update layout.tsx with branding injection**

```tsx
// apps/agent-site/app/layout.tsx
import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Real Estate Agent",
  description: "Your trusted real estate professional",
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
```

**Step 2: Update page.tsx to load agent and render template**

```tsx
// apps/agent-site/app/page.tsx
import { loadAgentConfig, loadAgentContent } from "@/lib/config";
import { buildCssVariables } from "@/lib/branding";
import { getTemplate } from "@/templates";

interface PageProps {
  searchParams: Promise<{ agentId?: string }>;
}

export const revalidate = 60; // ISR: revalidate every 60 seconds

export default async function AgentPage({ searchParams }: PageProps) {
  const { agentId } = await searchParams;

  // Default to jenise-buckalew for development
  const id = agentId || process.env.DEFAULT_AGENT_ID || "jenise-buckalew";

  try {
    const [agent, content] = await Promise.all([
      loadAgentConfig(id),
      loadAgentContent(id),
    ]);

    const cssVars = buildCssVariables(agent.branding);
    const Template = getTemplate(content.template);

    return (
      <div style={{ cssText: cssVars } as React.CSSProperties}>
        <Template agent={agent} content={content} />
      </div>
    );
  } catch {
    return (
      <main className="min-h-screen flex items-center justify-center">
        <div className="text-center">
          <h1 className="text-4xl font-bold mb-4">Agent Not Found</h1>
          <p className="text-gray-500">No agent site configured for this domain.</p>
        </div>
      </main>
    );
  }
}
```

**Step 3: Run the dev server and verify**

```bash
cd apps/agent-site
npm run dev
```

Open http://localhost:3000 — should render Jenise's site with the Emerald Classic template, green/gold branding, all sections populated from the content file.

**Step 4: Commit**

```bash
git add apps/agent-site/app/
git commit -m "feat: wire up agent page with config loading and template rendering"
```

---

## Phase 5: Navigation & Thank You Page

### Task 10: Create Navigation Component

**Files:**
- Create: `apps/agent-site/components/Nav.tsx`

**Step 1: Create the nav**

```tsx
// apps/agent-site/components/Nav.tsx
import type { AgentConfig } from "@/lib/types";

interface NavProps {
  agent: AgentConfig;
}

export function Nav({ agent }: NavProps) {
  const { identity } = agent;
  return (
    <nav
      className="fixed top-0 w-full z-50 px-10 py-3 flex items-center justify-between"
      style={{ backgroundColor: "var(--color-primary)" }}
    >
      <div className="flex items-center gap-3">
        <span className="text-sm font-semibold tracking-wide" style={{ color: "var(--color-accent)" }}>
          {identity.tagline?.toUpperCase() || identity.name.toUpperCase()}
        </span>
      </div>
      <div className="flex items-center gap-5">
        {identity.email && (
          <a href={`mailto:${identity.email}`} className="text-white text-sm hidden md:block">
            {identity.email}
          </a>
        )}
        {identity.phone && (
          <a
            href={`tel:${identity.phone}`}
            className="px-5 py-2 rounded-full text-sm font-bold"
            style={{ backgroundColor: "var(--color-accent)", color: "var(--color-primary)" }}
          >
            {identity.phone}
          </a>
        )}
      </div>
    </nav>
  );
}
```

**Step 2: Add Nav to the Emerald Classic template**

Add `import { Nav } from "@/components/Nav"` and render `<Nav agent={agent} />` as the first element, with a `<div className="pt-[74px]">` wrapper around the rest.

**Step 3: Commit**

```bash
git add apps/agent-site/components/Nav.tsx apps/agent-site/templates/emerald-classic.tsx
git commit -m "feat: add navigation component to template"
```

---

### Task 11: Create Thank You Page

**Files:**
- Create: `apps/agent-site/app/thank-you/page.tsx`

**Step 1: Create the page**

```tsx
// apps/agent-site/app/thank-you/page.tsx
import { loadAgentConfig } from "@/lib/config";
import { buildCssVariables } from "@/lib/branding";
import { Nav } from "@/components/Nav";
import { Footer } from "@/components/sections";

interface PageProps {
  searchParams: Promise<{ agentId?: string }>;
}

export default async function ThankYouPage({ searchParams }: PageProps) {
  const { agentId } = await searchParams;
  const id = agentId || process.env.DEFAULT_AGENT_ID || "jenise-buckalew";

  try {
    const agent = await loadAgentConfig(id);
    const cssVars = buildCssVariables(agent.branding);

    return (
      <div style={{ cssText: cssVars } as React.CSSProperties}>
        <Nav agent={agent} />
        <main className="pt-[74px] min-h-[70vh] flex items-center justify-center">
          <div className="text-center max-w-lg px-6">
            <div className="text-6xl mb-6">✓</div>
            <h1 className="text-3xl font-bold mb-3" style={{ color: "var(--color-primary)" }}>
              Thank You!
            </h1>
            <p className="text-lg font-semibold mb-4" style={{ color: "var(--color-accent)" }}>
              Your Free Home Value Report Is Being Prepared Now!
            </p>
            <p className="text-gray-600 mb-6">
              {agent.identity.name} will send your personalized Comparative Market Analysis
              to your email shortly. Keep an eye on your inbox!
            </p>
            <a
              href={`tel:${agent.identity.phone}`}
              className="inline-block px-8 py-3 rounded-full font-bold"
              style={{ backgroundColor: "var(--color-accent)", color: "var(--color-primary)" }}
            >
              Call {agent.identity.name}: {agent.identity.phone}
            </a>
          </div>
        </main>
        <Footer agent={agent} />
      </div>
    );
  } catch {
    return <p>Agent not found.</p>;
  }
}
```

**Step 2: Commit**

```bash
git add apps/agent-site/app/thank-you/
git commit -m "feat: add thank-you page"
```

---

## Phase 6: Content Schema & Validation

### Task 12: Create Content JSON Schema

**Files:**
- Create: `config/agent-content.schema.json`

**Step 1: Write the content schema**

Create a JSON Schema that validates the content file structure — ensures
templates reference valid template names, sections have correct data shapes,
and required fields are present.

**Step 2: Commit**

```bash
git add config/agent-content.schema.json
git commit -m "feat: add agent content JSON schema for validation"
```

---

## Phase 7: Dev Experience

### Task 13: Add Vitest Config and NPM Scripts

**Files:**
- Create: `apps/agent-site/vitest.config.ts`
- Modify: `apps/agent-site/package.json`

**Step 1: Create vitest config**

```typescript
// apps/agent-site/vitest.config.ts
import { defineConfig } from "vitest/config";
import path from "path";

export default defineConfig({
  test: {
    environment: "node",
  },
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "."),
    },
  },
});
```

**Step 2: Add test script to package.json**

Add to `scripts`:
```json
"test": "vitest run",
"test:watch": "vitest"
```

**Step 3: Run all tests**

```bash
cd apps/agent-site
npm test
```

Expected: All tests pass (types, config, routing, branding).

**Step 4: Commit**

```bash
git add apps/agent-site/vitest.config.ts apps/agent-site/package.json
git commit -m "chore: add vitest config and test scripts"
```

---

### Task 14: Update Setup Script

**Files:**
- Modify: `setup.sh`

**Step 1: Update setup.sh to install agent-site dependencies**

The setup script already has a section that checks for `apps/agent-site/package.json`.
Now that it exists, verify it works:

```bash
bash setup.sh
```

Expected: "Agent Site dependencies installed" shows as [OK].

**Step 2: Commit if any changes needed**

```bash
git add setup.sh
git commit -m "chore: update setup script for agent-site app"
```

---

### Task 15: Final Verification

**Step 1: Run all tests**

```bash
cd apps/agent-site
npm test
```

Expected: All pass.

**Step 2: Run dev server**

```bash
cd apps/agent-site
npm run dev
```

Expected: Jenise's site renders at http://localhost:3000 with:
- Fixed green nav bar with phone number
- Hero section with "Sell Your Home with Confidence"
- Gold accent colors
- Stats bar
- Services grid (7 cards)
- How It Works (3 steps)
- Recently Sold (5 homes)
- Testimonials (4 reviews)
- CMA form
- About section with credentials badges
- Footer with contact info

**Step 3: Test subdomain routing (manual)**

Add to `/etc/hosts` (or `C:\Windows\System32\drivers\etc\hosts`):
```
127.0.0.1 jenise-buckalew.localhost
```

Then visit `http://jenise-buckalew.localhost:3000` — should render the same site via subdomain routing.

**Step 4: Verify ISR config**

Check that `page.tsx` has `export const revalidate = 60`.

**Step 5: Commit any final cleanup**

```bash
git status
# If clean, done. Otherwise:
git add -A
git commit -m "chore: final cleanup for agent-site template engine"
```

---

## Execution Summary

| Phase | Tasks | Description |
|-------|-------|-------------|
| 1 | 1-3 | Scaffold app, types, config loader |
| 2 | 4-5 | Subdomain middleware, CSS branding |
| 3 | 6 | All 9 section components |
| 4 | 7-9 | Template, content file, main page wiring |
| 5 | 10-11 | Nav component, thank-you page |
| 6 | 12 | Content JSON schema |
| 7 | 13-15 | Vitest, setup script, verification |

**Total: 15 tasks, ~14 commits**

## What This Delivers

After execution, you will have:
- A working Next.js 16 agent site at `apps/agent-site/`
- Jenise's full prototype recreated as a dynamic template
- Multi-tenant subdomain routing
- Per-agent branding via CSS variables
- Toggleable sections via content config
- ISR caching (60s revalidation)
- Type-safe config and content models
- Tests for all utility code
- Ready for the next phase: AI agent builder + CMA pipeline
