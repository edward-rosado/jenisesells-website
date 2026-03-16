# Pre-Launch Legal, ADA & SEO Compliance Fixes

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix all CRITICAL and HIGH compliance findings from the 2026-03-15 audit so both the platform and agent-site apps are legally defensible, ADA-compliant, and Google-indexable before go-live.

**Architecture:** Two independent Next.js apps (apps/platform, apps/agent-site) share a UI package (packages/ui). Legal content on agent-site uses markdown templates rendered via MarkdownContent component. Platform legal pages are inline JSX. Both apps deploy to Cloudflare Workers via OpenNext.

**Tech Stack:** Next.js 16, TypeScript, Vitest, @testing-library/react, Cloudflare Workers

**Source of truth:** Compliance audit conducted 2026-03-15 across 3 parallel agents (agent-site audit, platform audit, NJ legal research). Findings cross-referenced against N.J.A.C. 11:5-6.1, TCPA 47 U.S.C. 227, CAN-SPAM 15 U.S.C. 7701, FTC 16 CFR Part 255, WCAG 2.1 AA, and Google Maps Platform ToS.

---

## File Structure

### Files to Create
| File | Purpose |
|------|---------|
| `apps/platform/public/robots.txt` | Platform crawler directives + sitemap pointer |
| `apps/platform/app/sitemap.ts` | Platform sitemap for search engines |
| `apps/agent-site/public/robots.txt` | Agent-site crawler directives + sitemap pointer |
| `apps/platform/app/onboard/layout.tsx` | Onboard page metadata (page is "use client") |

### Files to Modify
| File | Change |
|------|--------|
| `apps/platform/app/layout.tsx` | OG meta tags, footer contrast fix |
| `apps/platform/app/privacy/page.tsx` | Add TCPA + CAN-SPAM + NJ privacy sections |
| `apps/platform/app/terms/page.tsx` | Add NJREC disclaimer + NJ Consumer Fraud Act |
| `apps/platform/components/chat/GoogleAuthCard.tsx` | Add aria-label to SVGs |
| `apps/agent-site/components/sections/Footer.tsx` | NJREC prominence fix, Fair Housing statement, Google attribution |
| `apps/agent-site/components/sections/Testimonials.tsx` | FTC endorsement disclosure |
| `apps/agent-site/components/sections/CmaForm.tsx` | CMA "not an appraisal" disclaimer |
| `apps/agent-site/app/sitemap.ts` | Add legal pages to sitemap |
| `apps/agent-site/app/layout.tsx` | JSON-LD structured data |
| `packages/ui/LeadForm/LeadForm.tsx` | TCPA consent checkbox |

### Test Files to Modify
| File | Change |
|------|--------|
| `apps/platform/__tests__/layout.test.tsx` | Contrast fix assertions |
| `apps/agent-site/__tests__/components/Footer.test.tsx` | NJREC, Fair Housing, Google attribution |
| `apps/agent-site/__tests__/components/Testimonials.test.tsx` | FTC disclosure |
| `apps/agent-site/__tests__/components/CmaForm.test.tsx` | CMA disclaimer |
| `packages/ui/LeadForm/LeadForm.test.tsx` | TCPA checkbox |

---

## Chunk 1: CRITICAL Legal Fixes

### Task 1: TCPA Consent Checkbox on LeadForm (shared component)

**Why:** TCPA violations are $500-$1,500 per text/call. Lead forms collect phone numbers but have no consent language. The checkbox must be unchecked by default.

**Files:**
- Modify: `packages/ui/LeadForm/LeadForm.tsx`
- Test: `packages/ui/LeadForm/LeadForm.test.tsx`

- [ ] **Step 1: Write the failing test**

In `packages/ui/LeadForm/LeadForm.test.tsx`, add a test that asserts the TCPA checkbox exists and is unchecked by default:

```tsx
it("renders TCPA consent checkbox unchecked by default", () => {
  render(<LeadForm {...DEFAULT_PROPS} />);
  const checkbox = screen.getByRole("checkbox", { name: /consent to receive/i });
  expect(checkbox).toBeInTheDocument();
  expect(checkbox).not.toBeChecked();
});
```

