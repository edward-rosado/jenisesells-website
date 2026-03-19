# Premium Visual Components — Design Spec

## Goal

Add premium section variants and scroll-triggered animations to the agent site template system. Five new components (marquee banner, testimonial spotlight, full-bleed feature blocks, parallax hero, parallax about) plus a shared scroll-reveal utility that retrofits all 10 existing templates.

## Decisions

| Question | Decision |
|----------|----------|
| Marquee style | Continuous horizontal scroll, light editorial background (greyed-out logos) |
| Testimonial showcase | Large Quote Spotlight — one review at a time, auto-rotate with fade |
| Feature highlight cards | Full-bleed blocks — full viewport width, alternating light/dark, scroll-triggered reveals |
| Scroll animation scope | Shared `useScrollReveal` hook — new components + retrofit all 10 templates |
| Parallax hero | Background image zoom (1→1.15) + upward shift on scroll |
| Parallax about | Same engine as hero, agent photo as parallax background |
| Which templates upgraded | LightLuxury, LuxuryEstate, Commercial swap to premium variants |
| Other templates | Get `useScrollReveal` only (section fade-in/slide-up on scroll) |

## Architecture

### Variant Pool Approach

New components are added to the shared section variant pool — the same architecture used by all 76 existing section components. Templates select which variants to use. No new section types are needed except `marquee`.

```
components/sections/
  heroes/HeroParallax.tsx          (NEW — "use client")
  testimonials/TestimonialsSpotlight.tsx  (NEW — "use client")
  services/ServicesPremium.tsx     (NEW — "use client")
  about/AboutParallax.tsx          (NEW — "use client")
  marquee/MarqueeBanner.tsx        (NEW — new section category)
  marquee/index.ts                 (NEW)
  types.ts                         (MODIFIED — add MarqueeProps)
hooks/
  useScrollReveal.ts               (NEW — shared utility)
  useParallax.ts                   (NEW — shared utility)
  useReducedMotion.ts              (NEW — shared useSyncExternalStore pattern)
  index.ts                         (NEW)
```

Note: Hooks go in `apps/agent-site/hooks/` (top-level), not inside `components/sections/`. This follows Next.js conventions and allows reuse outside section components.

### Data Flow

```
content.json → template → section variant → useScrollReveal/useParallax (hooks)
                                              ↓
                                    IntersectionObserver / scroll listener
                                              ↓
                                    CSS transform + opacity transitions
```

## Component Specs

### 1. MarqueeBanner

**Purpose**: Social proof banner displaying brand logos, awards, or affiliations in a continuous scroll.

**Section type**: `marquee` (new addition to `PageSections`)

**Position in page flow**: Between hero and stats.

**Data schema**:
```typescript
interface MarqueeData {
  title?: string;           // e.g. "As Featured In", "Awards & Recognition"
  items: MarqueeItem[];
}

interface MarqueeItem {
  text: string;             // Display text (e.g. "SOTHEBY'S INTERNATIONAL")
  logo_url?: string;        // Optional image logo
  link?: string;            // Optional URL
}
```

**Visual design**:
- Light background (`#fafaf8` or theme-derived from `--color-bg`)
- Optional small-caps title centered above the scroll track
- Greyed-out text/logos (`color: rgba(0,0,0,0.35)`) — subtle, not attention-grabbing
- Continuous horizontal scroll via CSS `@keyframes translateX`
- Diamond or dot separators between items
- Duplicated item list to create seamless infinite loop

**Animation**:
- Pure CSS `animation: scroll Xs linear infinite` where X = items.length * 3s
- Pauses on hover (`animation-play-state: paused`)
- `prefers-reduced-motion` → static centered row, no scroll

**Accessibility**:
- `aria-hidden="true"` on entire banner (decorative content)
- Links within items get `tabindex="-1"` AND `aria-hidden="true"` individually (some AT traverses past ancestor `aria-hidden`)
- Screen readers will not announce the marquee — this is intentional (decorative)

