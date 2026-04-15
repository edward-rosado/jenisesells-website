# Routing policy next_lead CAS consumption

Two leads arriving simultaneously at a brokerage with `next_lead: alice` set in `routing-policy.json`. Only one lead goes to Alice; the other falls through to algorithmic routing via CAS on `SaveIfUnchangedAsync`.

```mermaid
sequenceDiagram
    participant L1 as Lead 1<br/>Noelle in Carteret
    participant L2 as Lead 2<br/>buyer in Edison
    participant R as RoutingService
    participant D as Drive<br/>routing-policy.json
    participant T as Azure Table<br/>counter

    par Both leads arrive roughly simultaneously
        L1->>R: route_lead Lead 1
        R->>D: Read routing-policy.json
        D-->>R: policy next_lead alice<br/>_version 7 _etag v1
    and
        L2->>R: route_lead Lead 2
        R->>D: Read routing-policy.json
        D-->>R: policy next_lead alice<br/>_version 7 _etag v1
    end
    Note over R: Both have seen next_lead alice
    par Both try to consume the override
        R->>D: SaveIfUnchangedAsync<br/>next_lead null<br/>_version 8<br/>If-Match v1
        D-->>R: 200 saved<br/>new ETag v2
        R-->>L1: Routed to Alice<br/>reason manual-override
    and
        R->>D: SaveIfUnchangedAsync<br/>next_lead null<br/>_version 8<br/>If-Match v1
        D-->>R: 412 precondition failed
        R-->>L2: Fell through
    end
    R->>R: Log LEAD-ROUTE-OVERRIDE-002<br/>override lost to concurrent lead
    R->>D: Re-read routing-policy.json
    D-->>R: policy next_lead null<br/>_version 8 _etag v2
    Note over R: next_lead already cleared<br/>Proceed with algorithmic routing
    R->>R: Filter by Edison service area<br/>Score by specialty
    R->>T: Read counter partition glr
    T-->>R: counter 3 policyVersion 8
    Note over R: Version matches<br/>no reset needed
    R->>T: UpdateEntity counter 4<br/>If-Match ETag
    T-->>R: 200 saved
    R-->>L2: Routed to Jenise<br/>reason specialty-match
```
