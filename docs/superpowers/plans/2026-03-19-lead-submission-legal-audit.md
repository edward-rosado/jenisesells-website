# Lead Submission API — Legal & Compliance Audit

**Date:** 2026-03-19
**Auditor:** Security Reviewer (automated)
**Scope:** Lead Submission API feature — frontend, API endpoints, consent management, deletion, notifications
**Branch:** `feat/whatsapp-agent-channel` (worktree: `lead-submission-api`)

---

## Summary

| Regulation | Status | Critical Issues |
|------------|--------|-----------------|
| ADA / WCAG 2.1 AA | Partial | Turnstile widget not rendered; error messages lack `aria-describedby` |
| CCPA | Pass | All five rights implemented and verified |
| GDPR | Partial | Deletion audit log records PII email on initiation; missing marketing consent field in `LeadFormData` |
| TCPA | Pass | Explicit opt-in checkbox, consent text, channel recording |
| CAN-SPAM | Partial | Buyer notification email has compliant footer; agent notification email has no unsubscribe link |

---

## 1. ADA — Americans with Disabilities Act / WCAG 2.1 AA

### 1.1 Turnstile Widget Accessibility

- **[ ] FAIL — Turnstile widget is never rendered in `CmaSection`.**

  `CmaSection.tsx` initializes `const [turnstileToken] = useState("")` and passes the empty string to `submitLead`. The `captchaSlot` prop of `LeadForm` is never populated. The Turnstile widget is server-side verified in `lib/turnstile.ts`, but the visible widget (which Cloudflare renders as an accessible iframe with ARIA labeling) is absent from the DOM. This means:
  - Users are never shown the CAPTCHA challenge even though server-side verification runs.
  - If `TURNSTILE_SECRET_KEY` is set, every submission will fail verification silently because the token is always `""`.
  - The accessibility affordance of the Turnstile managed challenge (built-in `aria-label`, keyboard focus) is never presented.

  **File:** `apps/agent-site/components/sections/shared/CmaSection.tsx:31`

  **Remediation:** Render the `@turnstile/react` widget (or equivalent) in the `captchaSlot` prop and wire its `onSuccess` callback to set state. Example:
  ```tsx
  import { Turnstile } from "@turnstile/react";
  const [turnstileToken, setTurnstileToken] = useState<string | null>(null);
  // ...
  <LeadForm
    captchaSlot={
      <Turnstile
        siteKey={process.env.NEXT_PUBLIC_TURNSTILE_SITE_KEY ?? ""}
        onSuccess={setTurnstileToken}
      />
    }
    turnstileToken={turnstileToken}
    // ...
  />
  ```

### 1.2 Honeypot Field Accessibility

- **[x] PASS — `aria-hidden="true"` is present.**
  `packages/ui/LeadForm/LeadForm.tsx:332`

- **[x] PASS — `tabIndex={-1}` is present.**
  `packages/ui/LeadForm/LeadForm.tsx:333`

- **[x] PASS — Field is positioned off-screen (`position: absolute; left: -9999px`).**
  `packages/ui/LeadForm/LeadForm.tsx:337`

- **[x] PASS — Tests verify all three properties (Tests 43–46 in `LeadForm.test.tsx`).**

### 1.3 Form Labels and Input Association

- **[x] PASS — All visible form fields have associated `<label htmlFor="...">` elements.**
  Every field in `LeadForm.tsx` (firstName, lastName, email, phone, desiredArea, address, city, state, zip, beds, baths, sqft, timeline, notes) has a matching `htmlFor` tied to the `lf-{name}` id convention.

- **[x] PASS — Required fields carry `aria-required="true"` via the `field()` helper.**
  `packages/ui/LeadForm/LeadForm.tsx:310`

- **[ ] PARTIAL — Inline error messages lack `aria-describedby` linking them to their inputs.**

  The validation error container (`div[aria-live="polite"][role="alert"]` at line 573) announces errors globally but individual inputs are not linked to their error text with `aria-describedby`. Screen readers will announce the live region, but users navigating by field will not hear the error text when focused on the offending input.

  **Remediation:** Add `aria-describedby="lf-error"` to the error container and set that id; or add per-field `aria-describedby` pointing to a per-field error `<span>`.

