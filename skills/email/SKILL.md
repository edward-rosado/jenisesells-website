---
name: send-email
description: |
  **Email Sender (Multi-Tenant)**: Send emails from any agent's configured email account with optional file attachments. Reads all identity and provider settings from the agent's config profile — no hardcoded values. Supports Gmail, Outlook, and generic SMTP.
  - MANDATORY TRIGGERS: send email, email the lead, send the CMA, email the report, send to client, email client, auto-send, send response, email response, mail the report
  - Also trigger when the CMA skill has finished generating a report and needs to deliver it to the lead
  - Trigger when the agent says things like "send it", "email them", "shoot them the report", "forward the CMA"
---

# Email Sender (Multi-Tenant)

Send emails from any agent's configured email account with file attachments. This is the final step in the lead response pipeline.

## Agent Config

Every email is sent on behalf of a specific agent. Before composing or sending, load the agent's profile:

```
config/agents/{agent-id}.json
```

**Required fields used by this skill:**

| Config Path | Purpose |
|---|---|
| `{agent.identity.name}` | From name and signature |
| `{agent.identity.title}` | Credential line (e.g. REALTOR) |
| `{agent.identity.email}` | From address, Reply-To, and BCC |
| `{agent.identity.phone}` | Signature phone number |
| `{agent.identity.website}` | Signature website link |
| `{agent.identity.brokerage}` | Signature brokerage line |
| `{agent.identity.languages}` | Language note in signature (if multilingual) |
| `{agent.identity.tagline}` | Closing tagline in signature |
| `{agent.integrations.email_provider}` | Send method: `gmail`, `outlook`, or `smtp` |

**Validation before sending:**
1. Confirm `{agent.identity.email}` is present — abort if missing.
2. Confirm `{agent.integrations.email_provider}` is one of `gmail`, `outlook`, `smtp` — abort if missing or unrecognized.
3. Confirm `{agent.identity.name}` is present — abort if missing.

---

## Sending Methods by Provider

### Gmail (`{agent.integrations.email_provider}` = `"gmail"`)

**Method 1: Gmail MCP + Browser (preferred)**
1. Use `gmail_create_draft` to create the email draft with full body text.
   - Set `from` to `{agent.identity.name} <{agent.identity.email}>`.
   - Set `bcc` to `{agent.identity.email}`.
2. If attachments are required (e.g. CMA PDF), use Claude in Chrome to open Gmail drafts, attach the file, and send.
3. Confirm sent status to the agent.

**Method 2: Manual Fallback**
- Present the full email text to the agent for copy/paste.
- Include: subject line, recipient, BCC reminder, full body, and note which file(s) to attach.

### Outlook (`{agent.integrations.email_provider}` = `"outlook"`)

**Method 1: Outlook MCP (when available)**
1. Use the Outlook MCP tool to create and send the email.
   - Set `from` to `{agent.identity.name} <{agent.identity.email}>`.
   - Set `bcc` to `{agent.identity.email}`.
   - Attach files directly if the MCP supports it.
2. Confirm sent status to the agent.

**Method 2: Manual Fallback**
- Present the full email text to the agent for copy/paste into Outlook.
- Include: subject line, recipient, BCC reminder, full body, and note which file(s) to attach.

### SMTP (`{agent.integrations.email_provider}` = `"smtp"`)

**Method 1: SMTP Send (when configured)**
1. Read SMTP credentials from the agent's secure credential store (never from the config JSON).
2. Compose the email with proper headers:
   - `From: {agent.identity.name} <{agent.identity.email}>`
   - `Reply-To: {agent.identity.email}`
   - `BCC: {agent.identity.email}`
3. Attach files as MIME multipart.
4. Send via the configured SMTP server.
5. Confirm sent status to the agent.

**Method 2: Manual Fallback**
- Present the full email text to the agent for manual sending through their email client.

---

## Dynamic Signature Block

Build the signature from the agent's config on every email. Never hardcode any identity values.

**Template:**

```
Warm regards,
{agent.identity.name}, {agent.identity.title}
{agent.identity.brokerage}
{agent.identity.phone}
{agent.identity.email}
{agent.identity.website}
{language_line}
{agent.identity.tagline}
```

**Rules:**
- `{language_line}`: If `{agent.identity.languages}` contains more than one entry, generate a line like `Se Habla Espanol` or `Languages: Spanish, Portuguese` based on the non-English languages listed. Omit this line entirely if the agent only speaks English.
- `{agent.identity.tagline}`: Include only if present in config. Omit the line if empty or missing.
- `{agent.identity.brokerage}`: Include only if present in config. Omit the line if empty or missing.
- `{agent.identity.website}`: Include only if present in config. Omit the line if empty or missing.
- Never add fields that are not in the agent's config.

---

## CMA Email Template

Use this template when sending a CMA report to a lead. All variables are resolved from the agent config and the CMA context.

**Subject:** `Your Home Value Report — {property_address}`

**Body:**

```
Hi {lead_first_name},

Thank you for reaching out through the website. I put together a Comparative
Market Analysis for {property_address} and wanted to share the key findings
with you.

Based on recent comparable sales in the area, the estimated value range for
your property is {value_range_low} to {value_range_high}. {market_conditions}

I have attached the full CMA report for your review. Please note that an
in-person visit and walkthrough of the property would allow me to provide a
more precise valuation based on your home's unique features and condition.

I would love to schedule a time to walk through the property with you. Feel
free to call or text me at {agent.identity.phone}, or simply reply to this
email.

{signature_block}
```

**Variables:**
- `{lead_first_name}` — from the lead inquiry
- `{property_address}` — full street address from the CMA
- `{value_range_low}`, `{value_range_high}` — from CMA output
- `{market_conditions}` — one-sentence summary from CMA (e.g. "The market in Old Bridge is currently favoring sellers with homes spending an average of 18 days on market.")
- `{signature_block}` — built dynamically per the signature template above

**Attachment:** Always attach the CMA PDF. Never send a CMA email without it.

**BCC:** Always BCC `{agent.identity.email}` so the agent has a record.

---

## General Email Sending Guidelines

1. **Always BCC the agent.** Every outbound email to a lead or client must BCC `{agent.identity.email}`.

2. **Never send without required attachments.** If the email references an attached document (CMA, contract, disclosure), confirm the file exists before sending. Abort and notify the agent if the file is missing.

3. **Never silently fail.** Always report the outcome to the agent:
   - Success: "Email sent to {recipient} with {attachment_name} attached."
   - Draft created: "Draft created in Gmail — attachment must be added manually before sending."
   - Failure: "Email could not be sent. Reason: {error}. Here is the full email text for manual sending."

4. **No hardcoded identity values.** Every name, phone number, email address, brokerage, and tagline must come from `config/agents/{agent-id}.json`. If a field is missing from the config, omit it — never substitute a default.

5. **Respect provider preference.** Always check `{agent.integrations.email_provider}` and use the corresponding method. Never assume Gmail.

6. **Subject line conventions:**
   - CMA emails: `Your Home Value Report — {property_address}`
   - Contract emails: `{document_type} — {property_address}`
   - General follow-up: Keep professional, under 60 characters, no ALL CAPS.

7. **Tone:** Professional, warm, and concise. Match the agent's brand voice if a style guide exists in the config. Default to a friendly-professional tone.

8. **Credentials and secrets:** Never read, log, or store email credentials in the config JSON or in conversation. SMTP passwords and OAuth tokens live in the secure credential store, separate from the agent profile.
