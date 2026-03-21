# Code Quality & Observability Rules

## Post-Build Smoke Check
Run these checks after writing code, before committing:

- [ ] Every `catch (Exception)` block has a `logger.Log*` call — silent swallowing hides failures
- [ ] No `handleSend()` called after `setInput()` without capturing the value first (stale closure pattern)
- [ ] No `onComplete()` / `onSuccess()` called before async operation confirms via server
- [ ] Every duplicate function name in the codebase has identical implementation — flag divergent copies
- [ ] Every string used as an enum value has a constant or actual enum backing it

## Observability Mandate
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
