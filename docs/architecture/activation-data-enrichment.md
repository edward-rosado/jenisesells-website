# Activation Data Enrichment

How shared data sources (extraction records, client reviews) flow into synthesis workers before and after the optimization. Workers that previously operated on raw emails alone now receive pre-extracted facts and client review context.

```mermaid
flowchart LR
    subgraph Sources ["Shared Data Sources"]
        EXT["Document Extractions<br/>Property, price, commission,<br/>clients, status"]
        REV["Client Reviews<br/>Rating, text, source"]
    end

    subgraph Before ["Before: Used by"]
        direction TB
        B_V["Voice — reviews"]
        B_P["Personality — reviews"]
        B_C["Coaching — reviews"]
    end

    subgraph After ["After: Now also used by"]
        direction TB
        A_MKT["Marketing Style<br/>+ reviews"]
        A_PL["Pipeline Analysis<br/>+ reviews + extractions"]
        A_FEE["Fee Structure<br/>+ reviews + extractions"]
        A_COMP["Compliance<br/>+ reviews"]
        A_COACH["Coaching<br/>+ extractions"]
    end

    REV --> B_V
    REV --> B_P
    REV --> B_C
    REV --> A_MKT
    REV --> A_PL
    REV --> A_FEE
    REV --> A_COMP

    EXT --> A_PL
    EXT --> A_FEE
    EXT --> A_COACH

    subgraph Shared ["Shared Formatters"]
        RF["ReviewFormatter<br/>Consistent format +<br/>per-worker instructions"]
        EF["ExtractionFormatter<br/>Structured facts as<br/>prompt sections"]
    end

    REV -.-> RF
    EXT -.-> EF
```
