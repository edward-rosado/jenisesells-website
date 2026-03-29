---
name: never-break-architecture
description: NEVER break architecture dependency rules — no exceptions, no allowlists, no "temporary" violations. Extract interfaces to Domain instead.
type: feedback
---

# Architecture Rules Are Inviolable

NEVER break the project's architecture dependency rules under any circumstance. No exceptions, no allowlists, no "temporary" violations, no "we'll fix it later."

**Why:** Eddie caught a violation where `Clients.Gmail/GDrive/GDocs/GSheets` were given an allowlist exception to reference `Clients.GoogleOAuth` — breaking the "Clients must not reference other Clients" rule. The correct fix was to extract `IOAuthRefresher` interface to Domain and have clients depend on the interface only. Eddie made this a permanent rule: architecture principles are non-negotiable.

**How to apply:**
- When shared logic is needed across Clients: extract an interface to Domain, implement in one Client, wire via DI in Api
- NEVER add allowlists or exceptions to architecture tests — if a test needs to change, the code is wrong, not the test
- NEVER weaken architecture tests to make code compile
- If an implementation seems to require a cross-layer dependency, use Dependency Inversion (interface in Domain, impl in the appropriate layer)
- The dependency graph is: `Domain → nothing`, `Clients.* → Domain only`, `Data → Domain only`, `Workers.* → Domain + Workers.Shared`, `Api → everything`
- When in doubt, ask before introducing any new project dependency
