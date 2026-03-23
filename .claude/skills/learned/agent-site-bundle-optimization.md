---
name: agent-site-bundle-optimization
description: "Dynamic imports, subsection barrel imports, lazy loading patterns for 3MB Cloudflare Worker limit"
user-invocable: false
origin: auto-extracted
---

# Agent-Site Bundle Optimization

**Extracted:** 2026-03-22
**Context:** Agent-site deploys to Cloudflare Workers with a 3MB bundle limit

## Problem

The agent-site runs on Cloudflare Workers with a hard 3MB limit. Eager imports of all templates, sections, and heavy libraries bloat the bundle.

## Solution

### Dynamic Template Loading
```tsx
const templateLoaders = {
  'emerald-classic': () => import('@/features/templates/emerald-classic'),
  // ... one per template
};
const Template = await getTemplate(templateName);
```

### Subsection Barrel Imports
```tsx
// Bad — pulls all 80+ sections into bundle
import { HeroGradient } from '@/features/sections';

// Good — only this subsection
import { HeroGradient } from '@/features/sections/heroes';
```

### Lazy Sentry
```tsx
// Only load Sentry when an error occurs
catch (error) {
  const Sentry = await import('@sentry/nextjs');
  Sentry.captureException(error);
}
```

### Dynamic Turnstile
```tsx
const Turnstile = dynamic(
  () => import('@marsidev/react-turnstile').then(m => m.Turnstile),
  { ssr: false }
);
```

### Verification
```bash
npm run build -w apps/agent-site && wc -c .open-next/worker.js
```
CI warns at 2.5MB, fails at 3MB.

## When to Use

- Adding new dependencies to agent-site
- Creating new templates or sections
- Any change that could increase bundle size
