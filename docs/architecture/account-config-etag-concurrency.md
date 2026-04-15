# AccountConfig ETag concurrency

Two concurrent brokerage join writers racing on the same `account.json`, reusing the existing `ITokenStore` ETag pattern. One writer succeeds on first attempt; the other gets 412, re-reads, and retries successfully.

```mermaid
sequenceDiagram
    participant W1 as Writer 1<br/>Noelle joins
    participant W2 as Writer 2<br/>Bob joins
    participant S as AccountConfigService
    participant T as Azure Table<br/>accounts
    participant Met as AccountConfigDiagnostics

    par Concurrent reads
        W1->>S: GetAccountAsync glr
        S->>T: ReadEntity
        T-->>S: AccountConfig ETag v1
        S-->>W1: account with ETag v1
    and
        W2->>S: GetAccountAsync glr
        S->>T: ReadEntity
        T-->>S: AccountConfig ETag v1
        S-->>W2: account with ETag v1
    end
    W1->>W1: Append Noelle to agents
    W2->>W2: Append Bob to agents
    par Concurrent writes
        W1->>S: SaveIfUnchangedAsync ETag v1
        S->>T: UpdateEntity If-Match v1
        T-->>S: 200 OK ETag now v2
        S-->>W1: true saved
    and
        W2->>S: SaveIfUnchangedAsync ETag v1
        S->>T: UpdateEntity If-Match v1
        T-->>S: 412 Precondition Failed
        S-->>W2: false conflict
    end
    W2->>Met: Conflicts.Add 1
    W2->>S: GetAccountAsync glr retry
    S->>T: ReadEntity
    T-->>S: AccountConfig ETag v2<br/>now contains Noelle
    S-->>W2: account with ETag v2
    W2->>W2: Append Bob to agents<br/>now Noelle plus Bob
    W2->>S: SaveIfUnchangedAsync ETag v2
    S->>T: UpdateEntity If-Match v2
    T-->>S: 200 OK ETag now v3
    S-->>W2: true saved
    Note over T: Final state contains<br/>Noelle and Bob<br/>No updates lost
```
