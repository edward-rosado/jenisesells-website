# Architecture Documentation

Architecture diagrams for Real Estate Star, rendered as Mermaid diagrams viewable directly on GitHub.

## Diagrams

| Document | Description |
|----------|------------|
| [System Overview](system-overview.md) | Monorepo structure, app relationships, tech stack |
| [Multi-Tenant Routing](multi-tenant-routing.md) | DNS → middleware → config → template request flow |
| [CMA Pipeline](cma-pipeline.md) | End-to-end CMA flow: form → API → PDF → email |
| [Template Rendering](template-rendering.md) | Config loading → template selection → section rendering → CSS variables |
| [Data Model](data-model.md) | Agent config entity model, section types, file relationships |
| [Skill Integration](skill-integration.md) | Config-driven skills, onboarding flow, field mapping |

## How to Read

All diagrams use [Mermaid](https://mermaid.js.org/) syntax. GitHub renders them natively in Markdown preview. For local viewing, use a Mermaid-compatible Markdown viewer or the [Mermaid Live Editor](https://mermaid.live/).

## Color Key

| Color | Meaning |
|-------|---------|
| Blue (#4A90D9) | Next.js frontend applications |
| Purple (#7B68EE) | .NET backend / API |
| Green (#2E7D32) | Skills / AI workflows |
| Gold (#C8A951) | Configuration / data |
