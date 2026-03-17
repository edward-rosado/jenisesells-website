---
name: template-builder
description: "Step-by-step process for creating a new agent site template with section variants, test agent, tests, and optional URL-based design extraction"
user-invocable: true
origin: auto-extracted
---

# Template Builder

**Extracted:** 2026-03-17
**Context:** Creating new agent site templates for the Real Estate Star multi-template system.

## Overview

This skill guides the creation of a complete agent site template from scratch — or from an existing website URL. Each template consists of 7 section component variants, a template composition file, a test agent with rich content, placeholder images, custom legal content, and full test coverage.

## Prerequisites

Before starting, read:
- `docs/superpowers/specs/2026-03-17-seven-new-templates-design.md` — full design spec
- `apps/agent-site/templates/index.ts` — template registry
- `apps/agent-site/components/sections/index.ts` — section barrel exports
- `apps/agent-site/components/sections/types.ts` — props interfaces
- `apps/agent-site/lib/types.ts` — data types (AgentContent, SoldHomeItem, ServiceItem, etc.)

## Two Modes

### Mode A: Design from Scratch
Start with a visual identity (colors, typography, personality) and build section variants from scratch.

### Mode B: Build from URL
Start with an existing real estate agent website URL and extract the design language to create a matching template.

**URL extraction process:**
1. Fetch the URL using WebFetch tool
2. Extract: color palette (primary, secondary, accent), font families, layout patterns
3. Identify section structure: hero style, stats presentation, services layout, etc.
4. Map extracted design to the closest section variant patterns
5. Create section variants that match the source site's aesthetic
6. Generate test agent content from the source site's copy (anonymized)

**Important:** Never copy agent identity, real phone numbers, or real email addresses from source URLs. Always create fictional test agent data.

---

## Step-by-Step Process

### Step 1: Define Visual Identity

Choose or extract:
- **Template ID:** kebab-case (e.g., `luxury-estate`)
- **Primary color:** Main brand color → `--color-primary`
- **Secondary color:** Supporting color → `--color-secondary`
- **Accent color:** CTA/highlight color → `--color-accent`
- **Font family:** Heading + body font → `--font-family`
- **Background base:** Section background colors (light/dark/warm)
- **Personality:** 2-3 adjective description (e.g., "dark, editorial, sophisticated")

### Step 2: Create Section Component Variants (7 files)

Each variant implements the same props interface as existing variants. Create these files:

```
apps/agent-site/components/sections/
  heroes/Hero{Variant}.tsx         → implements HeroProps
  stats/Stats{Variant}.tsx         → implements StatsProps
  services/Services{Variant}.tsx   → implements ServicesProps
  steps/Steps{Variant}.tsx         → implements StepsProps
  sold/Sold{Variant}.tsx           → implements SoldHomesProps
  testimonials/Testimonials{Variant}.tsx → implements TestimonialsProps
  about/About{Variant}.tsx         → implements AboutProps
```

**Naming convention:** `{Section}{Style}` — e.g., `HeroDark`, `SoldCarousel`, `TestimonialsBubble`

**Required for every variant:**
- Semantic HTML (section, article, h1-h3)
- CSS variables for colors: `var(--color-primary)`, `var(--color-accent)`, etc.
- Responsive: works at desktop (>1024px), tablet (769-1024px), mobile (≤768px)
- Accessible: aria-labels, focus-visible, color contrast ≥ 4.5:1
- Default fallback titles when `title` prop is undefined
- `id` attribute for anchor linking (e.g., `id="services"`, `id="sold"`)

**Reference existing variants for patterns:**
- Heroes: `HeroGradient`, `HeroSplit`, `HeroCentered` — see `hero-utils.tsx` for `safeHref()` and `renderHeadline()`
- Services: `ServicesIcons` has `resolveServiceIcon()` — reuse for icon-bearing variants
- Testimonials: `clampRating()` and `FTC_DISCLAIMER` from `types.ts`

