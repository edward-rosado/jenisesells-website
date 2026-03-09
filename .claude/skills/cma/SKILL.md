---
name: cma
description: |
  **Instant CMA Generator**: Create a professional Comparative Market Analysis (CMA) PDF report for a residential property in New Jersey. Use this skill whenever Jenise wants to generate a CMA, home valuation, market analysis, or comp report for a potential seller.
  - MANDATORY TRIGGERS: CMA, comparative market analysis, home valuation, home value, what's my home worth, comps, comparable sales, market analysis, property value, seller lead, price opinion
  - Also trigger when Jenise pastes a form submission from her website containing a seller's name, property address, and contact info
  - Even casual mentions like "run comps on 123 Main St" or "what's this house worth" or "new lead just came in" should trigger this skill
---

# Instant CMA Generator

You are generating a professional Comparative Market Analysis for Jenise Buckalew, a NJ State Licensed REALTOR® and independent agent with Green Light Realty LLC. This CMA will be sent directly to a potential seller, so it needs to look polished and professional.

## AUTOMATED WORKFLOW

When Jenise pastes a lead form submission (or provides a property address and seller info), the FULL automation kicks in:

1. Parse the lead info
2. Research comparable sales via web search
3. Generate the CMA PDF
4. Draft a personalized response email
5. AUTO-SEND the email via Gmail
6. Present the CMA PDF for Jenise to review

**This is fully automated.** Once Jenise pastes the lead info, do NOT ask clarifying questions — just run the entire pipeline.

## What You Need

At minimum: **Property address** (street, city, state, zip)

Nice to have (DO NOT ask if form submission was pasted):
- Beds / Baths / Approx sqft
- Seller's name and email
- Any known details about the home

## Step-by-Step Workflow

### 1. Parse the Seller Info
Extract property address, seller name, email, phone, timeline, and contact info.

### 2. Research Comparable Sales
Use web search to find 3-5 recent comparable sales near the subject property:
- Recently sold homes within ~0.5 miles
- Similar bed/bath count and square footage
- Sold within the last 6 months (prefer 3 months)

For each comp, find: Address, Sale price, Sale date, Beds/Baths/Sqft, Price per sqft, Days on market

### 3. Estimate the Value Range
- Calculate average and median price per sqft from comps
- Apply to subject property sqft for baseline estimate
- Provide a value range (low to high)
- Note any adjustments needed

### 4. Generate the CMA PDF
Use Python with reportlab to create a polished, branded PDF:

**Page 1 — Cover**: Title, property address, prepared for/by, Green Light Realty branding, date, contact info
**Page 2 — Subject Property Overview**: Property details, neighborhood description
**Page 3 — Comparable Sales**: Clean table with all comp data
**Page 4 — Estimated Value & Recommendation**: Value range, calculation method, market context, call to action
**Page 5 — About Jenise**: Bio, services, contact info

### Design Guidelines
- Brand colors: green (#2E7D32), dark green (#1B5E20), gold (#C8A951)
- Clean, modern, professional look
- Helvetica font family
- Page numbers on each page

### 5. Save the CMA PDF
Save to workspace: CMA_[Address]_[Date].pdf

### 6. Draft the Response Email
- Professional but warm tone
- Address lead by first name
- Highlight key findings: estimated value range, market conditions
- Reference attached CMA report
- Include call to action to schedule a walkthrough

**Subject:** "Your Home Value Report — [Property Address]"

**Email signature:**
Warm regards,
Jenise Buckalew, REALTOR®
Green Light Realty LLC
(347) 393-5993
jenisesellsnj@gmail.com
jenisesellsnj.com
Se Habla Español
Forward. Moving.

### 7. AUTO-SEND via Gmail
Use gmail_create_draft to create the email draft, then inform Jenise to attach the PDF and send.

## Jenise's Branding
- **Name**: Jenise Buckalew, REALTOR®
- **Brokerage**: Green Light Realty LLC
- **Phone**: (347) 393-5993
- **Email**: jenisesellsnj@gmail.com
- **Website**: jenisesellsnj.com
- **Tagline**: Forward. Moving.
- **Areas**: Middlesex, Monmouth & Ocean Counties, NJ
- **Languages**: English, Spanish