**CSS keyframes**: Since the codebase uses inline styles exclusively, inject a `<style>` tag in the component JSX (same pattern as `Nav.tsx` which already injects `<style>` for responsive breakpoints). The `@keyframes scroll` animation cannot be expressed as inline styles.

**Responsive**:
- Same behavior on all breakpoints (scroll track just gets narrower)

### 2. TestimonialsSpotlight

**Purpose**: Premium testimonial display showing one review at a time with auto-rotation.

**Data**: Existing `TestimonialItem[]` — no schema changes.

**Visual design**:
- Full-width section, generous vertical padding (80px+)
- Oversized opening quote mark (`"`) as decorative element, ~72px, very light opacity
- Review text: large (20-22px), italic, centered, max-width 600px
- Reviewer info below: circular avatar/initials + name + source + star rating
- Dot indicators at bottom showing position in rotation
- Optional prev/next arrow buttons (appear on hover)

**Animation**:
- Auto-rotates every 5 seconds with CSS opacity fade transition (0.6s)
- Dots are clickable for direct navigation
- Arrow keys navigate when section is focused
- `prefers-reduced-motion` → no auto-rotation, manual nav only

**Edge cases**:
- **0 items**: Section does not render (same pattern as existing testimonial variants)
- **1 item**: Render single review without dots, arrows, or auto-rotation
- **Very long text**: `max-width: 600px` with `overflow-wrap: break-word` prevents overflow

**FTC compliance**: Render `FTC_DISCLAIMER` from `components/sections/types.ts` below the dot indicators, matching the existing testimonial variants.

**Accessibility**:
- `aria-live="polite"` on the active review container
- `role="tablist"` on dot indicators, `role="tab"` on each dot
- Arrow key navigation between reviews
- Pause auto-rotation on focus within the section

### 3. ServicesPremium (Full-Bleed Feature Blocks)

**Purpose**: Full-viewport-width feature blocks that reveal on scroll with alternating layouts.

**Data**: Existing `FeatureItem[]` + optional new fields:
```typescript
// Additions to existing FeatureItem interface
interface FeatureItem {
  // ... existing fields (title, description, icon?, category?)
  background_color?: string;  // Override block background (e.g. "#1a1a2e")
  image_url?: string;         // Visual for the block (replaces emoji/gradient)
}
```

**Visual design**:
- Each feature item renders as a full-bleed block (no container max-width on the section itself)
- Content within each block has max-width ~560px for readability
- Alternating: item[0] text-left/visual-right, item[1] text-right/visual-left, etc.
- Alternating backgrounds: light → dark → light (default cycle, overridable via `background_color`)
- Small-caps label above title (derived from `category` field or omitted)
- Large title (32-36px), description text (16-17px)
- Visual side: gradient shape with icon/emoji, or actual image if `image_url` provided

**Animation** (via `useScrollReveal`):
- Text content: fade-up (opacity 0→1, translateY 40px→0) over 0.8s
- Visual: scale-in (0.92→1, opacity 0→1) over 1s, delayed 0.2s
- Each block animates independently when it enters viewport
- `prefers-reduced-motion` → all blocks render visible immediately

**Responsive**:
- Below 768px: stack vertically (visual above text), reduce padding
- Title scales down to 28px on mobile

### 4. HeroParallax

**Purpose**: Full-viewport hero with background image that zooms on scroll.

**Data**: Existing `HeroData` + optional field:
```typescript
// Addition to existing HeroData interface
interface HeroData {
  // ... existing fields (headline, tagline, body, cta_text, cta_link, etc.)
  background_image?: string;  // URL for parallax background image
}
```
Falls back to `agentPhotoUrl` prop (already available in all hero components) if `background_image` not set. Falls back to dark gradient overlay if neither is available. Note: `HeroData` does not have an `image_url` field — the background image comes from `background_image` (new) or the agent photo prop.