- [ ] **Step 2: Write a test that submit is blocked without TCPA consent**

```tsx
it("blocks submit when TCPA consent is not checked", async () => {
  const onSubmit = vi.fn();
  render(<LeadForm {...DEFAULT_PROPS} onSubmit={onSubmit} />);
  // Fill required fields (use existing fillForm helper)
  await fillForm();
  await userEvent.click(screen.getByRole("button", { name: /get started/i }));
  expect(onSubmit).not.toHaveBeenCalled();
  expect(screen.getByText(/you must consent/i)).toBeInTheDocument();
});
```

- [ ] **Step 3: Write a test that submit succeeds with TCPA consent checked**

```tsx
it("allows submit when TCPA consent is checked", async () => {
  const onSubmit = vi.fn();
  render(<LeadForm {...DEFAULT_PROPS} onSubmit={onSubmit} />);
  await fillForm();
  await userEvent.click(screen.getByRole("checkbox", { name: /consent to receive/i }));
  await userEvent.click(screen.getByRole("button", { name: /get started/i }));
  expect(onSubmit).toHaveBeenCalled();
});
```

- [ ] **Step 4: Run tests to verify they fail**

```bash
cd packages/ui && npx vitest run LeadForm/LeadForm.test.tsx --reporter=verbose
```

Expected: 3 new tests FAIL

- [ ] **Step 5: Implement TCPA checkbox in LeadForm.tsx**

Add to `LeadForm.tsx` state:
```tsx
const [tcpaConsent, setTcpaConsent] = useState(false);
```

Add validation in `handleSubmit` (before the existing state validation):
```tsx
if (!tcpaConsent) {
  setValidationError("You must consent to receive communications before submitting.");
  return;
}
```

Add checkbox JSX before the submit button:
```tsx
<label style={{ display: "flex", alignItems: "flex-start", gap: "8px", fontSize: "12px", color: "#666", textAlign: "left", marginTop: "16px" }}>
  <input
    type="checkbox"
    checked={tcpaConsent}
    onChange={(e) => setTcpaConsent(e.target.checked)}
    style={{ marginTop: "2px", flexShrink: 0 }}
  />
  <span>
    By checking this box, you consent to receive calls and text messages from the agent
    at the phone number you provided, including automated calls. Message and data rates
    may apply. Reply STOP to opt out. Consent is not a condition of purchasing any
    property or service.
  </span>
</label>
```

- [ ] **Step 6: Run tests to verify they pass**

```bash
cd packages/ui && npx vitest run LeadForm/LeadForm.test.tsx --reporter=verbose
```

Expected: All tests PASS

- [ ] **Step 7: Commit**

```bash
git add packages/ui/LeadForm/LeadForm.tsx packages/ui/LeadForm/LeadForm.test.tsx
git commit -m "feat: add TCPA consent checkbox to LeadForm

Required by 47 U.S.C. 227 -- unchecked by default, blocks submit
until checked. Consent language includes automated calls disclosure,
opt-out instructions, and not-a-condition-of-purchase statement."
```

---

### Task 2: NJREC Compliance in Agent-Site Footer

**Why:** N.J.A.C. 11:5-6.1 requires broker name in larger/more prominent text than agent name, brokerage office phone in prominent size, and a business designation (e.g., "Licensed Real Estate Salesperson"). The current footer shows broker name at 20px and agent name at 22px -- agent name is larger, violating the rule.

**Files:**
- Modify: `apps/agent-site/components/sections/Footer.tsx`
- Test: `apps/agent-site/__tests__/components/Footer.test.tsx`

- [ ] **Step 1: Write the failing test**

In `Footer.test.tsx`, add tests for NJREC compliance:

