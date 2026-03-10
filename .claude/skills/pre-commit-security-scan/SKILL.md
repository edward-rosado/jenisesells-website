---
name: pre-commit-security-scan
description: Automated security scan before commits. Use when reviewing code changes for security vulnerabilities before committing. Scans for hardcoded secrets, injection vectors, unsafe patterns, and payment flow issues.
---

# Pre-Commit Security Scan

Run this scan on all staged/changed files before committing.

## Scan Categories

### 1. Hardcoded Secrets
Search for these patterns in changed files:

```
- API keys: /[a-zA-Z0-9_-]{20,}/ near "key", "secret", "token", "password"
- Stripe keys: /sk_live_[a-zA-Z0-9]+/, /sk_test_[a-zA-Z0-9]+/
- Google credentials: /AIza[a-zA-Z0-9_-]{35}/
- JWT tokens: /eyJ[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+/
- Private keys: /-----BEGIN (RSA |EC )?PRIVATE KEY-----/
- Connection strings: /Server=.*Password=/
```

**Action:** Flag and block commit. Never commit secrets.

### 2. Injection Vectors
Search for unsafe patterns:

```csharp
// SQL injection — raw string concatenation in queries
$"SELECT * FROM {table} WHERE id = {id}"

// Command injection — user input in ProcessStartInfo without ArgumentList
new ProcessStartInfo { Arguments = userInput }

// Path traversal — Path.Combine with unvalidated user input
Path.Combine(basePath, userInput)  // without GetFullPath + StartsWith check

// XSS — unencoded user data in HTML responses
$"<p>{userMessage}</p>"  // without HtmlEncode
```

**Action:** Flag with exact file:line and suggested fix.

### 3. Unsafe Communication Patterns
```javascript
// postMessage with wildcard origin
postMessage(data, '*')

// Missing origin validation on message listener
window.addEventListener("message", (e) => { /* no origin check */ })

// Iframe without sandbox
<iframe src={url}>  // missing sandbox attribute
```

### 4. Silent Error Swallowing
```csharp
// catch without logging
catch (Exception) { return "error"; }
catch (Exception ex) { /* no logger call */ }
```

### 5. Payment Flow Anti-Patterns
```
// Client-side payment completion
onPaymentComplete={() => advanceState()}  // NEVER — must use webhooks

// Price from client
var price = request.Price;  // NEVER — use server-side PriceId

// Missing idempotency
await stripe.CreateSession(...)  // without IdempotencyKey
```

## How to Run

1. Get list of changed files: `git diff --cached --name-only`
2. For each file, run the pattern checks above
3. Report findings grouped by severity:
   - **BLOCK**: Secrets, SQL injection, command injection
   - **WARN**: Silent catch, missing origin check, payment anti-patterns
   - **INFO**: Potential improvements

## Integration

This skill should be invoked:
- Before any `git commit`
- During code review
- When the security-reviewer agent runs
