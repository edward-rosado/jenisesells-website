---
name: send-email
description: |
  **Gmail Email Sender**: Send emails from Jenise's Gmail (jenisesellsnj@gmail.com) with optional file attachments. This skill is the final step in the lead response pipeline — after generating a CMA, it sends the email with the PDF attached directly to the lead.
  - MANDATORY TRIGGERS: send email, email the lead, send the CMA, email the report, send to client, email client, auto-send, send response, email response, mail the report
  - Also trigger when the CMA skill has finished generating a report and needs to deliver it to the lead
  - Trigger when Jenise says things like "send it", "email them", "shoot them the report", "forward the CMA"
---

# Gmail Email Sender

Send emails from Jenise's Gmail (jenisesellsnj@gmail.com) with file attachments. This is the final step in the lead response pipeline.

## Sending Methods (in order of preference)

### Method 1: Gmail MCP + Browser (Cowork)
1. Use gmail_create_draft to create the email with full body text
2. Use Claude in Chrome to open Gmail drafts, attach the CMA PDF, and send
3. Confirm sent

### Method 2: Manual Fallback
- Present the full email text to Jenise for copy/paste
- Include: subject line, recipient, full body, and note which PDF to attach

## CMA Email Format

**Subject:** Your Home Value Report — [Full Property Address]

**Body includes:**
- Greeting with lead's first name
- Thank you for reaching out through the website
- Key findings: estimated value range and market conditions
- Reference to the attached CMA report
- Note that an in-person visit provides a more accurate number
- Clear call to action: schedule a walkthrough
- Jenise's full signature block

**Signature block (on every email):**
Warm regards,
Jenise Buckalew, REALTOR®
Green Light Realty LLC
(347) 393-5993
jenisesellsnj@gmail.com
jenisesellsnj.com
Se Habla Español
Forward. Moving.

**Attachment:** Always attach the CMA PDF. Never send without it.
**BCC:** Always BCC jenisesellsnj@gmail.com so Jenise has a record.

## Gmail Configuration
- **Account:** jenisesellsnj@gmail.com
- **From Name:** Jenise Buckalew

## Important Notes
- Always BCC jenisesellsnj@gmail.com on lead emails
- For CMA emails, always attach the PDF — never send without it
- Never silently fail — always tell Jenise the status
