---
name: CI needs config registry prebuild
description: Agent-site CI must generate config-registry.ts before tests — vitest doesn't trigger npm prebuild hook
type: feedback
---

The `config-registry.ts` file is auto-generated and gitignored. The npm `prebuild` script generates it before `next build`, but `vitest run` does NOT trigger `prebuild`.

**CI workflow must include this step before tests:**
```yaml
- name: Generate config registry
  run: node apps/agent-site/scripts/generate-config-registry.mjs
```

This was added to `.github/workflows/agent-site.yml` after CI failed with:
```
Error: Failed to resolve import "./config-registry" from "lib/config.ts". Does the file exist?
```

5 test suites failed because they depend on config.ts/routing.ts which import config-registry.
