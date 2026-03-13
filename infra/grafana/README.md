# Grafana Cloud Monitoring Setup

This guide covers setting up Grafana Cloud (free tier) to receive OpenTelemetry data from the Real Estate Star API running on Azure Container Apps.

## 1. Create a Grafana Cloud Account

1. Go to [grafana.com/auth/sign-up/create-user](https://grafana.com/auth/sign-up/create-user)
2. Sign up for the **free tier** (includes 10k metrics series, 50 GB logs, 50 GB traces)
3. Note your **stack URL** (e.g., `https://<your-stack>.grafana.net`)

## 2. Get OTLP Credentials

1. In Grafana Cloud, go to **Home > Connections > Add new connection**
2. Search for **OpenTelemetry (OTLP)**
3. Click **Configure** and generate an API token
4. Note the following values:
   - **OTLP Endpoint**: `https://otlp-gateway-<region>.grafana.net/otlp` (gRPC port 443)
   - **Instance ID**: shown on the connection page
   - **API Token**: the generated token (starts with `glc_`)

The OTLP exporter uses gRPC with Basic auth. The username is the Instance ID and the password is the API token.

## 3. Configure the API

The API reads `Otel:Endpoint` from configuration (see `Diagnostics/OpenTelemetryExtensions.cs`). It falls back to `http://localhost:4317` for local development with an OTel Collector.

For Grafana Cloud, set the following environment variables on Azure Container Apps:

| Variable | Value | Notes |
|----------|-------|-------|
| `Otel__Endpoint` | `https://otlp-gateway-<region>.grafana.net/otlp` | Double underscores for .NET config binding |
| `OTEL_EXPORTER_OTLP_HEADERS` | `Authorization=Basic <base64(instanceId:apiToken)>` | Base64-encode `instanceId:apiToken` |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `grpc` | Must match the exporter config |

Generate the Base64 value:

```bash
echo -n "INSTANCE_ID:API_TOKEN" | base64
```

### Setting env vars on Azure Container Apps

```bash
az containerapp update \
  --name real-estate-star-api \
  --resource-group real-estate-star-rg \
  --set-env-vars \
    "Otel__Endpoint=https://otlp-gateway-prod-us-east-0.grafana.net/otlp" \
    "OTEL_EXPORTER_OTLP_HEADERS=Authorization=Basic <base64-value>" \
    "OTEL_EXPORTER_OTLP_PROTOCOL=grpc"
```

## 4. Custom Metrics Reference

The API emits the following custom metrics through OpenTelemetry:

### CMA Pipeline Metrics (meter: `RealEstateStar.CmaPipeline`)

| Metric | Type | Description |
|--------|------|-------------|
| `cma.created` | Counter | Number of CMA jobs created |
| `cma.completed` | Counter | Number of CMA jobs completed successfully |
| `cma.failed` | Counter | Number of CMA jobs that failed |
| `cma.duration` | Histogram (ms) | Total CMA pipeline duration |
| `cma.step.duration` | Histogram (ms) | Per-step CMA pipeline duration |

### Onboarding Metrics (meter: `RealEstateStar.Onboarding`)

| Metric | Type | Description |
|--------|------|-------------|
| `onboarding.sessions_created` | Counter | Number of onboarding sessions created |
| `onboarding.state_transitions` | Counter | Number of onboarding state machine transitions |

### Auto-Instrumented Metrics

ASP.NET Core and HttpClient instrumentation automatically provides:

| Metric | Description |
|--------|-------------|
| `http.server.request.duration` | Inbound HTTP request duration |
| `http.server.active_requests` | Active inbound requests |
| `http.client.request.duration` | Outbound HTTP client request duration |

### Trace Sources

| Source | Description |
|--------|-------------|
| `RealEstateStar.CmaPipeline` | CMA pipeline spans (per-step timing, comp counts) |
| `RealEstateStar.Onboarding` | Onboarding chat spans (tool dispatch, streaming) |
| `Microsoft.AspNetCore` | Auto-instrumented ASP.NET Core traces |

## 5. Recommended Dashboards

Import these community dashboards in Grafana Cloud (**Dashboards > Import**):

| Dashboard ID | Name | What It Shows |
|-------------|------|---------------|
| 19924 | ASP.NET Core - OpenTelemetry | Request rate, latency, error rate by endpoint |
| 19925 | .NET Runtime - OpenTelemetry | GC, thread pool, memory pressure |
| 15983 | OpenTelemetry Collector | Collector health (if using a sidecar collector) |

For custom metrics, create a dashboard with these panels:

1. **CMA Pipeline Overview**: `cma.created`, `cma.completed`, `cma.failed` as stat panels
2. **CMA Duration**: `cma.duration` as histogram heatmap
3. **CMA Step Breakdown**: `cma.step.duration` grouped by `cma.step` tag
4. **Onboarding Funnel**: `onboarding.sessions_created` vs `onboarding.state_transitions`
5. **HTTP Error Rate**: `rate(http.server.request.duration{http_status_code=~"5.."})`

## 6. Alerting (Recommended)

Set up these alerts in Grafana Cloud Alerting:

| Alert | Condition | Severity |
|-------|-----------|----------|
| High error rate | 5xx rate > 5% for 5 min | Critical |
| CMA pipeline failures | `cma.failed` rate > 3/hour | Warning |
| Slow CMA pipeline | `cma.duration` p95 > 120s for 10 min | Warning |
| Health check down | `/health/ready` returns non-200 for 2 min | Critical |
| High latency | `http.server.request.duration` p99 > 5s for 5 min | Warning |

## 7. Local Development

For local development, the OTLP endpoint defaults to `http://localhost:4317`. Run a local OTel Collector + Grafana stack:

```bash
docker-compose -f infra/docker-compose.otel.yml up -d
```

Or override in `appsettings.Development.json`:

```json
{
  "Otel": {
    "Endpoint": "http://localhost:4317"
  }
}
```
