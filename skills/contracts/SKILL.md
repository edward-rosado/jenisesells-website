---
name: contracts
description: >
  Draft a state-specific real estate contract of sale for residential transactions.
  This skill should be used whenever the user wants to draft, write, create, or prepare
  a contract of sale, purchase agreement, or sales contract for a residential property.
  Trigger on mentions of "contract of sale," "sales contract," "purchase contract," or
  asks to put together paperwork for a new deal, listing, or transaction.
---

# Contract of Sale Drafter

This skill drafts a state-specific real estate sales contract based on deal-specific
details provided by the user and the active agent's profile configuration.

## Agent Identity

All agent identity, brokerage, licensing, and compliance details are loaded from:

```
config/agents/{agent-id}.json
```

The agent config provides:

| Field Path | Purpose |
|---|---|
| `{agent.name}` | Agent's full legal name |
| `{agent.license_id}` | Agent's REC license number |
| `{agent.brokerage.name}` | Brokerage firm name |
| `{agent.brokerage.license_id}` | Brokerage REC license number |
| `{agent.brokerage.address}` | Brokerage office address |
| `{agent.brokerage.phone}` | Office phone number |
| `{agent.brokerage.fax}` | Office fax number |
| `{agent.contact.cell}` | Agent's cell phone |
| `{agent.contact.email}` | Agent's email address |
| `{agent.location.state}` | State of practice (e.g., NJ, NY, PA) |
| `{agent.compliance.state_form}` | Official form identifier for the state |
| `{agent.compliance.transaction_platform}` | Transaction software (e.g., Lone Wolf / zipForm) |
| `{agent.defaults.role}` | Typical role (Buyer's Agent, Listing Agent) |

Never hardcode agent names, phone numbers, emails, brokerage names, or license
numbers. Always read them from the agent config.

## Template Resolution

The state template is resolved using the agent's compliance configuration:

1. Read `{agent.compliance.state_form}` to identify the form (e.g., `Form 118-Statewide`).
2. Read `{agent.location.state}` to determine the state abbreviation (e.g., `NJ`).
3. Load the template and its field reference from:

```
skills/contracts/templates/{STATE}/
```

Each state directory contains a `README.md` documenting form structure, sections,
field placement, line numbers, state-specific legal sections, addenda, and additional
provisions. Consult this file before drafting.

## Gathering Deal Information

Before drafting, collect the following deal-specific information from the user. Ask
for anything not provided. The section labels below are generic; map them to the
actual form sections documented in the state template README.

### Required Information

**Parties & Property:**
- Buyer name(s) and address(es)
- Seller name(s) and address(es)
- Property address (street, city/town, state, zip)
- Municipality name (for tax map)
- County
- Block and Lot numbers
- Qualifier (if condominium)
- Property type

**Purchase Price:**
- Total purchase price
- Initial deposit amount
- Additional deposit amount
- Mortgage amount (if financed)
- Balance of purchase price

**Financing:**
- Who receives the initial deposit
- Additional deposit due date
- Escrow holder
- Mortgage type (Conventional, FHA, VA, USDA, Other)
- If mortgage: Principal amount, term, payment schedule, commitment deadline
- Closing date
- Closing agent

**Broker Information:**
- Agent's role in this transaction (Buyer's Agent or Listing Agent)
- Other broker firm name, agent name, address, phone, fax, email, license IDs
- Commission structure

**Property-Specific Selections:**
- Items included/excluded from sale (appliances, fixtures, personal property)
- Certificate of Occupancy / Certificate of Continued Occupancy expense cap
- State-specific disclosures and contingencies (varies by state; see template README)
- Licensee disclosure (if agent has interest in the property)
- Addenda and additional provisions

### Mathematical Validation

Always verify before drafting:

```
Total Purchase Price = Initial Deposit + Additional Deposit + Mortgage Amount + Balance
```

If the numbers do not balance, ask the user to clarify before proceeding.

## Drafting the Contract

### Process

1. Load the agent config from `config/agents/{agent-id}.json`.
2. Load the state template reference from `skills/contracts/templates/{STATE}/README.md`.
3. Map all gathered deal information to the form fields documented in the template README.
4. Populate agent/brokerage fields from the agent config (never prompt for these).
5. Apply state-specific sections, disclosures, and legal language per the template README.
6. Generate the contract.

### Output Format

Generate the contract as a Word document (.docx) unless the user requests a different
format. The document should follow the form layout, section ordering, and line numbering
documented in the state template README.

### Footer

Each page footer should include:
- Form identifier and edition (from `{agent.compliance.state_form}`)
- Transaction platform attribution (from `{agent.compliance.transaction_platform}`)
- Page number / total pages

## Supported States

| State | Template Directory | Form | Status |
|---|---|---|---|
| NJ | `templates/NJ/` | Form 118-Statewide (07/2025.2) | Active |

Additional states will be added as templates are created and validated.

## Adding a New State

To add support for a new state:

1. **Create the template directory:**
   ```
   skills/contracts/templates/{STATE}/
   ```

2. **Add a `README.md`** documenting:
   - Official form name, edition, and page count
   - Total number of sections and line range
   - Section-by-section breakdown with line numbers
   - All required fields and their placement
   - State-specific legal sections and disclosures
   - Available addenda and their descriptions
   - Common additional provisions
   - Footer format requirements
   - Location of blank template PDF (if available)

3. **Place the blank template PDF** (if available) in the state directory:
   ```
   skills/contracts/templates/{STATE}/blank-template.pdf
   ```

4. **Update the Supported States table** in this file.

5. **Test the template** by drafting a sample contract with realistic data and having
   a licensed agent in that state review the output for accuracy and compliance.

6. **Validate legal requirements** with a real estate attorney licensed in the state
   to confirm all mandatory disclosures and contingencies are covered.

## Limitations

- This skill generates a **draft contract for review purposes only**.
- The user must review all terms before execution.
- Attorney review is strongly recommended for all contracts.
- The agent is responsible for ensuring compliance with current state law,
  local ordinances, and MLS rules.
- State laws change; template READMEs should be reviewed and updated when
  new form editions are released.
