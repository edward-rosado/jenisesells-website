---
name: dotnet-max-isolation-architecture
description: Eddie's architecture of choice for ALL .NET API projects — maximum dependency isolation with Domain-centric design, enforced at compile-time and CI-time
type: feedback
---

# .NET API Architecture of Choice — Maximum Dependency Isolation

Eddie has chosen this architecture as his standard for **every future .NET API project**, not just Real Estate Star. Apply this pattern whenever creating or restructuring a .NET backend.

## What Changed (PR #33 + PR #34)

A monolithic `RealEstateStar.Api` with ~160 files mixing HTTP endpoints, domain models, workers, storage, clients, and notifications was restructured into **19 isolated production projects + 23 test projects**.

Before: one project where everything sees everything — a change to Google Drive storage could accidentally touch lead processing. Workers could see HTTP DTOs. Clients were embedded in feature folders.

After: each project has **exactly one dependency on Domain** (except Workers which add Workers.Shared). Api is the sole composition root that wires implementations via DI. No project outside Api knows that Clients.*, Data, or DataServices exist.

## The Core Design Principle: Single Domain Dependency

```
Domain         → nothing (owns ALL interfaces, models, enums)
Data           → Domain only
Clients.*      → Domain only (11 isolated client projects)
DataServices   → Domain only
Notifications  → Domain only
Workers.Shared → Domain only
Workers.*      → Domain + Workers.Shared
Api            → everything (sole composition root)
```

**The rule:** Every non-Api project depends on **Domain and nothing else**. Domain defines every interface in the system — storage, client, sender, pipeline. Implementations live in their respective projects but are invisible to each other. Only Api sees all implementations and wires them together via DI.

## The "Who Answers What?" Separation

Each project exists to answer exactly one question. If you can't state which question a project answers, it shouldn't exist.

| Project | Question | Example |
|---------|----------|---------|
| **Domain** | "What should happen?" | Lead models, CMA interfaces, business rules. Zero deps. |
| **Data** | "How do we physically read/write?" | Local filesystem provider, in-memory provider |
| **DataServices** | "Where do we store this?" | Route to Drive vs local based on config, manage folder structure |
| **Notifications** | "Who needs to know?" | Email the agent, WhatsApp the seller, post chat card |
| **Workers.*** | "How do we do it?" | One per pipeline: Leads, CMA, HomeSearch, WhatsApp |
| **Clients.*** | "What external services?" | One per provider: Anthropic, Stripe, GDrive, Gmail, etc. |
| **Api** | "Who's asking?" | HTTP → validate → authorize → hand to Domain |

## Why Each Separation Matters

### Domain owns ALL interfaces
Every interface (`ILeadStore`, `IAnthropicClient`, `IEmailSender`, `ICmaPdfGenerator`) lives in Domain. This means any project can depend on Domain to get the contracts it needs without pulling in implementations. A Worker calls `ILeadStore` — it doesn't know or care whether that's Google Drive or local filesystem.

### Clients are fully isolated (one per provider)
Each external API gets its own project with its own DTOs, HttpClient config, and Polly resilience policies. `Clients.WhatsApp` knows about Meta Graph API request/response shapes. `Clients.Stripe` knows about Stripe's webhook format. Neither knows the other exists. Swapping a provider means changing one Client project and Api's DI wiring — nothing else.

**Client DTO boundary:** Clients define their own internal request/response DTOs for HTTP communication. The Domain interface they implement uses Domain types. The Client maps between its DTOs and Domain types internally. No other project ever sees Client DTOs.

### Workers are pipelines, not HTTP handlers
Workers have completely different lifecycles than endpoints (long-running BackgroundServices vs request-scoped handlers). Separating them means workers can't accidentally reference ASP.NET Core, can't see HTTP DTOs, and can't touch storage directly. Workers call Domain interfaces — `ILeadStore`, `IAnthropicClient` — never the real implementations.

