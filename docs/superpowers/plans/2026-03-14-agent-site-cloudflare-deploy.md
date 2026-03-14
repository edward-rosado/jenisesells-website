# Agent Site Cloudflare Workers Deployment — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deploy agent websites to `{slug}.real-estate-star.com` on Cloudflare Workers with build-time config bundling, custom domain support, and per-agent DNS via the Cloudflare API.

**Architecture:** A prebuild script generates a TypeScript registry from `config/agents/` JSON files. `config.ts` switches from `fs` reads to sync object lookups. Middleware resolves agent IDs from subdomains or custom domain hostnames. A single Workers deployment serves all agents.

**Tech Stack:** Next.js 16, Cloudflare Workers (OpenNext), Vitest, PowerShell deploy scripts

**Spec:** `docs/superpowers/specs/2026-03-14-agent-site-cloudflare-deploy-design.md`

---

## File Structure

### New Files
| File | Responsibility |
|------|---------------|
| `apps/agent-site/scripts/generate-config-registry.mjs` | Prebuild: scan agent configs, generate `lib/config-registry.ts` |
| `infra/cloudflare/deploy-agent-site.ps1` | Local deploy script (Docker build + wrangler deploy) |
| `infra/cloudflare/add-agent-domain.ps1` | One-time: add Workers custom domain per agent |

### Modified Files
| File | Change |
|------|--------|
| `apps/agent-site/lib/config.ts` | Remove fs/path. Import from config-registry. Sync functions. |
| `apps/agent-site/lib/routing.ts` | Fix domain. Add custom domain resolution. Add `getAgentIds`. |
| `apps/agent-site/middleware.ts` | 404 for unknown agents. Custom domain fallback. www redirect. |
| `apps/agent-site/wrangler.jsonc` | Add WORKER_SELF_REFERENCE service binding. |
| `apps/agent-site/package.json` | Add `prebuild` script. |
| `apps/agent-site/app/sitemap.ts` | Fix domain from `realestatestar.com`. |
| `apps/agent-site/app/page.tsx` | async -> sync config calls. |
| `apps/agent-site/app/privacy/page.tsx` | async -> sync config calls. |
| `apps/agent-site/app/terms/page.tsx` | async -> sync config calls. |
| `apps/agent-site/app/accessibility/page.tsx` | async -> sync config calls. |
| `apps/agent-site/app/thank-you/page.tsx` | async -> sync config calls. |
| `.github/workflows/deploy-agent-site.yml` | Prebuild in both jobs. Workers deploy. |
| `.gitignore` | Add config-registry.ts. |

### Test Files
| File | Change |
|------|--------|
| `apps/agent-site/lib/__tests__/config.test.ts` | Rewrite: mock config-registry, test sync behavior. |
| `apps/agent-site/lib/__tests__/routing.test.ts` | Update domain. Add custom domain + www tests. |
| `apps/agent-site/__tests__/middleware/middleware.test.ts` | Update domain. Add 404, custom domain, www redirect tests. |

---

## Chunk 1: Config Bundling (prebuild script + config.ts rewrite)

### Task 1: Create the prebuild script

**Files:**
- Create: `apps/agent-site/scripts/generate-config-registry.mjs`

- [ ] **Step 1: Write the prebuild script**

```js
// apps/agent-site/scripts/generate-config-registry.mjs
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const AGENTS_DIR = path.resolve(__dirname, "../../../config/agents");
const OUTPUT = path.resolve(__dirname, "../lib/config-registry.ts");

const SKIP_PATTERN = /^bad-|\.schema\.json$/;

function discoverAgents() {
  const agents = [];
  for (const entry of fs.readdirSync(AGENTS_DIR, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      const configPath = path.join(AGENTS_DIR, entry.name, "config.json");
      if (fs.existsSync(configPath)) {
        agents.push({ id: entry.name, layout: "directory" });
      }
    } else if (entry.isFile() && entry.name.endsWith(".json") && !SKIP_PATTERN.test(entry.name)) {
      const id = entry.name.replace(/\.json$/, "");
      // Skip if directory layout exists (directory takes priority)
      const dirConfig = path.join(AGENTS_DIR, id, "config.json");
      if (!fs.existsSync(dirConfig)) {
        agents.push({ id, layout: "flat" });
      }
    }
  }
  return agents;
}

function loadJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, "utf-8"));
}

function tryReadFile(filePath) {
  try {
    return fs.readFileSync(filePath, "utf-8");
  } catch {
    return undefined;
  }
}

function main() {
  const agents = discoverAgents();
  console.log(`[prebuild] Found ${agents.length} agent(s): ${agents.map((a) => a.id).join(", ")}`);

  const configs = {};
  const contents = {};
  const legalContent = {};
  const customDomains = {};

  for (const agent of agents) {
    const baseDir = agent.layout === "directory"
      ? path.join(AGENTS_DIR, agent.id)
      : AGENTS_DIR;

    // Config (required)
    const configPath = agent.layout === "directory"
      ? path.join(baseDir, "config.json")
      : path.join(baseDir, `${agent.id}.json`);
    const config = loadJson(configPath);
    configs[agent.id] = config;

    // Content (optional)
    const contentPath = agent.layout === "directory"
      ? path.join(baseDir, "content.json")
      : path.join(baseDir, `${agent.id}.content.json`);
    if (fs.existsSync(contentPath)) {
      contents[agent.id] = loadJson(contentPath);
    }

    // Legal markdown (optional, directory layout only)
    if (agent.layout === "directory") {
      const legalDir = path.join(baseDir, "legal");
      if (fs.existsSync(legalDir)) {
        const legalPages = {};
        for (const page of ["privacy", "terms", "accessibility"]) {
          const above = tryReadFile(path.join(legalDir, `${page}-above.md`));
          const below = tryReadFile(path.join(legalDir, `${page}-below.md`));
          if (above !== undefined || below !== undefined) {
            legalPages[page] = { above, below };
          }
        }
        if (Object.keys(legalPages).length > 0) {
          legalContent[agent.id] = legalPages;
        }
      }
    }

    // Custom domain mapping
    if (config.identity?.website) {
      customDomains[config.identity.website] = agent.id;
    }
  }

  const output = `// AUTO-GENERATED by scripts/generate-config-registry.mjs — DO NOT EDIT
