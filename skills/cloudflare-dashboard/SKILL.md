---
name: cloudflare-dashboard
description: >
  Navigate and configure the Cloudflare dashboard for the real-estate-star.com domain using browser
  automation tools. Use this skill whenever you need to interact with Cloudflare — adding DNS records,
  changing SSL/TLS settings, creating cache rules, configuring security settings, or any other
  Cloudflare dashboard task. Also use when the user mentions "Cloudflare", "DNS", "SSL", "CDN",
  "cache rules", "edge certificates", "HSTS", or anything related to the domain's CDN/proxy configuration.
  This skill encodes hard-won navigation patterns from real browser automation sessions so you don't
  have to rediscover how the Cloudflare UI works every time.
---

# Cloudflare Dashboard Navigation

This skill captures reliable navigation patterns for the Cloudflare dashboard at `dash.cloudflare.com`.
It was built from real browser automation sessions and documents the UI quirks, reliable click targets,
and URL patterns that actually work.

## Account & Domain Context

| Field | Value |
|-------|-------|
| Domain | `real-estate-star.com` |
| Account ID | `7674efd9381763796f39ea67fe5e0505` |
| Plan | Free |
| Dashboard Base URL | `https://dash.cloudflare.com/7674efd9381763796f39ea67fe5e0505/real-estate-star.com` |

## URL Structure

Every Cloudflare page follows this pattern:

```
https://dash.cloudflare.com/{account-id}/{domain}/{section}/{subsection}
```

You can navigate directly to any section by constructing the URL. This is often faster than
clicking through the sidebar, especially for deep pages.

### Key URLs (direct navigation)

| Section | URL Path |
|---------|----------|
| DNS Records | `/dns/records` |
| SSL/TLS Overview | `/ssl-tls` |
| SSL/TLS Configuration | `/ssl-tls/configuration` |
| Edge Certificates | `/ssl-tls/edge-certificates` |
| Security Settings | `/security/settings` |
| Cache Rules | `/caching/cache-rules` |
| Cache Rules (new) | `/caching/cache-rules/new` |
| Page Rules | `/rules` |

## General Navigation Tips

### Clicking Elements Reliably

The Cloudflare dashboard uses heavy JavaScript rendering. Coordinate-based clicks often miss
because elements shift during render. The most reliable pattern is:

1. Use the `find` tool to locate the element by its label or purpose
2. Click using the returned `ref` ID instead of coordinates
3. Take a screenshot after clicking to verify the action registered

This is especially important for toggles, radio buttons, Save buttons, and dropdown options.

### Text Input Fields

For text fields (like DNS Name, Target, rule Value), use the `form_input` tool with a ref ID
rather than clicking and typing. The `form_input` approach is more reliable because:
- It sets the value directly without needing to click-to-focus first
- It avoids issues with Cloudflare's input components intercepting keystrokes
- It works even if the field has placeholder text or validation overlays

Pattern: `find` the field → get ref → `form_input` with ref and value.

### Dropdowns

Cloudflare dropdowns are custom components, not native `<select>` elements. To interact with them:

1. Click the dropdown to open it
2. The options may render below or above — take a screenshot to see them
3. If options extend beyond the viewport, scroll **within the dropdown** (not the page)
4. Click the desired option by ref or coordinate
5. For text-entry dropdowns (like the Field selector in rules), you can sometimes type to filter

### Modals & Confirmation Dialogs

Some actions (like enabling HSTS) open modal dialogs with their own form elements.
After clicking the trigger button, take a screenshot to confirm the modal appeared,
then interact with elements inside the modal. Modals typically have their own Save/Cancel buttons.

### Settings Pages with Many Options

Pages like Security Settings and Edge Certificates have many options that require scrolling.
The Security Settings page has a **search box** at the top — use it to filter settings by name
instead of scrolling through everything.

---

## DNS Records

**URL:** `/dns/records`

### Adding a Record

1. Click the **"Add record"** button (blue button near the top)
2. An inline form appears with these fields:
   - **Type** dropdown — click to open, select the record type (CNAME, A, MX, etc.)
   - **Name** text field — the subdomain (e.g., `api`, `www`, `jenise-buckalew`)
   - **Target** text field — the destination (e.g., an Azure FQDN, a Pages URL)
   - **Proxy status** toggle — orange cloud = proxied (Cloudflare CDN), gray = DNS only
   - **TTL** — auto when proxied, configurable when DNS-only
3. Click **"Save"** to create the record

### Current DNS Setup (for reference)

| Type | Name | Target | Proxy |
|------|------|--------|-------|
| CNAME | `api` | `real-estate-star-api.blueisland-1da80c2a.eastus.azurecontainerapps.io` | Proxied |
| CNAME | `www` | `platform.real-estate-star.com` | Proxied |
| CNAME | `jenise-buckalew` | `real-estate-star-agent-site.pages.dev` | Proxied |
| CNAME | `platform` | `real-estate-star-portal.pages.dev` | Proxied |

