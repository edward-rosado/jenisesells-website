# Multi-Tenant Routing Architecture

## Request Flow

```mermaid
sequenceDiagram
    participant Browser
    participant DNS
    participant CF as Cloudflare Pages
    participant MW as Next.js Middleware
    participant RT as routing.ts
    participant CFG as config.ts
    participant TPL as Template Engine
    participant Cache as ISR Cache

    Browser->>DNS: jenise-buckalew.realestatestar.com
    DNS->>CF: Route to Cloudflare Pages
    CF->>MW: Forward request

    MW->>RT: extractAgentId(hostname)
    RT-->>MW: "jenise-buckalew"

    alt Cache Hit (< 60s)
        MW->>Cache: Check ISR cache
        Cache-->>Browser: Cached HTML
    else Cache Miss / Stale
        MW->>MW: Rewrite URL with ?agentId=jenise-buckalew
        MW->>CFG: loadAgentConfig("jenise-buckalew")
        CFG-->>MW: AgentConfig (identity, branding, location)
        MW->>CFG: loadAgentContent("jenise-buckalew")
        CFG-->>MW: AgentContent (template, sections)
        MW->>TPL: Render template with config + content
        TPL-->>Cache: Store rendered HTML (60s TTL)
        Cache-->>Browser: Fresh HTML
    end
```

## Subdomain Extraction Logic

```mermaid
flowchart TD
    A[Incoming Request] --> B{Extract hostname}
    B --> C[hostname = request.headers.host]
    C --> D{Match base domain?}
    D -->|realestatestar.com| E[Extract subdomain]
    D -->|localhost:*| F[Extract subdomain]
    D -->|No match| G[Return null — default agent]

    E --> H{Reserved subdomain?}
    F --> H
    H -->|www, api, portal, app, admin| G
    H -->|Valid agent ID| I[Return agent-id]

    I --> J[Middleware rewrites URL]
    J --> K[?agentId=agent-id added]
    K --> L[x-agent-id header set]
    L --> M[Page component receives agentId]

    G --> N[Fallback to DEFAULT_AGENT_ID env]
    N --> M

    style I fill:#2E7D32,color:#fff
    style G fill:#C8A951,color:#000
```

## Key Design Decisions

1. **Subdomain-based routing** — Each agent gets `{id}.realestatestar.com`, no path-based routing
2. **URL rewriting** — Middleware rewrites the URL rather than redirecting, preserving the clean subdomain URL
3. **Header propagation** — `x-agent-id` header set for downstream API calls
4. **ISR caching** — 60-second revalidation balances freshness with performance
5. **Fallback chain** — `searchParams.agentId` → `DEFAULT_AGENT_ID` env → `"jenise-buckalew"` hardcoded default
