---
name: orchestrator-activity-service-worker-taxonomy
description: "Layer taxonomy: Orchestrator owns Workers, calls Activities + Services, with strict dependency rules"
user-invocable: false
origin: auto-extracted
---

# Orchestrator-Activity-Service-Worker Taxonomy

**Extracted:** 2026-03-28
**Context:** Multi-step pipeline coordination in .NET with clear layer separation

## Problem
Pipeline code mixes coordination, compute, persistence, and external API calls in one class. No clear rules for what goes where.

## Solution
5-layer taxonomy with strict call direction:

| Layer | Purpose | Can Call | Cannot Call |
|-------|---------|---------|-------------|
| **Orchestrator** | Coordinates pipeline steps | Workers, Activities, Services | — |
| **Workers** | Pure compute (channel-based) | Clients | Orchestrator, Activities, Services, DataServices |
| **Activities** | Compute + persist | Services, DataServices | Workers, Orchestrator |
| **Services** | Sync business logic | Clients, DataServices | Activities, Workers |
| **DataServices** | Storage routing | Data providers | Workers, Services, Activities |

Key rules:
- Orchestrator is top-level — it OWNS sub-Workers, not the other way around
- Workers do NOT know about the Orchestrator (one-way dispatch via channel + TCS)
- Activities are launched by Orchestrators ONLY — Services cannot launch Activities
- Activities CAN call Services (but not vice versa)
- Services CAN call Clients (Gmail, WhatsApp, Anthropic) for external comms
- Services persist failure/fallback records via DataServices
- Data CAN call Clients (GDrive, Azure Blob) for cloud storage I/O
- If orchestrator has >5-6 direct calls, group into an Activity

## When to Use
- Designing multi-step pipelines with mixed compute + I/O + external APIs
- Deciding where a new class belongs in the architecture
- Reviewing code that violates layer boundaries
