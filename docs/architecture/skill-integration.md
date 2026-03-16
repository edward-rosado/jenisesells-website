# Skill Integration Architecture

## Config-Driven Skill System

```mermaid
flowchart TD
    subgraph "Agent Config (Single Source of Truth)"
        config["config/agents/{id}.json"]
        identity["identity.*<br/>name, phone, email, brokerage"]
        location["location.*<br/>state, service_areas"]
        branding["branding.*<br/>colors, fonts"]
        integrations["integrations.*<br/>email_provider"]
        compliance["compliance.*<br/>state_form, licensing_body"]
    end

    subgraph "Skills"
        cma["CMA Skill<br/>skills/cma/"]
        contracts["Contracts Skill<br/>skills/contracts/"]
        emailskill["Email Skill<br/>skills/email/"]
        deployskill["Deploy Skill<br/>skills/deploy/"]
    end

    subgraph "External Services"
        gmail["Gmail API"]
        outlook["Outlook API"]
        cfpages["Cloudflare Pages"]
        gws["Google Workspace<br/>(Drive, Sheets, Docs)"]
    end

    config --> identity
    config --> location
    config --> branding
    config --> integrations
    config --> compliance

    identity --> cma
    identity --> contracts
    identity --> emailskill
    location --> cma
    location --> contracts
    branding --> cma
    branding --> deployskill
    integrations --> emailskill
    integrations --> deployskill
    compliance --> contracts

    emailskill -->|gmail| gmail
    emailskill -->|outlook| outlook
    cma --> gws
    deployskill --> cfpages

    style config fill:#C8A951,color:#000
    style cma fill:#2E7D32,color:#fff
    style contracts fill:#2E7D32,color:#fff
    style emailskill fill:#2E7D32,color:#fff
    style deployskill fill:#2E7D32,color:#fff
```

## Agent Onboarding Flow

```mermaid
flowchart TD
    subgraph "Pass 1: WOW Moment (< 5 min)"
        start["New agent signs up"]
        scrape["Scrape public data<br/>(name, brokerage, phone, license)"]
        genConfig["Generate agent config JSON"]
        genContent["Generate default content"]
        deploy1["Deploy white-label site"]
        live["Agent site LIVE<br/>{id}.realestatestar.com"]
    end

    subgraph "Pass 2: Enrichment"
        portal["Agent logs into Portal"]
        customize["Customize content<br/>(bio, testimonials, sold homes)"]
        branding_edit["Upload logo, adjust colors"]
        integrations_setup["Connect Gmail, set up forms"]
        deploy2["Redeploy with enriched content"]
    end

    subgraph "Pass 3: Full Automation"
        cma_setup["Enable CMA pipeline"]
        contract_setup["Configure state contracts"]
        email_setup["Set up auto-replies"]
        full["Fully automated agent workflow"]
    end

    start --> scrape --> genConfig --> genContent --> deploy1 --> live
    live --> portal --> customize --> branding_edit --> integrations_setup --> deploy2
    deploy2 --> cma_setup --> contract_setup --> email_setup --> full

    style start fill:#4A90D9,color:#fff
    style live fill:#2E7D32,color:#fff
    style full fill:#C8A951,color:#000
```

## Skill → Config Field Mapping

| Skill | identity.* | location.* | branding.* | integrations.* | compliance.* |
|-------|-----------|-----------|-----------|---------------|-------------|
| **CMA** | name, phone, email, brokerage, title, tagline, languages | state, service_areas | primary_color, accent_color, font_family | email_provider | — |
| **Contracts** | name, title, license_id, brokerage, brokerage_id | state | — | — | state_form, licensing_body, disclosure_requirements |
| **Email** | name, title, phone, email, brokerage, tagline, languages | — | — | email_provider | — |
| **Deploy** | website | — | primary_color, secondary_color, accent_color, font_family | hosting | — |