### Per-Agent DNS Pattern

The Free plan's universal SSL covers `*.real-estate-star.com` but NOT second-level wildcards
like `*.real-estate-star.com`. Each agent gets an explicit CNAME registered via `infra/cloudflare/add-agent-domain.ps1`:

```
{handle}.real-estate-star.com → real-estate-star-agent-site.pages.dev
```

To add a new agent, run `.\infra\cloudflare\add-agent-domain.ps1 -Slug {handle}` which registers the subdomain on Cloudflare Workers.

---

## SSL/TLS

### Encryption Mode

**URL:** `/ssl-tls` (overview) → click **"Configure"** → `/ssl-tls/configuration`

The configuration page shows radio buttons for encryption modes:
- Off (not recommended)
- Flexible
- Full
- **Full (strict)** ← current setting, the correct choice

Click the radio button for the desired mode, then click **"Save"**.

### Edge Certificates

**URL:** `/ssl-tls/edge-certificates`

This page is long — scroll down to find these settings:

#### Always Use HTTPS
A toggle switch. When enabled, redirects all HTTP requests to HTTPS.
- Current: **Enabled** ✓

#### HSTS (HTTP Strict Transport Security)
Click the **"Enable HSTS"** button to open a configuration modal. The modal contains:
- An acknowledgment checkbox (must check before other options appear)
- **Max-Age** dropdown — select duration (e.g., 6 months)
- **Include subdomains** toggle
- **No-Sniff Header** toggle
- **Preload** toggle
- A **Save** button at the bottom of the modal

Current settings:
- Max-Age: 6 months
- includeSubDomains: On
- No-Sniff Header: On
- Preload: Off

#### Minimum TLS Version
A dropdown selector. Options: TLS 1.0, TLS 1.1, TLS 1.2, TLS 1.3.
- Current: **TLS 1.2** ✓

---

## Security Settings

**URL:** `/security/settings`

This page has a search box at the top to filter settings by name. Use it instead of
scrolling through the full list.

### Browser Integrity Check
A toggle switch. Evaluates the visitor's browser for suspicious behavior.
- Current: **Enabled** ✓

---

## Cache Rules

**URL:** `/caching/cache-rules`

Free plan allows up to 10 cache rules.

### Creating a Cache Rule

1. Click **"Create rule"** button
2. The form has these sections:

**Rule name (required):** A text field at the top.

**If incoming requests match:**
- Choose "Custom filter expression" (default) or "All incoming requests"
- For custom: select **Field** (e.g., Hostname), **Operator** (e.g., wildcard, equals),
  and **Value** (e.g., `api.real-estate-star.com`)
- Use "And" / "Or" buttons to add multiple conditions

**Then... Cache eligibility (required):**
- **Bypass cache** — requests matching this rule won't be cached
- **Eligible for cache** — requests will be cached (with additional TTL options)

3. Scroll down to find the **"Deploy"** button (saves and activates the rule)
   or **"Save as Draft"** (saves without activating)

### Current Cache Rules

| # | Name | Condition | Action |
|---|------|-----------|--------|
| 1 | Bypass cache for API | Hostname = `api.real-estate-star.com` | Bypass cache |

---

## Common Workflows

### Add a New Agent's DNS Record

1. Navigate to `/dns/records`
2. Click "Add record"
3. Run `.\infra\cloudflare\add-agent-domain.ps1 -Slug {handle}` to register the subdomain
4. Ensure Proxy status is orange (Proxied)
5. Click Save

### Verify SSL Configuration

1. Navigate to `/ssl-tls` — check encryption mode is "Full (strict)"
2. Navigate to `/ssl-tls/edge-certificates` — scroll down to verify:
   - Always Use HTTPS = On
   - HSTS enabled with correct settings
   - Minimum TLS Version = 1.2

### Create a New Cache Rule

1. Navigate to `/caching/cache-rules`
2. Click "Create rule"
3. Enter rule name
4. Configure the match condition (Field, Operator, Value)
5. Select cache eligibility (Bypass or Eligible)
6. Click "Deploy"

---

## Troubleshooting

### Element clicks not registering
Use the `find` tool to get a ref ID, then click by ref instead of coordinates.
Cloudflare's React-based UI re-renders frequently and shifts element positions.

### Dropdown options not visible
After clicking a dropdown, take a screenshot. Options may render in an overlay
that requires scrolling within the dropdown container itself.

### Settings appear to not save
After clicking Save, take a screenshot to confirm the change persisted.
Some settings (like HSTS) require accepting an acknowledgment before the
save button becomes active.

### Toggle switches
Use the `find` tool with a description like "Always Use HTTPS toggle" to locate
toggle switches reliably. They're custom components and coordinate clicks can
hit the label instead of the switch.