### Step 3: Create Template Composition File

File: `apps/agent-site/templates/{template-id}.tsx`

```typescript
import { Nav } from "@/components/Nav";
import {
  Hero{Variant},
  Stats{Variant},
  Services{Variant},
  Steps{Variant},
  Sold{Variant},
  Testimonials{Variant},
  CmaSection,
  About{Variant},
  Footer,
} from "@/components/sections";
import type { TemplateProps } from "./types";

export function TemplateName({ agent, content }: TemplateProps) {
  const s = content.sections;
  return (
    <>
      <Nav agent={agent} content={content} />
      {s.hero.enabled && <Hero{Variant} data={s.hero.data} agentPhotoUrl={agent.identity.headshot_url} agentName={agent.identity.name} />}
      {s.stats.enabled && <Stats{Variant} items={s.stats.data.items} />}
      {s.services.enabled && <Services{Variant} items={s.services.data.items} title={s.services.data.title} subtitle={s.services.data.subtitle} />}
      {s.how_it_works.enabled && <Steps{Variant} steps={s.how_it_works.data.steps} title={s.how_it_works.data.title} subtitle={s.how_it_works.data.subtitle} />}
      {s.sold_homes.enabled && <Sold{Variant} items={s.sold_homes.data.items} title={s.sold_homes.data.title} subtitle={s.sold_homes.data.subtitle} />}
      {s.testimonials.enabled && <Testimonials{Variant} items={s.testimonials.data.items} title={s.testimonials.data.title} />}
      <CmaSection agentId={agent.id} agentName={agent.identity.name} defaultState={agent.location.state} tracking={agent.integrations?.tracking} data={s.cma_form.data} serviceAreas={agent.location.service_areas} />
      {s.about.enabled && <About{Variant} agent={agent} data={s.about.data} />}
      <Footer agent={agent} content={content} />
    </>
  );
}
```

### Step 4: Register in Three Places

1. **Template registry** — `apps/agent-site/templates/index.ts`:
   ```typescript
   import { TemplateName } from "./{template-id}";
   // Add to TEMPLATES record:
   "{template-id}": TemplateName,
   ```

2. **Section barrel exports** — for each new variant:
   - Export from section folder index: `components/sections/heroes/index.ts`
   - Re-export from barrel: `components/sections/index.ts`

3. **Config registry** — run after creating test agent:
   ```bash
   node apps/agent-site/scripts/generate-config-registry.mjs
   ```

### Step 5: Create Test Agent

Directory: `config/agents/{test-agent-id}/`

**config.json** — must include:
```json
{
  "id": "{test-agent-id}",
  "identity": {
    "name": "...",
    "title": "...",
    "license_id": "...",
    "brokerage": "...",
    "brokerage_id": "...",
    "phone": "...",
    "office_phone": "...",
    "email": "...",
    "website": "...",
    "languages": ["English", "..."],
    "tagline": "...",
    "headshot_url": "/agents/{test-agent-id}/headshot.jpg"
  },
  "location": { "state": "...", "office_address": "...", "service_areas": [...] },
  "branding": {
    "primary_color": "...",
    "secondary_color": "...",
    "accent_color": "...",
    "font_family": "...",
    "logo_url": ""
  },
  "integrations": { "email_provider": "gmail" },
  "compliance": {
    "state_form": "...",
    "licensing_body": "...",
    "disclosure_requirements": [...]
  }
}
```

**content.json** — must include:
- `"template": "{template-id}"`
- `navigation` with 6 items
- `contact_info` with 3+ methods (phone, office, email)
- All sections enabled with rich content
- `pages.thank_you` with variable interpolation
- 4-5 sold homes with image URLs
- 3+ testimonials
- 3+ services
- 3 steps
- Stats with 4+ items
- About with multi-paragraph bio and credentials

