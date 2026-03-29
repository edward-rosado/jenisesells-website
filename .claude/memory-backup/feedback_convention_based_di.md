---
name: Convention-based DI registration
description: DI should use assembly scanning with interface-to-implementation loops, not manual registration per service
type: feedback
---

DI registration should be convention-based — scan assemblies for interfaces, find implementations, register in a loop. No manual `AddSingleton<IFoo, Foo>()` for every service.

**Why:** Manual DI registration is error-prone (forget a line → runtime crash), grows linearly with services (Program.cs already has 50+ registrations), and causes DI test failures when new services are added without updating Program.cs. Eddie wants interfaces to drive the registration automatically.

**How to apply:** Refactor Program.cs to scan Domain interface assemblies and auto-register matching implementations. This should be done as part of the lead pipeline redesign (Phase 4 DI wiring fix) or as a standalone cleanup. The DiRegistrationTests should also use the same scanning approach to verify coverage.
