---
name: pdf-timezone
description: CMA PDF footer date must use agent's local timezone based on state, not UTC
type: feedback
---

PDF footer shows "Generated Mar 29, 2026" in UTC. Must display in the agent's local timezone.

**Why:** An NJ agent generating a CMA at 11pm ET on Mar 28 would see "Generated Mar 29" — wrong date for their context.

**How to apply:** In `CmaPdfGenerator.AddFooter`, convert `DateTime.UtcNow` to the agent's timezone using `TimeZoneInfo.FindSystemTimeZoneById()` mapped from `AccountConfig.Location.State`. Use a state→timezone lookup (e.g., NJ → "Eastern Standard Time", CA → "Pacific Standard Time"). Format as `"Generated MMM d, yyyy h:mm tt tz"` (e.g., "Generated Mar 28, 2026 11:04 PM ET").
