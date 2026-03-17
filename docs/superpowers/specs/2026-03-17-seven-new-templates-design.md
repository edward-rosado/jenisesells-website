# Seven New Agent Site Templates — Design Spec

**Date:** 2026-03-17
**Author:** Eddie Rosado + Claude
**Status:** Draft

## Overview

Expand the agent site template system from 3 to 10 templates. Each new template targets a distinct market segment with unique visual personality, new section component variants, and a test agent with rich content and images.

## Template Inventory

### Existing (1-3)

| # | ID | Vibe | Hero Style | Color System |
|---|-----|------|------------|-------------|
| 1 | `emerald-classic` | Traditional professional | Gradient + circular photo | Green + gold |
| 2 | `modern-minimal` | Clean urban | Split text/image | Black + gray |
| 3 | `warm-community` | Friendly neighborhood | Centered + circular photo | Brown + warm cream |

### New (4-10)

| # | ID | Vibe | Target Agent | New Component |
|---|-----|------|-------------|---------------|
| 4 | `luxury-estate` | Dark + gold, editorial | Mansion/penthouse brokers | **Sold homes carousel** |
| 5 | `urban-loft` | Bright, punchy, bold | City apartment hunters | **Neighborhood spotlight cards** |
| 6 | `new-beginnings` | Warm greens, people-first | First-time buyer specialists | **Success stories section** |
| 7 | `light-luxury` | White marble, champagne, airy | High-end agents, bright aesthetic | **Featured property highlight** |
| 8 | `country-estate` | Earth tones, estate grounds | Rural luxury, sprawling properties | **Property features grid** (acreage, stables, etc.) |
| 9 | `coastal-living` | Ocean blues, sandy beiges | Beach/waterfront agents | **Property tags** (Oceanfront, Canal, Beach Access) |
| 10 | `commercial` | Data-forward, metrics-heavy | Office/retail/industrial brokers | **Property metrics cards** (cap rate, sq ft, NOI) |

---

## Architecture

### File Structure Per Template

Each new template adds:

```
apps/agent-site/
  templates/{template-id}.tsx                    # Template composition
  components/sections/
    heroes/Hero{Variant}.tsx                     # Hero variant
    stats/Stats{Variant}.tsx                     # Stats variant
    services/Services{Variant}.tsx               # Services variant
    steps/Steps{Variant}.tsx                     # Steps variant
    sold/Sold{Variant}.tsx                       # Sold variant
    testimonials/Testimonials{Variant}.tsx       # Testimonials variant
    about/About{Variant}.tsx                     # About variant
  __tests__/components/
    heroes/Hero{Variant}.test.tsx
    stats/Stats{Variant}.test.tsx
    services/Services{Variant}.test.tsx
    steps/Steps{Variant}.test.tsx
    sold/Sold{Variant}.test.tsx
    testimonials/Testimonials{Variant}.test.tsx
    about/About{Variant}.test.tsx

config/agents/{test-agent-id}/
  config.json                                    # Agent identity, branding, compliance
  content.json                                   # Template content, all sections
  legal/
    privacy-above.md                             # Custom privacy intro
    terms-above.md                               # Custom terms intro
    accessibility-above.md                       # Custom accessibility intro

apps/agent-site/public/agents/test-{template-id}/
  headshot.jpg                                   # Agent headshot
  sold/*.jpg                                     # Property images (4+ per agent)
```

### Shared Components (No Changes)

All templates reuse these unchanged:
- **Nav.tsx** — responsive nav with contact drawer
- **Footer.tsx** — legal links, equal housing, compliance
- **CmaSection.tsx** — lead form with mode pills
- **CookieConsentBanner** — GDPR/privacy consent
- **Legal pages** — privacy, terms, accessibility (auto-generated + custom markdown slots)

### Registration Checklist (Per Template)

Each new template must be registered in **three** places:
1. **Template registry:** `apps/agent-site/templates/index.ts` — add to `TEMPLATES` record
2. **Section barrel exports:** Each new variant must be exported from its section folder's index (e.g., `components/sections/heroes/index.ts`) and re-exported from `components/sections/index.ts`
3. **Config registry:** Run `node apps/agent-site/scripts/generate-config-registry.mjs` to pick up the new test agent

### Template Registration

