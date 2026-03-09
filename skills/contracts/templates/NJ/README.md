# New Jersey Contract of Sale Template

## Form Information

| Field | Value |
|---|---|
| **Form Name** | Form 118-Statewide |
| **Edition** | 07/2025.2 |
| **Pages** | 14 |
| **Sections** | 1 through 43 |
| **Line Range** | 1 through 770 |
| **Governing Law** | New Jersey Statutes, NJAC, NJREC regulations |
| **Blank Template PDF** | `skills/contracts/templates/NJ/blank-template.pdf` |

## Section-by-Section Breakdown

### Section 1: Parties and Property (Lines 1-45)

Identifies buyer(s), seller(s), and the subject property.

**Required Fields:**
- Buyer name(s) and mailing address(es)
- Seller name(s) and mailing address(es)
- Property street address, city/town, state, zip
- Municipality (as it appears on tax records)
- County
- Block number
- Lot number
- Qualifier (required for condominiums; leave blank otherwise)
- Property type (Single Family, Condo, Townhouse, Multi-Family, etc.)

### Section 2: Purchase Price (Lines 46-75)

Breaks down the total purchase price into its components.

**Required Fields:**
- Total purchase price (written and numeric)
- Initial deposit amount
- Additional deposit amount
- Mortgage amount (if financed; $0 for cash sales)
- Balance of purchase price due at closing

**Validation Rule:**
```
Total = Initial Deposit + Additional Deposit + Mortgage + Balance
```

### Section 3: Financing Terms (Lines 76-140)

Covers mortgage contingency, deposit handling, and escrow.

**Required Fields:**
- Recipient of initial deposit (Listing Broker, Buyer's Attorney, Seller's Attorney)
- Additional deposit due date (business days after attorney review)
- Escrow holder name and address
- Mortgage type: Conventional, FHA, VA, USDA, Other
- If mortgage applies:
  - Principal amount
  - Term (years)
  - Payment schedule (monthly)
  - Interest rate cap (if applicable)
  - Mortgage commitment deadline (business days after attorney review)
- Closing date
- Closing agent / title company

### Section 4: Items Included in Sale (Lines 141-180)

Checkbox-style section listing fixtures and personal property included.

**Standard Included Items:**
- Plumbing and lighting fixtures
- Shades, blinds, window treatments
- Built-in appliances (dishwasher, range/oven, microwave)
- Wall-to-wall carpeting
- Garage door opener(s) and remote(s)
- TV antenna / satellite dish
- Screens and storm windows
- Exterior plantings and landscaping

**Additional Items (specify if included):**
- Refrigerator
- Washer / Dryer
- Window air conditioning units
- Fireplace equipment
- Storage shed
- Playground equipment
- Other (free text)

**Excluded Items:**
List any items the seller intends to remove that a buyer might assume are included.

### Section 5: Certificate of Occupancy (Lines 181-210)

NJ municipalities may require a CO or CCO at transfer.

**Required Fields:**
- Maximum expense cap for CO/CCO compliance (dollar amount)
- Party responsible for costs (Buyer or Seller)
- Whether municipality requires CO, CCO, or neither

### Section 6: Tenancies (Lines 211-235)

**NJ-Specific:** Must indicate whether the property is subject to existing tenancies.

- If Applicable: Provide tenant name(s), lease terms, monthly rent, security deposits
- If Not Applicable: Mark as N/A

### Section 7: Lead-Based Paint Disclosure (Lines 236-270)

**NJ-Specific (Federal Requirement):** Required for all properties built before 1978.

- Disclosure of known lead-based paint or hazards
- Buyer's right to 10-day inspection period
- Buyer's election to waive or exercise inspection right
- Seller's disclosure of available records and reports

### Section 8: Radon (Lines 271-290)

NJ-specific radon disclosure and testing provisions.

### Section 9: POET Systems (Lines 291-320)

**NJ-Specific:** Pre-existing Oil Equipment and Tanks.

- Whether the property has or had underground storage tanks (USTs)
- Whether a tank sweep has been performed
- Seller's disclosure of known USTs, above-ground tanks, or heating oil systems
- Remediation responsibilities

### Section 10: Cesspool / Septic (Lines 321-345)

**NJ-Specific:** Required disclosure for properties not connected to municipal sewer.

- Type of system (cesspool, septic, other)
- Date of last inspection
- Whether system is in compliance with local health department requirements
- Responsibility for inspection and certification costs

### Sections 11-20: Standard Contract Terms (Lines 346-480)

- Section 11: Title and Survey
- Section 12: Risk of Loss
- Section 13: Conditions of Property / Inspections
- Section 14: Environmental Conditions
- Section 15: Flood Zone Disclosure
- Section 16: Homeowner's Insurance
- Section 17: Adjustments at Closing
- Section 18: Assessment of Liens
- Section 19: Municipal Assessments
- Section 20: Buyer's Default / Seller's Remedies

### Sections 21-30: Legal Provisions (Lines 481-580)