**Visual design**:
- Full viewport height (100vh)
- Background image covers entire section with dark overlay (rgba(0,0,0,0.45))
- Content centered: label (agent name/title), headline with accent word, body text, CTA button
- Scroll indicator at bottom ("↓" with bounce animation)

**Animation** (via `useParallax` hook):
- `requestAnimationFrame`-throttled scroll listener
- Background scales from 1.0 → 1.15 as user scrolls past hero
- Slight upward translateY (-20px at full scroll) for depth
- Background element has `inset: -10%` to prevent edge gaps during zoom
- `will-change: transform` for GPU acceleration
- `prefers-reduced-motion` → static background, no zoom/shift

**Responsive**:
- Headline: 56px → 36px below 768px
- Body: 18px → 16px
- Maintains 100vh on all breakpoints

### 5. AboutParallax

**Purpose**: Agent bio section with parallax background photo.

**Data**: Existing `AboutData` — no schema changes.

**Image priority**: `about.data.image_url` (if set) → `agent.headshot_url` (from AgentConfig/AccountConfig prop) → no parallax (falls back to solid background with no zoom effect, rendering as a standard about section).

**Visual design**:
- Full-width section with agent photo as parallax background
- Semi-transparent card overlay containing bio text and credentials
- Card: `background: rgba(255,255,255,0.92)` with backdrop blur, rounded corners, max-width 560px
- Agent name as heading, bio paragraphs, credentials as pill badges

**Animation**: Same `useParallax` hook as HeroParallax — background zooms on scroll.

**Responsive**:
- Card becomes full-width on mobile with increased opacity for readability

### 6. useScrollReveal Hook

**Purpose**: Shared utility for scroll-triggered CSS animations across all templates.

```typescript
function useScrollReveal(
  ref: RefObject<HTMLElement>,
  options?: {
    threshold?: number;    // Default: 0.15
    once?: boolean;        // Default: true (animate once, don't re-trigger)
    delay?: number;        // Default: 0 (ms)
  }
): boolean;  // returns isVisible
```

**Implementation**:
- Creates IntersectionObserver with given threshold inside `useEffect` (SSR-safe — no `window` access outside effect)
- Returns `true` when element enters viewport
- `once: true` disconnects observer after first trigger
- Uses `useReducedMotion()` hook (see below) — when true, returns `true` immediately (no animation)

**CSS patterns** (inline styles, matching existing codebase convention):
```typescript
// Fade up — applied to the container element that the ref points to
style={{
  opacity: isVisible ? 1 : 0,
  transform: isVisible ? 'translateY(0)' : 'translateY(24px)',
  transition: 'opacity 0.6s ease, transform 0.6s ease',
}}
```

**Stagger pattern** for grids — one `useScrollReveal` call on the parent container, CSS `transitionDelay` per child by index:
```typescript
// CORRECT: Single hook on the parent, stagger via CSS delay
const containerRef = useRef<HTMLDivElement>(null);
const isVisible = useScrollReveal(containerRef);

// Each child uses transitionDelay based on index
items.map((item, i) => (
  <div key={i} style={{
    opacity: isVisible ? 1 : 0,
    transform: isVisible ? 'translateY(0)' : 'translateY(24px)',
    transition: 'opacity 0.6s ease, transform 0.6s ease',
    transitionDelay: `${i * 100}ms`,
  }}>
    ...
  </div>
))
```
Note: Never call `useScrollReveal` inside `.map()` — this violates the Rules of Hooks.

**Retrofit plan** — wrap existing section content in reveal containers:

