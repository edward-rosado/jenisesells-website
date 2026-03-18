# Seven New Agent Site Templates — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expand the agent site template system from 3 to 10 templates, each targeting a distinct market segment with unique section variants, a test agent, and full test coverage.

**Architecture:** Each template = 7 section component variants + 1 template composition file + 1 test agent (config + content + legal + images) + 7 test files. All variants implement existing props interfaces. All templates use CSS variables from agent branding config — never hardcoded colors. Shared components (Nav, Footer, CmaSection) are reused unchanged.

**Tech Stack:** React (Next.js 16), TypeScript, Vitest + React Testing Library, inline styles with CSS variables, picsum.photos for placeholder images.

**Spec:** `docs/superpowers/specs/2026-03-17-seven-new-templates-design.md`

**Skill reference:** `/create-template` — `.claude/skills/learned/create-template.md`

---

## Chunk 1: Shared Infrastructure

Before building any template, extend the shared types and establish the patterns all templates will follow.

### Task 1: Extend SoldHomeItem with optional fields

**Files:**
- Modify: `apps/agent-site/lib/types.ts:94-101`

- [ ] **Step 1: Write the failing test**

Create `apps/agent-site/__tests__/lib/types-extensions.test.ts`:

```typescript
// @vitest-environment jsdom
import { describe, it, expect } from "vitest";
import type { SoldHomeItem, ServiceItem } from "@/lib/types";

describe("SoldHomeItem type extensions", () => {
  it("accepts optional commercial fields", () => {
    const item: SoldHomeItem = {
      address: "100 Main St",
      city: "Dallas",
      state: "TX",
      price: "$4,200,000",
      property_type: "Office",
      sq_ft: "45,000 SF",
      cap_rate: "6.2%",
      noi: "$280,000",
      badge_label: "CLOSED",
      features: [{ label: "Lot", value: "5 acres" }],
      client_quote: "We found our forever home!",
      client_name: "The Kim Family",
      tags: ["Oceanfront", "Beach Access"],
    };
    expect(item.property_type).toBe("Office");
    expect(item.features?.[0].label).toBe("Lot");
    expect(item.client_quote).toBe("We found our forever home!");
    expect(item.tags).toEqual(["Oceanfront", "Beach Access"]);
  });
});

describe("ServiceItem type extensions", () => {
  it("accepts optional category field", () => {
    const item: ServiceItem = {
      title: "Tenant Rep",
      description: "Finding the right space",
      category: "Services",
    };
    expect(item.category).toBe("Services");
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/lib/types-extensions.test.ts`
Expected: FAIL — `property_type`, `sq_ft`, `cap_rate`, `noi`, `badge_label`, `features`, `client_quote`, `client_name` don't exist on `SoldHomeItem`; `category` doesn't exist on `ServiceItem`.

- [ ] **Step 3: Add optional fields to SoldHomeItem and ServiceItem**

In `apps/agent-site/lib/types.ts`, update `SoldHomeItem`:

```typescript
export interface SoldHomeItem {
  address: string;
  city: string;
  state: string;
  price: string;
  sold_date?: string;
  image_url?: string;
  // Optional fields for specialized templates
  property_type?: string;    // "Office", "Oceanfront", "Estate"
  sq_ft?: string;            // "45,000 SF"
  cap_rate?: string;         // "6.2%"
  noi?: string;              // "$280,000"
  badge_label?: string;      // Override "SOLD" text (e.g., "CLOSED")
  features?: Array<{ label: string; value: string }>;  // e.g., [{ label: "Lot", value: "5 acres" }]
  client_quote?: string;     // For success story display
  client_name?: string;      // For success story attribution
  tags?: string[];           // Multiple property tags (e.g., ["Oceanfront", "Beach Access"])
}
```

Update `ServiceItem`:

```typescript
export interface ServiceItem {
  title: string;
  description: string;
  icon?: string;
  category?: string;  // For two-tier service grouping (e.g., commercial)
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/agent-site && npx vitest run __tests__/lib/types-extensions.test.ts`
Expected: PASS

- [ ] **Step 5: Run full test suite to confirm no regressions**

Run: `cd apps/agent-site && npx vitest run`
Expected: All existing tests pass (562+)

- [ ] **Step 6: Commit**

```bash
git add apps/agent-site/lib/types.ts apps/agent-site/__tests__/lib/types-extensions.test.ts
git commit -m "feat: extend SoldHomeItem and ServiceItem with optional fields for new templates"
```

---

## Chunk 2: Template 4 — Luxury Estate

### Template Recipe (applies to ALL templates in Chunks 2-5)

Each template follows this exact sequence. The per-template specifics (variant names, styles, test agent data) are listed below.

**For each template:**
1. Create 7 section variants (one file each)
2. Add barrel exports (section folder index + main sections index)
3. Create template composition file
4. Register in template index
5. Create test agent (config.json + content.json + legal/)
6. Download placeholder images
7. Write 7 test files
8. Regenerate config registry
9. Run tests

---

### Task 2: Create HeroDark section variant

**Files:**
- Create: `apps/agent-site/components/sections/heroes/HeroDark.tsx`
- Test: `apps/agent-site/__tests__/components/heroes/HeroDark.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/agent-site/__tests__/components/heroes/HeroDark.test.tsx`:

```typescript
// @vitest-environment jsdom
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { HeroDark } from "@/components/sections/heroes/HeroDark";

const heroData = {
  headline: "Exceptional Homes for Exceptional Lives",
  highlight_word: "Exceptional Lives",
  tagline: "Curating premier properties",
  body: "Since 2008, we have served Manhattan's most prestigious addresses.",
  cta_text: "View Portfolio",
  cta_link: "#sold",
};

describe("HeroDark", () => {
  it("renders headline", () => {
    render(<HeroDark data={heroData} />);
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Exceptional Homes for Exceptional Lives");
  });

  it("renders tagline", () => {
    render(<HeroDark data={heroData} />);
    expect(screen.getByText("Curating premier properties")).toBeInTheDocument();
  });

  it("renders body text when provided", () => {
    render(<HeroDark data={heroData} />);
    expect(screen.getByText(/Since 2008/)).toBeInTheDocument();
  });

  it("renders CTA link", () => {
    render(<HeroDark data={heroData} />);
    const cta = screen.getByText("View Portfolio");
    expect(cta).toBeInTheDocument();
    expect(cta.closest("a")).toHaveAttribute("href", "#sold");
  });

  it("highlights accent word in headline", () => {
    render(<HeroDark data={heroData} />);
    const heading = screen.getByRole("heading", { level: 1 });
    const accentSpan = heading.querySelector("span");
    expect(accentSpan).toHaveTextContent("Exceptional Lives");
    expect(accentSpan?.style.color).toBe("var(--color-accent)");
  });

  it("renders agent photo when provided", () => {
    render(<HeroDark data={heroData} agentPhotoUrl="/test.jpg" agentName="Victoria" />);
    expect(screen.getByAltText("Photo of Victoria")).toBeInTheDocument();
  });

  it("omits agent photo when not provided", () => {
    render(<HeroDark data={heroData} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("uses dark background styling", () => {
    const { container } = render(<HeroDark data={heroData} />);
    const section = container.querySelector("section");
    expect(section?.style.background).toContain("#0a0a0a");
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/heroes/HeroDark.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement HeroDark**

Create `apps/agent-site/components/sections/heroes/HeroDark.tsx`:

```typescript
"use client";

import { useState } from "react";
import Image from "next/image";
import type { HeroProps } from "@/components/sections/types";
import { safeHref, renderHeadline } from "./hero-utils";

