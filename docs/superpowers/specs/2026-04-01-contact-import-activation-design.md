# Contact Import During Activation

**Date:** 2026-04-01
**Status:** Implementing
**Scope:** Enhanced Phase 1 DriveIndex + new Phase 2.5 classifier + new Phase 3 persist activity

---

## Problem

When an agent connects their Google account, the activation pipeline reads their emails and Drive files for voice/personality analysis but discards all the structured client/property/transaction data. The agent's existing business — leads, active clients, properties under contract, closed deals — is invisible to the platform.

## Goals

- Extract contacts, properties, and transaction stages from Drive PDFs and emails
- Classify each contact into the correct pipeline stage (Lead, Active Client, Under Contract, Closed)
- Create the standard folder structure in Google Drive per the CMA pipeline spec
- Copy relevant documents into the correct client folders
- Save contacts to ILeadStore with appropriate status
- Create a summary spreadsheet in Drive

## Non-Goals

- OCR of handwritten documents (Claude Vision handles printed text only)
- Ongoing email monitoring (this is a one-time import during activation)
- Merging duplicate contacts across sources (simple dedup by email/name)

---

## End-to-End Pipeline Flow

```mermaid
flowchart TD
    A[OAuth Callback] --> B[Azure Queue]
    B --> C[ActivationOrchestrator]

    subgraph Phase1["Phase 1: Gather"]
        D[EmailFetchWorker] --> D1[EmailCorpus<br/>100 sent + 100 inbox]
        E[DriveIndexWorker] --> E1[ClassifiedDriveIndex<br/>+ DocumentExtractions]
        F[AgentDiscoveryWorker] --> F1[AgentDiscovery<br/>profiles, headshot, languages]
        E -->|NEW: Download PDFs| E2[Claude Vision<br/>3 pages per PDF]
        E2 --> E1
    end

    subgraph Phase2["Phase 2: Synthesize (12 workers — unchanged)"]
        G[Voice, Personality, Branding, CMA,<br/>Marketing, WebsiteStyle, Pipeline,<br/>Coaching, BrandExtraction, BrandVoice,<br/>Compliance, FeeStructure]
    end

    subgraph Phase25["Phase 2.5: Contact Detection (NEW — reusable Activity)"]
        H[ContactDetectionActivity]
        H -->|reads| E1
        H -->|reads| D1
        H -->|regex| H0[Lead generator parsing<br/>TruLead, Zillow, Realtor.com, etc.]
        H -->|Claude Sonnet| H1[General email contact extraction]
        H0 --> H2[Dedup + Stage Classification]
        H1 --> H2
        H2 --> H3["List&lt;ImportedContact&gt;"]
    end

    subgraph Phase3["Phase 3: Persist (enhanced)"]
        I[AgentProfilePersistActivity<br/>existing — skill files]
        J[ContactImportPersistActivity<br/>NEW — folders + file copy + leads]
        K[BrandMergeActivity<br/>existing]
    end

    subgraph Phase4["Phase 4: Welcome"]
        L[WelcomeNotificationService<br/>Opus 4.6 via Gmail]
    end

    C --> Phase1
    Phase1 --> Phase2
    Phase2 --> Phase25
    Phase25 --> Phase3
    Phase3 --> Phase4
    Phase4 --> M["[ACTV-003] Pipeline Complete"]
```

---

## PDF Extraction Flow (DriveIndexWorker Enhancement)

```mermaid
flowchart TD
    A[DriveIndexWorker.RunAsync] --> B[ListAllFilesAsync<br/>485+ files]
    B --> C{IsRealEstateFile?}
    C -->|No| D[Skip]
    C -->|Yes ~50 docs| E{MIME type?}

    E -->|PDF| F[DownloadPdfBytesAsync]
    F --> G[PdfPageExtractor<br/>Extract first 3 pages as images]
    G --> H[Claude Vision — Sonnet 4.6<br/>Single call per PDF]

    E -->|Google Doc / Sheets / .docx| I[GetFileContentAsync<br/>existing text extraction]
    I --> J[Claude Sonnet<br/>Extract contacts from text]

    H --> K[DocumentExtraction<br/>type, clients, property, date, terms]
    J --> K

    K --> L[Store in ClassifiedDriveIndex<br/>DocumentExtractions array]

    style F fill:#e1f5fe
    style G fill:#e1f5fe
    style H fill:#e1f5fe
    style J fill:#e1f5fe
    style K fill:#c8e6c9
```