```tsx
it("renders brokerage name more prominently than agent name", () => {
  render(<Footer agent={AGENT} />);
  const brokerage = screen.getByText(AGENT.identity.brokerage);
  const agentName = screen.getByText(/Jenise Buckalew/);
  // Brokerage font size (24px) >= agent name font size (22px)
  expect(brokerage).toHaveStyle({ fontSize: "24px" });
  expect(agentName).toHaveStyle({ fontSize: "22px" });
});

it("renders business designation", () => {
  render(<Footer agent={AGENT} />);
  expect(screen.getByText(/Licensed Real Estate/)).toBeInTheDocument();
});

it("renders office phone prominently", () => {
  render(<Footer agent={AGENT} />);
  expect(screen.getByLabelText(/call office/i)).toBeInTheDocument();
});
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd apps/agent-site && npx vitest run __tests__/components/Footer.test.tsx --reporter=verbose
```

- [ ] **Step 3: Fix font sizes in Footer.tsx**

Change brokerage name from `fontSize: "20px"` to `fontSize: "24px"` and `fontWeight: 700`.

The agent name stays at `fontSize: "22px"`. This ensures brokerage is more prominent per NJREC rules.

Verify the business designation line (line 54) already renders `identity.title || "Licensed Real Estate Salesperson"` -- this satisfies the business designation requirement. If `identity.title` is something like "REALTOR" that is also acceptable per N.J.A.C. 11:5-6.1.

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd apps/agent-site && npx vitest run __tests__/components/Footer.test.tsx --reporter=verbose
```

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/sections/Footer.tsx apps/agent-site/__tests__/components/Footer.test.tsx
git commit -m "fix: NJREC compliance -- brokerage name more prominent than agent name

N.J.A.C. 11:5-6.1 requires broker name in larger print than agent
name on all advertising including websites."
```

---

### Task 3: NJ Fair Housing Statement in Agent-Site Footer

**Why:** NJ Law Against Discrimination (N.J.S.A. 10:5-1) adds protected classes beyond federal Fair Housing Act (gender identity, source of lawful income, domestic partnership status, etc.). The footer has an EHO logo but no NJ-specific fair housing statement.

**Files:**
- Modify: `apps/agent-site/components/sections/Footer.tsx`
- Test: `apps/agent-site/__tests__/components/Footer.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
it("renders NJ fair housing statement with expanded protected classes", () => {
  render(<Footer agent={AGENT} />);
  expect(screen.getByText(/gender identity/i)).toBeInTheDocument();
  expect(screen.getByText(/source of lawful income/i)).toBeInTheDocument();
});
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Add fair housing statement to Footer.tsx**

After the existing EHO logo block (line 116), add:

```tsx
<p
  style={{
    fontSize: "10px",
    color: "rgba(255,255,255,0.6)",
    maxWidth: "700px",
    margin: "8px auto 0",
    lineHeight: 1.5,
  }}
>
  We are committed to compliance with federal and New Jersey fair housing laws.
  We do not discriminate on the basis of race, color, religion, sex, national origin,
  disability, familial status, ancestry, marital status, domestic partnership or
  civil union status, affectional or sexual orientation, gender identity or expression,
  or source of lawful income.
</p>
```

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/sections/Footer.tsx apps/agent-site/__tests__/components/Footer.test.tsx
git commit -m "feat: add NJ fair housing statement to agent-site footer

N.J.S.A. 10:5-1 adds protected classes beyond federal Fair Housing
Act including gender identity, source of lawful income, and domestic
partnership status."
```

---

### Task 4: CMA "Not an Appraisal" Disclaimer

**Why:** NJ regulators and NAR Responsible Valuation Policy require clear distinction between CMAs and licensed appraisals. The CmaForm section has no disclaimer.

**Files:**
- Modify: `apps/agent-site/components/sections/CmaForm.tsx`
- Test: `apps/agent-site/__tests__/components/CmaForm.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
it("renders CMA disclaimer stating it is not an appraisal", () => {
  render(<CmaForm {...API_PROPS} />);
  expect(screen.getByText(/not an appraisal/i)).toBeInTheDocument();
  expect(screen.getByText(/licensed appraiser/i)).toBeInTheDocument();
});
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Add disclaimer to CmaForm.tsx**

After the `<LeadForm>` component (before the closing `</div>`), add:

```tsx
<p
  style={{
    fontSize: "11px",
    color: "#999",
    marginTop: "16px",
    textAlign: "center",
    maxWidth: "600px",
    marginLeft: "auto",
    marginRight: "auto",
  }}
