---
name: feedback_security_lessons
description: "Security patterns from CMA pipeline and Lead Submission API reviews — PII, HMAC, YAML injection, rate limiting, Docker"
type: feedback
---

# Security Lessons

## From CMA Pipeline Review
- **Prompt injection**: Always use `system` role for instructions, wrap user data in XML delimiters, validate LLM output shape before using in emails/docs
- **Error messages**: Never expose raw `ex.Message` (includes stderr, paths, secrets). Log full error internally, return generic message to client
- **Subprocess args**: `ArgumentList` prevents shell injection but NOT argument injection. Validate emails/inputs with regex before passing to CLI tools. Use `--` separator.
- **PII in telemetry**: Don't put street addresses in OTel span tags or structured logs. Use city/state or hashed values only.
- **Singleton + IHttpClientFactory**: Pulling HttpClient once at DI resolution defeats IHttpClientFactory purpose (DNS rotation, handler lifecycle). Use `AddHttpClient<T>()` typed registration.
- **IDOR prevention**: Always validate resource ownership (job.AgentId == routeAgentId), don't just look up by ID
- **Rate limiting behind proxy**: `RemoteIpAddress` is always the proxy IP. Use `ForwardedHeaders` middleware with configured `KnownProxies`
- **Security headers**: Always set X-Content-Type-Options, X-Frame-Options, Referrer-Policy, HSTS on every API
- **Correlation ID**: Validate length (<=64) and charset before echoing back in response headers
- **Docker**: Always add non-root USER, pin image versions (no :latest), externalize credentials via env vars

## From Lead Submission API Review (2026-03-20)
- **YAML frontmatter injection**: User input in YAML frontmatter must be double-quoted with escaped newlines/quotes. `FirstName: "John\nstatus: closed"` injects keys. See `~/.claude/skills/learned/yaml-frontmatter-injection.md`
- **Per-agent HMAC key derivation**: Never use a single shared HMAC secret across tenants. Derive per-agent: `HMAC($"{secret}:{agentId}", timestamp.body)`
- **Constant-time token comparison everywhere**: Use `CryptographicOperations.FixedTimeEquals` for ALL secret comparisons, including internal bearer tokens
- **Hash PII in structured logs**: Agent emails in `GwsService`, deletion audit logs — use `SHA256.HashData` truncated to 12 hex chars
- **Input validation on ALL DTOs**: Every request DTO needs `[Required]`, `[EmailAddress]`, `[StringLength]` + `Validator.TryValidateObject` in handler
- **ForwardedHeaders must clear KnownProxies**: Behind Cloudflare + Azure, clear `KnownNetworks` and `KnownProxies` so real client IP is resolved for rate limiting
- **DI factory for services with string params**: `AddHttpClient<T>()` fails when constructor has string params. Use named client + manual factory: `AddHttpClient(nameof(T))` + `AddSingleton<IT>(sp => new T(...))`
- **CAN-SPAM physical address fallback**: If `OfficeAddress` is missing from agent config, fall back to brokerage address — never send commercial email without physical address
- **Marketing consent must flow end-to-end**: If UI captures consent, it MUST be in the DTO, server action, and API request. Consent captured but not transmitted = compliance violation.
- **OAuth tokens encrypted at rest**: Use ASP.NET Data Protection API (`IDataProtectionProvider`) as decorator around session store. Encrypt only sensitive fields (AccessToken, RefreshToken), leave debug fields plaintext.

**Why:** These patterns prevent the exact vulnerabilities found during security review and legal compliance audit of the Lead Submission API.

**How to apply:** Run security-reviewer and legal compliance audit agents on every feature branch before PR. Check the security-checklists.md rules file for trigger conditions.