| Section Category | Animation | Applied To |
|-----------------|-----------|------------|
| Stats | Stagger fade-up (100ms per card) | All stat variants |
| Testimonials | Fade-up on section enter | All testimonial variants |
| Gallery/Sold | Stagger fade-up (100ms per card) | All gallery variants |
| Steps | Sequential reveal (150ms stagger) | All step variants |
| Services | Stagger fade-up (100ms per card) | All service variants |
| About | Fade-up on section enter | All about variants |
| Profiles | Stagger fade-up (100ms per card) | All profile variants |
| Hero | No scroll reveal (always visible on load) | None |
| Contact Form | Fade-up on section enter | CmaSection wrapper |

### 7. useReducedMotion Hook

**Purpose**: Shared `prefers-reduced-motion` detection using the established codebase pattern.

```typescript
function useReducedMotion(): boolean;
```

**Implementation**: Uses `useSyncExternalStore` with `window.matchMedia("(prefers-reduced-motion: reduce)")` — the same pattern used in `SoldCarousel.tsx`. Server snapshot returns `false` (assume motion is OK during SSR), client hydrates with actual preference. This prevents hydration mismatches.

Used by: `useScrollReveal`, `useParallax`, `TestimonialsSpotlight`, `MarqueeBanner`.

### 8. useParallax Hook

**Purpose**: Shared parallax scroll engine used by HeroParallax and AboutParallax.

```typescript
function useParallax(
  ref: RefObject<HTMLElement>,
  bgRef: RefObject<HTMLElement>,
  options?: {
    maxScale?: number;     // Default: 1.15
    maxTranslateY?: number; // Default: -20 (px)
  }
): void;
```

**Implementation**:
- All `window` access happens inside `useEffect` (SSR-safe — Cloudflare Workers has no `window` during SSR)
- Components using this hook must have `"use client"` directive
- Attaches `requestAnimationFrame`-throttled scroll listener inside `useEffect`
- Calculates progress (0→1) based on element position relative to viewport
- Applies `transform: scale(${scale}) translateY(${translateY}px)` to bgRef
- Uses `useReducedMotion()` — when true, no-op (static background)
- Cleanup: removes scroll listener on unmount

**Mobile Safari**: Touch scroll events are throttled during momentum scrolling, causing jank. Accepted as a known limitation for MVP — the effect degrades gracefully (background may stutter but content remains fully usable). Future option: detect touch device via `'ontouchstart' in window` and disable parallax.

**Multiple instances**: When both HeroParallax and AboutParallax are on the same page, each gets its own scroll listener (2 total). This is acceptable for MVP — both are rAF-throttled so only 2 rAF callbacks per frame. If more parallax sections are added later, consolidate to a single shared scroll dispatcher.

## Schema Changes

### New types in `lib/types.ts`:
```typescript
interface MarqueeData {
  title?: string;
  items: MarqueeItem[];
}

interface MarqueeItem {
  text: string;
  logo_url?: string;
  link?: string;
}
```

### New props in `components/sections/types.ts`:
```typescript
interface MarqueeProps {
  title?: string;
  items: MarqueeItem[];
}
```
This follows the convention of all other section props (e.g., `TestimonialsProps`, `FeaturesProps`). `ServicesPremium` uses the existing `FeaturesProps` interface.

### Additions to existing interfaces:
```typescript
// In PageSections — add marquee
interface PageSections {
  // ... existing sections
  marquee?: SectionConfig<MarqueeData>;
}

// In FeatureItem — add optional fields
interface FeatureItem {
  // ... existing
  background_color?: string;
  image_url?: string;
}

// In HeroData — add optional field
interface HeroData {
  // ... existing
  background_image?: string;
}
```

### Nav update:
`getEnabledSections()` dynamically iterates `Object.entries(sections)`, so adding `marquee` to `PageSections` is sufficient — no code change needed. No nav item is added for marquee — it is not navigable (sits between hero and stats as ambient content). The Nav component only shows items from `navigation.items` in content.json, so `marquee` will not accidentally appear in navigation.

### Default content:
`marquee` defaults to absent in generated content — `buildDefaultContent` in `lib/config.ts` does not add a default `marquee` section. Templates that conditionally render `s.marquee?.enabled` handle the missing key gracefully via optional chaining.

