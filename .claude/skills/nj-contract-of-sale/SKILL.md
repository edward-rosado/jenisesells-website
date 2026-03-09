---
name: nj-contract-of-sale
description: >
  Draft a New Jersey REALTORS® Standard Form Contract of Sale (Form 118) for residential real estate transactions.
  This skill should be used whenever the user wants to draft, write, create, or prepare a contract of sale, purchase
  agreement, or sales contract for a New Jersey residential property (1-4 family or vacant lot). Also trigger when
  the user mentions "contract of sale," "sales contract," "purchase contract," "NJ REALTORS form," "Form 118," or
  asks to put together paperwork for a new deal, listing, or transaction. Even if the user just says something casual
  like "I need a contract for 123 Main St" or "write up the deal," this skill applies. Jenise Buckalew at Green Light
  Realty LLC is the primary user of this skill.
---

# NJ Contract of Sale Drafter

This skill drafts a New Jersey REALTORS® Standard Form of Real Estate Sales Contract (Form 118-Statewide) based on
deal-specific details provided by the user. The contracts follow the standard NJ REALTORS® format used for 1-4 family
residential properties or vacant one-family lots.

## Important Context

The user is **Jenise Buckalew**, a licensed real estate agent (REC License ID: 0676823) at **Green Light Realty LLC**
(REC License ID: 1751390), located at 1109 Englishtown Rd, Old Bridge, NJ 08857. Office phone: (732)251-2500,
Fax: (732)339-3339, Cell: (347)393-5993, Email: jenisesellsnj@gmail.com.

Jenise typically represents the **Buyer** as Buyer's Agent, though she sometimes represents the Seller as Listing
Broker. The contracts are produced with Lone Wolf Transactions (zipForm Edition).

## Gathering Deal Information

Before drafting, collect the following deal-specific information from the user. Ask for anything not provided. Group
your questions logically rather than asking one at a time.

### Required Information

**Parties & Property (Section 1):**
- Buyer name(s) and address(es)
- Seller name(s) and address(es)
- Property address (street, city/town, NJ zip)
- Municipality name (for tax map)
- County
- Block and Lot numbers
- Qualifier (if condominium)
- Property type for Section 11 (e.g., "Single family," "Townhouse," "Two family," etc.)

**Purchase Price (Section 2):**
- Total purchase price
- Initial deposit amount (if any)
- Additional deposit amount
- Mortgage amount (if financed; leave blank if cash)
- Balance of purchase price

**Financing (Section 3):**
- Who receives the initial deposit: Listing Broker / Participating Broker / Buyer's Attorney / Title Company / Other
- Additional deposit due date (Jenise typically writes "Once AR concludes" meaning after attorney review)
- Escrow holder (Jenise typically uses "sellers Attorney" or leaves for attorney review to determine)
- Mortgage type: VA / FHA / Section 203(k) / Conventional / Other (specify, e.g., "Cash")
- If mortgage: Principal amount, term (years), payment schedule (years), commitment deadline date
- Closing date
- Closing agent (if known)

**Broker Information (Sections 29-30):**
- Is Jenise representing Buyer or Seller in this transaction?
- Other broker firm name, agent name, address, phone, fax, email, REC License IDs
- Commission structure: From Seller / From Buyer, percentage or amount
- Jenise's commission: From Seller / From Buyer, percentage or amount

**Property-Specific Selections:**
- Items included in sale (typically "As per MLS [number]")
- Items excluded from sale (typically "Personal Property")
- Certificate of Occupancy expense cap (Section 9B) - dollar amount or leave blank for 1.5% default
- Tenancies: Applicable or Not Applicable? If applicable: tenant name, location, rent, security deposit, term
- Lead-based paint (Section 13): Applicable (pre-1978 dwelling) or Not Applicable
- POET systems (Section 14): Applicable or Not Applicable (almost always Not Applicable)
- Cesspool requirements (Section 15): Applicable or Not Applicable; if applicable, is there a cesspool?
- Municipal assessments (Section 10): Has Seller been notified or has not been notified?
- Licensee disclosure (Section 32): Applicable or Not Applicable (almost always Not Applicable)

**Addenda (Section 42):**
- Which addenda apply? Options include:
  - Buyer's Property Sale Contingency
  - Condominium/Homeowner's Associations
  - Coronavirus
  - FHA/VA Loans
  - Lead Based Paint Disclosure (Pre-1978)
  - New Construction
  - Private Sewage Disposal (Other than Cesspool)
  - Private Well Testing
  - Properties With Three (3) or More Units
  - Seller Concession
  - Short Sale
  - Solar Panel
  - Swimming Pools
  - Taxes for Properties $1 Million and More
  - Underground Fuel Tank(s)

