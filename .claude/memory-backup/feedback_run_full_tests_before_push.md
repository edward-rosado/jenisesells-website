---
name: run-full-test-suite-before-push
description: Always run the FULL dotnet test suite (not filtered) before pushing — integration tests catch issues targeted runs miss
type: feedback
---

Always run the full `dotnet test` suite before pushing, not just filtered/targeted tests.

**Why:** Targeted test runs (`--filter "SubmitLead"`) passed locally but the full CI run failed because integration tests (`LeadSubmission_FullSubmissionFlowTests`) weren't included. This caused 3 failed CI runs before the issue was caught. Integration tests use `WebApplicationFactory` with real DI wiring and catch interface changes that unit tests miss.

**How to apply:**
- After making interface changes (new methods on `ILeadNotifier`, new constructor params), run `dotnet test` without filters
- Integration tests in `LeadSubmissionIntegrationTests.cs` create a full `WebApplicationFactory` — they catch missing mock setups and DI wiring issues
- If tests take too long locally, at minimum run them in the background and check results before pushing
