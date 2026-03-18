# Section Variant Architecture

How section categories map to template-specific variants. Each template picks one variant per category. Shared sections are identical across all templates.

```mermaid
flowchart LR
    subgraph Templates ["10 Templates"]
        EC["Emerald Classic"]
        MM["Modern Minimal"]
        WC["Warm Community"]
        LE["Luxury Estate"]
        UL["Urban Loft"]
        NB["New Beginnings"]
        LL["Light Luxury"]
        CE["Country Estate"]
        CL["Coastal Living"]
        CM["Commercial"]
    end

    subgraph Heroes ["Hero Variants"]
        HG["HeroGradient"]
        HS["HeroSplit"]
        HC["HeroCentered"]
        HD["HeroDark"]
        HB["HeroBold"]
        HSt["HeroStory"]
        HA["HeroAiry"]
        HE["HeroEstate"]
        HCo["HeroCoastal"]
        HCp["HeroCorporate"]
    end

    subgraph Shared ["Shared Across All"]
        CF["CmaSection + LeadForm"]
        FT["Footer"]
    end

    EC --> HG
    MM --> HS
    WC --> HC
    LE --> HD
    UL --> HB
    NB --> HSt
    LL --> HA
    CE --> HE
    CL --> HCo
    CM --> HCp

    EC --> CF
    MM --> CF
    WC --> CF
    LE --> CF
    UL --> CF
    NB --> CF
    LL --> CF
    CE --> CF
    CL --> CF
    CM --> CF

    style EC fill:#2E7D32,color:#fff
    style MM fill:#4A90D9,color:#fff
    style WC fill:#7B68EE,color:#fff
    style LE fill:#1a1a2e,color:#D4AF37
    style UL fill:#FF6B6B,color:#fff
    style NB fill:#8FBC8F,color:#000
    style LL fill:#F5E6D3,color:#333
    style CE fill:#2D5A27,color:#fff
    style CL fill:#006994,color:#fff
    style CM fill:#1E3A5F,color:#fff
    style CF fill:#C8A951,color:#000
    style FT fill:#C8A951,color:#000
```
