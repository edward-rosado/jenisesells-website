---
name: edge-runtime-prebuild-pattern
description: Build-time config bundling pattern for Cloudflare Workers and other edge runtimes that lack filesystem access
type: reference
---

# Edge Runtime Prebuild Pattern

Cloudflare Workers (and similar edge runtimes like Deno Deploy) don't have a filesystem at runtime. The standard solution is a **prebuild script** that bundles data (configs, content, legal markdown, etc.) into a TypeScript module at build time.

**Pattern:**
1. Prebuild script scans source data (e.g., `config/agents/*.json`)
2. Generates a `.ts` file with `export const` declarations (object literals, Sets, Maps)
3. Runtime code imports from the generated module — all lookups are sync object access
4. Generated file is `.gitignored` (build artifact)
5. CI runs prebuild before both test and deploy jobs

**Trade-off:** Build-time flexibility (must rebuild to pick up config changes) for runtime simplicity (no fs, no network calls, instant lookups).

**Applied in:** `apps/agent-site/scripts/generate-config-registry.mjs` → `apps/agent-site/lib/config-registry.ts`