**Additional Contractual Provisions (Section 43):**
This is the custom language section. Ask the user what special terms apply. Common patterns from Jenise's deals:

- **Investment/tenant-occupied property:** "Buyer acknowledges that the property is currently tenant-occupied under
  an existing lease agreement expiring [DATE]. Buyer agrees to assume the tenant and existing lease at a monthly
  rental amount of $[AMOUNT] through the lease expiration date."
  Plus inspection limitation: "Buyer's inspection shall be limited to structural and mechanical components of the
  property, along with a septic system inspection. All other inspections shall be for informational purposes only.
  Buyer agrees not to request repairs, credits, or concessions for any items outside of structural, mechanical, or
  septic matters."

- **Cash sale:** "Not contingent upon the sale of any real estate." / "Cash Sale" / "Contingent upon oil tank sweep
  and clean title."

- **Limited inspection clause:** "Buyer shall have the right to conduct a limited home inspection, which shall be
  restricted to structural and mechanical systems only. Buyer agrees not to negotiate or request repairs for any
  cosmetic or minor issues discovered during the inspection."

- **Appraisal gap waiver:** "In the event the property does not appraise at the agreed-upon purchase price, the
  Buyer agrees to waive up to $[AMOUNT] of the appraisal shortfall. The Buyer will cover the difference between the
  appraised value and the purchase price up to this amount, with the Seller not being required to reduce the purchase
  price."

- **Not contingent on sale:** "Not contingent upon the sale of any real estate."

## Drafting the Contract

### Reference Template

A blank PDF template of the official NJ REALTORS® Form 118-Statewide (07/2025.2 edition) is stored at
`references/blank-contract-template.pdf`. This is the authoritative template produced with Lone Wolf
Transactions (zipForm Edition). The structured text version is at `references/contract-template.md`.

**When drafting a contract, always read `references/contract-template.md` first** to get the exact section
numbering, line numbering (1-770), boilerplate language, and field placement. Fill in the deal-specific
fields based on the information gathered from the user.

The template is 15 pages total: 14 pages of contract content (Sections 1-43 with signature block) plus
a Wire Fraud Notice page.

### Output Format

Generate the contract as a **Word document (.docx)** using the docx skill. The document should:

1. Start with the **Notice to Buyer and Seller** (page 1) with the broker representation box checked appropriately
2. Include the full **Standard Form contract** (Sections 1-43) with all deal-specific fields filled in
3. Use the NJ REALTORS® Form 118-Statewide (07/2025.2) format
4. Include the **Signature Block** with 4 Buyer lines and 4 Seller lines with Date fields
5. Include the **Wire Fraud Notice** as the final page with Seller/Landlord and Buyer/Tenant signature lines

### Key Formatting Notes

- The form uses numbered sections (1-43) with line numbers on the left margin (lines 1-770)
- Checkbox fields use "[X]" to indicate selections (e.g., "[X] Buyer's Agent" or "[X] Not Applicable")
- Dollar amounts are right-aligned with dollar signs
- Dates should be filled in where provided, with "(date)" placeholder text where blanks remain
- Each page footer should show:
  - "New Jersey REALTORS® Form 118-Statewide | 07/2025.2  Page X of 14"
  - "Buyer's Initials: ____  Seller's Initials: ____"
  - "Green Light Realty LLC, 1109 Englishtown Rd old bridge NJ 08857  Phone: (732)251-2500  Fax: (347)393-5993"
  - "Jenise Buckalew  Produced with Lone Wolf Transactions (zipForm Edition)"
- Match the exact boilerplate language from the template -- use the PDF template's wording verbatim for
  all standard sections. The boilerplate text must be the official 07/2025.2 edition language, not paraphrased.

### Mathematical Validation

Always verify that the purchase price math adds up:
- Total Purchase Price = Initial Deposit + Additional Deposit + Mortgage + Balance of Purchase Price
- If the numbers don't add up, flag this to the user before proceeding

## Limitations

This skill generates a draft contract for review purposes. The user should always:
- Review all terms carefully before sending to parties
- Have the contract reviewed during the attorney review period
- Ensure compliance with current NJ real estate law
- Use this as a starting point that will be finalized in their transaction management software (Lone Wolf/zipForm)