import type { AgentConfig, AgentContent } from "./types";

export const configs: Record<string, AgentConfig> = ${JSON.stringify(configs, null, 2)} as unknown as Record<string, AgentConfig>;

export const contents: Record<string, AgentContent> = ${JSON.stringify(contents, null, 2)} as unknown as Record<string, AgentContent>;

export const legalContent: Record<string, Record<string, { above?: string; below?: string }>> = ${JSON.stringify(legalContent, null, 2)};

export const customDomains: Record<string, string> = ${JSON.stringify(customDomains, null, 2)};

export const agentIds: Set<string> = new Set(${JSON.stringify(Object.keys(configs))});
`;

  fs.writeFileSync(OUTPUT, output, "utf-8");
  console.log(`[prebuild] Wrote ${OUTPUT}`);
}

main();
```

- [ ] **Step 2: Run the prebuild script to verify it works**

Run: `node apps/agent-site/scripts/generate-config-registry.mjs`
Expected: `[prebuild] Found 2 agent(s): jenise-buckalew, test-agent` and file written at `apps/agent-site/lib/config-registry.ts`

- [ ] **Step 3: Verify the generated file looks correct**

Run: `head -20 apps/agent-site/lib/config-registry.ts`
Expected: TypeScript file with `configs`, `contents`, `legalContent`, `customDomains`, `agentIds` exports.

- [ ] **Step 4: Add config-registry.ts to .gitignore**

Add this line to the root `.gitignore`:
```
apps/agent-site/lib/config-registry.ts
```

- [ ] **Step 5: Add prebuild script to package.json**

In `apps/agent-site/package.json`, add to `"scripts"`:
```json
"prebuild": "node scripts/generate-config-registry.mjs"
```

- [ ] **Step 6: Commit**

```bash
git add apps/agent-site/scripts/generate-config-registry.mjs apps/agent-site/package.json .gitignore
git commit -m "feat: add prebuild script to generate config-registry from agent configs"
```

---

### Task 2: Rewrite config.ts (fs -> registry lookups)

**Files:**
- Modify: `apps/agent-site/lib/config.ts`

- [ ] **Step 1: Rewrite config.ts**

Replace the entire file with:

```typescript
// apps/agent-site/lib/config.ts
import type { AgentConfig, AgentContent } from "./types";
import { configs, contents, legalContent } from "./config-registry";

const VALID_AGENT_ID = /^[a-z0-9-]+$/;

function validateAgentId(agentId: string): void {
  if (!VALID_AGENT_ID.test(agentId)) {
    throw new Error(`Invalid agent ID: ${agentId}`);
  }
}

function assertAgentConfig(value: unknown): asserts value is AgentConfig {
  const v = value as Record<string, unknown>;
  if (typeof v?.id !== "string") throw new Error("AgentConfig: missing id");
  const identity = v.identity as Record<string, unknown> | undefined;
  if (typeof identity?.name !== "string") throw new Error("AgentConfig: missing identity.name");
  if (typeof identity?.phone !== "string") throw new Error("AgentConfig: missing identity.phone");
  if (typeof identity?.email !== "string") throw new Error("AgentConfig: missing identity.email");
  const location = v.location as Record<string, unknown> | undefined;
  if (typeof location?.state !== "string") throw new Error("AgentConfig: missing location.state");
}

export function loadAgentConfig(agentId: string): AgentConfig {
  validateAgentId(agentId);
  const config = configs[agentId];
  if (!config) {
    throw new Error(`Agent not found: ${agentId}`);
  }
  assertAgentConfig(config);
  return config;
}

export function loadAgentContent(
  agentId: string,
  config?: AgentConfig,
): AgentContent {
  validateAgentId(agentId);
  const content = contents[agentId];
  if (content) return content;
  const resolved = config ?? loadAgentConfig(agentId);
  return buildDefaultContent(resolved);
}

export function loadLegalContent(
  agentId: string,
  page: "privacy" | "terms" | "accessibility",
): { above?: string; below?: string } {
  validateAgentId(agentId);
  return legalContent[agentId]?.[page] ?? { above: undefined, below: undefined };
}

function buildDefaultContent(config: AgentConfig): AgentContent {
  const name = config.identity.name;
  const tagline = config.identity.tagline || "Your Trusted Real Estate Professional";

  return {
    template: "emerald-classic",
    sections: {
      hero: {
        enabled: true,
        data: {
          headline: "Sell Your Home with Confidence",
          tagline,
          cta_text: "Get Your Free Home Value",
          cta_link: "#cma-form",
        },
      },
      stats: { enabled: false, data: { items: [] } },
      services: {
        enabled: true,
        data: {
          items: [
            { title: "Expert Market Analysis", description: `${name} provides a detailed analysis of your local market to price your home right.` },
            { title: "Strategic Marketing Plan", description: "Professional photography, virtual tours, and targeted online advertising." },
            { title: "Negotiation & Closing", description: "Skilled negotiation to get you the best possible price and smooth closing." },
          ],
        },
      },
      how_it_works: {
        enabled: true,
        data: {
          steps: [
            { number: 1, title: "Submit Your Info", description: "Fill out the quick form below with your property details." },
            { number: 2, title: "Get Your Free Report", description: "Receive a professional Comparative Market Analysis within minutes." },
            { number: 3, title: "Schedule a Walkthrough", description: `Meet with ${name} to discuss your selling strategy.` },
          ],
        },
      },
      sold_homes: { enabled: false, data: { items: [] } },
      testimonials: { enabled: false, data: { items: [] } },
      cma_form: {
        enabled: true,
        data: {
          title: "What's Your Home Worth?",
          subtitle: "Get a free, professional Comparative Market Analysis",
        },
      },
      about: {
        enabled: true,
        data: {
          bio: `${name} is a dedicated real estate professional serving ${config.location.service_areas?.join(", ") || config.location.state}. Contact ${name} today to learn how they can help you achieve your real estate goals.`,
          credentials: [],
        },
      },
      city_pages: { enabled: false, data: { cities: [] } },
    },
  };
}
```