**Batching:** Up to 5 PDFs processed in parallel.

**Claude Vision prompt:**
```
You are extracting structured data from a real estate document.
Return JSON with:
{
  "document_type": "listing_agreement|buyer_agreement|purchase_contract|
                    disclosure|closing_statement|cma|inspection|appraisal|other",
  "clients": [{"name": "...", "role": "buyer|seller|both", "email": "...", "phone": "..."}],
  "property": {"address": "...", "city": "...", "state": "...", "zip": "..."},
  "date": "YYYY-MM-DD",
  "key_terms": {"price": "...", "commission": "...", "contingencies": [...]}
}
Only extract what is clearly visible. Use null for missing fields.
```

**Cost:** ~$0.01-0.05 per page x 3 pages x ~30-50 relevant PDFs = $0.90-7.50

---

## Contact Classification Flow (Phase 2.5)

```mermaid
flowchart TD
    subgraph InputA["Input A: Drive Documents"]
        A1[DocumentExtractions<br/>from ClassifiedDriveIndex]
        A2[Flatten all clients<br/>from all extractions]
        A3[Attach document<br/>references to each client]
        A1 --> A2 --> A3
    end

    subgraph InputB["Input B: Emails"]
        B1[EmailCorpus<br/>from EmailFetchWorker]
        B2[Group by thread/sender]
        B2a{Known lead generator?}
        B3a[Parse structured lead notification<br/>TruLead, Zillow, Realtor.com,<br/>BoldLeads, CINC, kvCORE, etc.]
        B3b[Claude Sonnet batch<br/>Extract RE contacts from general emails]
        B4[Email-sourced contacts]
        B1 --> B2 --> B2a
        B2a -->|Yes| B3a --> B4
        B2a -->|No| B3b --> B4
    end

    A3 --> C[Merge]
    B4 --> C

    C --> D[Dedup by email or name]

    D --> E{Highest-evidence document?}

    E -->|closing_statement / HUD-1| F["4 - Closed"]
    E -->|purchase_contract| G["3 - Under Contract"]
    E -->|listing / buyer agreement| H["2 - Active Clients"]
    E -->|email inquiry only| I["1 - Leads"]
    E -->|no clear signal| I

    F --> J["List&lt;ImportedContact&gt;"]
    G --> J
    H --> J
    I --> J

    style F fill:#c8e6c9
    style G fill:#fff9c4
    style H fill:#bbdefb
    style I fill:#f5f5f5
```

---

## Activity Reuse: Two Callers, One Activity

```mermaid
flowchart TD
    subgraph Caller1["Caller 1: Activation Pipeline (one-time)"]
        A1[ActivationOrchestrator<br/>Phase 2.5] -->|full corpus + all Drive docs| CDA
    end

    subgraph Caller2["Caller 2: Gmail Inbox Check-in (scheduled, future)"]
        A2[InboxCheckInJob<br/>scheduled every N hours] -->|new emails since last check| CDA
    end

    subgraph Activity["ContactDetectionActivity (reusable)"]
        CDA[ContactDetectionActivity.ExecuteAsync]
        CDA --> PDF[PdfContactExtractor<br/>Claude Vision on new PDFs]
        CDA --> LG[LeadGeneratorPatterns<br/>regex: TruLead, Zillow, etc.]
        CDA --> EM[EmailContactExtractor<br/>Claude Sonnet for unknown senders]
        CDA --> CL[ContactClassifier<br/>dedup + stage assignment]
        PDF --> CL
        LG --> CL
        EM --> CL
        CL --> OUT["List&lt;ImportedContact&gt;"]
    end

    OUT --> P1[ContactImportPersistActivity<br/>folder creation + file copy + LeadStore]

    style Activity fill:#fff3e0
    style Caller1 fill:#e3f2fd
    style Caller2 fill:#f3e5f5
```

