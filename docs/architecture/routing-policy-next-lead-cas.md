# Routing policy next_lead CAS consumption

Two leads arriving simultaneously at a brokerage with `next_lead: alice` set in Drive's `routing-policy.json`. Only one lead goes to Alice; the other falls through to algorithmic routing. The CAS is on the Azure Table **consumption row** — Drive is read-only on this path because `IFileStorageProvider` has no conditional-write primitive.

```mermaid
sequenceDiagram
    participant L1 as Lead 1<br/>Noelle in Carteret
    participant L2 as Lead 2<br/>buyer in Edison
    participant R as RoutingService
    participant D as Drive<br/>routing-policy.json
    participant T as Azure Table<br/>brokerage-routing-consumption

    par Both leads arrive simultaneously
        L1->>R: route_lead Lead 1
        R->>D: Read routing-policy.json
        D-->>R: policy next_lead alice<br/>human-editable
        R->>R: currentHash H1
        R->>T: Read consumption row<br/>PK glr
    and
        L2->>R: route_lead Lead 2
        R->>D: Read routing-policy.json
        D-->>R: policy next_lead alice<br/>human-editable
        R->>R: currentHash H1
        R->>T: Read consumption row<br/>PK glr
    end
    T-->>R: row PolicyContentHash H1<br/>OverrideConsumed false<br/>Counter 3 ETag e1
    T-->>R: row PolicyContentHash H1<br/>OverrideConsumed false<br/>Counter 3 ETag e1
    Note over R: Both see the override available
    par Both try to consume via single CAS on Azure Table
        R->>T: UpdateEntity<br/>OverrideConsumed true<br/>ConsumedByLeadId L1<br/>If-Match e1
        T-->>R: 200 saved ETag e2
        R-->>L1: Routed to Alice<br/>reason manual-override
    and
        R->>T: UpdateEntity<br/>OverrideConsumed true<br/>ConsumedByLeadId L2<br/>If-Match e1
        T-->>R: 412 precondition failed
        R->>R: Log LEAD-ROUTE-OVERRIDE-002<br/>retry attempt 2
    end
    Note over R: L2 retries the whole decision
    R->>D: Re-read routing-policy.json
    D-->>R: policy next_lead alice<br/>still there human unchanged
    R->>R: currentHash still H1
    R->>T: Re-read consumption row
    T-->>R: row PolicyContentHash H1<br/>OverrideConsumed true<br/>ConsumedByLeadId L1 ETag e2
    Note over R: Override already consumed<br/>for this hash<br/>Fall through to algorithmic
    R->>R: Filter by Edison service area<br/>Score by specialty<br/>Jenise wins on language match
    R->>T: UpdateEntity<br/>Counter 4<br/>If-Match e2
    T-->>R: 200 saved ETag e3
    R-->>L2: Routed to Jenise<br/>reason specialty-match
    Note over D: Drive file is never written.<br/>routing-policy.json still<br/>shows next_lead alice —<br/>OverrideConsumed in the<br/>Azure Table row is the<br/>source of truth.
```

**Why the CAS lives in Azure Table and not in Drive**: `GDriveApiClient.UploadAsync` is an unconditional write — there is no If-Match header, no revision precondition, no way to atomically observe-and-mutate a field in Drive. Moving both mutations (override consumption AND counter increment) onto a single Azure Table row keyed by the policy's content hash gives us real ETag CAS via the same `SaveIfUnchangedAsync` primitive already implemented in `AzureTableTokenStore`. Drive remains the human-authored policy; the Azure Table row decides who gets the lead.

**How a brokerage owner re-issues the override**: they edit `routing-policy.json` in Drive — even a no-op save counts, because `_last_edited_at` changes, which changes the content hash, which realigns the Azure Table row on the next routing decision with `OverrideConsumed = false`.
