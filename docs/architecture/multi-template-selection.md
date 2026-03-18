# Multi-Template Selection

How agent site requests are resolved to a specific template and its section variants.

```mermaid
flowchart TD
    A["Browser Request<br/>{handle}.real-estate-star.com"] --> B["Middleware<br/>Extract handle from hostname"]
    B --> C["Config Registry<br/>Prebuild: JSON to TypeScript"]
    C --> D["Load agent config + content"]
    D --> E{"content.template?"}
    E -->|"emerald-classic"| T1["EmeraldClassic"]
    E -->|"modern-minimal"| T2["ModernMinimal"]
    E -->|"warm-community"| T3["WarmCommunity"]
    E -->|"luxury-estate"| T4["LuxuryEstate"]
    E -->|"urban-loft"| T5["UrbanLoft"]
    E -->|"new-beginnings"| T6["NewBeginnings"]
    E -->|"light-luxury"| T7["LightLuxury"]
    E -->|"country-estate"| T8["CountryEstate"]
    E -->|"coastal-living"| T9["CoastalLiving"]
    E -->|"commercial"| T10["Commercial"]
    E -->|"unknown / missing"| T1

    T1 --> I["Render sections<br/>from content.sections"]
    T2 --> I
    T3 --> I
    T4 --> I
    T5 --> I
    T6 --> I
    T7 --> I
    T8 --> I
    T9 --> I
    T10 --> I

    I --> J{"section.enabled?"}
    J -->|true| K["Render variant component<br/>with section.data"]
    J -->|false| L["Skip section"]

    style A fill:#4A90D9,color:#fff
    style C fill:#C8A951,color:#000
    style T1 fill:#2E7D32,color:#fff
    style T2 fill:#2E7D32,color:#fff
    style T3 fill:#2E7D32,color:#fff
    style T4 fill:#2E7D32,color:#fff
    style T5 fill:#2E7D32,color:#fff
    style T6 fill:#2E7D32,color:#fff
    style T7 fill:#2E7D32,color:#fff
    style T8 fill:#2E7D32,color:#fff
    style T9 fill:#2E7D32,color:#fff
    style T10 fill:#2E7D32,color:#fff
```