**Activation (Caller 1):** Processes the full email corpus (200 emails) + all Drive PDFs. One-time bulk import.

**Inbox Check-in (Caller 2, future):** Processes only emails received since the last check. Lightweight — mostly lead generator regex parsing, minimal Claude usage. Runs on a schedule (e.g., every 2 hours) or triggered by Gmail push notification.

---

## Lead Generator Detection

The agent's inbox likely contains automated lead notifications from third-party platforms. These have structured formats that are easier to parse than general emails.

**Known lead generators to detect (by sender domain or subject pattern):**

| Platform | Sender / Pattern | Data Available |
|----------|-----------------|----------------|
| TruLead | `@trulead.com` | Name, email, phone, property, intent |
| Zillow Premier Agent | `@zillow.com`, "New lead from Zillow" | Name, email, phone, property URL, budget |
| Realtor.com | `@realtor.com`, "New connection" | Name, email, phone, property, timeline |
| BoldLeads | `@boldleads.com` | Name, email, phone, property address |
| CINC | `@cincpro.com` | Name, email, phone, search criteria |
| kvCORE | `@kvcore.com`, `@insiderealestate.com` | Name, email, phone, property views |
| Ylopo | `@ylopo.com` | Name, email, phone, saved searches |
| Real Geeks | `@realgeeks.com` | Name, email, phone, viewed properties |
| BoomTown | `@boomtownroi.com` | Name, email, phone, lead score |
| Follow Up Boss | `@followupboss.com` | Forwarded lead with source attribution |
| Sierra Interactive | `@sierraint.com` | Name, email, phone, property alerts |

**Detection approach:**
1. First pass: regex match on sender email domain against known platforms
2. For matched emails: parse the structured notification format (each platform has a consistent template)
3. For unmatched emails: send to Claude Sonnet for general contact extraction

This avoids sending 200 emails to Claude when 80% are parseable with simple regex. Saves tokens and is faster.

---

## Folder Creation + File Organization (Phase 3)

```mermaid
flowchart TD
    A[ContactImportPersistActivity] --> B[Create top-level folders]

    B --> B1["1 - Leads/"]
    B --> B2["2 - Active Clients/"]
    B --> B3["3 - Under Contract/"]
    B --> B4["4 - Closed/"]
    B --> B5["5 - Inactive/"]

    A --> C{For each ImportedContact}

    C -->|Stage: Lead| D1["1 - Leads/{Name}/"]
    D1 --> D2["{Property Address}/"]
    D2 --> D3["Communications/"]

    C -->|Stage: Active Client| E1["2 - Active Clients/{Name}/"]
    E1 --> E2["Agreements/"]
    E1 --> E3["Documents Sent/"]
    E1 --> E4["Communications/"]
    E2 -->|copy| E5["listing/buyer agreements"]

    C -->|Stage: Under Contract| F1["3 - Under Contract/{Name}/"]
    F1 --> F2["{Address} Transaction/"]
    F2 --> F3["Contracts/"]
    F2 --> F4["Inspection/"]
    F2 --> F5["Appraisal/"]
    F2 --> F6["Communications/"]
    F3 -->|copy| F7["purchase contracts"]
    F4 -->|copy| F8["inspection reports"]
    F5 -->|copy| F9["appraisal reports"]

    C -->|Stage: Closed| G1["4 - Closed/{Name}/"]
    G1 --> G2["Audit Log/"]
    G1 --> G3["Reports/"]
    G1 --> G4["Communications/"]
    G2 -->|copy| G5["closing statements"]
    G3 -->|copy| G6["CMAs"]

    A --> H[Save to ILeadStore]
    A --> I["Create 'Client Import Summary' spreadsheet"]

    style B1 fill:#f5f5f5
    style B2 fill:#bbdefb
    style B3 fill:#fff9c4
    style B4 fill:#c8e6c9
    style B5 fill:#ffcdd2
```