- Section 21: Seller's Default / Buyer's Remedies
- Section 22: Notices
- Section 23: Entire Agreement
- Section 24: Binding Effect
- Section 25: Governing Law
- Section 26: Severability
- Section 27: Attorney Review (3 business days)
- Section 28: Buyer's Representations
- Section 29: Seller's Representations
- Section 30: Survival of Representations

### Sections 31-40: Broker and Compliance (Lines 581-700)

- Section 31: Broker Disclosure
- Section 32: Commission Agreement
- Section 33: Dual Agency Disclosure
- Section 34: Licensee Disclosure (if agent has personal interest)
- Section 35: Consumer Information Statement
- Section 36: New Construction Provisions (if applicable)
- Section 37: Condominium / HOA Provisions (if applicable)
- Section 38: Short Sale / REO Provisions (if applicable)
- Section 39: Arbitration / Mediation
- Section 40: Additional Provisions (free text)

### Sections 41-43: Execution (Lines 701-770)

- Section 41: Signature Block (Buyer)
- Section 42: Signature Block (Seller)
- Section 43: Broker Acknowledgment and Signatures

**Broker Acknowledgment Fields (populated from agent config):**
- Listing Broker: firm name, address, phone, fax, agent name, email, license IDs
- Buyer's Broker: firm name, address, phone, fax, agent name, email, license IDs

## NJ-Specific Addenda

The following addenda are commonly attached to NJ contracts. Include only those
applicable to the transaction.

| Addendum | When to Include |
|---|---|
| **Buyer's Property Sale Contingency** | Buyer must sell existing property before closing |
| **Condominium / HOA Addendum** | Property is a condo or in an HOA community |
| **Coronavirus / Force Majeure Addendum** | Pandemic-related contingencies or deadline extensions |
| **FHA Financing Addendum** | Buyer is using FHA-insured mortgage |
| **VA Financing Addendum** | Buyer is using VA-guaranteed mortgage |
| **USDA Financing Addendum** | Buyer is using USDA Rural Development mortgage |
| **Short Sale Addendum** | Sale is subject to lender approval |
| **REO / Bank-Owned Addendum** | Property is bank-owned / foreclosure |
| **New Construction Addendum** | Property is new construction or to-be-built |
| **Seller's Concession Addendum** | Seller is contributing to buyer's closing costs |
| **Home Warranty Addendum** | Home warranty is included in the transaction |
| **Lead-Based Paint Addendum** | Extended lead inspection provisions (pre-1978 properties) |
| **Kick-Out Clause Addendum** | Seller retains right to continue marketing |
| **Escrow Agreement Addendum** | Special escrow arrangements beyond standard terms |

## Common NJ-Specific Additional Provisions

These are frequently added to Section 40 (Additional Provisions) as free-text clauses.

### Investment Property Clause
When the buyer is purchasing the property as an investment (not owner-occupied):
- Buyer acknowledges property is being purchased as investment
- Buyer waives owner-occupancy representations
- Mortgage contingency may reference investor loan programs
- Include landlord-tenant transfer provisions if tenanted

### Cash Sale Provision
When no mortgage is involved:
- Strike mortgage contingency (Section 3 mortgage fields = $0)
- Add proof-of-funds requirement (bank statement or verification letter)
- Specify deadline for proof of funds
- Shorter closing timeline is typical

### Limited Inspection Provision
When buyer agrees to a restricted inspection scope:
- Define which inspections are waived vs. retained
- Specify dollar threshold for repair requests (if any)
- Clarify that structural, environmental, and safety inspections may still apply
- Note that this does not waive buyer's right to attorney review

### Appraisal Gap Waiver
When buyer agrees to cover a shortfall between appraised value and purchase price:
- Maximum gap amount buyer will cover (dollar amount or unlimited)
- Source of gap funds (cash reserves)
- Buyer's right to terminate if gap exceeds stated maximum
- Does not waive mortgage contingency (lender may still decline)

### As-Is Provision
When property is sold in current condition:
- Seller makes no representations regarding property condition
- Buyer accepts property in its current state
- Does not waive buyer's right to inspections (inspections are informational only)
- Seller not obligated to make repairs
- Does not override statutory disclosure requirements

### Rent-Back Agreement
When seller needs to remain in the property after closing:
- Duration of rent-back period
- Daily or monthly rent amount
- Security deposit (typically held in escrow)
- Insurance and liability during rent-back
- Penalties for holdover beyond agreed period

## Footer Format

Each page of the generated contract should include the following footer:

```
Form 118-Statewide | 07/2025.2 | {transaction_platform} | Page {n} of {total}
```

Where:
- `{transaction_platform}` is read from `{agent.compliance.transaction_platform}` in the agent config
- `{n}` is the current page number
- `{total}` is the total page count of the generated document

## Notes for Contributors

- When a new edition of Form 118-Statewide is released by the New Jersey REALTORS,
  update the edition, line numbers, and section references in this file.
- Verify all section numbers and line ranges against the official form before updating.
- Have a licensed NJ real estate attorney review any changes to legal language or
  disclosure requirements.
- The blank template PDF should be the unmodified official form for reference only;
  it is not distributed to end users.