>
  This Comparative Market Analysis is not an appraisal. It is an estimate of market
  value based on comparable sales data and market conditions. It should not be used in
  lieu of an appraisal for lending purposes. Only a licensed appraiser can provide an
  appraisal.
</p>
```

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/sections/CmaForm.tsx apps/agent-site/__tests__/components/CmaForm.test.tsx
git commit -m "feat: add CMA disclaimer -- not an appraisal

Required by NJ regulators and NAR Responsible Valuation Policy.
Distinguishes CMA from licensed appraisal."
```

---

### Task 5: FTC Endorsement Disclosure on Testimonials

**Why:** FTC Rule on Consumer Reviews (effective Oct 2024) requires clear and conspicuous disclosure of review source. Fines up to $50,120 per violation. Current "Verified customer reviews from Zillow" text is present but needs strengthening.

**Files:**
- Modify: `apps/agent-site/components/sections/Testimonials.tsx`
- Test: `apps/agent-site/__tests__/components/Testimonials.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
it("renders FTC endorsement disclosure", () => {
  render(<Testimonials items={ITEMS} />);
  expect(screen.getByText(/unedited excerpts/i)).toBeInTheDocument();
  expect(screen.getByText(/individual results may vary/i)).toBeInTheDocument();
});
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Update disclosure text in Testimonials.tsx**

Change the existing disclaimer paragraph (lines 43-52) to:

```tsx
<p
  style={{
    textAlign: "center",
    color: "#999",
    fontSize: "13px",
    marginBottom: "45px",
  }}
>
  Unedited excerpts from verified customer reviews on Zillow. No compensation was
  provided for these reviews. Individual results may vary.
</p>
```

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/sections/Testimonials.tsx apps/agent-site/__tests__/components/Testimonials.test.tsx
git commit -m "fix: strengthen FTC endorsement disclosure on testimonials

FTC 16 CFR Part 255 requires clear disclosure of review source,
no-compensation statement, and typical results caveat."
```

---

### Task 6: Platform Privacy Policy -- TCPA + CAN-SPAM Sections

**Why:** Platform collects phone numbers and sends emails. Missing TCPA consent language ($500-$1,500/violation) and CAN-SPAM compliance ($43,280/violation).

**Files:**
- Modify: `apps/platform/app/privacy/page.tsx`

- [ ] **Step 1: Add TCPA section after Section 3 (How We Use Your Data)**

Add a new `<Section title="4. SMS and Phone Communications (TCPA)">` with:
- Consent disclosure language
- Opt-out instructions (Reply STOP)
- "Not a condition of purchase" statement
- Message frequency and data rate notice

Content:

```tsx
<Section title="4. SMS and Phone Communications (TCPA)">
  <p>
    If you or your leads opt in to SMS/text message features, the
    recipient explicitly consents to receive automated text messages to
    the phone number provided. By opting in, the recipient acknowledges:
  </p>
  <ul>
    <li>They are the account owner or authorized user of the phone number</li>
    <li>They consent to receive recurring automated messages</li>
    <li>Message and data rates may apply</li>
    <li>To opt out, reply STOP to any message</li>
    <li>Consent is not a condition of purchasing any property or service</li>
  </ul>
  <p>
    We comply with the Telephone Consumer Protection Act (47 U.S.C.
    &sect; 227) and maintain records of all opt-in consents.
  </p>
</Section>
```

Renumber all subsequent sections (4->5, 5->6, etc.).

- [ ] **Step 2: Add CAN-SPAM section after the new TCPA section**

Add a new `<Section title="5. Email Communications (CAN-SPAM)">`:

```tsx
<Section title="5. Email Communications (CAN-SPAM)">
  <p>
    All marketing emails sent through Real Estate Star comply with the
    CAN-SPAM Act (15 U.S.C. &sect; 7701):
  </p>
  <ul>
    <li>All emails include the agent&apos;s business physical address</li>
    <li>All emails allow recipients to unsubscribe via a clear link</li>
    <li>We process unsubscribe requests within 3 business days</li>
    <li>We do not use deceptive subject lines or headers</li>
    <li>
      AI-generated email drafts are subject to the same CAN-SPAM
      requirements as manually composed emails
    </li>
  </ul>
  <p>
    Transactional emails (account confirmations, payment receipts) are
    exempt from marketing opt-out requirements per FTC guidance.
  </p>
</Section>
```

Renumber all subsequent sections.

- [ ] **Step 3: Add NJ Data Privacy Rights section**

Add after the CCPA section (which becomes Section 10):

```tsx
<Section title="11. New Jersey Privacy Rights">
  <p>
    If you are a New Jersey resident, under the New Jersey Data Privacy
    Act (N.J.S.A. 56:8-166), you have the right to confirm, access,
    correct, delete, and obtain a portable copy of your personal data.
    You also have the right to opt out of the sale of personal data,
    targeted advertising, and profiling. We honor browser-based opt-out
    signals (Global Privacy Control).
  </p>
  <p>
    To exercise your rights, contact us at{" "}
    <a
      href="mailto:privacy@real-estate-star.com"
      className="text-emerald-400 underline hover:text-emerald-300"
    >
      privacy@real-estate-star.com
    </a>
    . We will respond within 45 days.
  </p>
</Section>
```

- [ ] **Step 4: Update "Last Updated" date to March 15, 2026**

Change line 16:
```tsx
<p className="text-sm text-gray-400 mb-8">
  Effective Date: March 11, 2026 | Last Updated: March 15, 2026
</p>
```

- [ ] **Step 5: Commit**

```bash
git add apps/platform/app/privacy/page.tsx
git commit -m "feat: add TCPA, CAN-SPAM, and NJ privacy sections to platform privacy policy

47 U.S.C. 227 (TCPA), 15 U.S.C. 7701 (CAN-SPAM), and
N.J.S.A. 56:8-166 (NJDPA) compliance."
```

---

### Task 7: Platform Terms -- NJREC + NJ Consumer Fraud Act

**Why:** Platform facilitates NJ real estate workflows but Terms make no mention of NJREC regulations or NJ Consumer Fraud Act responsibilities.

**Files:**
- Modify: `apps/platform/app/terms/page.tsx`

- [ ] **Step 1: Add NJREC disclaimer to Section 3 (Eligibility)**

After the existing eligibility bullets and the licensure verification paragraph, add:

```tsx
<p>
  <strong>New Jersey Real Estate Commission (NJREC) Notice:</strong>{" "}
  Real Estate Star is a software tool and does not provide brokerage,
  appraisal, or legal services. All agents must maintain current NJREC
  licensure and comply with N.J.A.C. 11:5 rules. Your broker retains
  full supervisory responsibility for all transactions and marketing
  materials generated through this platform. Licensure by the NJ Real
  Estate Commission does not imply endorsement.
</p>
```

- [ ] **Step 2: Add NJ Consumer Fraud Act disclaimer to Section 7 (CMA)**

After the existing CMA bullets, add:

```tsx
<p>
  Under the New Jersey Consumer Fraud Act (N.J.S.A. 56:8-1), agents
  are prohibited from presenting CMA estimates as definitive property
  values. All market data and valuations are estimates only. Agents
  assume liability for misrepresenting estimates as professional
  appraisals.
</p>
```

- [ ] **Step 3: Add advertising compliance to Section 8 (Website Deployment)**

Add to existing paragraph:

```tsx
<p>
  All agent websites must comply with NJREC advertising rules (N.J.A.C.
  11:5-6.1), including displaying broker name more prominently than
  agent name, including brokerage office phone number, and displaying
  appropriate business designation. If referencing commission rates, the
  statement &quot;In New Jersey commissions are negotiable&quot; must
  appear clearly.
</p>
```

- [ ] **Step 4: Update "Last Updated" date to March 15, 2026**

- [ ] **Step 5: Commit**

```bash
git add apps/platform/app/terms/page.tsx
git commit -m "feat: add NJREC and NJ Consumer Fraud Act disclaimers to platform terms

N.J.A.C. 11:5-6.1 broker supervision, N.J.S.A. 56:8-1 consumer
protection, and advertising compliance requirements."
```

