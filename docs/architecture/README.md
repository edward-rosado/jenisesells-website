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

## Shared LeadForm Component

| Document | Description |
|----------|------------|
| [Component Hierarchy](shared-lead-form-component-hierarchy.md) | How shared packages are consumed by agent sites and platform |
| [Data Flow](lead-form-data-flow.md) | User input through submission to the .NET CMA API |
| [Data Model](lead-form-data-model.md) | Shared type hierarchy for buyer and seller lead capture |
| [Google Maps Autocomplete](google-maps-autocomplete-lifecycle.md) | Lazy SDK loading and address autocomplete lifecycle |

## CMA Integration & Multi-Template

| Document | Description |
|----------|------------|
| [CMA Form Submission Flow](cma-form-submission-flow.md) | Seller vs buyer lead routing through shared hook to .NET API |
| [Multi-Template Composition](multi-template-composition.md) | How 10 templates compose section variants from the shared library |
| [Multi-Template Selection](multi-template-selection.md) | How requests resolve to one of 10 templates and render enabled sections |
| [Section Variant Architecture](section-variant-architecture.md) | How section categories map to template-specific variants across all 10 templates |
| [Shared Package Dependencies](shared-package-dependencies.md) | How packages/ui and shared-types are consumed by apps |

## Compliance and Legal

| Document | Description |
|----------|------------|
| [Compliance Component Hierarchy](compliance-component-hierarchy.md) | How shared TCPA, EHO, and CMA compliance components flow through apps |
| [Legal Page Rendering](legal-page-rendering.md) | Dynamic state-specific content rendering on terms and privacy pages |
| [Legal Content System](legal-content-system.md) | Above/below-the-fold legal markdown discovery and rendering pipeline |

## How to Read

All diagrams use [Mermaid](https://mermaid.js.org/) syntax. GitHub renders them natively in Markdown preview. For local viewing, use a Mermaid-compatible Markdown viewer or the [Mermaid Live Editor](https://mermaid.live/).

## Color Key

| Color | Meaning |
|-------|---------|
| Blue (#4A90D9) | Next.js frontend applications |
| Purple (#7B68EE) | .NET backend / API |
| Green (#2E7D32) | Skills / AI workflows |
| Gold (#C8A951) | Configuration / data |