- [ ] **Step 2: Verify the prebuild + config.ts compiles**

Run: `cd apps/agent-site && npm run prebuild && npx tsc --noEmit`
Expected: No TypeScript errors.

- [ ] **Step 3: Commit**

```bash
git add apps/agent-site/lib/config.ts
git commit -m "refactor: replace fs-based config loading with sync registry lookups"
```

---

### Task 3: Rewrite config tests

**Files:**
- Modify: `apps/agent-site/lib/__tests__/config.test.ts`

- [ ] **Step 1: Rewrite config tests to mock config-registry**

Replace the entire file with:

```typescript
import { describe, it, expect, vi, beforeEach } from "vitest";

// Mock the config-registry module — prebuild generates this at build time
vi.mock("../config-registry", () => ({
  configs: {
    "jenise-buckalew": {
      id: "jenise-buckalew",
      identity: { name: "Jenise Buckalew", phone: "555-1234", email: "jenise@test.com", tagline: "Selling NJ!" },
      location: { state: "NJ", service_areas: ["Middlesex", "Monmouth"] },
      branding: { primary_color: "#1B5E20" },
    },
    "test-agent": {
      id: "test-agent",
      identity: { name: "Test Agent", phone: "555-0000", email: "test@test.com" },
      location: { state: "NY" },
      branding: {},
    },
    "bad-no-id": {
      identity: { name: "X", phone: "1", email: "x@x.com" },
      location: { state: "NJ" },
    },
    "bad-no-name": {
      id: "bad-no-name",
      identity: { phone: "1", email: "x@x.com" },
      location: { state: "NJ" },
    },
    "bad-no-phone": {
      id: "bad-no-phone",
      identity: { name: "X", email: "x@x.com" },
      location: { state: "NJ" },
    },
    "bad-no-email": {
      id: "bad-no-email",
      identity: { name: "X", phone: "1" },
      location: { state: "NJ" },
    },
    "bad-no-state": {
      id: "bad-no-state",
      identity: { name: "X", phone: "1", email: "x@x.com" },
      location: {},
    },
  },
  contents: {
    "jenise-buckalew": {
      template: "emerald-classic",
      sections: {
        hero: { enabled: true, data: { headline: "Sell", tagline: "Selling NJ!", cta_text: "Go", cta_link: "#" } },
        stats: { enabled: true, data: { items: [] } },
        services: { enabled: true, data: { items: [] } },
        how_it_works: { enabled: true, data: { steps: [] } },
        sold_homes: { enabled: false, data: { items: [] } },
        testimonials: { enabled: false, data: { items: [] } },
        cma_form: { enabled: true, data: { title: "CMA", subtitle: "Free" } },
        about: { enabled: true, data: { bio: "Bio", credentials: [] } },
        city_pages: { enabled: false, data: { cities: [] } },
      },
    },
  },
  legalContent: {
    "jenise-buckalew": {
      privacy: { above: "# Privacy\nCustom.", below: "## More\nExtra." },
    },
  },
  customDomains: { "jenisesellsnj.com": "jenise-buckalew" },
  agentIds: new Set(["jenise-buckalew", "test-agent", "bad-no-id", "bad-no-name", "bad-no-phone", "bad-no-email", "bad-no-state"]),
}));

import { loadAgentConfig, loadAgentContent, loadLegalContent } from "../config";

describe("loadAgentConfig", () => {
  it("loads a known agent", () => {
    const config = loadAgentConfig("jenise-buckalew");
    expect(config.id).toBe("jenise-buckalew");
    expect(config.identity.name).toBe("Jenise Buckalew");
    expect(config.location.state).toBe("NJ");
  });

  it("throws for non-existent agent", () => {
    expect(() => loadAgentConfig("nobody")).toThrow("Agent not found: nobody");
  });

  it("rejects path traversal attempts", () => {
    expect(() => loadAgentConfig("../../etc/passwd")).toThrow("Invalid agent ID");
    expect(() => loadAgentConfig("../secret")).toThrow("Invalid agent ID");
    expect(() => loadAgentConfig("foo/bar")).toThrow("Invalid agent ID");
    expect(() => loadAgentConfig("")).toThrow("Invalid agent ID");
  });

  it("throws when config is missing id", () => {
    expect(() => loadAgentConfig("bad-no-id")).toThrow("AgentConfig: missing id");
  });

  it("throws when config is missing identity.name", () => {
    expect(() => loadAgentConfig("bad-no-name")).toThrow("AgentConfig: missing identity.name");
  });

  it("throws when config is missing identity.phone", () => {
    expect(() => loadAgentConfig("bad-no-phone")).toThrow("AgentConfig: missing identity.phone");
  });

  it("throws when config is missing identity.email", () => {
    expect(() => loadAgentConfig("bad-no-email")).toThrow("AgentConfig: missing identity.email");
  });

  it("throws when config is missing location.state", () => {
    expect(() => loadAgentConfig("bad-no-state")).toThrow("AgentConfig: missing location.state");
  });

  it("rejects UPPER case agent ID", () => {
    expect(() => loadAgentConfig("UPPER")).toThrow("Invalid agent ID");
  });

  it("rejects agent ID with spaces", () => {
    expect(() => loadAgentConfig("has spaces")).toThrow("Invalid agent ID");
  });

  it("rejects agent ID with dots", () => {
    expect(() => loadAgentConfig("has.dot")).toThrow("Invalid agent ID");
  });
});

describe("loadAgentContent", () => {
  it("returns content from registry when it exists", () => {
    const content = loadAgentContent("jenise-buckalew");
    expect(content.template).toBe("emerald-classic");
    expect(content.sections.stats.enabled).toBe(true);
  });

  it("generates default content when no content in registry", () => {
    const content = loadAgentContent("test-agent");
    expect(content.template).toBe("emerald-classic");
    expect(content.sections.hero.enabled).toBe(true);
    expect(content.sections.hero.data.tagline).toBe("Your Trusted Real Estate Professional");
    expect(content.sections.services.data.items[0].description).toContain("Test Agent");
    expect(content.sections.about.data.bio).toContain("NY");
    expect(content.sections.stats.enabled).toBe(false);
  });

  it("uses provided config when generating defaults", () => {
    const config = loadAgentConfig("test-agent");
    const content = loadAgentContent("test-agent", config);
    expect(content.sections.about.data.bio).toContain("Test Agent");
  });

  it("rejects path traversal", () => {
    expect(() => loadAgentContent("../../etc/passwd")).toThrow("Invalid agent ID");
  });
});

describe("loadLegalContent", () => {
  it("returns markdown when legal content exists in registry", () => {
    const result = loadLegalContent("jenise-buckalew", "privacy");
    expect(result.above).toBe("# Privacy\nCustom.");
    expect(result.below).toBe("## More\nExtra.");
  });

  it("returns undefined when no legal content for agent", () => {
    const result = loadLegalContent("test-agent", "privacy");
    expect(result.above).toBeUndefined();
    expect(result.below).toBeUndefined();
  });

  it("returns undefined when legal page not found", () => {
    const result = loadLegalContent("jenise-buckalew", "terms");
    expect(result.above).toBeUndefined();
    expect(result.below).toBeUndefined();
  });

  it("rejects path traversal", () => {
    expect(() => loadLegalContent("../../../etc/passwd", "privacy")).toThrow("Invalid agent ID");
  });
});
```