---

## Chunk 2: SEO & Indexability

### Task 8: robots.txt for Both Apps

**Files:**
- Create: `apps/platform/public/robots.txt`
- Create: `apps/agent-site/public/robots.txt`

- [ ] **Step 1: Create platform robots.txt**

Create `apps/platform/public/robots.txt`:

```
User-agent: *
Allow: /
Disallow: /onboard
Disallow: /onboard/*

Sitemap: https://platform.real-estate-star.com/sitemap.xml
```

- [ ] **Step 2: Create agent-site robots.txt**

Create `apps/agent-site/public/robots.txt`:

```
User-agent: *
Allow: /

Sitemap: https://real-estate-star.com/sitemap.xml
```

- [ ] **Step 3: Commit**

```bash
git add apps/platform/public/robots.txt apps/agent-site/public/robots.txt
git commit -m "feat: add robots.txt for platform and agent-site

Directs crawlers, blocks onboarding flow from indexing, and
points to sitemap."
```

---

### Task 9: Platform Sitemap + Expand Agent-Site Sitemap

**Files:**
- Create: `apps/platform/app/sitemap.ts`
- Modify: `apps/agent-site/app/sitemap.ts`

- [ ] **Step 1: Create platform sitemap**

Create `apps/platform/app/sitemap.ts`:

```typescript
import type { MetadataRoute } from "next";

export default function sitemap(): MetadataRoute.Sitemap {
  const base = "https://platform.real-estate-star.com";
  return [
    { url: base, lastModified: new Date(), changeFrequency: "weekly", priority: 1.0 },
    { url: `${base}/privacy`, lastModified: new Date("2026-03-15"), changeFrequency: "yearly", priority: 0.5 },
    { url: `${base}/terms`, lastModified: new Date("2026-03-15"), changeFrequency: "yearly", priority: 0.5 },
    { url: `${base}/dmca`, lastModified: new Date("2026-03-11"), changeFrequency: "yearly", priority: 0.3 },
    { url: `${base}/accessibility`, lastModified: new Date("2026-03-11"), changeFrequency: "yearly", priority: 0.3 },
  ];
}
```

- [ ] **Step 2: Expand agent-site sitemap**

Replace `apps/agent-site/app/sitemap.ts` content with:

```typescript
import type { MetadataRoute } from "next";

export default function sitemap(): MetadataRoute.Sitemap {
  const baseUrl = process.env.SITE_URL || "https://real-estate-star.com";
  return [
    { url: baseUrl, lastModified: new Date(), changeFrequency: "weekly", priority: 1.0 },
    { url: `${baseUrl}/privacy`, lastModified: new Date("2026-03-15"), changeFrequency: "yearly", priority: 0.5 },
    { url: `${baseUrl}/terms`, lastModified: new Date("2026-03-15"), changeFrequency: "yearly", priority: 0.5 },
    { url: `${baseUrl}/accessibility`, lastModified: new Date("2026-03-11"), changeFrequency: "yearly", priority: 0.3 },
  ];
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/platform/app/sitemap.ts apps/agent-site/app/sitemap.ts
git commit -m "feat: add platform sitemap and expand agent-site sitemap

Includes all public pages with lastModified dates and priority."
```

---

### Task 10: OG Meta Tags -- Platform

**Files:**
- Modify: `apps/platform/app/layout.tsx`
- Create: `apps/platform/app/onboard/layout.tsx`

- [ ] **Step 1: Update metadata export in layout.tsx**

Replace the existing metadata export (lines 8-11) with:

```typescript
export const metadata: Metadata = {
  title: "Real Estate Star",
  description: "14 days free, then $14.99/mo. Your business, automated by AI.",
  openGraph: {
    title: "Real Estate Star -- AI-Powered Real Estate Automation",
    description: "Deploy your website, generate CMAs, and manage leads. 14 days free.",
    url: "https://platform.real-estate-star.com",
    type: "website",
    siteName: "Real Estate Star",
  },
  twitter: {
    card: "summary",
    title: "Real Estate Star",
    description: "AI-Powered Real Estate Automation for Agents",
  },
  alternates: {
    canonical: "https://platform.real-estate-star.com",
  },
};
```