### Notifications vs DataServices distinction
"Who needs to know?" (Notifications) is different from "Where do we store this?" (DataServices). Sending an email about a new lead is a notification. Writing the lead file to Google Drive is storage. They change for different reasons: a new notification channel (Slack) doesn't affect storage; a new storage provider (S3) doesn't affect notifications.

### Data vs DataServices distinction
Data is pure physical I/O — "write these bytes to this path." DataServices is orchestration — "this lead goes in the agent's Drive folder under `{Lead Name}/{Address}/`." DataServices makes routing decisions (Drive vs local based on config). Data just reads and writes.

## Architecture Enforcement (Three Layers)

1. **Compile-time (free):** `.csproj` `<ProjectReference>` — if a project doesn't reference another, the code won't compile. This is the primary guard.

2. **CI-time assembly-level (31 tests in `DependencyTests.cs`):** Reflection-based tests that scan assembly references. Catches violations the csproj might miss (e.g., a transitive reference that sneaks in via a NuGet package). Includes:
   - Per-project allowed dependency whitelist (19 InlineData theory cases)
   - Api-only guards: only Api may reference Clients.*, DataServices, Data, Notifications
   - No circular dependencies (DFS cycle detection)
   - Workers don't reference ASP.NET Core
   - Clients don't reference other Clients
   - Domain interfaces aren't duplicated outside Domain

3. **CI-time type-level (10 tests in `LayerTests.cs`):** NetArchTest.Rules tests that scan individual C# types for namespace dependencies. Catches sneaky `using` statements that compile because of transitive NuGet refs but violate the architecture. E.g., a Domain type that `using`s a DataServices namespace would compile (no direct project ref needed if a shared NuGet brings it in) but the NetArchTest catches it.

## Why This Architecture (Eddie's Rationale)

1. **Blast radius control**: A bug in Clients.Stripe can't cascade into the lead pipeline because they don't know each other exist. Only Api (the composition root) sees both.

2. **Testability without mocks-of-mocks**: Each project has at most 2 dependencies (Domain, or Domain + Workers.Shared), so unit tests mock 1-2 interfaces max. No deep mock chains. No integration test headaches.

3. **Enforceability**: The rules are enforced at three levels — compile-time, CI assembly-level, CI type-level. 41 architecture tests total. You can't accidentally violate the architecture and get past CI.

4. **Onboarding clarity**: New developers can understand any project by reading its dependencies. "Workers.Leads depends on Domain and Workers.Shared" tells you its entire world. No hidden coupling.

5. **Parallel development**: Teams can work on separate Clients, Workers, or Notifications projects without merge conflicts because they share no code except Domain interfaces.

6. **Swap-ability**: Need to replace Stripe? Only Clients.Stripe and Api's DI wiring change. Workers, Notifications, DataServices — untouched. Need to add a new notification channel? Only Notifications and Api's DI wiring. The blast radius of any change is contained to 1-2 projects.

7. **Monolith prevention**: The restructure immediately caught duplicate CMA pipeline registrations (6 `ICompSource` instances instead of 3, doubling scrape cost) and duplicate HMAC middleware that had been hiding in the monolithic Program.cs. Separation makes these bugs impossible — each pipeline is registered once in its own project.

## How to Apply to a New .NET API Project

1. **Start with Domain** — pure models, interfaces, enums, zero deps. Every interface in the system lives here.
2. **Create Api** as the sole composition root — HTTP endpoints, DI wiring, middleware.
3. **Split by the question each project answers:**
   - Need physical file I/O? → Data
   - Need storage routing/orchestration? → DataServices
   - Need to notify people? → Notifications
   - Need a background pipeline? → Workers.{PipelineName}
   - Need to call an external API? → Clients.{ProviderName}
4. **Add Architecture.Tests from day one** with NetArchTest + reflection tests.
5. **Each project gets its own test project** — `tests/RealEstateStar.{Project}.Tests/` + shared `TestUtilities`.

**How to apply:** Every time you create a .NET project or add a new project to an existing solution, check that it follows the single-Domain-dependency rule. If a new project needs to reference something other than Domain (or Domain + Workers.Shared for workers), that's a design smell — push the interface into Domain instead.