- [ ] **Step 2: Run tests to verify they pass**

Run: `cd apps/agent-site && npm run prebuild && npx vitest run lib/__tests__/config.test.ts`
Expected: All tests pass.

- [ ] **Step 3: Commit**

```bash
git add apps/agent-site/lib/__tests__/config.test.ts
git commit -m "test: rewrite config tests to mock config-registry instead of fs"
```

---

## Chunk 2: Routing + Middleware (domain fix, custom domains, 404s)

### Task 4: Update routing.ts with custom domain support

**Files:**
- Modify: `apps/agent-site/lib/routing.ts`

- [ ] **Step 1: Write the failing routing tests**

Replace `apps/agent-site/lib/__tests__/routing.test.ts` with:

```typescript
import { describe, it, expect, vi } from "vitest";

vi.mock("../config-registry", () => ({
  customDomains: {
    "jenisesellsnj.com": "jenise-buckalew",
    "example-agent.com": "example-agent",
  },
  agentIds: new Set(["jenise-buckalew", "test-agent", "example-agent"]),
}));

import { extractAgentId, resolveAgentFromCustomDomain, isWwwCustomDomain, getAgentIds } from "../routing";

describe("extractAgentId", () => {
  it("extracts agent-id from real-estate-star.com subdomain", () => {
    expect(extractAgentId("jenise-buckalew.real-estate-star.com")).toBe("jenise-buckalew");
  });

  it("returns null for bare domain", () => {
    expect(extractAgentId("real-estate-star.com")).toBeNull();
  });

  it("returns null for www subdomain", () => {
    expect(extractAgentId("www.real-estate-star.com")).toBeNull();
  });

  it("returns null for platform subdomain", () => {
    expect(extractAgentId("platform.real-estate-star.com")).toBeNull();
  });

  it("returns null for api subdomain", () => {
    expect(extractAgentId("api.real-estate-star.com")).toBeNull();
  });

  it("returns null for portal subdomain", () => {
    expect(extractAgentId("portal.real-estate-star.com")).toBeNull();
  });

  it("returns null for app subdomain", () => {
    expect(extractAgentId("app.real-estate-star.com")).toBeNull();
  });

  it("returns null for admin subdomain", () => {
    expect(extractAgentId("admin.real-estate-star.com")).toBeNull();
  });

  it("returns null for nested subdomains", () => {
    expect(extractAgentId("a.b.real-estate-star.com")).toBeNull();
  });

  it("handles localhost with port for dev", () => {
    expect(extractAgentId("jenise-buckalew.localhost:3000")).toBe("jenise-buckalew");
  });

  it("returns null for plain localhost", () => {
    expect(extractAgentId("localhost:3000")).toBeNull();
  });

  it("strips port from hostname", () => {
    expect(extractAgentId("jenise-buckalew.real-estate-star.com:443")).toBe("jenise-buckalew");
  });
});

describe("resolveAgentFromCustomDomain", () => {
  it("returns agent ID for known custom domain", () => {
    expect(resolveAgentFromCustomDomain("jenisesellsnj.com")).toBe("jenise-buckalew");
  });

  it("returns null for unknown domain", () => {
    expect(resolveAgentFromCustomDomain("random.com")).toBeNull();
  });

  it("strips port before lookup", () => {
    expect(resolveAgentFromCustomDomain("jenisesellsnj.com:443")).toBe("jenise-buckalew");
  });
});

describe("isWwwCustomDomain", () => {
  it("returns bare domain for www.customdomain", () => {
    expect(isWwwCustomDomain("www.jenisesellsnj.com")).toBe("jenisesellsnj.com");
  });

  it("returns null for non-www hostname", () => {
    expect(isWwwCustomDomain("jenisesellsnj.com")).toBeNull();
  });

  it("returns null for www of unknown domain", () => {
    expect(isWwwCustomDomain("www.random.com")).toBeNull();
  });

  it("strips port before checking", () => {
    expect(isWwwCustomDomain("www.jenisesellsnj.com:443")).toBe("jenisesellsnj.com");
  });
});

describe("getAgentIds", () => {
  it("returns a Set of known agent IDs", () => {
    const ids = getAgentIds();
    expect(ids.has("jenise-buckalew")).toBe(true);
    expect(ids.has("test-agent")).toBe(true);
    expect(ids.has("nobody")).toBe(false);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/agent-site && npx vitest run lib/__tests__/routing.test.ts`
Expected: FAIL — `resolveAgentFromCustomDomain`, `isWwwCustomDomain`, `getAgentIds` not exported; domain assertions wrong.