Note: OG image skipped for now -- requires creating a 1200x630 image asset. Can be added later.

- [ ] **Step 2: Add onboard page metadata**

Since `onboard/page.tsx` is `"use client"`, create `apps/platform/app/onboard/layout.tsx`:

```typescript
import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Onboard Your Business | Real Estate Star",
  description: "Set up your AI-powered agent website in 10 minutes. 14 days free.",
};

export default function OnboardLayout({ children }: { children: React.ReactNode }) {
  return children;
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/platform/app/layout.tsx apps/platform/app/onboard/layout.tsx
git commit -m "feat: add OG meta tags and canonical URL to platform

Improves social sharing previews and prevents duplicate content."
```

---

### Task 11: JSON-LD Structured Data -- Agent Site

**Why:** Enables rich snippets in Google search results for real estate agents.

**Files:**
- Modify: `apps/agent-site/app/layout.tsx`

- [ ] **Step 1: Add JSON-LD structured data to layout**

Note: The JSON-LD uses `dangerouslySetInnerHTML` which is safe here because the content is a static object we control (no user input). This is the standard Next.js pattern for structured data.

Update `apps/agent-site/app/layout.tsx`:

```tsx
import "./globals.css";

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  const structuredData = {
    "@context": "https://schema.org",
    "@type": "RealEstateAgent",
    "url": process.env.SITE_URL || "https://real-estate-star.com",
  };

  return (
    <html lang="en">
      <head>
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: JSON.stringify(structuredData) }}
        />
      </head>
      <body>
        <a href="#main-content" className="skip-nav">
          Skip to main content
        </a>
        <main id="main-content" tabIndex={-1}>
          {children}
        </main>
      </body>
    </html>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/agent-site/app/layout.tsx
git commit -m "feat: add JSON-LD RealEstateAgent structured data to agent-site

Enables rich snippets in Google search results."
```

---

## Chunk 3: ADA/WCAG 2.1 AA Fixes

### Task 12: Google SVG aria-labels in Platform

**Why:** Screen readers cannot describe informative SVGs without aria-label. WCAG 2.1 AA 1.1.1.

**Files:**
- Modify: `apps/platform/components/chat/GoogleAuthCard.tsx`

- [ ] **Step 1: Add aria-label to both SVG elements**

Line 49 -- the display logo SVG:
```tsx
<svg className="w-10 h-10" viewBox="0 0 24 24" role="img" aria-label="Google logo" xmlns="http://www.w3.org/2000/svg">
```

Line 78 -- the button SVG:
```tsx
<svg className="w-5 h-5" viewBox="0 0 24 24" role="img" aria-label="Google logo" xmlns="http://www.w3.org/2000/svg">
```

- [ ] **Step 2: Commit**

```bash
git add apps/platform/components/chat/GoogleAuthCard.tsx
git commit -m "fix: add aria-label to Google SVGs in GoogleAuthCard

WCAG 2.1 AA requires meaningful alt text on informative images."
```

---

### Task 13: Footer Color Contrast Fix -- Platform