**legal/** — custom markdown intros:
- `privacy-above.md` — 2-3 sentences in agent's voice
- `terms-above.md` — 2-3 sentences about service commitment
- `accessibility-above.md` — 2-3 sentences about accessibility commitment

### Step 6: Download Placeholder Images

```bash
# Headshot (400x500)
curl -sL "https://picsum.photos/id/{N}/400/500" -o "apps/agent-site/public/agents/{id}/headshot.jpg"

# Sold homes (800x500 each)
curl -sL "https://picsum.photos/id/{N}/800/500" -o "apps/agent-site/public/agents/{id}/sold/{name}.jpg"
```

Choose picsum IDs that match the template vibe:
- Luxury: architecture, skyline IDs (260, 336, 374, 439)
- Coastal: beach, ocean IDs (141, 160, 169, 188)
- Country: landscape, rural IDs (1029, 1039, 1043, 1048)
- Urban: city, building IDs (164, 260, 336, 374)

### Step 7: Write Tests (7 test files)

One test file per section variant at:
```
apps/agent-site/__tests__/components/{section}/
```

**Minimum test coverage per variant:**
- Renders section heading (default + custom title)
- Renders all items/content
- Uses correct section ID for anchor linking
- Renders subtitle when provided
- Has proper styling (border-radius, shadows, etc.)
- Uses semantic HTML elements

**For new components (carousel, etc.):**
- Test keyboard navigation
- Test aria attributes
- Test prefers-reduced-motion behavior
- Test fallback when optional data is missing

### Step 8: Verify

```bash
# Regenerate config registry
node apps/agent-site/scripts/generate-config-registry.mjs

# Run all tests
cd apps/agent-site && npx vitest run

# Run UI package tests (if shared components changed)
cd packages/ui && npx vitest run
```

### Step 9: Verify Responsive & ADA

Manual checks:
- [ ] Desktop layout renders correctly
- [ ] Tablet (resize to ~900px) stacks appropriately
- [ ] Mobile (resize to ~375px) single column, hamburger nav
- [ ] Tab through all interactive elements — visible focus ring
- [ ] Color contrast ≥ 4.5:1 (use browser dev tools)
- [ ] Legal pages render with custom above content (`/privacy?agentId={id}`)

---

## URL-Based Template Generation (Mode B)

When given a URL to base a template on:

1. **Fetch and analyze the site:**
   ```
   WebFetch URL → extract HTML/CSS
   ```

2. **Extract design tokens:**
   - Primary/secondary/accent colors from CSS or inline styles
   - Font families from CSS font-family declarations
   - Layout patterns (grid vs flex, card styles, section spacing)
   - Hero style (full-bleed, split, centered, gradient overlay)
   - Section background alternation pattern

3. **Map to section variants:**
   - Compare extracted layout to existing variants
   - If close match exists, note which variant to reuse or slightly modify
   - If no match, design new variant inspired by the source

4. **Generate anonymized test content:**
   - Use the source site's copy structure but replace all PII
   - Create fictional agent identity
   - Keep section counts similar (same number of services, testimonials, etc.)

5. **Proceed with Steps 2-9 above**

---

## Common Pitfalls

- **Forgetting barrel exports** — every new variant must be in both the section folder index AND the main sections index
- **Hardcoded colors** — always use `var(--color-primary)`, never hex values
- **Missing config registry regen** — tests will fail if the registry doesn't include the new test agent
- **Image paths** — must start with `/agents/{id}/` and images go in `apps/agent-site/public/agents/{id}/`
- **Section IDs** — each section must have the correct `id` attribute for nav anchor linking (services, how-it-works, sold, testimonials, cma-form, about)
- **CmaSection is shared** — never create a template-specific CMA form. Always use `CmaSection` from shared components.
- **Footer is shared** — never create a template-specific footer. Always use `Footer` from shared components.
- **Nav is shared** — never create a template-specific nav. Always use `Nav` component.

## When to Use

- Creating a brand new template for the agent site
- Building a template inspired by an existing real estate website
- Adding a new section variant to an existing template
- Onboarding a new agent who needs a custom look