## Template Upgrade Map

### LightLuxury
- Hero: `HeroAiry` → `HeroParallax`
- Marquee: Add `MarqueeBanner` (new section)
- Services: `ServicesRefined` → `ServicesPremium`
- Testimonials: `TestimonialsQuote` → `TestimonialsSpotlight`
- About: `AboutGrace` → `AboutParallax`
- All sections: Add `useScrollReveal` wrappers

### LuxuryEstate
- Hero: `HeroDark` → `HeroParallax`
- Marquee: Add `MarqueeBanner` (new section)
- Services: `ServicesElegant` → `ServicesPremium`
- Testimonials: `TestimonialsMinimal` → `TestimonialsSpotlight`
- About: `AboutEditorial` → `AboutParallax`
- All sections: Add `useScrollReveal` wrappers

### Commercial
- Hero: Keep `HeroCorporate` (no parallax — professional/clean fits better)
- Marquee: Add `MarqueeBanner` (new section)
- Services: `ServicesCommercial` → `ServicesPremium`
- Testimonials: `TestimonialsCorporate` → `TestimonialsSpotlight`
- About: Keep `AboutProfessional` (no parallax — corporate feel)
- All sections: Add `useScrollReveal` wrappers

### All Other Templates (7)
- No section variant swaps
- All sections: Add `useScrollReveal` wrappers

## Test Account Content Updates

Update content.json for test accounts using upgraded templates to include `marquee` data:

- `test-light-luxury` — marquee with luxury brand affiliations
- `test-luxury` — marquee with awards/recognitions
- `test-commercial` — marquee with industry certifications

## Testing Strategy

### Unit Tests (per component)
- Renders correctly with minimal data
- Renders correctly with full data (all optional fields populated)
- Handles empty items array gracefully (does not render)
- Handles single item (TestimonialsSpotlight: no dots/arrows; MarqueeBanner: static display)
- DOM structure assertions (behavioral tests, not snapshots — matching existing test style)

### Hook Tests
- `useScrollReveal`: Returns `false` initially, `true` after IntersectionObserver callback fires
- `useScrollReveal`: Returns `true` immediately when `prefers-reduced-motion` is set
- `useReducedMotion`: Returns `false` on server snapshot, detects media query on client
- `useParallax`: Applies correct transform values on simulated scroll
- `useParallax`: No-op when `prefers-reduced-motion` is set

### Animation Tests
- `MarqueeBanner`: CSS animation-play-state changes on hover
- `TestimonialsSpotlight`: Auto-rotates (advance after interval) and stops on focus
- `TestimonialsSpotlight`: FTC disclaimer renders

### Integration Tests
- Each upgraded template (LightLuxury, LuxuryEstate, Commercial) renders without errors
- Marquee section appears when `marquee.enabled: true`
- Marquee section hidden when `marquee.enabled: false`
- `getEnabledSections` includes `marquee` when enabled

### Test Fixture Updates
- Add `marquee` section to shared `CONTENT` fixture in test fixtures
- Or create `CONTENT_WITH_MARQUEE` variant for template tests that need it
- Update `light-luxury.test.tsx`, `luxury-estate.test.tsx`, `commercial.test.tsx` with marquee assertions

### Accessibility Tests
- Marquee has `aria-hidden="true"` on container and individual links
- Testimonial spotlight has `aria-live="polite"`
- All animations respect `prefers-reduced-motion`
- Keyboard navigation works on testimonial dots (arrow keys)

## Non-Goals

- No new templates — only upgrading existing ones
- No Framer Motion or animation libraries — pure CSS + IntersectionObserver
- No 3D transforms or WebGL effects
- No lazy-loading images (Next.js Image handles this already)
- No animation configuration in content.json — animation behavior is determined by the component variant, not by content authors