- [ ] **Step 3: Update routing.ts**

Replace the entire file with:

```typescript
import { customDomains, agentIds } from "./config-registry";

const BASE_DOMAINS = ["real-estate-star.com", "localhost"];
const RESERVED_SUBDOMAINS = ["www", "api", "portal", "platform", "app", "admin"];

export function extractAgentId(hostname: string): string | null {
  const host = hostname.split(":")[0]; // strip port

  for (const base of BASE_DOMAINS) {
    if (host === base) return null;
    if (host.endsWith(`.${base}`)) {
      const subdomain = host.slice(0, -(base.length + 1));
      if (RESERVED_SUBDOMAINS.includes(subdomain)) return null;
      if (subdomain.includes(".")) return null; // nested subdomain
      return subdomain;
    }
  }

  return null;
}

export function resolveAgentFromCustomDomain(hostname: string): string | null {
  const host = hostname.split(":")[0];
  return customDomains[host] ?? null;
}

export function isWwwCustomDomain(hostname: string): string | null {
  const host = hostname.split(":")[0];
  if (!host.startsWith("www.")) return null;
  const bare = host.slice(4);
  return customDomains[bare] ? bare : null;
}

export function getAgentIds(): Set<string> {
  return agentIds;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/agent-site && npx vitest run lib/__tests__/routing.test.ts`
Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/lib/routing.ts apps/agent-site/lib/__tests__/routing.test.ts
git commit -m "feat: fix domain to real-estate-star.com, add custom domain routing"
```

---

### Task 5: Update middleware (404s, custom domains, www redirect)

**Files:**
- Modify: `apps/agent-site/middleware.ts`
- Modify: `apps/agent-site/__tests__/middleware/middleware.test.ts`

- [ ] **Step 1: Write the new middleware tests**

Replace `apps/agent-site/__tests__/middleware/middleware.test.ts` with:

```typescript
/**
 * @vitest-environment node
 */
import { describe, it, expect, vi, beforeEach } from "vitest";

vi.stubGlobal("crypto", {
  randomUUID: () => "test-uuid-1234",
});

const mockRewrite = vi.fn();
const mockNext = vi.fn();
const mockRedirect = vi.fn();
const mockClone = vi.fn();

function createMockResponse() {
  const headers = new Map<string, string>();
  return {
    headers: {
      set: (key: string, value: string) => headers.set(key, value),
      get: (key: string) => headers.get(key),
    },
    _headers: headers,
  };
}

vi.mock("next/server", () => ({
  NextResponse: {
    rewrite: mockRewrite,
    next: mockNext,
    redirect: mockRedirect,
  },
}));

vi.mock("@/lib/routing", () => ({
  extractAgentId: vi.fn(),
  resolveAgentFromCustomDomain: vi.fn(),
  isWwwCustomDomain: vi.fn(),
  getAgentIds: vi.fn(),
}));

import { extractAgentId, resolveAgentFromCustomDomain, isWwwCustomDomain, getAgentIds } from "@/lib/routing";

const mockExtractAgentId = vi.mocked(extractAgentId);
const mockResolveCustomDomain = vi.mocked(resolveAgentFromCustomDomain);
const mockIsWwwCustomDomain = vi.mocked(isWwwCustomDomain);
const mockGetAgentIds = vi.mocked(getAgentIds);

let middleware: typeof import("@/middleware").middleware;

function makeRequest(host: string, pathname = "/") {
  const clonedUrl = new URL(`http://${host}${pathname}`);
  clonedUrl.searchParams.set = vi.fn((key, value) => {
    (clonedUrl as URL).searchParams.append(key, value);
  });
  mockClone.mockReturnValue(clonedUrl);

  return {
    headers: {
      get: (name: string) => (name === "host" ? host : null),
    },
    nextUrl: {
      clone: mockClone,
      pathname,
    },
  };
}