**Why:** `text-gray-600` (#4b5563) on dark background fails WCAG AA 4.5:1 contrast ratio (currently ~2.8:1).

**Files:**
- Modify: `apps/platform/app/layout.tsx`
- Test: `apps/platform/__tests__/layout.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
it("version text uses accessible contrast class", () => {
  render(<RootLayout><div /></RootLayout>);
  const version = screen.getByTestId("version");
  expect(version.className).toContain("text-gray-500");
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/platform && npx vitest run __tests__/layout.test.tsx --reporter=verbose
```

- [ ] **Step 3: Change `text-gray-600` to `text-gray-500` on line 60**

```tsx
<p className="mt-1 text-gray-500 text-xs" data-testid="version">v1.0</p>
```

`text-gray-500` (#6b7280) on `bg-gray-950` gives ~4.6:1 contrast -- passes WCAG AA.

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd apps/platform && npx vitest run __tests__/layout.test.tsx --reporter=verbose
```

- [ ] **Step 5: Commit**

```bash
git add apps/platform/app/layout.tsx apps/platform/__tests__/layout.test.tsx
git commit -m "fix: improve footer version text contrast to meet WCAG AA 4.5:1

Changed text-gray-600 to text-gray-500 for sufficient contrast
on dark background."
```

---

### Task 14: Google Maps "Powered by Google" Attribution -- Agent Site

**Why:** Google Maps Platform ToS requires "Powered by Google" attribution when using Places Autocomplete.

**Files:**
- Modify: `apps/agent-site/components/sections/Footer.tsx`
- Test: `apps/agent-site/__tests__/components/Footer.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
it("renders Google Maps attribution", () => {
  render(<Footer agent={AGENT} />);
  expect(screen.getByText(/powered by google/i)).toBeInTheDocument();
});
```

- [ ] **Step 2: Add "Powered by Google" to footer**

After the fair housing statement (added in Task 3), add:

```tsx
<p
  style={{
    fontSize: "10px",
    color: "rgba(255,255,255,0.5)",
    marginTop: "8px",
  }}
>
  Address autocomplete powered by Google Maps.
</p>
```

- [ ] **Step 3: Run tests**

```bash
cd apps/agent-site && npx vitest run __tests__/components/Footer.test.tsx --reporter=verbose
```

- [ ] **Step 4: Commit**

```bash
git add apps/agent-site/components/sections/Footer.tsx apps/agent-site/__tests__/components/Footer.test.tsx
git commit -m "feat: add Google Maps attribution to agent-site footer

Required by Google Maps Platform Terms of Service when using
Places Autocomplete."
```

---

## Chunk 4: Final Verification

### Task 15: Run Full Test Suites

- [ ] **Step 1: Run packages/ui tests**

```bash
cd packages/ui && npx vitest run --reporter=verbose
```

Expected: All tests pass including new TCPA tests.

- [ ] **Step 2: Run platform tests**

```bash
cd apps/platform && npx vitest run --reporter=verbose
```

Expected: All tests pass including contrast and metadata tests.

- [ ] **Step 3: Run agent-site tests**

```bash
cd apps/agent-site && npx vitest run --reporter=verbose
```

Expected: All tests pass including Footer, Testimonials, and CmaForm tests.

- [ ] **Step 4: Run lint on all apps**

```bash
cd apps/platform && npx next lint
cd apps/agent-site && npx next lint
```

- [ ] **Step 5: Verify builds**

```bash
cd apps/platform && npm run build
cd apps/agent-site && npm run build
```

---

### Task 16: Coverage Verification

- [ ] **Step 1: Run coverage for packages/ui**

```bash
cd packages/ui && npx vitest run --coverage
```

- [ ] **Step 2: Run coverage for platform**

```bash
cd apps/platform && npx vitest run --coverage
```

- [ ] **Step 3: Run coverage for agent-site**

```bash
cd apps/agent-site && npx vitest run --coverage
```

All must maintain 100% branch coverage per project requirements.

---

## Deferred Items (Not in This Plan)

These were identified in the audit but are lower priority and can be addressed in a separate plan:

| Item | Severity | Reason for Deferral |
|------|----------|-------------------|
| Chat focus management for screen readers | MEDIUM | Requires significant ChatWindow refactor |
| Heading hierarchy fix (H1 to H3 skip) | MEDIUM | Cosmetic WCAG advisory, not a violation |
| Iframe focus trapping in SitePreview | MEDIUM | Only impacts keyboard-only users in preview mode |
| OG image creation (1200x630) | MEDIUM | Requires design work, not code |
| NJDPA Global Privacy Control signals | MEDIUM | Threshold unlikely met at launch (100K+ consumers) |
| JSON-LD with full agent data | LOW | Requires dynamic per-agent structured data |
| Platform JSON-LD SoftwareApplication | LOW | Nice-to-have for search rich snippets |
| Have attorney review Terms for NJREC | LOW | Process step, not code |