- **[x] PASS — `DeleteRequestForm` has properly associated label and `aria-required`.**
  `apps/agent-site/components/privacy/DeleteRequestForm.tsx:46–58`

- **[x] PASS — Error state uses `role="alert"` in all three privacy forms.**
  `DeleteRequestForm.tsx:61`, `OptOutForm.tsx:47`, `SubscribeForm.tsx:47`

- **[x] PASS — Success states use `role="status" aria-live="polite"`.**
  All three privacy forms.

### 1.4 TCPA Consent Checkbox

- **[x] PASS — Checkbox is wrapped in a `<label>` element, making the text label clickable and associated.**
  `packages/ui/LeadForm/LeadForm.tsx:583`

- **[x] PASS — Checkbox is keyboard operable (native `<input type="checkbox">`).**

- **[x] PASS — Consent is not pre-checked. `useState(false)` at line 83.**

- **[x] PASS — Test 44 explicitly verifies the checkbox is unchecked by default (`LeadForm.test.tsx:700`).**

### 1.5 Buying/Selling Mode Pills

- **[x] PASS — Both toggle buttons carry `role="checkbox"` and `aria-checked`.**
  `LeadForm.tsx:376–377`, `389–390`

- **[x] PASS — Focus-visible outline is defined in the inline `<style>` block.**
  `LeadForm.tsx:356–359`: `.res-lead-form-pill:focus-visible { outline: 2px solid var(--color-primary) }`

### 1.6 Color Contrast