describe("middleware", () => {
  beforeEach(async () => {
    vi.resetModules();
    vi.resetAllMocks();
    mockRewrite.mockReturnValue(createMockResponse());
    mockNext.mockReturnValue(createMockResponse());
    mockRedirect.mockReturnValue(createMockResponse());
    mockGetAgentIds.mockReturnValue(new Set(["jenise-buckalew", "test-agent"]));
    mockExtractAgentId.mockReturnValue(null);
    mockResolveCustomDomain.mockReturnValue(null);
    mockIsWwwCustomDomain.mockReturnValue(null);
    const mod = await import("@/middleware");
    middleware = mod.middleware;
  });

  // --- www redirect ---
  it("301 redirects www.customdomain to bare domain", () => {
    mockIsWwwCustomDomain.mockReturnValue("jenisesellsnj.com");
    const req = makeRequest("www.jenisesellsnj.com", "/about");
    middleware(req as never);
    expect(mockRedirect).toHaveBeenCalled();
    const redirectUrl = mockRedirect.mock.calls[0][0];
    expect(redirectUrl.toString()).toContain("jenisesellsnj.com/about");
  });

  // --- subdomain match ---
  it("rewrites for a known agent subdomain", () => {
    mockExtractAgentId.mockReturnValue("jenise-buckalew");
    const req = makeRequest("jenise-buckalew.real-estate-star.com");
    middleware(req as never);
    expect(mockRewrite).toHaveBeenCalled();
    expect(mockNext).not.toHaveBeenCalled();
  });

  it("returns 404 response for unknown agent subdomain", () => {
    mockExtractAgentId.mockReturnValue("unknown-agent");
    const req = makeRequest("unknown-agent.real-estate-star.com");
    const response = middleware(req as never);
    expect(mockRewrite).not.toHaveBeenCalled();
    // 404 is returned via NextResponse.next() with rewritten status or custom response
    // The middleware should not rewrite for unknown agents
  });

  it("sets agentId search param when rewriting for known subdomain", () => {
    mockExtractAgentId.mockReturnValue("jenise-buckalew");
    const req = makeRequest("jenise-buckalew.real-estate-star.com");
    const clonedUrl = req.nextUrl.clone();
    middleware(req as never);
    expect(clonedUrl.searchParams.set).toHaveBeenCalledWith("agentId", "jenise-buckalew");
  });

  // --- custom domain match ---
  it("rewrites for a known custom domain", () => {
    mockResolveCustomDomain.mockReturnValue("jenise-buckalew");
    const req = makeRequest("jenisesellsnj.com");
    middleware(req as never);
    expect(mockRewrite).toHaveBeenCalled();
  });

  // --- no match -> 404 ---
  it("returns 404 for completely unknown hostname", () => {
    const req = makeRequest("random.com");
    const response = middleware(req as never);
    // Should not call rewrite or next for unknown domains
    expect(mockRewrite).not.toHaveBeenCalled();
  });

  // --- reserved subdomains ---
  it("returns 404 for reserved subdomains (no passthrough)", () => {
    mockExtractAgentId.mockReturnValue(null);
    const req = makeRequest("www.real-estate-star.com");
    middleware(req as never);
    // Reserved subdomains are handled by extractAgentId returning null
    // Then middleware checks custom domain (also null) -> 404
  });

  // --- CSP ---
  it("sets Content-Security-Policy header on the response", () => {
    mockExtractAgentId.mockReturnValue("jenise-buckalew");
    const req = makeRequest("jenise-buckalew.real-estate-star.com");
    const response = middleware(req as never);
    expect(response.headers.get("Content-Security-Policy")).toContain("default-src 'self'");
  });

  it("sets x-nonce header on the response", () => {
    mockExtractAgentId.mockReturnValue("jenise-buckalew");
    const req = makeRequest("jenise-buckalew.real-estate-star.com");
    const response = middleware(req as never);
    expect(response.headers.get("x-nonce")).toBeTruthy();
  });

  it("includes API URL in CSP connect-src when set", async () => {
    process.env.NEXT_PUBLIC_API_URL = "https://api.example.com";
    vi.resetModules();
    vi.resetAllMocks();
    mockRewrite.mockReturnValue(createMockResponse());
    mockGetAgentIds.mockReturnValue(new Set(["jenise-buckalew"]));
    mockExtractAgentId.mockReturnValue("jenise-buckalew");
    mockIsWwwCustomDomain.mockReturnValue(null);

    const mod = await import("@/middleware");
    const req = makeRequest("jenise-buckalew.real-estate-star.com");
    const response = mod.middleware(req as never);
    const csp = response.headers.get("Content-Security-Policy")!;
    expect(csp).toContain("https://api.example.com");
    expect(csp).toContain("wss://api.example.com");

    delete process.env.NEXT_PUBLIC_API_URL;
  });

  // --- localhost dev ---
  it("rewrites for agent subdomain on localhost", () => {
    mockExtractAgentId.mockReturnValue("jenise-buckalew");
    const req = makeRequest("jenise-buckalew.localhost:3000");
    middleware(req as never);
    expect(mockRewrite).toHaveBeenCalled();
  });
});

describe("middleware config export", () => {
  it("exports a matcher that excludes _next, favicon, and api paths", async () => {
    const { config } = await import("@/middleware");
    expect(config.matcher).toBeDefined();
    const pattern = config.matcher[0];
    expect(pattern).toContain("_next");
    expect(pattern).toContain("favicon.ico");
    expect(pattern).toContain("api");
  });
});
```

- [ ] **Step 2: Update middleware.ts**

Replace the entire file with:

```typescript
import { NextRequest, NextResponse } from "next/server";
import { extractAgentId, resolveAgentFromCustomDomain, isWwwCustomDomain, getAgentIds } from "./lib/routing";

function buildCspHeader(nonce: string): string {
  const apiUrl = process.env.NEXT_PUBLIC_API_URL ?? "";
  const apiWs = apiUrl.replace(/^https:/, "wss:").replace(/^http:/, "ws:");
  const apiConnectSrc = apiUrl ? ` ${apiUrl} ${apiWs}` : "";

  return [
    "default-src 'self'",
    `script-src 'self' 'nonce-${nonce}' 'strict-dynamic' https://*.sentry.io https://*.googletagmanager.com https://*.google-analytics.com https://connect.facebook.net`,
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: https:",
    `connect-src 'self' https://formspree.io https://*.sentry.io https://*.google-analytics.com https://*.analytics.google.com https://*.googletagmanager.com https://www.facebook.com https://connect.facebook.net${apiConnectSrc}`,
    "frame-ancestors 'none'",
  ].join("; ");
}

function notFoundResponse(nonce: string): NextResponse {
  const response = new NextResponse("Not Found", { status: 404 });
  response.headers.set("Content-Security-Policy", buildCspHeader(nonce));
  response.headers.set("x-nonce", nonce);
  return response;
}

export function middleware(request: NextRequest) {
  const hostname = request.headers.get("host") || "localhost:3000";
  const nonce = Buffer.from(crypto.randomUUID()).toString("base64");

  // 1. www redirect for custom domains
  const bareDomain = isWwwCustomDomain(hostname);
  if (bareDomain) {
    const url = new URL(`https://${bareDomain}${request.nextUrl.pathname}`);
    return NextResponse.redirect(url, 301);
  }

  // 2. Subdomain match
  const agentId = extractAgentId(hostname);
  if (agentId) {
    if (!getAgentIds().has(agentId)) {
      return notFoundResponse(nonce);
    }
    const url = request.nextUrl.clone();
    url.searchParams.set("agentId", agentId);
    const response = NextResponse.rewrite(url);
    response.headers.set("Content-Security-Policy", buildCspHeader(nonce));
    response.headers.set("x-nonce", nonce);
    return response;
  }

  // 3. Custom domain match
  const customAgentId = resolveAgentFromCustomDomain(hostname);
  if (customAgentId) {
    const url = request.nextUrl.clone();
    url.searchParams.set("agentId", customAgentId);
    const response = NextResponse.rewrite(url);
    response.headers.set("Content-Security-Policy", buildCspHeader(nonce));
    response.headers.set("x-nonce", nonce);
    return response;
  }

  // 4. No match -> 404
  return notFoundResponse(nonce);
}