Each template is added to `apps/agent-site/templates/index.ts`:

```typescript
import { LuxuryEstate } from "./luxury-estate";
// ... etc
export const TEMPLATES: Record<string, React.ComponentType<TemplateProps>> = {
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
```

### Props Contract

All section variants implement the same props interfaces (no changes to types.ts beyond what already exists):

- `HeroProps` — `{ data: HeroData, agentPhotoUrl?, agentName? }`
- `StatsProps` — `{ items: StatItem[], sourceDisclaimer? }`
- `ServicesProps` — `{ items: ServiceItem[], title?, subtitle? }`
- `StepsProps` — `{ steps: StepItem[], title?, subtitle? }`
- `SoldHomesProps` — `{ items: SoldHomeItem[], title?, subtitle? }`
- `TestimonialsProps` — `{ items: TestimonialItem[], title? }`
- `AboutProps` — `{ agent: AgentConfig, data: AboutData }`

The **sold homes carousel** (Template 4) uses the same `SoldHomesProps` — the carousel behavior is purely a rendering choice, not a data model change.

---

## Template 4: Luxury Estate

### Visual Identity
- **Background:** Dark (#0a0a0a to #1a1a2e gradient)
- **Accent:** Gold (#d4af37) — configurable via `--color-accent`
- **Typography:** Serif headings (Georgia/Playfair Display via `--font-family`), thin weight (300-400)
- **Spacing:** Generous whitespace, editorial feel
- **Text:** White primary, 60% opacity secondary

### Section Variants

**HeroDark** — Full-width dark gradient background. Small caps "LUXURY REAL ESTATE" label in accent. Large serif headline with accent-colored italic word. Agent photo optional (can be hidden for a more editorial look). CTA as outlined button (1px accent border). Stats bar overlaid at bottom with backdrop blur.

**StatsOverlay** — Transparent/dark background, inline horizontal. Gold accent values, uppercase letter-spaced labels. Feels like a bar at the bottom of the hero or a standalone dark strip.

**ServicesElegant** — Dark cards with subtle border (rgba white 0.1). Accent left border (2px). White title, muted description. No icons — typography-driven (luxury = restrained).

**StepsElegant** — Horizontal timeline with gold connecting line. Gold circle step numbers. White titles, muted descriptions. Minimal, refined.

**SoldCarousel** *(NEW COMPONENT)* — Full-width horizontal carousel:
- Each slide: full-bleed property image (100% width, 500px height) with dark gradient overlay
- Price in large serif font overlaid on image
- Address and city below in small caps
- Auto-advances every 5 seconds
- Manual prev/next arrows (gold accent)
- Dot indicators at bottom
- Touch/swipe support for mobile
- `aria-live="polite"` for screen readers, pause on focus
- Accessible prev/next buttons with aria-labels
- CSS scroll-snap for smooth native scrolling
- Falls back to grid on `prefers-reduced-motion`

**TestimonialsMinimal** — Single testimonial at a time, centered, large italic serif quote. Gold star rating. Reviewer name in small caps. Minimal — let the words speak.

**AboutEditorial** — Full-width dark section. Large circular photo with gold border. Bio in serif font, generous line height. Credentials as gold-bordered pills.

### Test Agent: `test-luxury`
- **Name:** Victoria Sterling
- **Title:** Licensed Associate Real Estate Broker
- **Location:** Manhattan, NY (Upper East Side, Tribeca, SoHo, Greenwich)
- **Brokerage:** Sterling & Associates
- **License:** 10491087654
- **Branding:** Primary #0a0a0a, Secondary #1a1a2e, Accent #d4af37, Font "Georgia"
- **Stats:** $2.1B total volume, 340+ properties, #1 NYC Luxury
- **Sold homes:** 4-5 properties ($2M-$12M range) — penthouses, townhouses
- **Content tone:** Sophisticated, understated, confident

---

## Template 5: Urban Loft

### Visual Identity
- **Background:** Light (#fafafa) with white cards
- **Accent:** Coral red (#ff6b6b) — configurable
- **Typography:** Bold sans-serif (800 weight headings), compact
- **Special:** Rainbow gradient accent bar (top of page/nav)
- **Personality:** Energetic, youthful, social-media-native

### Section Variants

**HeroBold** — Light background. Oversized bold headline (48px+, 800 weight). Accent-colored period at end of headline. Dual CTA buttons (filled + outlined). Agent photo in rounded rectangle (not circle). Neighborhood tag pills at bottom.

**StatsCompact** — Inline pills/badges instead of cards. Dark background pills with white text. Compact, horizontal, no wrapping on desktop.

**ServicesPills** — Horizontal scrollable pill categories at top (filters). Service cards below with category color coding. Compact descriptions. Tag-based visual language.

**StepsCards** — Horizontal card layout (not timeline). Each step is a standalone card with large step number as watermark behind title. Clean borders, no connecting lines.

**SoldCompact** — Compact grid (smaller cards than other templates). Neighborhood tag on each card. Bed/bath count badges. Square images (1:1 ratio). "View Details" hover state.

**TestimonialsStack** — Vertically stacked cards (not grid). Full-width, with reviewer photo placeholder (circle with initial). Left-aligned quote, reviewer info right-aligned. Source pill badge.

**AboutCompact** — Horizontal layout, compact. Small circular photo, name/title inline, bio in single column. Social-media-style link row at bottom. Credentials as inline badges.

### Test Agent: `test-loft`
- **Name:** Kai Nakamura
- **Title:** Licensed Real Estate Salesperson
- **Location:** Brooklyn/Manhattan, NY (Williamsburg, East Village, LIC, Bushwick, Hell's Kitchen)
- **Brokerage:** Loft & Key Realty
- **License:** 10401055432
- **Branding:** Primary #1a1a1a, Secondary #333333, Accent #ff6b6b, Font "Inter"
- **Stats:** 200+ apartments found, 4.9 Google, 6 years, $80M+ volume
- **Sold homes:** 4-5 apartments ($400K-$900K) — lofts, studios, 1BR/2BR
- **Content tone:** Casual, confident, neighborhood-savvy

---

## Template 6: New Beginnings

### Visual Identity
- **Background:** Soft sage/mint (#f0f7f4) with white cards
- **Accent:** Warm green (#5a9e7c) — configurable
- **Secondary accent:** Earthy copper (#d4a574) for warmth
- **Typography:** Friendly rounded sans-serif (600 weight), generous line height
- **Imagery:** People-focused — families on porches, keys in hands, moving day

### Section Variants

**HeroStory** — Large hero image (happy family outside home) with overlay text. Warm gradient overlay (sage green to transparent). Headline emphasizes "your story" / "new chapter" language. Rounded CTA button. Agent photo small and circular, positioned as a badge.

**StatsWarm** — Rounded cards with soft shadows. Green accent values. Warm background. Heart/people-oriented labels ("Families Helped", "Dreams Realized", "Happy Homeowners").

**ServicesHeart** — Cards with rounded corners and warm backgrounds. Each card has an icon (reuses resolveServiceIcon with warm styling). Emphasis on the human benefit, not the technical service.

**StepsJourney** *(NEW VARIANT)* — Visual journey/path layout. Curved connecting line between steps (SVG path). Each step has a small illustration-style icon. Step descriptions written in second person ("You'll tell us..."). Warm colors throughout.

**SoldStories** *(NEW COMPONENT)* — Instead of just property cards, each sold home includes:
- Property photo
- Client photo (small circular, overlapping property photo corner)
- Short client quote ("We found our forever home!")
- Price, address
- "Their Story" label
- This transforms sold homes into success stories

**TestimonialsHeart** — Large cards with warm background. Client photo (circle). Large quote with decorative quotation mark. Warm accent star rating. "— The [Family Name]" attribution. Emphasis on emotional connection.

**AboutWarm** — Centered layout with large circular photo. Bio written in first person, warm tone. "Why I Do This" subtitle. Credentials with heart/people icons.

### Test Agent: `test-beginnings`
- **Name:** Rachel & David Kim (team)
- **Title:** REALTOR® Team
- **Location:** Charlotte, NC (Myers Park, South End, NoDa, Ballantyne, Lake Norman)
- **Brokerage:** Hearthstone Realty Group
- **License:** NC-312456
- **Branding:** Primary #2d4a3e, Secondary #3d6b56, Accent #5a9e7c, Font "Nunito"
- **Stats:** 150+ families helped, 4.9 Google, 10 years, "Dreams Realized: 150+"
- **Sold homes:** 4-5 homes ($250K-$600K) with client quotes
- **Content tone:** Warm, empathetic, story-driven, second-person

---

## Template 7: Light Luxury

### Visual Identity
- **Background:** White (#ffffff) with subtle warm gray (#f8f6f3) section alternation
- **Accent:** Champagne/rose gold (#b8926a) — configurable
- **Typography:** Elegant serif (Playfair Display feel via `--font-family`), thin weight
- **Special:** Marble/stone texture hints via subtle CSS gradients
- **Personality:** Bright, airy, aspirational — the opposite of dark luxury

### Section Variants

**HeroAiry** — Clean white background. Large serif headline, thin weight. Champagne accent on key word. Full-width property image below headline (not behind). Agent photo as small elegant circle with thin gold border. CTA in champagne fill.

**StatsElegant** — Serif numerals, champagne accent. Horizontal layout with thin separating lines (not cards). Light and breathing.

**ServicesRefined** — No cards — just a clean list with champagne left border. Title in serif, description in sans-serif. Alternating subtle background. Restrained, editorial.

**StepsRefined** — Numbered with champagne circles. Vertical layout with thin connecting line. Serif step titles. Clean, breathing layout.

**SoldElegant** — Large images (16:9 ratio), thin champagne border. Price in serif. Minimal info — just price, address, one-liner. Hover reveals "View Details". Gallery-like presentation.

**TestimonialsQuote** — Single large quote centered, decorative champagne quotation mark. Serif italic text. Reviewer in small caps below. One at a time, elegant.

**AboutGrace** — Side-by-side: large rectangular photo (portrait orientation) left, bio right. Serif headings, sans-serif body. Credentials as a simple comma-separated line (not pills).

### Test Agent: `test-light-luxury`
- **Name:** Isabelle Fontaine
- **Title:** Licensed Associate Real Estate Broker
- **Location:** Greenwich, CT (Greenwich, Westport, New Canaan, Darien, Stamford)
- **Brokerage:** Fontaine & Partners
- **License:** REB.0795432
- **Branding:** Primary #3d3028, Secondary #5a4a3a, Accent #b8926a, Font "Georgia"
- **Stats:** $850M+ career volume, 200+ homes, 18 years
- **Sold homes:** 4-5 properties ($1.2M-$5.5M) — colonial estates, waterfront homes
- **Content tone:** Refined, gracious, aspirational without being pretentious

---

## Template 8: Country Estate

### Visual Identity
- **Background:** Warm cream (#faf6f0) with stone-gray (#e8e2d8) section alternation
- **Accent:** Hunter green (#4a6741) — configurable
- **Secondary:** Saddle brown (#8b6b3d) for warmth
- **Typography:** Mixed — serif headings (stately), sans-serif body (readable)
- **Personality:** Stately, grounded, wide-open-spaces feel

### Section Variants

**HeroEstate** — Full-width landscape property image with dark gradient overlay from bottom. White headline overlaid. "Est. 20XX" style tagline. Agent photo small in corner. CTA as solid green button.

**StatsRugged** — Dark green background, white/gold text. Horizontal layout. Land-oriented labels ("Acres Sold", "Properties Listed", "Counties Served").

**ServicesEstate** — Two-column layout (not grid). Each service is a row with icon left, text right. Hunter green icons. Earthy card backgrounds. Services like "Land & Acreage", "Estate Sales", "Equestrian Properties".

**StepsPath** — Vertical path with green dots/markers (like a trail map). Earthy tones. Each step connects to the next with a dotted line. Outdoor/journey metaphor.

**SoldEstate** *(NEW VARIANT)* — Property cards with feature badges:
- Large landscape image
- Property features grid: acreage, bedrooms, bathrooms, garage, lot size
- Price in bold serif
- "SOLD" badge in hunter green
- Different from other sold sections — emphasizes property features, not just price/address

**TestimonialsRustic** — Cards with warm cream background. Subtle wood-grain-like border (CSS pattern). Large serif quote. Reviewer with location ("— The Hendersons, Middleburg, VA").

**AboutHomestead** — Full-width section. Large landscape photo (agent on property). Bio emphasizes land knowledge, community roots. Credentials include acreage-related achievements.

### Test Agent: `test-country`
- **Name:** James Whitfield
- **Title:** REALTOR®, Land Specialist
- **Location:** Loudoun County, VA (Middleburg, Leesburg, Purcellville, Upperville, Bluemont)
- **Brokerage:** Whitfield Land & Estate
- **License:** VA-0225876
- **Branding:** Primary #4a6741, Secondary #5d7a54, Accent #8b6b3d, Font "Georgia"
- **Stats:** 5,000+ acres sold, 120+ estates, 22 years, 4.8 Google
- **Sold homes:** 4-5 estates ($800K-$3.5M) with acreage/features — farmhouses, equestrian, vineyard
- **Content tone:** Grounded, knowledgeable, deep local expertise

---

## Template 9: Coastal Living

### Visual Identity
- **Background:** Sandy white (#fefcf8) with pale ocean (#e8f4f8) section alternation
- **Accent:** Ocean teal (#2c7a7b) — configurable
- **Secondary:** Sandy gold (#b7791f) for warmth
- **Typography:** Clean sans-serif, relaxed weight (400-600)
- **Special:** Subtle wave/curve design elements (CSS clip-path on section dividers)
- **Personality:** Breezy, relaxed, vacation-home energy

### Section Variants

**HeroCoastal** — Full-width beach/waterfront image with white gradient overlay from bottom. Headline in dark text overlaid. Wave-shaped section divider at bottom (CSS clip-path). Agent photo circular with teal border.

**StatsWave** — Teal background with subtle wave pattern. White text. Horizontal layout. Beach-themed labels ("Waterfront Homes Sold", "Coastal Markets", "Years on the Coast").

**ServicesCoastal** — Cards with subtle ocean-blue top border. Rounded corners. Teal icons from resolveServiceIcon. Sandy background. Services like "Waterfront Properties", "Vacation Homes", "Coastal Investment".

**StepsBreeze** — Horizontal layout with wave connecting line (SVG). Teal circle step numbers. Light, airy spacing.

**SoldCoastal** *(NEW VARIANT)* — Property cards with location tags:
- Large image (16:9, ocean/beach visible)
- Property type tags: "Oceanfront", "Canal", "Beach Access", "Bay View"
- Tags as colored pills (teal, gold, etc.)
- Price + address
- "SOLD" badge in teal

**TestimonialsBeach** — Cards with sandy background. Teal star ratings. Quote with beach/relaxation tone. Decorative wave separator between cards.

**AboutCoastal** — Side-by-side layout. Agent photo in rounded rectangle (beach setting). Bio emphasizes coastal expertise, water activities, local lifestyle. Teal credentials pills.

### Test Agent: `test-coastal`
- **Name:** Maya Torres
- **Title:** REALTOR®, Waterfront Specialist
- **Location:** Outer Banks, NC (Nags Head, Kill Devil Hills, Duck, Corolla, Kitty Hawk)
- **Brokerage:** Tidewater Realty Group
- **License:** NC-298765
- **Branding:** Primary #1a5f5f, Secondary #2c7a7b, Accent #b7791f, Font "Lato"
- **Stats:** 100+ waterfront homes, 4.9 Google, 14 years, "$120M+ coastal sales"
- **Sold homes:** 4-5 beach properties ($450K-$2.2M) with tags (Oceanfront, Sound-side, etc.)
- **Content tone:** Relaxed, knowledgeable, lifestyle-oriented

---

## Template 10: Commercial

### Visual Identity
- **Background:** Cool gray (#f4f5f7) with white cards
- **Accent:** Corporate blue (#2563eb) — configurable
- **Typography:** Clean sans-serif (500-700 weight), data-friendly
- **Special:** Metrics-forward design, tables/grids, data visualization aesthetic
- **Personality:** Professional, data-driven, ROI-focused

### Section Variants

**HeroCorporate** — Clean, structured. Large headline in bold sans-serif. Subheading with key metric ("$500M+ in commercial transactions"). No agent photo in hero (photo in About). Dual CTAs: "View Portfolio" + "Schedule Consultation". Corporate blue gradient accent bar at top.

**StatsMetrics** — Card grid (2x2 or 4-column). Each card has icon + large value + label + optional trend indicator (up/down arrow). Data-dashboard aesthetic.

**ServicesCommercial** *(NEW VARIANT)* — Categorized services with two-tier layout:
- Top tier: property type icon cards (Office, Retail, Industrial, Mixed-Use)
- Bottom tier: service detail cards (Tenant Rep, Landlord Rep, Investment Sales, 1031 Exchange)
- Uses optional `category?: string` field on `ServiceItem` to group items (add to type)
- Items without `category` render in a default group
- Purely visual grouping — the component splits items by category and renders in tiers

**StepsCorporate** — Clean numbered list with corporate styling. No decorative elements. Step number in blue circle, title bold, description matter-of-fact. Timeline connecting line.

**SoldMetrics** *(NEW COMPONENT)* — Property cards with commercial metrics:
- Property photo
- Property type badge (Office, Retail, Industrial)
- Key metrics grid: Sq Ft, Cap Rate, NOI, Year Built
- Sale price (or lease rate)
- Tenant/use info
- "CLOSED" badge instead of "SOLD"

**TestimonialsCorporate** — Professional layout. No stars — instead, client company name and title. Quote focused on ROI/results. Clean borders, minimal decoration.

**AboutProfessional** — Professional headshot (rectangular, suit/office setting). Title, certifications, designations. Bio focused on track record, specializations, market expertise. Credentials include professional designations (CCIM, SIOR, etc.).

### Test Agent: `test-commercial`
- **Name:** Robert Chen
- **Title:** CCIM, Licensed Commercial Real Estate Broker
- **Location:** Dallas-Fort Worth, TX (Uptown, Deep Ellum, Design District, Las Colinas, Frisco)
- **Brokerage:** Apex Commercial Partners
- **License:** TX-0789012
- **Branding:** Primary #1e3a5f, Secondary #2563eb, Accent #059669, Font "Inter"
- **Stats:** $500M+ transactions, 85+ commercial deals, 16 years, CCIM designated
- **Sold homes → "Recent Transactions":** 4-5 commercial properties with sq ft, cap rate, NOI
- **Content tone:** Data-driven, professional, ROI-focused, no fluff

---

## New Components (Shared Across Templates)

### SoldCarousel (Template 4: Luxury Estate)

A horizontal, auto-advancing carousel for showcasing sold properties:

```
┌──────────────────────────────────────────────┐
│  ◄  │     [Full-width property image]     │  ►  │
│     │   ┌─────────────────────────────┐   │     │
│     │   │  $4,200,000                 │   │     │
│     │   │  432 Park Ave, Penthouse 82A│   │     │
│     │   │  Manhattan, NY              │   │     │
│     │   └─────────────────────────────┘   │     │
│     │            ● ○ ○ ○                  │     │
└──────────────────────────────────────────────┘
```

- CSS scroll-snap for smooth native scrolling
- Auto-advances every 5 seconds, pauses on hover/focus
- Outer container: `role="region"` with `aria-roledescription="carousel"` and `aria-label`
- `aria-live="polite"` region, `role="group"` per slide with `aria-roledescription="slide"`
- Prev/next buttons with `aria-label`
- Dot indicators (current: `aria-current="true"`)
- Falls back to vertical stack on `prefers-reduced-motion`
- Touch/swipe on mobile via scroll-snap
- Same `SoldHomesProps` — no data model changes

### SoldStories (Template 6: New Beginnings)

Sold homes + client success stories combined:

```
┌─────────────────────────────────┐
│  [Property Photo]               │
│  ┌──┐                           │
│  │👤│ ← client photo overlay    │
│  └──┘                           │
├─────────────────────────────────┤
│  $385,000                       │
│  42 Maple Dr, Charlotte, NC     │
│  "We found our forever home!"   │
│  — The Kim Family               │
└─────────────────────────────────┘
```

- Uses same `SoldHomesProps` — no new props interface needed
- Client quote comes from `client_quote` and `client_name` optional fields on `SoldHomeItem`
- Falls back gracefully to standard sold card when `client_quote` is absent

### SoldMetrics (Template 10: Commercial)

Property cards with commercial data:

```
┌─────────────────────────────────┐
│  [Property Photo]               │
│  ┌──────────┐                   │
│  │ OFFICE   │ ← type badge      │
│  └──────────┘                   │
├─────────────────────────────────┤
│  $4,200,000                     │
│  350 Fifth Ave, Dallas, TX      │
│  ┌────────┬─────────┬──────┐   │
│  │ 45K SF │ 6.2% CR │ $280K│   │
│  │ Sq Ft  │ Cap Rate │ NOI  │   │
│  └────────┴─────────┴──────┘   │
│  CLOSED                         │
└─────────────────────────────────┘
```

- Extends `SoldHomeItem` type with optional commercial fields:
  - `property_type?: string` — "Office", "Retail", "Industrial", "Mixed-Use"
  - `sq_ft?: string` — "45,000 SF"
  - `cap_rate?: string` — "6.2%"
  - `noi?: string` — "$280,000"
  - `badge_label?: string` — "CLOSED" instead of "SOLD"
- All new fields optional — non-commercial templates ignore them

---

## Type Extensions

Minimal additions to `apps/agent-site/lib/types.ts`:

```typescript
// Extend SoldHomeItem with optional commercial fields
export interface SoldHomeItem {
  address: string;
  city: string;
  state: string;
  price: string;
  sold_date?: string;
  image_url?: string;
  // New optional fields (used by commercial, coastal, country templates)
  property_type?: string;    // "Office", "Oceanfront", "Estate"
  sq_ft?: string;
  cap_rate?: string;
  noi?: string;
  badge_label?: string;      // Override "SOLD" text
  features?: Array<{ label: string; value: string }>;  // [{ label: "Lot", value: "5 acres" }]
  client_quote?: string;     // For New Beginnings success stories
  client_name?: string;      // For New Beginnings
}
```

Also add `category?: string` to `ServiceItem`:

```typescript
export interface ServiceItem {
  title: string;
  description: string;
  icon?: string;
  category?: string;  // For ServicesCommercial two-tier grouping
}
```

No other type changes needed. All templates use the same `AgentContent` shape.

---

## Font Loading Strategy

Templates reference non-system fonts (Inter, Nunito, Lato). Strategy:
- **System fonts** (Georgia, Segoe UI): No loading needed — use directly via `--font-family`
- **Google fonts** (Inter, Nunito, Lato): Set via `--font-family` CSS variable as-is. Browsers fall back to system sans-serif if not loaded. This is acceptable for MVP — fonts degrade gracefully.
- **Future enhancement:** Add `next/font/google` imports in template files for proper font optimization. Not required for initial implementation.
- Test agents use `font_family` in config.json which becomes `--font-family` CSS variable via `buildCssVariableStyle()`.

## Image Sourcing Strategy

Test agent images use **picsum.photos** with specific IDs known to produce relevant content:
- **Headshots:** 400x500 portrait-oriented images
- **Sold properties:** 800x500 landscape images
- Use `https://picsum.photos/id/{N}/{width}/{height}` for reproducible results
- Specific IDs chosen per template to match the vibe (architecture IDs for luxury, beach IDs for coastal, etc.)
- These are placeholders — real agents will upload their own photos
- Total: ~35-42 images across 7 test agents

---

## Test Agents

### Required Per Agent

Each test agent must include:
- `config.json` — full identity, location, branding, compliance with license_id, brokerage_id
- `content.json` — all sections enabled with rich content, navigation, 3+ contact methods
- `legal/privacy-above.md` — 2-3 sentence custom privacy intro
- `legal/terms-above.md` — 2-3 sentence custom terms intro
- `legal/accessibility-above.md` — 2-3 sentence custom accessibility intro
- Headshot image (400x500)
- 4-5 sold property images (800x500)

### Agent Summary

| Agent ID | Name | State | Template | Colors |
|----------|------|-------|----------|--------|
| `test-luxury` | Victoria Sterling | NY | luxury-estate | Black + Gold |
| `test-loft` | Kai Nakamura | NY | urban-loft | Black + Coral |
| `test-beginnings` | Rachel & David Kim | NC | new-beginnings | Sage + Copper |
| `test-light-luxury` | Isabelle Fontaine | CT | light-luxury | Warm brown + Champagne |
| `test-country` | James Whitfield | VA | country-estate | Hunter green + Saddle brown |
| `test-coastal` | Maya Torres | NC | coastal-living | Teal + Sandy gold |
| `test-commercial` | Robert Chen | TX | commercial | Navy + Corporate blue |

---

## Responsive Breakpoints

All new templates must support the same breakpoints as existing:

| Breakpoint | Behavior |
|-----------|----------|
| Desktop (>1024px) | Full layout, multi-column grids, horizontal stats |
| Tablet (769-1024px) | Reduced columns, stacked where needed, contact drawer |
| Mobile (≤768px) | Single column, hamburger nav, stacked everything |

The **SoldCarousel** specifically:
- Desktop: Full-width slides with prev/next arrows
- Tablet: Full-width slides, arrows smaller
- Mobile: Scroll-snap horizontal scroll, swipe gesture, no arrows (dots only)

---

## ADA / Accessibility Requirements

All new components must meet:
- Semantic HTML (section, article, h1-h3, nav, ol/ul)
- `aria-label` on all interactive elements
- Color contrast ratio ≥ 4.5:1 (WCAG AA) — especially important for dark templates
- Focus-visible outlines on all interactive elements
- `prefers-reduced-motion` — disable auto-advance carousel, simplify transitions
- Screen reader support: `aria-live` for carousel, `role="img"` for star ratings
- Keyboard navigation: carousel prev/next via arrow keys, tab through all controls

### Dark Template Contrast Check

For Luxury Estate (dark bg):
- Gold (#d4af37) on dark (#0a0a0a) = 8.5:1 ratio ✓
- White on dark = 19.3:1 ✓
- 60% white on dark = needs verification — may need to be 70%+

---

## Legal Page Content for Test Agents

The legal content injection system already exists — `loadLegalContent()` in `config.ts` reads markdown files from `config/agents/{id}/legal/` via the config registry. The existing test agents (`test-emerald`, `test-warm`, `test-modern`) and Jenise have empty `legal/` directories. The **new** test agents will be the first to use this feature, demonstrating how custom legal intros work.

Also add `legal/` markdown files to the existing 3 test agents and Jenise to showcase the feature.

Each test agent gets custom markdown in `legal/` directory:

**privacy-above.md** — 2-3 sentences introducing the agent's privacy commitment in their voice/brand tone.

**terms-above.md** — 2-3 sentences about the agent's service commitment.

**accessibility-above.md** — 2-3 sentences about the agent's commitment to accessibility.

These inject above the auto-generated legal content, adding a personal touch while keeping all required compliance language intact.

---

## Template Builder Skill

After implementation, extract the process into `.claude/skills/learned/template-builder.md`:

```yaml
---
name: template-builder
description: "Step-by-step process for creating a new agent site template with section variants, test agent, and tests"
user-invocable: true
origin: auto-extracted
---
```

The skill will document:
1. Choose visual identity (colors, typography, personality)
2. Create section component variants (7 files)
3. Create template composition file
4. Register in template index + sections index
5. Create test agent (config.json + content.json + legal/ + images)
6. Write tests for all section variants
7. Generate config registry
8. Run full test suite
9. Verify responsive behavior and ADA compliance

---

## Execution Strategy

Build templates in pairs to maximize code review efficiency:

| Phase | Templates | New Components |
|-------|-----------|---------------|
| Phase 1 | Luxury Estate + Light Luxury | SoldCarousel |
| Phase 2 | Urban Loft + Coastal Living | Property tags, neighborhood cards |
| Phase 3 | New Beginnings + Country Estate | SoldStories, property features |
| Phase 4 | Commercial | SoldMetrics, commercial services |

Each phase: implement sections → compose template → create test agent → write tests → verify.

---

## Verification Checklist (Per Template)

- [ ] All 7 section variants render correctly
- [ ] Template composition file registered
- [ ] Test agent config.json validates against schema
- [ ] Test agent content.json has all sections enabled
- [ ] Custom legal markdown renders on legal pages
- [ ] All images download and display
- [ ] Desktop/tablet/mobile responsive
- [ ] Color contrast passes WCAG AA
- [ ] Keyboard navigation works
- [ ] Screen reader announces content correctly
- [ ] `prefers-reduced-motion` respected (carousel)
- [ ] All tests pass
- [ ] Config registry regenerated
- [ ] Full test suite green
