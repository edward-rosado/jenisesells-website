# CMA Pipeline Architecture

## End-to-End Flow

```mermaid
sequenceDiagram
    participant Lead as Home Seller
    participant Site as Agent Site<br/>(Next.js)
    participant Form as CmaSection<br/>(Client Component)
    participant API as .NET API
    participant Search as Web Search
    participant PDF as QuestPDF
    participant GWS as Google Workspace
    participant Agent as Real Estate Agent

    Lead->>Site: Visits {handle}.real-estate-star.com
    Site->>Lead: Renders branded agent site

    Lead->>Form: Fills CMA form<br/>(address, name, email, phone)
    Form->>Form: Client-side validation

    Form->>API: POST /agents/{id}/cma
    API-->>Form: { jobId, status: "processing" }

    Form->>Site: Redirect to /thank-you

    rect rgb(240, 248, 255)
        Note over API,GWS: Background Processing (9 steps)
        API->>API: Load agent config<br/>config/agents/{id}.json
        API->>Search: Search comparable sales<br/>(3-5 comps, ±15% sqft, 6mo)
        Search-->>API: Comp results

        API->>API: Claude analysis<br/>(valuation, narrative, insights)

        API->>PDF: Generate CMA Report
        Note over PDF: QuestPDF (.NET)<br/>Agent branding<br/>Adaptive depth by timeline

        PDF-->>API: CMA PDF file

        API->>GWS: gws drive upload<br/>(store in agent's Drive folder)
        API->>GWS: gws sheets append<br/>(log lead in tracking sheet)
        API->>GWS: gws gmail send<br/>(email PDF to lead)
    end

    GWS-->>Lead: Email with CMA PDF attached
    Lead-->>Agent: Lead engages (call/reply)
```

## CMA Form Data Flow

```mermaid
flowchart LR
    subgraph "Client (Browser)"
        form["CmaSection.tsx<br/>uses useCmaSubmit hook<br/>from packages/ui"]
        fields["LeadForm fields:<br/>firstName, lastName<br/>email, phone<br/>address, city, state, zip<br/>timeline, notes"]
    end

    subgraph ".NET API"
        endpoint["POST /agents/{id}/cma"]
        config["Agent Config Loader"]
        pipeline["CMA Pipeline<br/>(9 steps)"]
    end

    form --> fields
    fields -->|"mapToCmaRequest()"| endpoint
    endpoint --> config
    config --> pipeline

    style form fill:#4A90D9,color:#fff
    style endpoint fill:#7B68EE,color:#fff
    style pipeline fill:#2E7D32,color:#fff
```

## CMA PDF Structure

| Page | Content | Data Source |
|------|---------|------------|
| 1 | Cover — title, property address, agent info | `agent.identity.*`, form data |
| 2 | Subject property overview | Form submission + enrichment |
| 3 | Comparable sales table (3-5 comps) | Web search results |
| 4 | Valuation estimate (low-high range) | Calculated from comps |
| 5 | About the agent + credentials | `agent.identity.*`, content.about |

All pages use `agent.branding.*` for colors and fonts via QuestPDF styling.