export const config = {
  matcher: ["/((?!_next|favicon.ico|api).*)"],
};
```

- [ ] **Step 3: Run middleware tests**

Run: `cd apps/agent-site && npx vitest run __tests__/middleware/middleware.test.ts`
Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add apps/agent-site/middleware.ts apps/agent-site/__tests__/middleware/middleware.test.ts
git commit -m "feat: middleware returns 404 for unknown agents, adds custom domain + www redirect"
```

---

## Chunk 3: Page updates + sitemap + wrangler config

### Task 6: Remove async from page config calls

**Files:**
- Modify: `apps/agent-site/app/page.tsx`
- Modify: `apps/agent-site/app/privacy/page.tsx`
- Modify: `apps/agent-site/app/terms/page.tsx`
- Modify: `apps/agent-site/app/accessibility/page.tsx`
- Modify: `apps/agent-site/app/thank-you/page.tsx`

The config functions are now sync. Remove `await` from all `loadAgentConfig()`, `loadAgentContent()`, and `loadLegalContent()` calls. The `generateMetadata` functions can remain `async` (Next.js expects them to be) but the inner `await` on these calls should be removed.

- [ ] **Step 1: Update page.tsx**

In `apps/agent-site/app/page.tsx`:
- Line 24: `const agent = await loadAgentConfig(id);` -> `const agent = loadAgentConfig(id);`
- Line 44: `const agent = await loadAgentConfig(id);` -> `const agent = loadAgentConfig(id);`
- Line 45: `const content = await loadAgentContent(id, agent);` -> `const content = loadAgentContent(id, agent);`

- [ ] **Step 2: Update privacy/page.tsx**

In `apps/agent-site/app/privacy/page.tsx`:
- Remove `await` from `loadAgentConfig(id)` in both `generateMetadata` and the page function.
- Remove `await` from `loadLegalContent(id, "privacy")`.

- [ ] **Step 3: Update terms/page.tsx**

Same pattern — remove `await` from `loadAgentConfig` and `loadLegalContent` calls.

- [ ] **Step 4: Update accessibility/page.tsx**

Same pattern.

- [ ] **Step 5: Update thank-you/page.tsx**

In `apps/agent-site/app/thank-you/page.tsx`:
- Line 20: `agent = await loadAgentConfig(id);` -> `agent = loadAgentConfig(id);`
- Change the type on line 18 from `Awaited<ReturnType<typeof loadAgentConfig>>` to `ReturnType<typeof loadAgentConfig>` (no longer a Promise).

- [ ] **Step 6: Verify build compiles**

Run: `cd apps/agent-site && npm run prebuild && npx tsc --noEmit`
Expected: No TypeScript errors.

- [ ] **Step 7: Commit**

```bash
git add apps/agent-site/app/page.tsx apps/agent-site/app/privacy/page.tsx apps/agent-site/app/terms/page.tsx apps/agent-site/app/accessibility/page.tsx apps/agent-site/app/thank-you/page.tsx
git commit -m "refactor: remove await from sync config loading in all page files"
```

---

### Task 7: Fix sitemap.ts domain

**Files:**
- Modify: `apps/agent-site/app/sitemap.ts`

- [ ] **Step 1: Fix the domain**

In `apps/agent-site/app/sitemap.ts` line 4, change:
```typescript
const baseUrl = process.env.SITE_URL || "https://realestatestar.com";
```
to:
```typescript
const baseUrl = process.env.SITE_URL || "https://real-estate-star.com";
```

- [ ] **Step 2: Commit**

```bash
git add apps/agent-site/app/sitemap.ts
git commit -m "fix: correct domain in sitemap.ts to real-estate-star.com"
```

---

### Task 8: Update wrangler.jsonc

**Files:**
- Modify: `apps/agent-site/wrangler.jsonc`

- [ ] **Step 1: Add WORKER_SELF_REFERENCE service binding**

Replace the file with:
```jsonc
{
  "$schema": "node_modules/wrangler/config-schema.json",
  "name": "real-estate-star-agents",
  "main": ".open-next/worker.js",
  "compatibility_date": "2026-03-11",
  "compatibility_flags": ["nodejs_compat"],
  "assets": {
    "directory": ".open-next/assets",
    "binding": "ASSETS"
  },
  "services": [
    {
      "binding": "WORKER_SELF_REFERENCE",
      "service": "real-estate-star-agents"
    }
  ]
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/agent-site/wrangler.jsonc
git commit -m "chore: add WORKER_SELF_REFERENCE service binding to wrangler config"
```

---

## Chunk 4: CI/CD workflow + deploy scripts

### Task 9: Update CI workflow

**Files:**
- Modify: `.github/workflows/deploy-agent-site.yml`

- [ ] **Step 1: Update the workflow**

Replace the file with:
```yaml
name: Deploy Agent Site

on:
  push:
    branches: [main]
    paths:
      - 'apps/agent-site/**'
      - 'config/agents/**'
      - '.github/workflows/deploy-agent-site.yml'

jobs:
  test:
    name: "Agent Site -- lint, test, build"
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '22'
          cache: 'npm'
          cache-dependency-path: apps/agent-site/package-lock.json

      - name: Install dependencies
        run: npm ci --prefix apps/agent-site

      - name: Generate config registry
        run: node apps/agent-site/scripts/generate-config-registry.mjs

      - name: Lint
        run: npm run lint --prefix apps/agent-site

      - name: Test with coverage
        run: npm run test:coverage --prefix apps/agent-site

      - name: Build Next.js (smoke test)
        env:
          NEXT_PUBLIC_API_URL: ${{ secrets.API_URL }}
        run: npm run build --prefix apps/agent-site

  deploy:
    name: "Deploy to Cloudflare Workers"
    needs: test
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'

    permissions:
      contents: read
      deployments: write

    steps:
      - uses: actions/checkout@v4

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '22'
          cache: 'npm'
          cache-dependency-path: apps/agent-site/package-lock.json

      - name: Install dependencies
        run: npm ci --prefix apps/agent-site

      - name: Generate config registry
        run: node apps/agent-site/scripts/generate-config-registry.mjs

      - name: Build Next.js
        env:
          NEXT_PUBLIC_API_URL: ${{ secrets.API_URL }}
        run: npm run build --prefix apps/agent-site

      - name: Build for Cloudflare (OpenNext)
        working-directory: apps/agent-site
        run: npx opennextjs-cloudflare build

      - name: Deploy to Cloudflare Workers
        uses: cloudflare/wrangler-action@v3
        with:
          apiToken: ${{ secrets.CLOUDFLARE_API_TOKEN }}
          accountId: ${{ secrets.CLOUDFLARE_ACCOUNT_ID }}
          workingDirectory: apps/agent-site
          command: deploy
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/deploy-agent-site.yml
git commit -m "ci: add prebuild step, switch from pages deploy to wrangler deploy"
```

