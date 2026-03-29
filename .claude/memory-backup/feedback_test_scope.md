---
name: Platform Test File Scope
description: Map of which test files cover which components/pages — must check ALL affected test files when changing ANY page copy or component text
type: feedback
---

# Platform Test File Scope — All Pages & Components

When changing copy/text on ANY page or component, ALL test files that reference that text may need updating. Check every one before running tests. This applies to every page in the app, not just the landing page.

## Landing Page (`apps/platform/app/page.tsx`)

| Test File | What It Tests |
|-----------|--------------|
| `__tests__/landing.test.tsx` | Full page render: headline, price, CTA button, disclaimer, section headings, form, navigation |
| `__tests__/components/ComparisonTable.test.tsx` | ComparisonTable: column headers, prices, feature rows, accessibility |
| `__tests__/components/TrustStrip.test.tsx` | TrustStrip: trust item labels and count |
| `__tests__/components/FinalCta.test.tsx` | FinalCta: heading, CTA link text + href, subheading/value prop |
| `__tests__/components/FeatureCards.test.tsx` | FeatureCards: section heading, card count, card content |
| `__tests__/components/GeometricStar.test.tsx` | GeometricStar SVG component |

## Onboard Page (`apps/platform/app/onboard/page.tsx`)

| Test File | What It Tests |
|-----------|--------------|
| `__tests__/onboard.test.tsx` | Session creation, payment flows, Coming Soon gate, error states |

## Chat Components

| Test File | What It Tests |
|-----------|--------------|
| `__tests__/components/ChatWindow.test.tsx` | SSE streaming, message sending, auto-send, cards, auth headers |
| `__tests__/components/MessageBubble.test.tsx` | Message rendering, roles, styling |

## Layout & Legal Pages

| Test File | What It Tests |
|-----------|--------------|
| `__tests__/layout.test.tsx` | Root layout, metadata, fonts, ConsentBanner |
| `__tests__/components/ConsentBanner.test.tsx` | Cookie consent banner |
| `__tests__/components/MessageRenderer.test.tsx` | Card rendering (CMA progress, branding, etc.) |
| `__tests__/components/CmaProgressCard.test.tsx` | CMA progress card states |

## Rules

1. **Check ALL affected test files** before running tests when changing ANY page or component text. A component change affects both its own test AND any integration test that renders the parent page.
2. **Component tests duplicate integration test assertions.** When copy changes, update BOTH the component-level test AND the page-level integration test.
3. **Text appearing in multiple components** (e.g. "$10/mo" in TrustStrip AND FinalCta) causes `getByText` to find multiple matches on full-page renders — use `getAllByText` in that case.
4. **Before changing any text/copy**, grep the `__tests__/` directory for the text being changed to find ALL test files that assert on it.