---

## Data Model

```mermaid
classDiagram
    class ClassifiedDriveIndex {
        +string FolderId
        +List~ClassifiedFile~ Files
        +Dict~string,string~ Contents
        +List~string~ DiscoveredUrls
        +List~PropertyGroup~ Properties
        +List~DocumentExtraction~ Extractions %%NEW
    }

    class DocumentExtraction {
        +string DriveFileId
        +string FileName
        +DocumentType Type
        +List~ExtractedClient~ Clients
        +ExtractedProperty Property
        +DateTime Date
        +ExtractedKeyTerms KeyTerms
    }

    class ExtractedClient {
        +string Name
        +ContactRole Role
        +string Email
        +string Phone
    }

    class ExtractedProperty {
        +string Address
        +string City
        +string State
        +string Zip
    }

    class ExtractedKeyTerms {
        +string Price
        +string Commission
        +List~string~ Contingencies
    }

    class ImportedContact {
        +string Name
        +string Email
        +string Phone
        +ContactRole Role
        +PipelineStage Stage
        +string PropertyAddress
        +List~DocumentReference~ Documents
    }

    class DocumentReference {
        +string DriveFileId
        +string FileName
        +DocumentType Type
        +DateTime Date
    }

    class DocumentType {
        <<enumeration>>
        ListingAgreement
        BuyerAgreement
        PurchaseContract
        Disclosure
        ClosingStatement
        Cma
        Inspection
        Appraisal
        Other
    }

    class ContactRole {
        <<enumeration>>
        Buyer
        Seller
        Both
        Unknown
    }

    class PipelineStage {
        <<enumeration>>
        Lead
        ActiveClient
        UnderContract
        Closed
    }

    ClassifiedDriveIndex "1" --> "*" DocumentExtraction : extractions
    DocumentExtraction "1" --> "*" ExtractedClient
    DocumentExtraction "1" --> "0..1" ExtractedProperty
    DocumentExtraction "1" --> "0..1" ExtractedKeyTerms
    DocumentExtraction --> DocumentType
    ImportedContact "1" --> "*" DocumentReference
    ImportedContact --> ContactRole
    ImportedContact --> PipelineStage
    DocumentReference --> DocumentType
    ExtractedClient --> ContactRole
```

---

## Sequence: Single PDF Extraction

```mermaid
sequenceDiagram
    participant DW as DriveIndexWorker
    participant GD as IGDriveClient
    participant PE as PdfPageExtractor
    participant CV as Claude Vision (Sonnet 4.6)

    DW->>GD: DownloadBinaryAsync(fileId)
    GD-->>DW: byte[] pdfBytes

    DW->>PE: ExtractPages(pdfBytes, maxPages: 3)
    PE-->>DW: List<byte[]> pageImages

    DW->>CV: SendAsync(images + extraction prompt)
    Note over CV: Extracts document_type,<br/>clients[], property,<br/>date, key_terms

    CV-->>DW: DocumentExtraction JSON

    DW->>DW: Store in ClassifiedDriveIndex.Extractions[]
```

---

## C# Domain Models

```csharp
public sealed record DocumentExtraction(
    string DriveFileId,
    string FileName,
    DocumentType Type,
    IReadOnlyList<ExtractedClient> Clients,
    ExtractedProperty? Property,
    DateTime? Date,
    ExtractedKeyTerms? KeyTerms);

public sealed record ExtractedClient(
    string Name,
    ContactRole Role,
    string? Email,
    string? Phone);

public sealed record ExtractedProperty(
    string Address,
    string? City,
    string? State,
    string? Zip);

public sealed record ExtractedKeyTerms(
    string? Price,
    string? Commission,
    IReadOnlyList<string> Contingencies);

[JsonStringEnumConverter]
public enum DocumentType
{
    ListingAgreement, BuyerAgreement, PurchaseContract,
    Disclosure, ClosingStatement, Cma, Inspection,
    Appraisal, Other
}

public sealed record ImportedContact(
    string Name,
    string? Email,
    string? Phone,
    ContactRole Role,
    PipelineStage Stage,
    string? PropertyAddress,
    IReadOnlyList<DocumentReference> Documents);

[JsonStringEnumConverter]
public enum ContactRole { Buyer, Seller, Both, Unknown }

[JsonStringEnumConverter]
public enum PipelineStage { Lead, ActiveClient, UnderContract, Closed }

public sealed record DocumentReference(
    string DriveFileId,
    string FileName,
    DocumentType Type,
    DateTime? Date);
```

