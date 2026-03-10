---
name: dotnet-endpoint-audit
description: Audit new .NET minimal API endpoints for security, auth, validation, and compliance. Use when creating or reviewing HTTP endpoints.
---

# .NET Endpoint Audit

Run this audit whenever a new endpoint is created or an existing endpoint is modified.

## Checklist

### Authentication & Authorization
- [ ] Endpoint has `.RequireAuthorization()` or explicit auth policy (unless intentionally public)
- [ ] Public endpoints are documented with a comment explaining why they're unauthenticated
- [ ] IDOR protection: resource ownership validated (`job.AgentId == routeAgentId`), not just lookup by ID
- [ ] Session/token validation before any state mutation

### Input Validation
- [ ] All route parameters validated (format, length, allowed characters)
- [ ] All query string parameters validated
- [ ] Request body validated with data annotations or FluentValidation
- [ ] File paths from user input: regex allowlist + `Path.GetFullPath` + `StartsWith(basePath)`
- [ ] URLs from user input: domain allowlist + scheme check (HTTPS only)
- [ ] Email addresses validated with regex before use in external services

### Rate Limiting & Security Headers
- [ ] Rate limiting applied (unless webhook endpoint)
- [ ] CORS configured for the endpoint's needs
- [ ] Security headers set: `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`
- [ ] Correlation ID validated (length <= 64, charset check) before echoing in response

### Error Handling
- [ ] No raw `ex.Message` in HTTP responses (log internally, return generic message)
- [ ] Appropriate HTTP status codes (400 for validation, 401 for auth, 403 for authz, 404 for not found)
- [ ] Problem Details format for error responses
- [ ] All `catch` blocks log the exception

### Observability
- [ ] Structured logging on entry and exit
- [ ] ActivitySource span for the operation
- [ ] No PII in log fields or span tags
- [ ] Business metrics counted (requests, successes, failures)

### Testing
- [ ] Happy path test
- [ ] Auth failure test (401/403)
- [ ] Validation failure test (400)
- [ ] Not found test (404)
- [ ] Concurrent access test (if stateful)

## REPR Pattern Compliance
- [ ] Endpoint class implements `IEndpoint`
- [ ] Handle method is `internal static`
- [ ] Request/Response DTOs in the same operation folder
- [ ] No business logic in the endpoint — delegate to services