export function HeroDark({ data, agentPhotoUrl, agentName }: HeroProps) {
  const [ctaHover, setCtaHover] = useState(false);

  return (
    <section
      style={{
        background: "linear-gradient(135deg, #0a0a0a 0%, var(--color-primary, #1a1a2e) 50%, var(--color-secondary, #16213e) 100%)",
        color: "white",
        padding: "120px 60px 100px",
        position: "relative",
        minHeight: "500px",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        gap: "60px",
        flexWrap: "wrap",
      }}
    >
      {/* Gold accent line */}
      <div style={{
        position: "absolute",
        top: 0,
        left: 0,
        right: 0,
        height: "3px",
        background: `linear-gradient(90deg, transparent, var(--color-accent, #d4af37), transparent)`,
      }} />

      <div style={{ maxWidth: "500px", flex: 1 }}>
        <div style={{
          fontSize: "11px",
          letterSpacing: "4px",
          textTransform: "uppercase",
          color: "var(--color-accent, #d4af37)",
          marginBottom: "12px",
        }}>
          {data.tagline}
        </div>
        <h1 style={{
          fontSize: "42px",
          fontWeight: 300,
          lineHeight: 1.15,
          fontFamily: "var(--font-family, Georgia), serif",
          marginBottom: "16px",
        }}>
          {renderHeadline(data.headline, data.highlight_word)}
        </h1>
        {data.body && (
          <p style={{
            fontSize: "15px",
            color: "rgba(255,255,255,0.6)",
            lineHeight: 1.7,
            marginBottom: "28px",
          }}>
            {data.body}
          </p>
        )}
        <a
          href={safeHref(data.cta_link)}
          onMouseEnter={() => setCtaHover(true)}
          onMouseLeave={() => setCtaHover(false)}
          onFocus={() => setCtaHover(true)}
          onBlur={() => setCtaHover(false)}
          style={{
            display: "inline-block",
            border: `1px solid var(--color-accent, #d4af37)`,
            color: ctaHover ? "white" : "var(--color-accent, #d4af37)",
            background: ctaHover ? "var(--color-accent, #d4af37)" : "transparent",
            padding: "12px 32px",
            fontSize: "11px",
            letterSpacing: "2px",
            textTransform: "uppercase",
            textDecoration: "none",
            transition: "all 0.3s",
          }}
        >
          {data.cta_text}
        </a>
      </div>

      {agentPhotoUrl && (
        <div style={{
          width: "280px",
          height: "340px",
          borderRadius: "4px",
          overflow: "hidden",
          border: `2px solid var(--color-accent, #d4af37)`,
          flexShrink: 0,
        }}>
          <Image
            src={agentPhotoUrl}
            alt={agentName ? `Photo of ${agentName}` : "Agent photo"}
            width={280}
            height={340}
            style={{ width: "100%", height: "100%", objectFit: "cover" }}
            priority
          />
        </div>
      )}
    </section>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/agent-site && npx vitest run __tests__/components/heroes/HeroDark.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/sections/heroes/HeroDark.tsx apps/agent-site/__tests__/components/heroes/HeroDark.test.tsx
git commit -m "feat: add HeroDark section variant for luxury-estate template"
```

### Task 3: Create StatsOverlay section variant

**Files:**
- Create: `apps/agent-site/components/sections/stats/StatsOverlay.tsx`
- Test: `apps/agent-site/__tests__/components/stats/StatsOverlay.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/agent-site/__tests__/components/stats/StatsOverlay.test.tsx`:

```typescript
// @vitest-environment jsdom
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { StatsOverlay } from "@/components/sections/stats/StatsOverlay";

const ITEMS = [
  { value: "$2.1B", label: "Total Volume" },
  { value: "340+", label: "Properties Sold" },
  { value: "#1", label: "NYC Luxury Agent" },
];

describe("StatsOverlay", () => {
  it("renders all stat items", () => {
    render(<StatsOverlay items={ITEMS} />);
    expect(screen.getByText("$2.1B")).toBeInTheDocument();
    expect(screen.getByText("340+")).toBeInTheDocument();
    expect(screen.getByText("#1")).toBeInTheDocument();
  });

  it("renders stat labels", () => {
    render(<StatsOverlay items={ITEMS} />);
    expect(screen.getByText("Total Volume")).toBeInTheDocument();
    expect(screen.getByText("NYC Luxury Agent")).toBeInTheDocument();
  });

  it("uses correct section id for anchor linking", () => {
    const { container } = render(<StatsOverlay items={ITEMS} />);
    expect(container.querySelector("#stats")).toBeInTheDocument();
  });

  it("uses accent color for values", () => {
    render(<StatsOverlay items={ITEMS} />);
    const value = screen.getByText("$2.1B");
    expect(value.style.color).toBe("var(--color-accent, #d4af37)");
  });

  it("renders dark background", () => {
    const { container } = render(<StatsOverlay items={ITEMS} />);
    const section = container.querySelector("section");
    expect(section?.style.background).toContain("rgba(0,0,0");
  });

  it("renders source disclaimer when provided", () => {
    render(<StatsOverlay items={ITEMS} sourceDisclaimer="Based on data from MLS." />);
    expect(screen.getByText("Based on data from MLS.")).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/stats/StatsOverlay.test.tsx`
Expected: FAIL

- [ ] **Step 3: Implement StatsOverlay**

Create `apps/agent-site/components/sections/stats/StatsOverlay.tsx`:

```typescript
import type { StatsProps } from "@/components/sections/types";

export function StatsOverlay({ items, sourceDisclaimer }: StatsProps) {
  return (
    <section
      id="stats"
      style={{
        background: "rgba(0,0,0,0.85)",
        backdropFilter: "blur(4px)",
        padding: "32px 40px",
        display: "flex",
        justifyContent: "center",
        alignItems: "center",
        gap: "48px",
        flexWrap: "wrap",
        color: "white",
      }}
    >
      {items.map((item) => (
        <div key={item.label} style={{ textAlign: "center" }}>
          <div style={{
            color: "var(--color-accent, #d4af37)",
            fontSize: "24px",
            fontWeight: 600,
            fontFamily: "var(--font-family, Georgia), serif",
          }}>
            {item.value}
          </div>
          <div style={{
            fontSize: "10px",
            letterSpacing: "2px",
            textTransform: "uppercase",
            opacity: 0.5,
            marginTop: "4px",
          }}>
            {item.label}
          </div>
        </div>
      ))}
      {sourceDisclaimer && (
        <p style={{ fontSize: "10px", opacity: 0.3, width: "100%", textAlign: "center", marginTop: "8px" }}>
          {sourceDisclaimer}
        </p>
      )}
    </section>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/agent-site && npx vitest run __tests__/components/stats/StatsOverlay.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/sections/stats/StatsOverlay.tsx apps/agent-site/__tests__/components/stats/StatsOverlay.test.tsx
git commit -m "feat: add StatsOverlay section variant for luxury-estate template"
```

### Task 4: Create ServicesElegant section variant

**Files:**
- Create: `apps/agent-site/components/sections/services/ServicesElegant.tsx`
- Test: `apps/agent-site/__tests__/components/services/ServicesElegant.test.tsx`

- [ ] **Step 1: Write the failing test**

```typescript
// @vitest-environment jsdom
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ServicesElegant } from "@/components/sections/services/ServicesElegant";

const ITEMS = [
  { title: "Private Portfolio Access", description: "Exclusive off-market listings" },
  { title: "White-Glove Service", description: "End-to-end concierge experience" },
  { title: "Investment Advisory", description: "Strategic portfolio guidance" },
];

describe("ServicesElegant", () => {
  it("renders default heading", () => {
    render(<ServicesElegant items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("Our Services");
  });

  it("renders custom title", () => {
    render(<ServicesElegant items={ITEMS} title="Exclusive Services" />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("Exclusive Services");
  });

  it("renders subtitle when provided", () => {
    render(<ServicesElegant items={ITEMS} subtitle="Tailored to your needs" />);
    expect(screen.getByText("Tailored to your needs")).toBeInTheDocument();
  });

  it("renders all service items", () => {
    render(<ServicesElegant items={ITEMS} />);
    expect(screen.getByText("Private Portfolio Access")).toBeInTheDocument();
    expect(screen.getByText("White-Glove Service")).toBeInTheDocument();
    expect(screen.getByText("Investment Advisory")).toBeInTheDocument();
  });

  it("renders descriptions", () => {
    render(<ServicesElegant items={ITEMS} />);
    expect(screen.getByText("Exclusive off-market listings")).toBeInTheDocument();
  });

  it("uses correct section id", () => {
    const { container } = render(<ServicesElegant items={ITEMS} />);
    expect(container.querySelector("#services")).toBeInTheDocument();
  });

  it("uses semantic HTML articles for each service", () => {
    const { container } = render(<ServicesElegant items={ITEMS} />);
    expect(container.querySelectorAll("article").length).toBe(3);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**
- [ ] **Step 3: Implement ServicesElegant**

Create `apps/agent-site/components/sections/services/ServicesElegant.tsx`:

```typescript
import type { ServicesProps } from "@/components/sections/types";

export function ServicesElegant({ items, title, subtitle }: ServicesProps) {
  return (
    <section
      id="services"
      style={{
        padding: "80px 40px",
        background: "var(--color-primary, #0a0a0a)",
        color: "white",
      }}
    >
      <div style={{ maxWidth: "900px", margin: "0 auto" }}>
        <h2 style={{
          textAlign: "center",
          fontSize: "32px",
          fontWeight: 300,
          fontFamily: "var(--font-family, Georgia), serif",
          marginBottom: subtitle ? "8px" : "48px",
        }}>
          {title ?? "Our Services"}
        </h2>
        {subtitle && (
          <p style={{
            textAlign: "center",
            color: "rgba(255,255,255,0.5)",
            fontSize: "14px",
            marginBottom: "48px",
          }}>
            {subtitle}
          </p>
        )}
        <div style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
          {items.map((item) => (
            <article
              key={item.title}
              style={{
                padding: "24px 28px",
                borderLeft: "2px solid var(--color-accent, #d4af37)",
                background: "rgba(255,255,255,0.03)",
                border: "1px solid rgba(255,255,255,0.08)",
                borderLeftWidth: "2px",
                borderLeftColor: "var(--color-accent, #d4af37)",
              }}
            >
              <h3 style={{
                fontSize: "18px",
                fontWeight: 400,
                fontFamily: "var(--font-family, Georgia), serif",
                marginBottom: "6px",
              }}>
                {item.title}
              </h3>
              <p style={{
                fontSize: "14px",
                color: "rgba(255,255,255,0.5)",
                lineHeight: 1.6,
              }}>
                {item.description}
              </p>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**
- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/sections/services/ServicesElegant.tsx apps/agent-site/__tests__/components/services/ServicesElegant.test.tsx
git commit -m "feat: add ServicesElegant section variant for luxury-estate template"
```

### Task 5: Create StepsElegant section variant

**Files:**
- Create: `apps/agent-site/components/sections/steps/StepsElegant.tsx`
- Test: `apps/agent-site/__tests__/components/steps/StepsElegant.test.tsx`

- [ ] **Step 1: Write the failing test**

```typescript
// @vitest-environment jsdom
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { StepsElegant } from "@/components/sections/steps/StepsElegant";

const STEPS = [
  { number: 1, title: "Schedule Consultation", description: "Meet to discuss your goals" },
  { number: 2, title: "Market Analysis", description: "Comprehensive property valuation" },
  { number: 3, title: "List & Sell", description: "Strategic marketing and negotiation" },
];

describe("StepsElegant", () => {
  it("renders default heading", () => {
    render(<StepsElegant steps={STEPS} />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("How It Works");
  });

  it("renders custom title", () => {
    render(<StepsElegant steps={STEPS} title="The Process" />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("The Process");
  });

  it("renders all step titles", () => {
    render(<StepsElegant steps={STEPS} />);
    expect(screen.getByText("Schedule Consultation")).toBeInTheDocument();
    expect(screen.getByText("Market Analysis")).toBeInTheDocument();
    expect(screen.getByText("List & Sell")).toBeInTheDocument();
  });

  it("renders step numbers", () => {
    render(<StepsElegant steps={STEPS} />);
    expect(screen.getByText("1")).toBeInTheDocument();
    expect(screen.getByText("2")).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
  });

  it("uses correct section id", () => {
    const { container } = render(<StepsElegant steps={STEPS} />);
    expect(container.querySelector("#how-it-works")).toBeInTheDocument();
  });

  it("renders subtitle when provided", () => {
    render(<StepsElegant steps={STEPS} subtitle="Simple and refined" />);
    expect(screen.getByText("Simple and refined")).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**
- [ ] **Step 3: Implement StepsElegant**

Create `apps/agent-site/components/sections/steps/StepsElegant.tsx`:

```typescript
import type { StepsProps } from "@/components/sections/types";

export function StepsElegant({ steps, title, subtitle }: StepsProps) {
  return (
    <section
      id="how-it-works"
      style={{
        padding: "80px 40px",
        background: "var(--color-primary, #0a0a0a)",
        color: "white",
      }}
    >
      <div style={{ maxWidth: "900px", margin: "0 auto" }}>
        <h2 style={{
          textAlign: "center",
          fontSize: "32px",
          fontWeight: 300,
          fontFamily: "var(--font-family, Georgia), serif",
          marginBottom: subtitle ? "8px" : "48px",
        }}>
          {title ?? "How It Works"}
        </h2>
        {subtitle && (
          <p style={{ textAlign: "center", color: "rgba(255,255,255,0.5)", fontSize: "14px", marginBottom: "48px" }}>
            {subtitle}
          </p>
        )}
        <div style={{ display: "flex", justifyContent: "center", gap: "40px", flexWrap: "wrap", position: "relative" }}>
          {steps.map((step, i) => (
            <div key={step.number} style={{ textAlign: "center", flex: "1 1 200px", maxWidth: "260px", position: "relative" }}>
              {/* Connecting line */}
              {i < steps.length - 1 && (
                <div style={{
                  position: "absolute",
                  top: "20px",
                  left: "60%",
                  width: "80%",
                  height: "1px",
                  background: "var(--color-accent, #d4af37)",
                  opacity: 0.3,
                }} />
              )}
              <div style={{
                width: "40px",
                height: "40px",
                borderRadius: "50%",
                border: "2px solid var(--color-accent, #d4af37)",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                margin: "0 auto 16px",
                fontSize: "16px",
                fontWeight: 600,
                color: "var(--color-accent, #d4af37)",
                position: "relative",
                zIndex: 1,
                background: "var(--color-primary, #0a0a0a)",
              }}>
                {step.number}
              </div>
              <h3 style={{ fontSize: "16px", fontWeight: 400, fontFamily: "var(--font-family, Georgia), serif", marginBottom: "8px" }}>
                {step.title}
              </h3>
              <p style={{ fontSize: "13px", color: "rgba(255,255,255,0.5)", lineHeight: 1.6 }}>
                {step.description}
              </p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**
- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/sections/steps/StepsElegant.tsx apps/agent-site/__tests__/components/steps/StepsElegant.test.tsx
git commit -m "feat: add StepsElegant section variant for luxury-estate template"
```

### Task 6: Create SoldCarousel section variant (NEW COMPONENT)

**Files:**
- Create: `apps/agent-site/components/sections/sold/SoldCarousel.tsx`
- Test: `apps/agent-site/__tests__/components/sold/SoldCarousel.test.tsx`

- [ ] **Step 1: Write the failing test**

```typescript
// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { SoldCarousel } from "@/components/sections/sold/SoldCarousel";

const ITEMS = [
  { address: "432 Park Ave", city: "Manhattan", state: "NY", price: "$12,500,000", image_url: "/sold/park-ave.jpg" },
  { address: "15 Central Park W", city: "Manhattan", state: "NY", price: "$8,200,000", image_url: "/sold/cpw.jpg" },
  { address: "56 Leonard St", city: "Manhattan", state: "NY", price: "$4,500,000", image_url: "/sold/leonard.jpg" },
];

describe("SoldCarousel", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("renders default heading", () => {
    render(<SoldCarousel items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("Portfolio");
  });

  it("renders custom title", () => {
    render(<SoldCarousel items={ITEMS} title="Recent Sales" />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("Recent Sales");
  });

  it("renders all property prices", () => {
    render(<SoldCarousel items={ITEMS} />);
    expect(screen.getByText("$12,500,000")).toBeInTheDocument();
  });

  it("renders property addresses", () => {
    render(<SoldCarousel items={ITEMS} />);
    expect(screen.getByText(/432 Park Ave/)).toBeInTheDocument();
  });

  it("has carousel region with aria-roledescription", () => {
    const { container } = render(<SoldCarousel items={ITEMS} />);
    const region = container.querySelector('[role="region"]');
    expect(region).toBeInTheDocument();
    expect(region?.getAttribute("aria-roledescription")).toBe("carousel");
  });

  it("renders prev/next buttons with aria-labels", () => {
    render(<SoldCarousel items={ITEMS} />);
    expect(screen.getByLabelText("Previous property")).toBeInTheDocument();
    expect(screen.getByLabelText("Next property")).toBeInTheDocument();
  });

  it("renders dot indicators", () => {
    const { container } = render(<SoldCarousel items={ITEMS} />);
    const dots = container.querySelectorAll('[role="tab"]');
    expect(dots.length).toBe(3);
  });

  it("navigates to next slide on button click", () => {
    const { container } = render(<SoldCarousel items={ITEMS} />);
    const nextBtn = screen.getByLabelText("Next property");
    fireEvent.click(nextBtn);
    const tabs = container.querySelectorAll('[role="tab"]');
    expect(tabs[1]?.getAttribute("aria-selected")).toBe("true");
  });

  it("falls back to vertical stack on prefers-reduced-motion", () => {
    // Mock matchMedia to return reduced motion
    Object.defineProperty(window, "matchMedia", {
      writable: true,
      value: vi.fn().mockImplementation((query: string) => ({
        matches: query === "(prefers-reduced-motion: reduce)",
        media: query,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
      })),
    });
    const { container } = render(<SoldCarousel items={ITEMS} />);
    const slideContainer = container.querySelector('[aria-live="polite"]');
    expect(slideContainer?.style.flexDirection).toBe("column");
  });

  it("supports keyboard arrow navigation", () => {
    const { container } = render(<SoldCarousel items={ITEMS} />);
    const region = container.querySelector('[role="region"]')!;
    fireEvent.keyDown(region, { key: "ArrowRight" });
    const tabs = container.querySelectorAll('[role="tab"]');
    expect(tabs[1]?.getAttribute("aria-selected")).toBe("true");
    fireEvent.keyDown(region, { key: "ArrowLeft" });
    expect(tabs[0]?.getAttribute("aria-selected")).toBe("true");
  });

  it("uses correct section id", () => {
    const { container } = render(<SoldCarousel items={ITEMS} />);
    expect(container.querySelector("#sold")).toBeInTheDocument();
  });

  it("renders subtitle when provided", () => {
    render(<SoldCarousel items={ITEMS} subtitle="Trophy properties" />);
    expect(screen.getByText("Trophy properties")).toBeInTheDocument();
  });

  it("renders images for properties", () => {
    render(<SoldCarousel items={ITEMS} />);
    const images = screen.getAllByRole("img");
    expect(images.length).toBeGreaterThanOrEqual(1);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**
- [ ] **Step 3: Implement SoldCarousel**

Create `apps/agent-site/components/sections/sold/SoldCarousel.tsx`:

```typescript
"use client";

import { useState, useEffect, useCallback, useRef } from "react";
import Image from "next/image";
import type { SoldHomesProps } from "@/components/sections/types";

export function SoldCarousel({ items, title, subtitle }: SoldHomesProps) {
  const [current, setCurrent] = useState(0);
  const [paused, setPaused] = useState(false);
  const [reducedMotion, setReducedMotion] = useState(false);
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const mq = window.matchMedia("(prefers-reduced-motion: reduce)");
    setReducedMotion(mq.matches);
    const handler = (e: MediaQueryListEvent) => setReducedMotion(e.matches);
    mq.addEventListener("change", handler);
    return () => mq.removeEventListener("change", handler);
  }, []);

  const goTo = useCallback((index: number) => {
    setCurrent(index);
    if (scrollRef.current && !reducedMotion) {
      const slideWidth = scrollRef.current.offsetWidth;
      scrollRef.current.scrollTo({ left: slideWidth * index, behavior: "smooth" });
    }
  }, [reducedMotion]);

  const next = useCallback(() => goTo((current + 1) % items.length), [current, items.length, goTo]);
  const prev = useCallback(() => goTo((current - 1 + items.length) % items.length), [current, items.length, goTo]);

  // Auto-advance every 5s (unless paused or reduced-motion)
  useEffect(() => {
    if (paused || reducedMotion || items.length <= 1) return;
    const timer = setInterval(next, 5000);
    return () => clearInterval(timer);
  }, [paused, reducedMotion, next, items.length]);

  // Keyboard arrow navigation
  const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.key === "ArrowRight") { next(); e.preventDefault(); }
    if (e.key === "ArrowLeft") { prev(); e.preventDefault(); }
  }, [next, prev]);

  if (items.length === 0) return null;

  // Reduced motion: render vertical stack instead of carousel
  if (reducedMotion) {
    return (
      <section id="sold" style={{ padding: "80px 0", background: "var(--color-primary, #0a0a0a)", color: "white" }}>
        <div style={{ maxWidth: "1200px", margin: "0 auto", padding: "0 40px" }}>
          <h2 style={{ textAlign: "center", fontSize: "32px", fontWeight: 300, fontFamily: "var(--font-family, Georgia), serif", marginBottom: subtitle ? "8px" : "40px" }}>
            {title ?? "Portfolio"}
          </h2>
          {subtitle && <p style={{ textAlign: "center", color: "rgba(255,255,255,0.5)", fontSize: "14px", marginBottom: "40px" }}>{subtitle}</p>}
          <div role="region" aria-label={title ?? "Sold properties"} aria-live="polite" style={{ display: "flex", flexDirection: "column", gap: "24px" }}>
            {items.map((item) => (
              <div key={item.address} style={{ position: "relative" }}>
                {item.image_url ? (
                  <Image src={item.image_url} alt={`${item.address}, ${item.city}, ${item.state}`} width={1200} height={500} style={{ width: "100%", height: "400px", objectFit: "cover", display: "block" }} />
                ) : (
                  <div style={{ width: "100%", height: "400px", background: "rgba(255,255,255,0.05)" }} />
                )}
                <div style={{ padding: "16px 0" }}>
                  <div style={{ fontSize: "28px", fontWeight: 300, fontFamily: "var(--font-family, Georgia), serif", color: "var(--color-accent, #d4af37)" }}>{item.price}</div>
                  <div style={{ fontSize: "14px", letterSpacing: "1px", textTransform: "uppercase", opacity: 0.7 }}>{item.address}, {item.city}, {item.state}</div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>
    );
  }

  return (
    <section id="sold" style={{ padding: "80px 0", background: "var(--color-primary, #0a0a0a)", color: "white" }}>
      <div style={{ maxWidth: "1200px", margin: "0 auto", padding: "0 40px" }}>
        <h2 style={{
          textAlign: "center",
          fontSize: "32px",
          fontWeight: 300,
          fontFamily: "var(--font-family, Georgia), serif",
          marginBottom: subtitle ? "8px" : "40px",
        }}>
          {title ?? "Portfolio"}
        </h2>
        {subtitle && (
          <p style={{ textAlign: "center", color: "rgba(255,255,255,0.5)", fontSize: "14px", marginBottom: "40px" }}>
            {subtitle}
          </p>
        )}
      </div>

      <div
        role="region"
        aria-roledescription="carousel"
        aria-label={title ?? "Sold properties"}
        tabIndex={0}
        onKeyDown={handleKeyDown}
        onMouseEnter={() => setPaused(true)}
        onMouseLeave={() => setPaused(false)}
        onFocus={() => setPaused(true)}
        onBlur={() => setPaused(false)}
        style={{ position: "relative", maxWidth: "1200px", margin: "0 auto", overflow: "hidden", outline: "none" }}
      >
        {/* Slides — CSS scroll-snap for native touch/swipe */}
        <div
          ref={scrollRef}
          aria-live="polite"
          style={{
            display: "flex",
            overflowX: "scroll",
            scrollSnapType: "x mandatory",
            scrollBehavior: "smooth",
            msOverflowStyle: "none",
            scrollbarWidth: "none",
          }}
        >
          {items.map((item, i) => (
            <div
              key={item.address}
              role="group"
              aria-roledescription="slide"
              aria-label={`${i + 1} of ${items.length}: ${item.address}`}
              style={{ minWidth: "100%", position: "relative", scrollSnapAlign: "start" }}
            >
              {item.image_url ? (
                <Image
                  src={item.image_url}
                  alt={`${item.address}, ${item.city}, ${item.state}`}
                  width={1200}
                  height={500}
                  style={{ width: "100%", height: "500px", objectFit: "cover", display: "block" }}
                />
              ) : (
                <div style={{ width: "100%", height: "500px", background: "rgba(255,255,255,0.05)" }} />
              )}
              {/* Overlay */}
              <div style={{
                position: "absolute",
                bottom: 0,
                left: 0,
                right: 0,
                padding: "40px",
                background: "linear-gradient(transparent, rgba(0,0,0,0.8))",
              }}>
                <div style={{
                  fontSize: "36px",
                  fontWeight: 300,
                  fontFamily: "var(--font-family, Georgia), serif",
                  color: "var(--color-accent, #d4af37)",
                }}>
                  {item.price}
                </div>
                <div style={{ fontSize: "14px", letterSpacing: "1px", textTransform: "uppercase", opacity: 0.7, marginTop: "4px" }}>
                  {item.address}, {item.city}, {item.state}
                </div>
              </div>
            </div>
          ))}
        </div>

        {/* Prev/Next buttons */}
        {items.length > 1 && (
          <>
            <button
              onClick={prev}
              aria-label="Previous property"
              style={{
                position: "absolute",
                top: "50%",
                left: "16px",
                transform: "translateY(-50%)",
                background: "rgba(0,0,0,0.5)",
                color: "var(--color-accent, #d4af37)",
                border: "none",
                width: "44px",
                height: "44px",
                borderRadius: "50%",
                cursor: "pointer",
                fontSize: "20px",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
              }}
            >
              ‹
            </button>
            <button
              onClick={next}
              aria-label="Next property"
              style={{
                position: "absolute",
                top: "50%",
                right: "16px",
                transform: "translateY(-50%)",
                background: "rgba(0,0,0,0.5)",
                color: "var(--color-accent, #d4af37)",
                border: "none",
                width: "44px",
                height: "44px",
                borderRadius: "50%",
                cursor: "pointer",
                fontSize: "20px",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
              }}
            >
              ›
            </button>
          </>
        )}

        {/* Dot indicators */}
        {items.length > 1 && (
          <div role="tablist" aria-label="Slide indicators" style={{ display: "flex", justifyContent: "center", gap: "8px", marginTop: "16px" }}>
            {items.map((_, i) => (
              <button
                key={i}
                role="tab"
                aria-selected={i === current}
                aria-label={`Go to slide ${i + 1}`}
                onClick={() => setCurrent(i)}
                style={{
                  width: "10px",
                  height: "10px",
                  borderRadius: "50%",
                  border: "none",
                  background: i === current ? "var(--color-accent, #d4af37)" : "rgba(255,255,255,0.3)",
                  cursor: "pointer",
                  padding: 0,
                }}
              />
            ))}
          </div>
        )}
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**
- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/sections/sold/SoldCarousel.tsx apps/agent-site/__tests__/components/sold/SoldCarousel.test.tsx
git commit -m "feat: add SoldCarousel component with auto-advance and a11y for luxury-estate"
```

### Task 7: Create TestimonialsMinimal section variant

**Files:**
- Create: `apps/agent-site/components/sections/testimonials/TestimonialsMinimal.tsx`
- Test: `apps/agent-site/__tests__/components/testimonials/TestimonialsMinimal.test.tsx`

- [ ] **Step 1: Write the failing test**

```typescript
// @vitest-environment jsdom
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { TestimonialsMinimal } from "@/components/sections/testimonials/TestimonialsMinimal";

const ITEMS = [
  { text: "Victoria made the impossible possible.", reviewer: "The Hendersons", rating: 5, source: "Zillow" },
  { text: "Truly exceptional service.", reviewer: "M. Wang", rating: 5 },
];

describe("TestimonialsMinimal", () => {
  it("renders default heading", () => {
    render(<TestimonialsMinimal items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("Client Testimonials");
  });

  it("renders custom title", () => {
    render(<TestimonialsMinimal items={ITEMS} title="What Clients Say" />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("What Clients Say");
  });

  it("renders testimonial text", () => {
    render(<TestimonialsMinimal items={ITEMS} />);
    expect(screen.getByText(/Victoria made the impossible possible/)).toBeInTheDocument();
  });

  it("renders reviewer name", () => {
    render(<TestimonialsMinimal items={ITEMS} />);
    expect(screen.getByText(/The Hendersons/)).toBeInTheDocument();
  });

  it("uses correct section id", () => {
    const { container } = render(<TestimonialsMinimal items={ITEMS} />);
    expect(container.querySelector("#testimonials")).toBeInTheDocument();
  });

  it("includes FTC disclaimer", () => {
    render(<TestimonialsMinimal items={ITEMS} />);
    expect(screen.getByText(/Real reviews from real clients/)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2-5: Implement, test, commit** (same TDD flow)

Implement as centered single-testimonial display with serif italic quote, accent star rating, small caps reviewer name. Use `clampRating()` and `FTC_DISCLAIMER` from `types.ts`.

```bash
git commit -m "feat: add TestimonialsMinimal section variant for luxury-estate template"
```

### Task 8: Create AboutEditorial section variant

**Files:**
- Create: `apps/agent-site/components/sections/about/AboutEditorial.tsx`
- Test: `apps/agent-site/__tests__/components/about/AboutEditorial.test.tsx`

- [ ] **Step 1: Write the failing test**

```typescript
// @vitest-environment jsdom
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { AboutEditorial } from "@/components/sections/about/AboutEditorial";

const agent = {
  id: "test-luxury",
  identity: { name: "Victoria Sterling", title: "Licensed Associate RE Broker", phone: "(212) 555-0199", email: "v@test.com", headshot_url: "/agents/test-luxury/headshot.jpg" },
  location: { state: "NY" },
  branding: {},
};

const data = {
  title: "About Victoria",
  bio: ["Paragraph one.", "Paragraph two."],
  credentials: ["Top 1% NYC Agents", "$2.1B Career Volume"],
};

describe("AboutEditorial", () => {
  it("renders heading", () => {
    render(<AboutEditorial agent={agent} data={data} />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("About Victoria");
  });

  it("renders bio paragraphs", () => {
    render(<AboutEditorial agent={agent} data={data} />);
    expect(screen.getByText("Paragraph one.")).toBeInTheDocument();
    expect(screen.getByText("Paragraph two.")).toBeInTheDocument();
  });

  it("renders credentials", () => {
    render(<AboutEditorial agent={agent} data={data} />);
    expect(screen.getByText("Top 1% NYC Agents")).toBeInTheDocument();
    expect(screen.getByText("$2.1B Career Volume")).toBeInTheDocument();
  });

  it("renders agent photo", () => {
    render(<AboutEditorial agent={agent} data={data} />);
    expect(screen.getByAltText(/Victoria Sterling/)).toBeInTheDocument();
  });

  it("uses correct section id", () => {
    const { container } = render(<AboutEditorial agent={agent} data={data} />);
    expect(container.querySelector("#about")).toBeInTheDocument();
  });

  it("renders default title when not provided", () => {
    render(<AboutEditorial agent={agent} data={{ bio: "Bio text" }} />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("About");
  });
});
```

- [ ] **Step 2-5: Implement, test, commit**

Dark section, large circular photo with accent border, serif bio, credentials as accent-bordered pills.

```bash
git commit -m "feat: add AboutEditorial section variant for luxury-estate template"
```

### Task 9: Register all Luxury Estate variants in barrel exports

**Files:**
- Modify: `apps/agent-site/components/sections/heroes/index.ts`
- Modify: `apps/agent-site/components/sections/stats/index.ts`
- Modify: `apps/agent-site/components/sections/services/index.ts`
- Modify: `apps/agent-site/components/sections/steps/index.ts`
- Modify: `apps/agent-site/components/sections/sold/index.ts`
- Modify: `apps/agent-site/components/sections/testimonials/index.ts`
- Modify: `apps/agent-site/components/sections/about/index.ts`
- Modify: `apps/agent-site/components/sections/index.ts`

- [ ] **Step 1: Add exports to each section folder index**

Add to `heroes/index.ts`: `export { HeroDark } from "./HeroDark";`
Add to `stats/index.ts`: `export { StatsOverlay } from "./StatsOverlay";`
Add to `services/index.ts`: `export { ServicesElegant } from "./ServicesElegant";`
Add to `steps/index.ts`: `export { StepsElegant } from "./StepsElegant";`
Add to `sold/index.ts`: `export { SoldCarousel } from "./SoldCarousel";`
Add to `testimonials/index.ts`: `export { TestimonialsMinimal } from "./TestimonialsMinimal";`
Add to `about/index.ts`: `export { AboutEditorial } from "./AboutEditorial";`

- [ ] **Step 2: Add re-exports to main sections index**

Add to `apps/agent-site/components/sections/index.ts`:

```typescript
// Luxury Estate variants
export { HeroDark } from "./heroes/HeroDark";
export { StatsOverlay } from "./stats/StatsOverlay";
export { ServicesElegant } from "./services/ServicesElegant";
export { StepsElegant } from "./steps/StepsElegant";
export { SoldCarousel } from "./sold/SoldCarousel";
export { TestimonialsMinimal } from "./testimonials/TestimonialsMinimal";
export { AboutEditorial } from "./about/AboutEditorial";
```

- [ ] **Step 3: Commit**

```bash
git add apps/agent-site/components/sections/*/index.ts apps/agent-site/components/sections/index.ts
git commit -m "feat: register Luxury Estate section variants in barrel exports"
```

### Task 10: Create luxury-estate template composition

**Files:**
- Create: `apps/agent-site/templates/luxury-estate.tsx`
- Modify: `apps/agent-site/templates/index.ts`

- [ ] **Step 1: Create template file**

Create `apps/agent-site/templates/luxury-estate.tsx`:

```typescript
import { Nav } from "@/components/Nav";
import {
  HeroDark,
  StatsOverlay,
  ServicesElegant,
  StepsElegant,
  SoldCarousel,
  TestimonialsMinimal,
  CmaSection,
  AboutEditorial,
  Footer,
} from "@/components/sections";
import type { TemplateProps } from "./types";

export function LuxuryEstate({ agent, content }: TemplateProps) {
  const s = content.sections;
  return (
    <>
      <Nav agent={agent} navigation={content.navigation} contactInfo={content.contact_info} />
      <div style={{ paddingTop: "0" }}>
      {s.hero.enabled && (
        <HeroDark
          data={s.hero.data}
          agentPhotoUrl={agent.identity.headshot_url}
          agentName={agent.identity.name}
        />
      )}
      {s.stats.enabled && s.stats.data.items.length > 0 && (
        <StatsOverlay items={s.stats.data.items} sourceDisclaimer="Based on data from MLS. Individual results may vary." />
      )}
      {s.services.enabled && (
        <ServicesElegant
          items={s.services.data.items}
          title={s.services.data.title}
          subtitle={s.services.data.subtitle}
        />
      )}
      {s.how_it_works.enabled && (
        <StepsElegant
          steps={s.how_it_works.data.steps}
          title={s.how_it_works.data.title}
          subtitle={s.how_it_works.data.subtitle}
        />
      )}
      {s.sold_homes.enabled && s.sold_homes.data.items.length > 0 && (
        <SoldCarousel
          items={s.sold_homes.data.items}
          title={s.sold_homes.data.title}
          subtitle={s.sold_homes.data.subtitle}
        />
      )}
      {s.testimonials.enabled && s.testimonials.data.items.length > 0 && (
        <TestimonialsMinimal
          items={s.testimonials.data.items}
          title={s.testimonials.data.title}
        />
      )}
      {s.cma_form.enabled && (
        <CmaSection
          agentId={agent.id}
          agentName={agent.identity.name}
          defaultState={agent.location.state}
          tracking={agent.integrations?.tracking}
          data={s.cma_form.data}
          serviceAreas={agent.location.service_areas}
        />
      )}
      {s.about.enabled && <AboutEditorial agent={agent} data={s.about.data} />}
      <Footer agent={agent} agentId={agent.id} />
      </div>
    </>
  );
}
```

- [ ] **Step 2: Register in template index**

Add to `apps/agent-site/templates/index.ts`:

```typescript
import { LuxuryEstate } from "./luxury-estate";
// Add to TEMPLATES record:
"luxury-estate": LuxuryEstate,
```

- [ ] **Step 3: Commit**

```bash
git add apps/agent-site/templates/luxury-estate.tsx apps/agent-site/templates/index.ts
git commit -m "feat: add luxury-estate template composition and register in template system"
```

### Task 11: Create test-luxury agent

**Files:**
- Create: `config/agents/test-luxury/config.json`
- Create: `config/agents/test-luxury/content.json`
- Create: `config/agents/test-luxury/legal/privacy-above.md`
- Create: `config/agents/test-luxury/legal/terms-above.md`
- Create: `config/agents/test-luxury/legal/accessibility-above.md`

- [ ] **Step 1: Create config.json**

```json
{
  "id": "test-luxury",
  "identity": {
    "name": "Victoria Sterling",
    "title": "Licensed Associate Real Estate Broker",
    "license_id": "10491087654",
    "brokerage": "Sterling & Associates",
    "brokerage_id": "SA-NYC-0801",
    "phone": "(212) 555-0199",
    "office_phone": "(212) 555-0190",
    "email": "victoria@testrealty.com",
    "website": "victoriasterlingsells.com",
    "languages": ["English", "French"],
    "tagline": "Luxury Real Estate",
    "headshot_url": "/agents/test-luxury/headshot.jpg"
  },
  "location": {
    "state": "NY",
    "office_address": "740 Park Avenue, Suite 12A, New York, NY 10021",
    "service_areas": ["Upper East Side", "Tribeca", "SoHo", "Greenwich Village"]
  },
  "branding": {
    "primary_color": "#0a0a0a",
    "secondary_color": "#1a1a2e",
    "accent_color": "#d4af37",
    "font_family": "Georgia",
    "logo_url": ""
  },
  "integrations": {
    "email_provider": "gmail"
  },
  "compliance": {
    "state_form": "NY-DOS-1736",
    "licensing_body": "NY Department of State",
    "disclosure_requirements": [
      "Property condition disclosure (PCDS)",
      "Lead-based paint disclosure (pre-1978)",
      "Smoke/CO detector compliance"
    ]
  }
}
```

- [ ] **Step 2: Create content.json**

Template: `luxury-estate`. Navigation: 6 items. All sections enabled. 4+ stats, 4+ services, 3 steps, 5 sold homes ($2M-$12M), 3 testimonials, multi-paragraph bio with credentials. Tone: sophisticated, understated.

*(Content.json is long — see design spec for the full agent details. The implementer should create rich content matching Victoria Sterling's Manhattan luxury persona.)*

Key fields for content.json:
- `"template": "luxury-estate"`
- Hero: headline "Exceptional Homes for Exceptional Lives", highlight_word "Exceptional Lives"
- Stats: "$2.1B Total Volume", "340+ Properties", "#1 NYC Luxury Agent", "18 Years"
- Services: Private Portfolio Access, White-Glove Service, Investment Advisory, Strategic Marketing, International Buyers, Architectural Consultation
- Steps: 3 steps — Consultation, Analysis, Execution
- Sold homes: 5 properties ($2M-$12M) with image URLs `/agents/test-luxury/sold/*.jpg`
- Testimonials: 3 five-star reviews from wealthy clients
- CMA form: title "What's Your Property Worth?", subtitle "Complimentary Confidential Valuation"
- About: multi-paragraph bio, credentials ["Top 1% NYC Agents", "$2.1B Career Volume", "REBNY Member"]
- Thank you page with luxury-appropriate copy

- [ ] **Step 3: Create legal markdown files**

`legal/privacy-above.md`:
```markdown
At Sterling & Associates, your privacy is paramount. We handle your personal information with the same discretion and care that defines every aspect of our client relationships.
```

`legal/terms-above.md`:
```markdown
Our commitment to excellence extends to every interaction. These terms reflect the high standards we hold ourselves to in serving Manhattan's most discerning clientele.
```

`legal/accessibility-above.md`:
```markdown
We believe exceptional service means being accessible to everyone. Our commitment to accessibility ensures all clients can explore our portfolio and services with ease.
```

- [ ] **Step 4: Commit**

```bash
git add config/agents/test-luxury/
git commit -m "feat: add test-luxury agent for luxury-estate template"
```

### Task 12: Download placeholder images for test-luxury

- [ ] **Step 1: Download headshot and sold property images**

```bash
mkdir -p apps/agent-site/public/agents/test-luxury/sold
curl -sL "https://picsum.photos/id/1005/400/500" -o "apps/agent-site/public/agents/test-luxury/headshot.jpg"
curl -sL "https://picsum.photos/id/260/800/500" -o "apps/agent-site/public/agents/test-luxury/sold/432-park-ave.jpg"
curl -sL "https://picsum.photos/id/336/800/500" -o "apps/agent-site/public/agents/test-luxury/sold/15-central-park.jpg"
curl -sL "https://picsum.photos/id/374/800/500" -o "apps/agent-site/public/agents/test-luxury/sold/56-leonard.jpg"
curl -sL "https://picsum.photos/id/439/800/500" -o "apps/agent-site/public/agents/test-luxury/sold/1-john-st.jpg"
curl -sL "https://picsum.photos/id/164/800/500" -o "apps/agent-site/public/agents/test-luxury/sold/110-central-park.jpg"
```

- [ ] **Step 2: Commit**

```bash
git add apps/agent-site/public/agents/test-luxury/
git commit -m "feat: add placeholder images for test-luxury agent"
```

### Task 13: Regenerate config registry and run full test suite

- [ ] **Step 1: Regenerate config registry**

```bash
node apps/agent-site/scripts/generate-config-registry.mjs
```

- [ ] **Step 2: Run full test suite**

```bash
cd apps/agent-site && npx vitest run
```

Expected: All tests pass (existing 562+ plus new ~50 tests for luxury-estate variants)

- [ ] **Step 3: Commit registry update**

```bash
git add apps/agent-site/lib/config-registry.ts
git commit -m "chore: regenerate config registry with test-luxury agent"
```

---

## Chunk 3: Template 5 — Urban Loft

### Task 14-24: Urban Loft (same pattern as Chunk 2)

**Variant names:** HeroBold, StatsCompact, ServicesPills, StepsCards, SoldCompact, TestimonialsStack, AboutCompact

**Key differences from Luxury Estate:**
- Light background (#fafafa), not dark
- Bold sans-serif (800 weight), not thin serif
- Coral accent (#ff6b6b) via CSS variables
- Rainbow gradient accent bar at top
- Compact layouts, pill/badge visual language
- No carousel — SoldCompact is a tight grid with neighborhood tags and bed/bath badges

**Section variant details:**

| Variant | Key Visual Feature |
|---------|-------------------|
| HeroBold | Oversized 800-weight headline, accent period at end, dual CTAs (filled + outlined), rounded agent photo, neighborhood tag pills at bottom |
| StatsCompact | Dark background pills with white text, horizontal inline layout, no card borders |
| ServicesPills | Scrollable category pills at top, service cards with color-coded category tags |
| StepsCards | Horizontal card layout, large watermark step number behind title, no connecting lines |
| SoldCompact | Compact grid, 1:1 square images, neighborhood tag per card, bed/bath badges, "View Details" hover |
| TestimonialsStack | Vertical stack (not grid), full-width cards, circle initial avatar, left-aligned quote, right-aligned reviewer info, source pill |
| AboutCompact | Horizontal layout, small circular photo, inline name/title, single-column bio, social-style link row, credential badges |

**Test agent:** `test-loft`
- Kai Nakamura, Brooklyn/Manhattan NY
- Loft & Key Realty, License 10401055432
- Primary #1a1a1a, Accent #ff6b6b, Font "Inter"
- 4-5 apartments ($400K-$900K), casual/confident tone
- Picsum IDs: urban/city architecture (164, 260, 336, 374, 439)

**Files per variant follow the same pattern as Tasks 2-8:**
1. Write test → 2. Run (fail) → 3. Implement → 4. Run (pass) → 5. Commit

**Then:** barrel exports (Task 9 pattern), template composition (Task 10), test agent (Task 11), images (Task 12), config registry (Task 13).

Commit messages: `feat: add {VariantName} section variant for urban-loft template`
Final commit: `feat: add urban-loft template composition and register in template system`

---

## Chunk 4: Templates 6-7 — New Beginnings + Light Luxury

### Task 25-37: New Beginnings

**Variant names:** HeroStory, StatsWarm, ServicesHeart, StepsJourney, SoldStories, TestimonialsHeart, AboutWarm

**Key differences:**
- Warm sage/mint backgrounds (#f0f7f4)
- Green accent (#5a9e7c) + earthy copper secondary (#d4a574)
- People-first imagery, warm rounded sans-serif (600 weight)
- **SoldStories** is a NEW COMPONENT — each sold home includes `client_quote` and `client_name` overlaid
- **StepsJourney** uses curved SVG connecting path between steps (journey metaphor)
- Second-person copy ("You'll tell us...")

| Variant | Key Visual Feature |
|---------|-------------------|
| HeroStory | Large hero image with warm green gradient overlay, "your story" language, rounded CTA, small agent photo badge |
| StatsWarm | Rounded cards with soft shadows, green accent values, heart/people labels |
| ServicesHeart | Rounded cards with warm backgrounds, icons via `resolveServiceIcon()`, human benefit emphasis |
| StepsJourney | Curved SVG connecting line between steps, illustration-style icons, second-person descriptions |
| SoldStories | Property photo + client photo overlay + client quote + price/address — success stories |
| TestimonialsHeart | Large cards with warm bg, client photo circle, decorative quotation mark, "— The [Family]" |
| AboutWarm | Centered layout, large circular photo, first-person bio, "Why I Do This" subtitle, heart/people credential icons |

**Test agent:** `test-beginnings`
- Rachel & David Kim, Charlotte NC
- Hearthstone Realty Group, License NC-312456
- Primary #2d4a3e, Accent #5a9e7c, Font "Nunito"
- 4-5 homes ($250K-$600K) WITH client quotes
- Picsum IDs: people/home-oriented (1012, 1024, 1029, 1039, 1048)

### Task 38-50: Light Luxury

**Variant names:** HeroAiry, StatsElegant, ServicesRefined, StepsRefined, SoldElegant, TestimonialsQuote, AboutGrace

**Key differences:**
- White (#ffffff) with warm gray (#f8f6f3) alternation
- Champagne/rose gold accent (#b8926a)
- Thin serif headings, elegant and airy — bright counterpart to dark Luxury Estate
- No icons on services — typography-driven
- Gallery-like sold homes presentation (16:9 images, thin borders)

| Variant | Key Visual Feature |
|---------|-------------------|
| HeroAiry | White background, thin serif headline, champagne accent word, full-width property image below headline, small elegant photo circle, champagne CTA |
| StatsElegant | Serif numerals, champagne accent, horizontal with thin separating lines, no cards |
| ServicesRefined | No cards — clean list with champagne left border, serif title + sans-serif description, alternating subtle bg |
| StepsRefined | Numbered with champagne circles, vertical layout, thin connecting line, serif titles |
| SoldElegant | Large 16:9 images, thin champagne border, serif price, minimal info, gallery-like |
| TestimonialsQuote | Single large centered quote, decorative champagne quotation mark, serif italic, small caps reviewer |
| AboutGrace | Side-by-side: portrait photo left, bio right, serif headings, credentials as comma-separated line |

**Test agent:** `test-light-luxury`
- Isabelle Fontaine, Greenwich CT
- Fontaine & Partners, License REB.0795432
- Primary #3d3028, Accent #b8926a, Font "Georgia"
- 4-5 properties ($1.2M-$5.5M), refined/gracious tone
- Picsum IDs: bright architecture/interiors (110, 164, 188, 260, 336)

---

## Chunk 5: Templates 8-10 — Country Estate + Coastal Living + Commercial

### Task 51-63: Country Estate

**Variant names:** HeroEstate, StatsRugged, ServicesEstate, StepsPath, SoldEstate, TestimonialsRustic, AboutHomestead

**Key differences:**
- Warm cream (#faf6f0) + stone gray (#e8e2d8)
- Hunter green accent (#4a6741) + saddle brown (#8b6b3d)
- Mixed typography: serif headings, sans-serif body
- **SoldEstate** uses `features` field for property details (acreage, stables, etc.)
- Land/outdoor metaphors throughout

| Variant | Key Visual Feature |
|---------|-------------------|
| HeroEstate | Full-width landscape image, dark gradient overlay from bottom, white headline overlaid, "Est." tagline, green CTA |
| StatsRugged | Dark green bg, white/gold text, horizontal, land labels ("Acres Sold") |
| ServicesEstate | Two-column rows, icon left + text right, green icons, earthy card backgrounds |
| StepsPath | Vertical path with green dots like trail markers, dotted connecting line, outdoor metaphor |
| SoldEstate | Large landscape images, property feature badges (acreage, bedrooms, garage), serif price, green "SOLD" badge |
| TestimonialsRustic | Warm cream cards, subtle border pattern, serif quote, reviewer with location |
| AboutHomestead | Full-width section, large landscape photo, bio emphasizes land/community knowledge |

**Test agent:** `test-country` — James Whitfield, Loudoun County VA, estates $800K-$3.5M

### Task 64-76: Coastal Living

**Variant names:** HeroCoastal, StatsWave, ServicesCoastal, StepsBreeze, SoldCoastal, TestimonialsBeach, AboutCoastal

**Key differences:**
- Sandy white (#fefcf8) + pale ocean (#e8f4f8)
- Ocean teal accent (#2c7a7b) + sandy gold (#b7791f)
- Wave/curve design elements (CSS clip-path on section dividers)
- **SoldCoastal** uses `tags?: string[]` field for multiple property tags as colored pills (e.g., `["Oceanfront", "Beach Access"]`)

| Variant | Key Visual Feature |
|---------|-------------------|
| HeroCoastal | Full-width beach image, white gradient overlay from bottom, wave-shaped CSS clip-path divider, teal photo border |
| StatsWave | Teal bg, subtle wave pattern, white text, coastal labels |
| ServicesCoastal | Cards with ocean-blue top border, rounded corners, teal icons, sandy bg |
| StepsBreeze | Horizontal layout, wave SVG connecting line, teal step numbers |
| SoldCoastal | Images with property type tag pills (Oceanfront, Canal, Beach Access), teal "SOLD" badge |
| TestimonialsBeach | Sandy bg cards, teal stars, wave separator between cards |
| AboutCoastal | Side-by-side, rounded rect photo, coastal expertise bio, teal credential pills |

**Test agent:** `test-coastal` — Maya Torres, Outer Banks NC, beach properties $450K-$2.2M

### Task 77-89: Commercial

**Variant names:** HeroCorporate, StatsMetrics, ServicesCommercial, StepsCorporate, SoldMetrics, TestimonialsCorporate, AboutProfessional

**Key differences:**
- Cool gray (#f4f5f7) with white cards
- Corporate blue accent (#2563eb)
- Data-forward: metrics, tables, grids
- **ServicesCommercial** uses `category` field for two-tier grouping
- **SoldMetrics** uses `property_type`, `sq_ft`, `cap_rate`, `noi`, `badge_label` fields
- No stars in testimonials — client company name + title instead
- "CLOSED" instead of "SOLD"

| Variant | Key Visual Feature |
|---------|-------------------|
| HeroCorporate | No agent photo, bold headline, key metric in subheading, dual CTAs, blue gradient accent bar |
| StatsMetrics | Card grid, icon + large value + label + optional trend indicator, dashboard aesthetic |
| ServicesCommercial | Two-tier: property type icon cards (Office, Retail, etc.) + service detail cards, grouped by `category` |
| StepsCorporate | Clean numbered list, blue circles, no decorative elements, timeline line |
| SoldMetrics | Property cards with type badge + metrics grid (Sq Ft, Cap Rate, NOI) + "CLOSED" badge |
| TestimonialsCorporate | No stars, client company name + title, ROI-focused quotes, clean borders |
| AboutProfessional | Rectangular headshot, certifications/designations, track record bio, professional designation pills |

**Test agent:** `test-commercial` — Robert Chen, Dallas-Fort Worth TX, commercial properties with metrics

---

## Chunk 6: Final Integration + Verification

### Task 90: Register ALL remaining templates in barrel exports and template index

Follow the same pattern as Task 9-10 for each of the 6 remaining templates:
- Add each variant to its section folder `index.ts`
- Add re-exports to `components/sections/index.ts`
- Create template composition file in `templates/`
- Register in `templates/index.ts`

Final `templates/index.ts` should have all 10 entries:
```typescript
import { EmeraldClassic } from "./emerald-classic";
import { ModernMinimal } from "./modern-minimal";
import { WarmCommunity } from "./warm-community";
import { LuxuryEstate } from "./luxury-estate";
import { UrbanLoft } from "./urban-loft";
import { NewBeginnings } from "./new-beginnings";
import { LightLuxury } from "./light-luxury";
import { CountryEstate } from "./country-estate";
import { CoastalLiving } from "./coastal-living";
import { Commercial } from "./commercial";

export type { TemplateProps } from "./types";

export const TEMPLATES: Record<string, typeof EmeraldClassic> = {
  "emerald-classic": EmeraldClassic,
  "modern-minimal": ModernMinimal,
  "warm-community": WarmCommunity,
  "luxury-estate": LuxuryEstate,
  "urban-loft": UrbanLoft,
  "new-beginnings": NewBeginnings,
  "light-luxury": LightLuxury,
  "country-estate": CountryEstate,
  "coastal-living": CoastalLiving,
  "commercial": Commercial,
};

export function getTemplate(name: string) {
  return TEMPLATES[name] || EmeraldClassic;
}
```

### Task 91: Create all remaining test agents

For each of: `test-loft`, `test-beginnings`, `test-light-luxury`, `test-country`, `test-coastal`, `test-commercial`:
1. Create `config/agents/{id}/config.json` — see design spec for identity, location, branding, compliance
2. Create `config/agents/{id}/content.json` — all sections enabled, rich content per spec
3. Create `config/agents/{id}/legal/*.md` — 3 files, voice matches agent persona
4. Download placeholder images from picsum.photos

### Task 92: Add legal markdown to existing test agents

The existing agents need `legal/` directories created (only `jenise-buckalew` has one, and it may be empty). Create the directories and add custom markdown files to each:

- [ ] Create `config/agents/jenise-buckalew/legal/privacy-above.md`
- [ ] Create `config/agents/jenise-buckalew/legal/terms-above.md`
- [ ] Create `config/agents/jenise-buckalew/legal/accessibility-above.md`
- [ ] Repeat for `test-emerald`, `test-modern`, `test-warm`

### Task 93: Final verification

- [ ] **Step 1: Regenerate config registry**

```bash
node apps/agent-site/scripts/generate-config-registry.mjs
```

- [ ] **Step 2: Run full agent-site test suite**

```bash
cd apps/agent-site && npx vitest run
```

Expected: All tests pass (~850+ tests — 562 existing + ~49 per template × 7 = ~343 new)

- [ ] **Step 3: Run UI package tests (no regressions)**

```bash
cd packages/ui && npx vitest run
```

Expected: All 89 tests pass

- [ ] **Step 4: Commit all registry and integration changes**

```bash
git add -A
git commit -m "feat: complete 7 new templates with test agents, images, and full test coverage"
```

---

## Parallelization Guide

Templates are fully independent after Task 1 (type extensions). For maximum throughput with subagent-driven development:

| Subagent | Templates | Tasks |
|----------|-----------|-------|
| Agent A | Luxury Estate (dark luxury) | Tasks 2-13 |
| Agent B | Urban Loft (bright city) | Tasks 14-24 |
| Agent C | New Beginnings (warm people-first) | Tasks 25-37 |
| Agent D | Light Luxury (bright luxury) | Tasks 38-50 |
| Agent E | Country Estate (rural estate) | Tasks 51-63 |
| Agent F | Coastal Living (beach) | Tasks 64-76 |
| Agent G | Commercial (data-forward) | Tasks 77-89 |

**Dependency:** Task 1 must complete before any template subagent starts.
**Final integration:** Task 90-93 runs after all subagents complete.

Each subagent should follow the `/create-template` skill and use the TDD flow: test → fail → implement → pass → commit.

---

## File Count Summary

| Category | Files per Template | Total (7 templates) |
|----------|-------------------|---------------------|
| Section variants (.tsx) | 7 | 49 |
| Test files (.test.tsx) | 7 | 49 |
| Template composition (.tsx) | 1 | 7 |
| Test agent config.json | 1 | 7 |
| Test agent content.json | 1 | 7 |
| Legal markdown (.md) | 3 | 21 |
| Images (headshot + sold) | ~6 | ~42 |
| **Total** | **~26** | **~182** |

Plus: 1 type extension, barrel export updates, template index updates, config registry regeneration.
