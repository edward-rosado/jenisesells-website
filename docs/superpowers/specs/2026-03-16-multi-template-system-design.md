# Multi-Template System for Agent Sites

## Goal

Transform the agent-site from a single-template app into a multi-template system with 3 templates (Emerald Classic, Modern Minimal, Warm Community). Each agent picks a template in their config, and the site renders with that template's layout and style. Section components are reusable across templates via a variant system.

## Templates

### 1. Emerald Classic (existing)
- **Feel:** Professional, established, trustworthy
- **Hero:** Gradient background + circular agent photo
- **Sections:** Horizontal stats bar, border-left service cards, numbered step circles, 3-col sold homes grid, card testimonials
- **Best for:** Traditional agents who want a polished, proven look

### 2. Modern Minimal (new)
- **Feel:** Clean, contemporary, tech-savvy
- **Hero:** Split layout — headline left, agent photo right
- **Sections:** Bordered stat cards, clean typography with whitespace, vertical timeline steps, minimal sold home cards, clean testimonial layout
- **Best for:** Younger agents, luxury markets, tech-forward branding

### 3. Warm Community (new)
- **Feel:** Friendly, approachable, family-oriented
- **Hero:** Centered agent photo with warm tones, rounded corners
- **Sections:** Soft shadow cards, icon-led services, friendly step cards, rounded sold home cards with shadows, chat-bubble testimonials with avatars
- **Best for:** Family-focused agents, first-time buyer specialists, suburban markets

## Architecture

### Core Principle: Section Variants

Each section type (Hero, Stats, Services, etc.) has multiple variant components — one per template. All variants of the same section type share the same props interface. Templates compose their chosen variants.

### Shared Components (identical across all templates)

- `Nav` — Fixed header with responsive drawer (3-tier breakpoints)
- `CmaForm` — Lead capture form (API or Formspree modes)
- `Footer` — Legal footer with contact info, Equal Housing, license
- `CookieConsentBanner` — GDPR/cookie consent
- `Analytics` — GA4, GTM, Meta Pixel tracking

### Section Variant Matrix

| Section Type | Emerald Classic | Modern Minimal | Warm Community |
|-------------|----------------|----------------|----------------|
| Hero | `HeroGradient` — gradient bg + circle photo | `HeroSplit` — text left, photo right | `HeroCentered` — centered photo, warm tones |
| Stats | `StatsBar` — horizontal bar | `StatsCards` — bordered cards | `StatsInline` — soft inline display |
| Services | `ServicesGrid` — border-left cards | `ServicesClean` — minimal cards, whitespace | `ServicesIcons` — icon-led, rounded cards |
| Steps | `StepsNumbered` — numbered circles | `StepsTimeline` — vertical timeline | `StepsFriendly` — soft rounded cards |
| Sold Homes | `SoldGrid` — 3-col image grid | `SoldMinimal` — clean cards | `SoldCards` — rounded shadow cards |
| Testimonials | `TestimonialsGrid` — card grid | `TestimonialsClean` — minimal layout | `TestimonialsBubble` — chat-bubble style |
| About | `AboutSplit` — photo left, bio right | `AboutMinimal` — clean layout | `AboutCard` — rounded card layout |

### Props Contracts

All variants of the same section type accept identical props. This ensures templates can swap variants without changing data flow.

```
HeroProps         = { data: HeroData; agentPhotoUrl?: string; agentName?: string }
StatsProps        = { items: StatItem[]; sourceDisclaimer?: string }
ServicesProps     = { items: ServiceItem[]; title?: string; subtitle?: string }
StepsProps        = { steps: StepItem[]; title?: string; subtitle?: string }
SoldHomesProps    = { items: SoldHomeItem[]; title?: string; subtitle?: string }
TestimonialsProps = { items: TestimonialItem[]; title?: string }
AboutProps        = { agent: AgentConfig; data: AboutData }
```

The data types (`HeroData`, `StatItem`, etc.) already exist in `lib/types.ts`. The props interfaces are currently defined locally in each component file. As part of the restructure, these shared props interfaces will be centralized in a new `components/sections/types.ts` file so all variants can import them.

### Template Registry

`templates/index.ts` maps template names to components:

```
TEMPLATES = {
  "emerald-classic": EmeraldClassic,
  "modern-minimal": ModernMinimal,
  "warm-community": WarmCommunity,
}

getTemplate(name) returns TEMPLATES[name] || EmeraldClassic
```

### Config Integration

The `content.template` field already exists in `AgentContent` and defaults to `"emerald-classic"` in `loadAgentContent()`. No schema or config changes are needed. New valid values: `"modern-minimal"`, `"warm-community"`.

## File Structure

```
apps/agent-site/
  components/
    Nav.tsx                              # Shared (unchanged)
    sections/
      shared/                            # Shared across all templates
        CmaForm.tsx                      # Moved from sections/CmaForm.tsx
        Footer.tsx                       # Moved from sections/Footer.tsx
        index.ts
      heroes/                            # Hero variants
        HeroGradient.tsx                 # Renamed from Hero.tsx (Emerald)
        HeroSplit.tsx                    # New (Modern Minimal)
        HeroCentered.tsx                 # New (Warm Community)
        index.ts
      stats/                             # Stats variants
        StatsBar.tsx                     # Moved from StatsBar.tsx (Emerald)
        StatsCards.tsx                   # New (Modern Minimal)
        StatsInline.tsx                  # New (Warm Community)
        index.ts
      services/                          # Services variants
        ServicesGrid.tsx                 # Renamed from Services.tsx (Emerald)
        ServicesClean.tsx                # New (Modern Minimal)
        ServicesIcons.tsx                # New (Warm Community)
        index.ts
      steps/                             # HowItWorks variants
        StepsNumbered.tsx                # Renamed from HowItWorks.tsx (Emerald)
        StepsTimeline.tsx                # New (Modern Minimal)
        StepsFriendly.tsx                # New (Warm Community)
        index.ts
      sold/                              # SoldHomes variants
        SoldGrid.tsx                     # Renamed from SoldHomes.tsx (Emerald)
        SoldMinimal.tsx                  # New (Modern Minimal)
        SoldCards.tsx                    # New (Warm Community)
        index.ts
      testimonials/                      # Testimonials variants
        TestimonialsGrid.tsx             # Renamed from Testimonials.tsx (Emerald)
        TestimonialsClean.tsx            # New (Modern Minimal)
        TestimonialsBubble.tsx           # New (Warm Community)
        index.ts
      about/                             # About variants
        AboutSplit.tsx                   # Renamed from About.tsx (Emerald)
        AboutMinimal.tsx                 # New (Modern Minimal)
        AboutCard.tsx                    # New (Warm Community)
        index.ts
      index.ts                           # Barrel re-export
    legal/                               # Shared (unchanged)
  templates/
    index.ts                             # Registry (3 templates)
    emerald-classic.tsx                  # Updated imports
    modern-minimal.tsx                   # New
    warm-community.tsx                   # New
  __tests__/
    components/
      shared/                            # Shared section tests
        CmaForm.test.tsx                 # Moved
        Footer.test.tsx                  # Moved
      heroes/                            # Hero variant tests
        HeroGradient.test.tsx            # Renamed from Hero.test.tsx
        HeroSplit.test.tsx               # New
        HeroCentered.test.tsx            # New
      stats/
        StatsBar.test.tsx                # Moved
        StatsCards.test.tsx              # New
        StatsInline.test.tsx             # New
      services/
        ServicesGrid.test.tsx            # Renamed
        ServicesClean.test.tsx           # New
        ServicesIcons.test.tsx           # New
      steps/
        StepsNumbered.test.tsx           # Renamed
        StepsTimeline.test.tsx           # New
        StepsFriendly.test.tsx           # New
      sold/
        SoldGrid.test.tsx                # Renamed
        SoldMinimal.test.tsx             # New
        SoldCards.test.tsx               # New
      testimonials/
        TestimonialsGrid.test.tsx        # Renamed
        TestimonialsClean.test.tsx       # New
        TestimonialsBubble.test.tsx      # New
      about/
        AboutSplit.test.tsx              # Renamed
        AboutMinimal.test.tsx            # New
        AboutCard.test.tsx               # New
    templates/
      emerald-classic.test.tsx           # Updated imports
      modern-minimal.test.tsx            # New
      warm-community.test.tsx            # New
      index.test.ts                      # Updated for 3 templates
```

