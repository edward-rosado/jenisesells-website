---
name: arch-tests-belt-and-suspenders
description: Always maintain BOTH reflection-based and NetArchTest architecture tests — overlap is intentional, never remove one because the other covers it
type: feedback
---

# Architecture Tests: Belt-and-Suspenders (Keep Both)

Keep both DependencyTests.cs (reflection) and LayerTests.cs (NetArchTest) even where they overlap. When adding new architecture constraints, add tests to BOTH suites.

**Why:** Eddie wants maximum confidence that architecture violations are caught. The two test suites operate at different levels (assembly refs vs type-level namespace usage) and catch different edge cases. The overlap is intentional redundancy, not waste.

**How to apply:**
- When adding a new project: add it to the InlineData whitelist in DependencyTests.cs AND add NetArchTest assertions in LayerTests.cs
- When adding a new architectural constraint: write it as both a reflection test AND a NetArchTest
- Never remove a test from one suite because the other suite "already covers it"
- Before writing any code that changes project dependencies, read both test files to understand what will break
- Run `dotnet test` on Architecture.Tests before committing any structural changes
