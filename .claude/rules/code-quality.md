# Code Quality & Observability Rules

## Resilience & Retry Rules (Durable Functions)

### Google API Clients MUST Throw on Missing Credentials
- [ ] `GetCredentialAsync` / `BuildServiceAsync` NEVER returns null — always throw `InvalidOperationException`
- [ ] Gmail, GDrive, GDocs, GSheets: no silent `return;` or `return null;` or `return [];` on missing OAuth token
- [ ] The caller (activity function) gets the exception → DF retries the activity automatically
- [ ] Rationale: A silent null return means the entire pipeline proceeds with empty data, producing garbage results that look like success

### Activity Functions MUST Have try/catch/log/rethrow
- [ ] Every activity function wraps its core logic in `try { ... } catch (Exception ex) { logger.LogError(ex, "[ACTV-FN-NNN] ..."); throw; }`
- [ ] The catch block MUST include a unique `[ACTV-FN-NNN]` log code and the `agentId` for Grafana correlation
- [ ] NEVER catch and swallow — always rethrow so Durable Functions can retry
- [ ] Pattern: see `VoiceExtractionFunction.cs` as the reference implementation

### Every CallActivityAsync MUST Have a Retry Policy
- [ ] No bare `ctx.CallActivityAsync(name, input)` — always pass `TaskOptions` from the retry policy class
- [ ] Activation pipeline: use `ActivationRetryPolicies.Gather`, `.Synthesis`, `.Persist`, or `.Notify`
- [ ] Lead pipeline: use `LeadRetryPolicies.Standard` or `.EmailDelivery`
- [ ] Rationale: Without retry, a single transient failure (rate limit, token refresh race, network blip) permanently fails the activity

### Classify Activities as FATAL vs BEST-EFFORT
- [ ] **FATAL activities** (EmailFetch, DriveIndex, PersistProfile): Let exceptions propagate — the orchestration SHOULD fail if these fail after all retries
- [ ] **BEST-EFFORT activities** (EmailClassification, SynthesisMerge, ContactDetection, BrandMerge, WelcomeNotification): Wrap in try/catch in the orchestrator — log warning, continue with degraded output
- [ ] WelcomeNotification is BEST-EFFORT after PersistProfile succeeds — a notification failure must never make activation appear failed

### No Bare catch Blocks
- [ ] Every `catch` MUST have an exception type filter (`catch (JsonException ex)`, not bare `catch`)
- [ ] Every `catch` MUST have a `logger.Log*` call with a unique log code
- [ ] Returning null from a catch is acceptable (best-effort parsing), but the log must be present

## Post-Build Smoke Check
Run these checks after writing code, before committing:

- [ ] Every `catch (Exception)` block has a `logger.Log*` call — silent swallowing hides failures
- [ ] No `handleSend()` called after `setInput()` without capturing the value first (stale closure pattern)
- [ ] No `onComplete()` / `onSuccess()` called before async operation confirms via server
- [ ] Every duplicate function name in the codebase has identical implementation — flag divergent copies
- [ ] Every string used as an enum value has a constant or actual enum backing it

## Memory & OOM Prevention (Azure Consumption Plan = 1.5 GB)
For any worker, activity, or service that processes external data:

- [ ] No two memory-heavy activities dispatched in parallel from an orchestrator
- [ ] File/document processing has a max count cap (e.g. MaxStagedFiles)
- [ ] File/document processing has a max size cap — skip files over threshold
- [ ] Binary downloads (PDFs, images) batched in pairs, not all-at-once via Task.WhenAll
- [ ] Content strings released after staging/processing — never accumulated in a growing collection
- [ ] No `List<byte[]>` or `Dictionary<string, string>` holding unbounded external data
- [ ] PDF parallelism ≤ 2 (each PDF can be multi-MB in memory)
- [ ] Regex/string operations on large content use spans or bounded slices where possible

## Orchestrator Replay Safety (Durable Functions)
For any change to orchestrator code:

- [ ] Activity dispatch order matches existing in-flight instance history (or instances are purged)
- [ ] Parallel → sequential (or vice versa) is a BREAKING change — requires instance purge
- [ ] Adding/removing/reordering `CallActivityAsync` calls breaks replay — always purge first
- [ ] New activities added at the END of the orchestrator, not inserted between existing calls
- [ ] Test with both fresh instances AND replayed instances when possible

## Frontend Observability
For any frontend feature or user-facing action:

- [ ] Every form/action has lifecycle telemetry: Viewed, Started, Submitted, Succeeded, Failed
- [ ] Use `reportError()` from `@real-estate-star/analytics`, NEVER `console.error`
- [ ] Telemetry event names use PascalCase matching backend `FormEvent` enum
- [ ] API calls include `X-Correlation-ID` (auto-injected by api-client)
- [ ] Error boundaries use `reportError()` not just `console.error`
- [ ] Bundle size verified after adding dependencies (agent-site 3MB limit)

## Backend Observability Mandate
For any new feature with 3+ endpoints:

- [ ] ActivitySource with spans for key operations
- [ ] Meter with counters for business events (sessions created, states reached, payments)
- [ ] Structured logging with correlation IDs on all service methods
- [ ] No PII in span tags or log fields (hash or omit street addresses, emails in telemetry)

## Deduplication Triggers
Flag for refactoring when detected:

- Functions with the same name in different files but different implementations
- String constants used in multiple places without a shared constant
- Domain logic duplicated between tools and services (e.g., slug generation in multiple files)

## Test Quality Gates
Enforce these before marking test coverage as complete:

- [ ] Every `catch` block in production code has a test that triggers it
- [ ] Every HTML-producing endpoint has content assertions (not just `Assert.NotNull`)
- [ ] Every file I/O operation has a path traversal test case
- [ ] Every webhook handler has signature validation tests (valid, invalid, missing)
- [ ] Every state machine transition has a test, including terminal state
- [ ] Frontend components with user interaction have behavioral tests (click, type, submit)
- [ ] Serialization roundtrip tested for all domain types stored in JSON/DB
- [ ] Concurrent access scenarios tested for shared-state services
- [ ] Every `IAsyncEnumerable` / streaming endpoint has at least one integration-style test

- [ ] Every `LeadMarkdownRenderer.Render*` method has a roundtrip test (render → parse frontmatter → assert values match)
- [ ] Every `IFileStorageProvider` implementation (GDrive, Local) has symmetric test coverage for all methods
- [ ] Every YAML frontmatter field has an injection test (newlines, colons, quotes in value don't break parsing)
- [ ] Lead submission endpoint has test for: valid lead, missing required field, invalid email format, oversized payload