## Migration Strategy

### Phase 1: Restructure (zero behavior change)

Move and rename existing section components into the variant folder structure. Update all imports in `emerald-classic.tsx` and test files. **Emerald Classic renders identically before and after.**

Renames:
- `sections/Hero.tsx` → `sections/heroes/HeroGradient.tsx`
- `sections/StatsBar.tsx` → `sections/stats/StatsBar.tsx`
- `sections/Services.tsx` → `sections/services/ServicesGrid.tsx`
- `sections/HowItWorks.tsx` → `sections/steps/StepsNumbered.tsx`
- `sections/SoldHomes.tsx` → `sections/sold/SoldGrid.tsx`
- `sections/Testimonials.tsx` → `sections/testimonials/TestimonialsGrid.tsx`
- `sections/About.tsx` → `sections/about/AboutSplit.tsx`
- `sections/CmaForm.tsx` → `sections/shared/CmaForm.tsx`
- `sections/Footer.tsx` → `sections/shared/Footer.tsx`

The old `sections/index.ts` barrel export is replaced with per-folder barrel exports + a root `sections/index.ts` that re-exports everything.

### Phase 2: Modern Minimal template

Create all Modern Minimal variant components + template file + tests. Each variant shares the same props as its Emerald counterpart but uses different styling: light backgrounds, split layouts, bordered cards, clean typography, generous whitespace. Register `ModernMinimal` in `templates/index.ts` and add template registry tests.

### Phase 3: Warm Community template

Create all Warm Community variant components + template file + tests. Each variant shares the same props as its Emerald counterpart but uses different styling: warm color interpretation, rounded corners everywhere, soft shadows, icon-led cards, chat-bubble testimonials. Register `WarmCommunity` in `templates/index.ts` and update registry tests to cover all 3 templates.

## Styling Approach

All templates use the same CSS variable system (`--color-primary`, `--color-secondary`, `--color-accent`, `--font-family`). Templates interpret these variables differently:

- **Emerald Classic:** Primary as backgrounds, accent as highlights, bold contrast
- **Modern Minimal:** Primary as accents on white backgrounds, accent for CTAs, lots of white
- **Warm Community:** Primary as subtle accents, warm interpretation, soft shadows

All styling remains inline (consistent with existing codebase — no CSS modules or Tailwind).

## Data Flow

No changes to the data flow:

```
app/page.tsx
  → loadAgentConfig(agentId)     # From config-registry
  → loadAgentContent(agentId)    # Returns content.template field
  → getTemplate(content.template) # Resolves to component
  → <Template agent={agent} content={content} />
    → Template composes its chosen section variants
    → Each variant receives identical props as before
```

## Testing Strategy

### Variant Tests
Each variant gets its own test file verifying:
- Renders correct content from props
- Applies expected styling (inline styles, CSS classes)
- Conditional rendering (disabled sections don't render)
- Accessibility (semantic HTML, ARIA attributes)

### Template Tests
Each template test verifies:
- Correct variants are composed (e.g., Modern Minimal uses HeroSplit, not HeroGradient)
- Section ordering matches template design
- Disabled sections are skipped
- Nav and Footer are always rendered

### Registry Tests
- All 3 templates registered
- `getTemplate("modern-minimal")` returns `ModernMinimal`
- `getTemplate("warm-community")` returns `WarmCommunity`
- `getTemplate("unknown")` falls back to `EmeraldClassic`

## Scope

### In Scope
- 3 templates: Emerald Classic (restructured), Modern Minimal (new), Warm Community (new)
- 14 new section variant components (7 sections x 2 new templates)
- 2 new template composition files
- File restructure from flat sections/ to variant folders
- Updated imports and barrel exports
- Full test coverage for all new components

### Out of Scope
- Nav/Footer variants (shared across all templates)
- New section types (no new data fields)
- Config schema changes
- Onboarding flow changes (template selection during onboarding is a separate feature)
- Template preview/switching UI
- Per-template font or color override systems beyond existing CSS variables
