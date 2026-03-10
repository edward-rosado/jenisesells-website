# System Architecture Overview

## Monorepo Structure

```mermaid
graph TB
    subgraph "Real Estate Star Monorepo"
        subgraph "Applications"
            portal["apps/portal<br/>Admin Dashboard<br/>(Next.js 16)"]
            agentsite["apps/agent-site<br/>White-Label Websites<br/>(Next.js 16)"]
            api["apps/api<br/>Backend API<br/>(.NET 10)"]
        end

        subgraph "Shared Packages"
            types["packages/shared-types<br/>TypeScript Interfaces"]
            ui["packages/ui<br/>Component Library"]
        end

        subgraph "Skills (AI Workflows)"
            cma["skills/cma<br/>CMA Generator"]
            contracts["skills/contracts<br/>Contract Drafting"]
            email["skills/email<br/>Email Sending"]
            deploy["skills/deploy<br/>Site Deployment"]
        end

        subgraph "Configuration"
            schema["config/agent.schema.json<br/>JSON Schema"]
            agents["config/agents/*.json<br/>Per-Tenant Profiles"]
            content["config/agents/*.content.json<br/>Per-Tenant Content"]
        end

        subgraph "Infrastructure"
            infra["infra/<br/>Hosting & CI/CD"]
            proto["prototype/<br/>Original Static Site"]
        end
    end

    portal --> types
    agentsite --> types
    portal --> ui
    agentsite --> ui
    api --> schema
    agentsite --> agents
    agentsite --> content
    cma --> agents
    contracts --> agents
    email --> agents
    deploy --> agents
    agents -.->|validates| schema

    style portal fill:#4A90D9,color:#fff
    style agentsite fill:#4A90D9,color:#fff
    style api fill:#7B68EE,color:#fff
    style cma fill:#2E7D32,color:#fff
    style contracts fill:#2E7D32,color:#fff
    style email fill:#2E7D32,color:#fff
    style deploy fill:#2E7D32,color:#fff
    style schema fill:#C8A951,color:#000
    style agents fill:#C8A951,color:#000
    style content fill:#C8A951,color:#000
```

## Technology Stack

| Layer | Technology | Purpose |
|-------|-----------|---------|
| Frontend | Next.js 16, React 19, TypeScript | Portal + Agent Sites |
| Styling | Tailwind CSS 4, CSS Variables | Responsive, per-agent branding |
| Backend | .NET 10, C# | API, PDF generation, integrations |
| PDF | QuestPDF | CMA report generation |
| Config | JSON Schema (2020-12) | Agent profile validation |
| Hosting | Cloudflare Pages | Agent site CDN + edge rendering |
| Email | Gmail / Outlook / SMTP | Multi-provider via config |
| CI/CD | GitHub Actions | Build, test, deploy |
| PM | GitHub Issues + Projects | Free project management |
