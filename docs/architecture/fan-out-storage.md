# Fan-Out Storage

Describes how `FanOutStorageProvider` replicates lead files across three storage tiers.

## Overview

`FanOutStorageProvider` (in `RealEstateStar.DataServices`) implements `IFileStorageProvider` (Domain) and writes every lead file to three destinations simultaneously. Failures at any tier are logged as warnings and do not block the operation — the write continues to the remaining tiers.

## Storage Tiers

```
IFileStorageProvider (Domain interface)
    └── FanOutStorageProvider (DataServices)
            │
            ├─ Tier 1: Agent Drive
            │       IGDriveClient → GDriveClient (Clients.GDrive)
            │       writes to agent's personal Google Drive folder
            │       configured via account.json integrations.google_drive.folder_id_leads
            │
            ├─ Tier 2: Account Drive
            │       IGDriveClient → GDriveClient (Clients.GDrive)
            │       writes to brokerage/account-level shared Drive
            │       configured via account.json integrations.google_drive.account_folder_id
            │
            └─ Tier 3: Platform Drive
                    IGwsService → GwsService (Clients.Gws)
                    writes to platform-owned Google Workspace Drive
                    provides operational backup for all agents
```

## Fan-Out Write Flow

```
IFileStorageProvider.WriteAsync(path, content)
    │
    ▼
FanOutStorageProvider
    │
    ├─ Task 1: Agent Drive (IGDriveClient, agent token)
    │       ├── success → log Info
    │       └── failure → log Warning, continue
    │
    ├─ Task 2: Account Drive (IGDriveClient, account token)
    │       ├── success → log Info
    │       └── failure → log Warning, continue
    │
    └─ Task 3: Platform Drive (IGwsService)
            ├── success → log Info
            └── failure → log Warning, continue

All three tasks run concurrently (Task.WhenAll).
Result: success if at least one tier succeeds (best-effort).
```

## Sheets Passthrough

Google Sheets operations (e.g., consent log append) bypass `FanOutStorageProvider` entirely and go directly to `IGwsService`:

```
ConsentLogger
    └── IGwsService.AppendRowAsync(spreadsheetId, row)
            └── GwsService (Clients.Gws) — GWS CLI wrapper
```

Sheets writes are single-destination by design — the consent log lives in one authoritative spreadsheet.

## Dependency Rules

```
FanOutStorageProvider (DataServices)
    → IFileStorageProvider (Domain)   [interface it implements]
    → IGDriveClient (Domain)          [injected, resolved from DI]
    → IGwsService (Domain)            [injected, resolved from DI]

DataServices → Domain only (no Clients.* references — DI handles resolution)
```

## Failure Semantics

| Scenario | Behavior |
|----------|----------|
| Agent Drive fails | Warning logged, write continues to account + platform |
| Account Drive fails | Warning logged, write continues to agent + platform |
| Platform Drive fails | Warning logged, write continues to agent + account |
| All three fail | Exception thrown — caller receives error, lead not saved |
| Any one succeeds | Overall operation reported as success to caller |

This design ensures that a GDrive outage or token expiry for one tier does not block lead ingestion. Recovery can re-sync from the surviving tiers.
