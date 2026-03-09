# CMA Pipeline Architecture

## End-to-End Flow

```mermaid
sequenceDiagram
    participant Lead as Home Seller
    participant Site as Agent Site<br/>(Next.js)
    participant Form as CmaForm<br/>(Client Component)
    participant API as .NET API
    participant Search as Web Search
    participant PDF as QuestPDF
    participant GWS as Google Workspace
    participant Agent as Real Estate Agent

    Lead->>Site: Visits jenise-buckalew.realestatestar.com
    Site->>Lead: Renders branded agent site

    Lead->>Form: Fills CMA form<br/>(address, name, email, phone)
    Form->>Form: Client-side validation

    alt MVP (Formspree)
        Form->>API: POST formspree.io/f/{id}
        API-->>Form: 200 OK
    else Production (.NET API)
        Form->>API: POST /agents/{id}/cma
    end

    Form->>Site: Redirect to /thank-you

    rect rgb(240, 248, 255)
        Note over API,GWS: Background Processing
        API->>API: Load agent config<br/>config/agents/{id}.json
        API->>Search: Search comparable sales<br/>(3-5 comps, ±15% sqft, 6mo)
        Search-->>API: Comp results

        API->>API: Calculate valuation<br/>(avg/median $/sqft × subject sqft)

        API->>PDF: Generate CMA Report
        Note over PDF: QuestPDF (.NET)<br/>Agent branding<br/>Cover + Comps + Valuation

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
        form["CmaForm.tsx<br/>(Client Component)"]
        fields["Fields:<br/>firstName, lastName<br/>email, phone<br/>address, city, state, zip<br/>timeline, notes"]
    end

    subgraph "Routing Decision"
        handler{form_handler<br/>config value?}
    end

    subgraph "External"
        formspree["Formspree<br/>formspree.io/f/{id}"]
    end

    subgraph ".NET API"
        endpoint["POST /agents/{id}/cma"]
        config["Agent Config Loader"]
        pipeline["CMA Pipeline"]
    end

    form --> fields
    fields --> handler
    handler -->|"formspree"| formspree
    handler -->|"custom/api"| endpoint
    endpoint --> config
    config --> pipeline

    style form fill:#4A90D9,color:#fff
    style formspree fill:#C8A951,color:#000
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