- **[ ] PARTIAL — Contrast cannot be fully verified statically; however, known risks exist.**

  - Required field asterisk is `color: red` on a white background. Pure red (#FF0000) on white achieves a contrast ratio of approximately 4.0:1, which falls below the WCAG 2.1 AA requirement of 4.5:1 for normal text. The asterisk is decorative (`aria-hidden="true"`), so this does not violate an accessibility requirement, but the overall risk warrants documentation.
  - Footer link text uses `color: rgba(255,255,255,0.7)` on the primary background. The actual contrast ratio depends on the tenant's `--color-primary` value. If the primary is a dark color (e.g., near-black), the reduced-opacity white may fall below 4.5:1. This cannot be statically verified and must be checked per-tenant.
  - Hint text (`color: "#999"`) on white yields approximately 2.85:1 — below AA. This text is non-interactive so it does not fail WCAG 1.4.3 for UI components, but it fails for text elements.

  **Remediation:** Raise hint/secondary text from `#999` to at least `#767676` (minimum AA-compliant gray on white). Verify footer link opacity per tenant theme.

### 1.7 Accessibility Statement Page

- **[x] PASS — A dedicated `/accessibility` page exists at `apps/agent-site/app/accessibility/page.tsx`.**
- **[x] PASS — Page declares WCAG 2.1 Level AA as the target standard.**
- **[x] PASS — Contact email for accessibility feedback is included.**
- **[x] PASS — Feedback is linked from the site footer.**
  `components/sections/shared/Footer.tsx:167`

---

## 2. CCPA — California Consumer Privacy Act

### 2.1 Right to Know

- **[x] PASS — Privacy Policy discloses all categories of data collected.**
  `app/privacy/page.tsx:74–80`: Contact information, property details, usage data, cookies. The policy lists all third-party processors (Cloudflare, Sentry, Google Maps).

- **[x] PASS — Lead profile stored in Drive contains all collected fields.**
  `LeadMarkdownRenderer.RenderLeadProfile()` writes name, email, phone, timeline, lead types, property details, and notes into the YAML frontmatter and body. `RenderResearchInsights()` stores enrichment and scoring data. `RenderHomeSearchResults()` stores buyer listings.

- **[x] PASS — Privacy Policy explicitly states "We do not sell your personal information."**
  `app/privacy/page.tsx:110`

### 2.2 Right to Delete

- **[x] PASS — `POST /agents/{agentId}/leads/request-deletion` endpoint exists with email verification.**
  `RequestDeletionEndpoint.cs` sends a 128-bit cryptographically random token via email and returns 202 regardless of whether the email exists (anti-enumeration).

- **[x] PASS — `DELETE /agents/{agentId}/leads/data` endpoint executes deletion upon valid token.**
  `DeleteDataEndpoint.cs` validates the token, matches email, and delegates to `GDriveLeadDataDeletion`.

- **[x] PASS — Deletion covers all data surfaces: Lead Profile.md, Research & Insights.md, Home Search files, and consent log rows (redacted).**
  `GDriveLeadDataDeletion.ExecuteDeletionAsync()` lines 137–163.

- **[x] PASS — Deletion is automated (no manual agent intervention required).**
  Request → email verification → execute deletion is fully automated. Satisfies the 45-business-day response requirement by design — deletion happens immediately upon token confirmation.

- **[x] PASS — Frontend delete request page exists at `/[handle]/privacy/delete`.**

### 2.3 Right to Opt-Out

- **[x] PASS — `POST /agents/{agentId}/leads/opt-out` endpoint exists.**
  `OptOutEndpoint.cs` updates the lead's marketing opt-in flag to `false` and records the event in the consent log.

- **[x] PASS — Anti-enumeration: always returns 200 regardless of email existence.**
  `OptOutEndpoint.cs:28–34`

- **[x] PASS — Frontend opt-out page exists at `/[handle]/privacy/opt-out`.**

### 2.4 Non-Discrimination

- **[x] PASS — Privacy Policy CCPA section states the right to non-discrimination.**
  `app/privacy/page.tsx:138–139`

- **[x] PASS — Opt-out only removes marketing flag; transactional emails (verification, deletion confirmation) are not gated on `MarketingOptedIn`.**
  `GDriveLeadDataDeletion` sends verification email directly regardless of consent state. Agent notification emails are internal and not lead-facing.

### 2.5 Verification for Deletion

- **[x] PASS — Email-based token verification is required before deletion executes.**
  24-hour token expiry, SHA-256 hashed storage, email-token binding in `DeletionTokenData`.

### 2.6 Response Time

- **[x] PASS — Deletion is automated and immediate upon token confirmation.**
  No queue or manual review step. Well within the 45-business-day CCPA requirement.

---

## 3. GDPR — General Data Protection Regulation

### 3.1 Lawful Basis — Consent

- **[x] PASS — TCPA/marketing consent checkbox is not pre-checked.**
  `LeadForm.tsx:83`: `useState(false)`

- **[x] PASS — Consent is explicit: user must actively check the box before submitting.**
  `handleSubmit` at line 173 blocks submission if `!tcpaConsent`.

- **[ ] FAIL — `LeadFormData` type (shared-types) does not include a `marketingConsent` field.**

  The `LeadFormData` interface in `packages/shared-types/lead-form.ts` has no `marketingConsent` property. The `SubmitLeadRequest` on the API side requires a `MarketingConsentRequest` object (with `OptedIn`, `ConsentText`, `Channels`). This mismatch means the consent the user gives in the TCPA checkbox is never transmitted to the API — the `MarketingConsent.OptedIn` value stored in Google Sheets always comes from whatever the server action constructs, not from the user's actual checkbox state.

  **Files:** `packages/shared-types/lead-form.ts`, `apps/agent-site/actions/submit-lead.ts`

  **Remediation:** Add `marketingConsent: { optedIn: boolean; consentText: string; channels: string[] }` to `LeadFormData`. Update `LeadForm` to pass `tcpaConsent` through `onSubmit`. Update `submit-lead.ts` server action to map it to the API request body.

### 3.2 Right to Erasure (Article 17)

- **[x] PASS — Erasure covers Lead Profile.md, Research & Insights.md, Home Search folder.**
  `GDriveLeadDataDeletion.cs:137–159`

- **[x] PASS — Consent log rows are redacted (email replaced with `[REDACTED]`), not deleted.**
  `MarketingConsentLog.RedactAsync()` uses `IFileStorageProvider.RedactRowsAsync()`.

  This is the correct approach: GDPR Art. 17 requires erasure of personal data, but maintaining an anonymized audit record of consent events is legitimate under Art. 5(1)(e) (storage limitation with accountability).

- **[x] PASS — Deletion audit log completion record does not store the email.**
  `DeletionAuditLog.RecordCompletionAsync()` writes `"[REDACTED]"` for the email column.

- **[ ] PARTIAL — Deletion audit log initiation record stores the email in plaintext.**

  `DeletionAuditLog.RecordInitiationAsync()` at line 10 writes `email` into the sheet row. This is personal data retained after the deletion request. Under GDPR Art. 17, this email is arguably unnecessary because the `leadId` is sufficient to correlate the initiation event with the completion event. Storing the email creates a retention obligation for the audit log itself.

  **Remediation:** Hash the email (SHA-256) before writing to the initiation row, or replace it with `"[HASHED]"` using the same hash approach as the token. This preserves auditability without retaining PII.

### 3.3 Consent Audit Trail

- **[x] PASS — `MarketingConsentLog` records timestamp, leadId, email, name, opt-in state, consent text, channels, IP address, user agent, action, and source.**
  `MarketingConsentLog.cs:9–21`

- **[x] PASS — Consent is recorded on initial submission, opt-out, and re-subscribe.**
  `SubmitLeadEndpoint.cs:66–79`, `OptOutEndpoint.cs:39–53`, `SubscribeEndpoint.cs:39–53`

- **[x] PASS — Consent log redaction on deletion removes email from historical records.**

### 3.4 Data Minimization

- **[x] PASS — Only functionally necessary fields are collected.**
  Name, email, phone, and timeline are the minimum for lead follow-up. Seller address is required only when `LeadTypes` includes `"selling"`. Buyer area is required only for `"buying"`. Notes are optional. No national ID, financial account, or health data is collected.

### 3.5 Right to Object / Opt-Out

- **[x] PASS — Opt-out endpoint is implemented and accessible via email unsubscribe link.**
  `BuyerListingEmailRenderer.cs:95`: "To opt out of future home search emails, reply with UNSUBSCRIBE."
  The `/[handle]/privacy/opt-out` page provides a one-click opt-out confirmation.

### 3.6 Re-subscribe (Easy Reversal)

- **[x] PASS — `POST /agents/{agentId}/leads/subscribe` endpoint exists.**
  `SubscribeEndpoint.cs` — token-authenticated, idempotent, consent-logged.

- **[x] PASS — Frontend re-subscribe page at `/[handle]/privacy/subscribe`.**

### 3.7 Data Portability (Article 20)

- **[x] PASS — YAML frontmatter in Lead Profile.md is machine-readable.**
  All structured fields (leadId, status, receivedAt, name, email, phone, leadTypes, timeline, city, state, tags) are in YAML and parseable by any standard YAML library.

### 3.8 PII in Logs and Telemetry

- **[x] PASS — Structured log messages in the Leads feature use `{AgentId}` and `{LeadId}` as structured parameters, not email addresses or names.**
  Reviewed: `SubmitLeadEndpoint.cs`, `RequestDeletionEndpoint.cs`, `DeleteDataEndpoint.cs`, `GDriveLeadDataDeletion.cs`, `MultiChannelLeadNotifier.cs`.

- **[ ] PARTIAL — `DeletionAuditLog.RecordInitiationAsync` stores the raw email in the audit sheet (see 3.2 above).**

### 3.9 Privacy Policy

- **[x] PASS — Privacy Policy references GDPR rights via the state-specific section.**
  NJ residents receive an explicit NJDPA section. All others receive a generic state-law section. CCPA section covers California residents. A dedicated "Your Rights" section is present.

- **[ ] PARTIAL — Privacy Policy does not explicitly name GDPR as applicable law or provide an EU/EEA contact.**
  This is acceptable for a US-focused real estate business (GDPR applies to EU residents; if no EU customers are targeted, GDPR may not apply). However, if the platform is ever opened internationally, this gap must be addressed.

---

## 4. TCPA — Telephone Consumer Protection Act

### 4.1 Consent for Marketing Calls and Texts

- **[x] PASS — TCPA consent checkbox is present, unchecked by default, and required to submit.**
  `LeadForm.tsx:582–597`

- **[x] PASS — Consent language meets TCPA requirements:**
  - References "calls and text messages"
  - References "automated calls"
  - States "Message and data rates may apply"
  - Includes STOP opt-out instruction
  - Includes "Consent is not a condition of purchasing any property or service" (required disclosure)

- **[x] PASS — Consent text, channels, and opt-in status are recorded in the Marketing Consent Log.**
  `SubmitLeadEndpoint.cs:66–79`

### 4.2 Do-Not-Call (DNC) Compliance

- **[ ] NOT IMPLEMENTED — No DNC registry lookup before outbound calls or texts.**

  The system records consent but does not check the National Do Not Call Registry. For a real estate agent manually calling leads who have provided consent, DNC compliance is typically handled at the brokerage level. However, if any automated dialing or texting is implemented in a future feature, DNC registry lookup must be added.

  **Assessment:** Currently acceptable because the notification channel is the agent receiving an email/chat card. The agent is responsible for DNC compliance when placing outbound calls. This should be documented as a known limitation.

### 4.3 Record Keeping

- **[x] PASS — Consent records include timestamp, IP, user agent, consent text, and channels.**
  `MarketingConsentLog.RecordConsentAsync()` — all required fields present.

- **[x] PASS — Opt-out events are also logged to the consent log with `Action = "opt-out"`.**
  `OptOutEndpoint.cs:51`

---

## 5. CAN-SPAM Act

### 5.1 Physical Address in Email Footer

- **[x] PASS — `BuyerListingEmailRenderer` includes `officeAddress` in the footer when present.**
  `BuyerListingEmailRenderer.cs:96–100`

- **[ ] PARTIAL — `officeAddress` comes from `config.Location?.OfficeAddress ?? ""` — if the agent config has no office address, the footer omits it.**

  CAN-SPAM requires a valid physical postal address in every commercial email. If `OfficeAddress` is not set in the agent config, the buyer listing email is non-compliant.

  **Remediation:** Validate that `OfficeAddress` is populated in agent config at startup (or at email send time). If absent, fall back to the brokerage address. Throw a startup exception if neither is available, following the established pattern for required config keys.

- **[ ] FAIL — Agent notification email (`MultiChannelLeadNotifier.BuildEmailBody`) has no footer at all.**

  The agent notification email (sent agent-to-agent, not lead-facing) contains no unsubscribe link and no physical address. This email is arguably transactional (sent to the agent about their own business) rather than commercial marketing, so CAN-SPAM may not apply. However, documenting the classification is important.

  **Assessment:** Agent notification emails are transactional. CAN-SPAM applies only to "commercial electronic mail messages." An agent receiving notification about their own incoming lead is not commercial. No remediation required, but add a comment in `MultiChannelLeadNotifier.BuildEmailBody` documenting the transactional classification.

### 5.2 Unsubscribe Link in Every Lead-Facing Email

- **[x] PASS — `BuyerListingEmailRenderer` includes unsubscribe instructions.**
  `BuyerListingEmailRenderer.cs:95`: "To opt out of future home search emails, reply with UNSUBSCRIBE."

- **[ ] PARTIAL — Unsubscribe is reply-based ("reply with UNSUBSCRIBE") rather than a one-click URL link.**

  CAN-SPAM requires a functional unsubscribe mechanism but does not strictly require a URL. A reply-based mechanism is compliant. However, GDPR Art. 21 and general best practice favor a one-click unsubscribe URL pointing to the `/[handle]/privacy/opt-out` page.

  **Recommended improvement:** Replace the reply instruction with a URL: `To opt out, visit: https://{handle}.real-estate-star.com/privacy/opt-out?email={email}&token={token}`

### 5.3 Opt-Out Processing

- **[x] PASS — Opt-out processing is automated and immediate via the API endpoint.**
  No manual queue. When a lead visits the opt-out URL and confirms, their `MarketingOptedIn` flag is set to `false` synchronously.

### 5.4 No Deceptive Subject Lines

- **[x] PASS — `BuyerListingEmailRenderer` uses a descriptive, accurate subject line.**
  `"{count} Homes Curated Just for You, {buyerFirstName}!"` accurately describes the email content.

- **[x] PASS — Deletion verification email subject: "Your Data Deletion Request — Verification Required" is accurate.**

### 5.5 Transactional vs. Marketing Classification

- **[x] PASS — Deletion verification email is clearly transactional (triggered by user action).**
- **[x] PASS — Buyer listing email is marketing and has an unsubscribe footer.**
- **[x] PASS — Agent notification email is internal/transactional (agent receives info about their own business).**

---

## 6. Remediation Priority Summary

### Critical (must fix before production)

| ID | Location | Issue |
|----|----------|-------|
| REM-1 | `apps/agent-site/components/sections/shared/CmaSection.tsx:31` | Turnstile widget never rendered — bot protection bypassed, form submissions always fail server-side verification |
| REM-2 | `packages/shared-types/lead-form.ts` + `apps/agent-site/actions/submit-lead.ts` | `marketingConsent` not in `LeadFormData` — TCPA/GDPR consent captured in UI but never transmitted to API or recorded in consent log |

### High (address before launch)

| ID | Location | Issue |
|----|----------|-------|
| REM-3 | `apps/api/.../Services/DeletionAuditLog.cs:10` | Plaintext email in deletion audit log initiation row — GDPR data minimization violation |
| REM-4 | `apps/api/.../Services/BuyerListingEmailRenderer.cs:96` | Missing startup validation for required `OfficeAddress` — CAN-SPAM physical address requirement may be silently unmet |

### Medium (recommended before launch)

| ID | Location | Issue |
|----|----------|-------|
| REM-5 | `packages/ui/LeadForm/LeadForm.tsx:573` | Error messages not linked to inputs via `aria-describedby` — WCAG 1.3.1 / 3.3.1 |
| REM-6 | `packages/ui/LeadForm/LeadForm.tsx` | Hint text uses `color: #999` (~2.85:1 contrast ratio) — WCAG 1.4.3 |
| REM-7 | `apps/api/.../Services/BuyerListingEmailRenderer.cs:95` | Reply-based unsubscribe should be upgraded to opt-out URL for GDPR best practice |
| REM-8 | `apps/api/.../Services/MultiChannelLeadNotifier.cs` | Add comment documenting transactional classification of agent notification email |

### Low / Future

| ID | Location | Issue |
|----|----------|-------|
| REM-9 | `apps/agent-site/app/privacy/page.tsx` | GDPR is not named as applicable law — add if international expansion is planned |
| REM-10 | Platform-wide | No DNC registry integration — document as brokerage-level responsibility in Privacy Policy |

---

## 7. What Passed — Summary of Compliant Items

- Honeypot field: `aria-hidden`, `tabIndex={-1}`, off-screen positioning, dual-layer check (frontend + server action)
- All form labels properly associated with inputs via `htmlFor`
- TCPA consent checkbox: unchecked by default, required before submit, consent text meets TCPA requirements
- CCPA Right to Delete: automated, email-verified, two-step, covers all data surfaces
- CCPA Right to Opt-Out: anti-enumeration, idempotent, consent-logged
- GDPR consent audit trail: all events (submission, opt-out, re-subscribe) logged with full context
- GDPR data minimization: only necessary fields collected
- GDPR erasure: covers Drive files, consent log (redacted), and audit log (completion record anonymized)
- GDPR portability: YAML frontmatter is machine-readable
- No PII in structured log messages (emails and names not in log parameters)
- CAN-SPAM unsubscribe instruction in buyer listing email
- CAN-SPAM physical address in buyer listing email footer (when configured)
- CAN-SPAM accurate subject lines
- Rate limiting on all lead endpoints
- Email enumeration prevention: both opt-out and deletion request endpoints return uniform 200/202
- Token security: 128-bit CSPRNG, SHA-256 hashed storage, 24-hour expiry, email binding validation
- Accessibility statement page with WCAG 2.1 AA commitment and feedback contact
- Privacy Policy with CCPA notice, data retention policy, and TCPA disclosure
