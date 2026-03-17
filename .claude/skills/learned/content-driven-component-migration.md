---
name: content-driven-component-migration
description: "Pattern for migrating hardcoded UI content to JSON config with typed fallbacks in agent-site"
user-invocable: false
origin: auto-extracted
---

# Content-Driven Component Migration Pattern

**Extracted:** 2026-03-17
**Context:** When agent-site components have hardcoded copy/data that should be customizable per agent via content.json

## Problem
Agent-site components (Nav, CmaSection, ThankYou, Footer) contain hardcoded text, navigation items, or contact info that differs per agent. New agents can't customize their site without code changes.

## Solution

### Step 1: Add optional typed field to content types
```typescript
// types.ts — always optional to preserve backward compatibility
export interface CmaFormData {
  title: string;
  subtitle: string;
  description?: string;  // NEW — optional
}
```

### Step 2: Add content to content.json
```json
{
  "cma_form": {
    "data": {
      "title": "Ready to Make a Move?",
      "description": "Selling? Enter your address... **100% free.**"
    }
  }
}
```

### Step 3: Component renders content with fallback
```tsx
// For section data — render only when present (no fallback needed)
{data.description && <p>{data.description}</p>}

// For structural data (nav, contacts) — provide DEFAULT_* constant
const navItems = navigation?.items ?? DEFAULT_NAV_ITEMS;

// For page data — provide DEFAULT_* object
const thankYou = content.pages?.thank_you ?? DEFAULT_THANK_YOU;
```

### Step 4: Template interpolation for dynamic values
```typescript
function interpolate(template: string, vars: Record<string, string>): string {
  return template.replace(/\{(\w+)\}/g, (_, key) => vars[key] ?? `{${key}}`);
}
// Usage: interpolate("Call {firstName}: {phone}", { firstName, phone })
```

### Step 5: Regenerate config-registry
```bash
node scripts/generate-config-registry.mjs
```

### Step 6: Update test fixtures
Add the new field to CONTENT fixture in fixtures.ts. Add tests for both paths (content provided vs fallback).

## Content placement rules
| Data type | Where | Example |
|-----------|-------|---------|
| Identity/legal (name, license, phone) | config.json | Footer contact info |
| Marketing copy (headlines, descriptions) | content.json sections | CmaSection description |
| Navigation structure | content.json navigation | Nav items |
| Contact display preferences | content.json contact_info | Preferred phone, extensions |
| Standalone page copy | content.json pages.{page} | ThankYou heading/body |

## When to Use
- Component has hardcoded strings that should vary per agent
- New agents need different copy without code changes
- Content is marketing/UX (→ content.json), not identity/legal (→ config.json)
