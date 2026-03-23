---
name: frontend-feature-isolation
description: "How to add a new feature folder to platform or agent-site with barrel exports and ESLint isolation"
user-invocable: false
origin: auto-extracted
---

# Frontend Feature Isolation

**Extracted:** 2026-03-22
**Context:** Adding new features to the Real Estate Star platform or agent-site apps

## Problem

New features must be isolated so multiple agents can work in parallel without merge conflicts. Each feature needs its own directory, barrel export, tests, and ESLint import restrictions.

## Solution

### Adding a New Feature

1. **Create the feature directory:**
   ```bash
   mkdir -p apps/{app}/features/{name}/__tests__
   ```

2. **Create barrel export `features/{name}/index.ts`:**
   ```ts
   export { MyComponent } from './MyComponent';
   export { useMyHook } from './useMyHook';
   ```
   Use named re-exports only. Never `export *`.

3. **Create thin route `app/{name}/page.tsx`:**
   ```tsx
   import { MyComponent } from '@/features/{name}';
   export default function Page() { return <MyComponent />; }
   ```

4. **Add ESLint restriction in `eslint.config.mjs`:**
   ```js
   {
     files: ["apps/{app}/features/{name}/**"],
     rules: {
       "no-restricted-imports": ["error", {
         patterns: [
           { group: ["**/features/{other}/**"], message: "Features cannot cross-import." },
         ]
       }]
     }
   }
   ```

5. **Add to Feature Scope Map in `.claude/CLAUDE.md`**

## When to Use

- Creating a new page/feature in either app
- Breaking a growing feature into sub-features
- Any time you need isolated, parallel-safe code boundaries
