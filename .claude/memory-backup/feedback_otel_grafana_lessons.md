---
name: OTel / Grafana Cloud Lessons
description: Critical lessons from debugging silent OTLP export failures — URI trailing slash, health checks, deploy gates
type: feedback
---

## .NET OTLP URI Trailing Slash Bug
`new Uri("https://host/otlp", "v1/traces")` → `/v1/traces` (WRONG — drops base path).
`new Uri("https://host/otlp/", "v1/traces")` → `/otlp/v1/traces` (CORRECT).
**Why:** .NET Uri combination replaces the last segment without a trailing slash. This caused all OTLP exports to 404 silently for days.
**How to apply:** Always `TrimEnd('/') + "/"` on OTLP base URLs. Use explicit per-signal endpoints (v1/traces, v1/metrics) rather than relying on SDK auto-append.

## OTel SDK Swallows Export Failures
The .NET OTel SDK logs export failures (401, 403, 404) at INF level — same as successes. No warning, no error. Telemetry silently vanishes.
**Why:** OTel is "best effort" by design. Fine for local collectors, catastrophic for cloud providers.
**How to apply:** Always add an OtlpExportHealthCheck that probes the endpoint. Register it on /health/ready so deploy verification catches it. Error codes: [OTEL-HC-001] auth, [OTEL-HC-002] 404, [OTEL-HC-003] unexpected status, [OTEL-HC-004] unreachable.

## Deploy Must Check Readiness, Not Just Liveness
/health/live always returns 200 (container is running). /health/ready checks dependencies (OTLP, Claude, GDrive, etc.). Checking only liveness lets broken deployments through.
**Why:** The 404 bug passed deploy verification for days because we only checked /health/live.
**How to apply:** deploy-api.yml now does Phase 1 (liveness) then Phase 2 (readiness). Unhealthy = rollback. Degraded = warning.

## Grafana Cloud Architecture
OTel is the transport. Grafana Cloud stores data in: Tempo (traces), Mimir (metrics), Loki (logs). Dashboard queries use PromQL against Mimir. Data source in dashboard import = the Prometheus/Mimir source, usually `grafanacloud-<stack>-prom`.

## Grafana Dashboard Location
`infra/grafana/real-estate-star-api-dashboard.json` — 54 panels, 8 rows covering all pipelines. Import via Grafana Cloud → Dashboards → Import.

## OTel Headers with Spaces
`Authorization=Basic <token>` gets space-split by shell in `az containerapp update --set-env-vars`. Fix: pass OTel headers as a SEPARATE az containerapp update call, not combined with other env vars.