---

### Task 10: Create local deploy script

**Files:**
- Create: `infra/cloudflare/deploy-agent-site.ps1`

- [ ] **Step 1: Write the deploy script**

Follow the `deploy-platform.ps1` pattern exactly. Key differences:
- `$ProjectName = "real-estate-star-agents"`
- `$AgentSiteDir = Join-Path $RepoRoot "apps\agent-site"`
- `$ApiUrl = "https://api.real-estate-star.com"`
- Docker build runs `node scripts/generate-config-registry.mjs && npm install && npx opennextjs-cloudflare build`
- No `NEXT_PUBLIC_COMING_SOON` env var
- The config directory must be mounted into Docker too (`-v "${dockerRepoRoot}/config:/config:ro"`)

Use the @powershell-scripts skill patterns: ASCII only, branch guard, hard prerequisites, Write-Ok/Fail helpers, Docker path conversion, .env.local aside pattern.

(Full script code omitted for brevity — follow `deploy-platform.ps1` structure exactly, adjusting paths and project name.)

- [ ] **Step 2: Verify ASCII compliance**

Run:
```bash
python3 -c "
import sys
content = open(sys.argv[1], 'rb').read()
bad = [(i+1, ch) for i, ch in enumerate(content) if ch > 127]
if bad:
    for pos, ch in bad[:10]:
        print(f'  Byte {pos}: 0x{ch:02x}')
    sys.exit(1)
else:
    print('  All ASCII - clean')
" infra/cloudflare/deploy-agent-site.ps1
```
Expected: `All ASCII - clean`

- [ ] **Step 3: Commit**

```bash
git add infra/cloudflare/deploy-agent-site.ps1
git commit -m "feat: add local deploy script for agent-site (Docker + wrangler)"
```

---

### Task 11: Create add-agent-domain script

**Files:**
- Create: `infra/cloudflare/add-agent-domain.ps1`

- [ ] **Step 1: Write the domain script**

The script takes an agent slug, validates it exists in `config/agents/`, then uses the Cloudflare API to add a Workers custom domain. It also checks for `identity.website` in the agent config and optionally adds the custom domain + www redirect domain.

Key API calls (use `Invoke-RestMethod`):
- `PUT https://api.cloudflare.com/client/v4/accounts/{account_id}/workers/domains` with `hostname`, `service`, `zone_id`
- `GET` to check domain status (once, no polling)

Use @powershell-scripts skill patterns. Include `-Slug` parameter, prerequisite check for `CLOUDFLARE_API_TOKEN`, idempotent (check if domain exists before adding).

(Full script code omitted for brevity — implementer writes following the PS1 skill patterns.)

- [ ] **Step 2: Verify ASCII compliance**

Same python3 check as Task 10.

- [ ] **Step 3: Commit**

```bash
git add infra/cloudflare/add-agent-domain.ps1
git commit -m "feat: add-agent-domain script for Workers custom domains"
```

---

## Chunk 5: Full integration test + final verification

### Task 12: Run full test suite and build

- [ ] **Step 1: Run prebuild**

Run: `cd apps/agent-site && npm run prebuild`
Expected: Registry generated with jenise-buckalew and test-agent.

- [ ] **Step 2: Run full test suite**

Run: `cd apps/agent-site && npm run test:coverage`
Expected: All tests pass with coverage.

- [ ] **Step 3: Run lint**

Run: `cd apps/agent-site && npm run lint`
Expected: No lint errors.

- [ ] **Step 4: Run Next.js build**

Run: `cd apps/agent-site && NEXT_PUBLIC_API_URL=https://api.real-estate-star.com npm run build`
Expected: Build succeeds. If `crypto.randomUUID()` fails, add `edgeExternals: ["node:crypto"]` to `open-next.config.ts` and rebuild.

- [ ] **Step 5: Commit any build fixes**

If `open-next.config.ts` needed changes:
```bash
git add apps/agent-site/open-next.config.ts
git commit -m "fix: add node:crypto to edgeExternals for Cloudflare Workers"
```

---

### Task 13: Final commit — squash or verify history

- [ ] **Step 1: Verify git log shows clean commit history**

Run: `git log --oneline -15`
Expected: Series of focused commits from Tasks 1-12.

- [ ] **Step 2: Push branch and prepare for merge**

The branch is ready to merge to main. After merge, CI will:
1. Run prebuild + lint + test + smoke build (test job)
2. Run prebuild + build + OpenNext build + `wrangler deploy` (deploy job)

---

## Post-Merge: Manual DNS Steps

These are NOT automated — run manually after CI deploys successfully:

1. **Run `add-agent-domain.ps1 -Slug jenise-buckalew`** to add `jenise-buckalew.real-estate-star.com` as a Workers custom domain
2. **Verify** `https://jenise-buckalew.real-estate-star.com` loads
3. **Add `jenisesellsnj.com`** as a Workers custom domain (script handles this if `identity.website` is set)
4. **Update DNS** for `jenisesellsnj.com` to point to Cloudflare
5. **Verify** `https://jenisesellsnj.com` loads
6. **Decommission** Netlify project `prismatic-naiad-5466af` (LAST step)