---

## Project Structure

```
Workers/Activation/
  RealEstateStar.Workers.Activation.DriveIndex/  (MODIFIED)
    DriveIndexWorker.cs        -- add PDF download + Claude Vision extraction
    PdfPageExtractor.cs        -- convert PDF pages to images (NEW)

Activities/Leads/
  RealEstateStar.Activities.Lead.ContactDetection/  (NEW -- reusable Activity)
    ContactDetectionActivity.cs    -- orchestrates extraction + classification
    PdfContactExtractor.cs         -- Claude Vision extraction from PDF pages
    EmailContactExtractor.cs       -- lead generator regex + Claude Sonnet batch
    ContactClassifier.cs           -- dedup + stage classification
    LeadGeneratorPatterns.cs       -- known sender domains + parsing templates

Activities/Activation/
  RealEstateStar.Activities.Activation.ContactImportPersist/  (NEW)
    ContactImportPersistActivity.cs

Domain/Activation/Models/
  DocumentExtraction.cs  (NEW) -- extracted contact/property/terms per document
  ImportedContact.cs     (NEW) -- classified contact with stage + documents
```

**Why an Activity, not a Worker:** The contact detection logic needs to be called from two places:
1. Activation pipeline (Phase 2.5) -- one-time import of all existing contacts
2. Future Gmail inbox check-in (scheduled) -- periodic scan for new lead notifications

An Activity is callable by any orchestrator. A Worker is tied to a specific pipeline.

**Dependencies:**
- DriveIndexWorker: Domain + Workers.Shared (already has IAnthropicClient access via orchestrator)
- ContactDetectionActivity: Domain + IAnthropicClient + IGDriveClient (calls Claude + reads PDFs)
- ContactImportPersistActivity: Domain + IFileStorageProviderFactory + ILeadStore

---

## Observability

```
ActivitySource: "RealEstateStar.ContactImport"

Spans:
  contact-import.pdf-extract      -- per PDF, tags: file_id, document_type
  contact-import.email-extract    -- per batch
  contact-import.classify         -- full classification pass
  contact-import.persist          -- folder creation + file copy + lead store

Counters:
  contacts.imported               -- by stage dimension
  pdfs.processed                  -- total PDFs sent to Claude Vision
  pdfs.pages_read                 -- total pages (max 3 per doc)
  contacts.duplicates_merged      -- dedup count

Error codes:
  CONTACT-001  PDF extraction succeeded
  CONTACT-002  Email extraction succeeded
  CONTACT-003  Classification complete
  CONTACT-010  PDF extraction failed (per file, non-fatal)
  CONTACT-011  Email extraction failed
  CONTACT-012  Classification failed
  CONTACT-020  Persist failed
```

---

## Cost Summary

| Component | Estimated Cost |
|-----------|---------------|
| PDF extraction (50 docs x 3 pages x Claude Vision) | $1.50-7.50 |
| Email contact extraction (batch Sonnet) | $0.30-0.50 |
| Classification + dedup | negligible |
| **Total per activation** | **$1.80-8.00** |
| **With filename pre-filter (30 relevant PDFs)** | **$0.90-4.50** |

**Full activation cost per agent (including existing pipeline):**

| Component | Cost |
|-----------|------|
| Existing activation (12 workers + welcome) | $0.80-1.20 |
| Contact import (new) | $0.90-4.50 |
| Container compute (~5 min) | $0.02 |
| **Total** | **$1.72-5.72** |
