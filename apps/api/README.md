# Real Estate Star API

Backend API for the Real Estate Star platform. Accepts lead submissions from agent websites, runs an automated Comparative Market Analysis (CMA) pipeline, and delivers a personalized PDF report to the homeowner via email.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10 (Minimal API) |
| PDF Generation | QuestPDF 2026.2 |
| AI Analysis | Anthropic Claude (Anthropic.SDK 5.10) |
| Real-time | SignalR |
| Observability | OpenTelemetry + Serilog |
| Google Workspace | `gws` CLI (Drive, Gmail, Sheets, Docs) |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [`gws` CLI](https://github.com/nicholasgasior/gws) installed and on PATH (for Drive/Gmail/Sheets/Docs operations)
- Anthropic API key (for Claude analysis)
- ATTOM API key (optional, for property data enrichment)

### Run Locally

```bash
cd apps/api
dotnet run --project RealEstateStar.Api
```

The API starts on `https://localhost:5001` (or `http://localhost:5000`).

### Configuration

**appsettings.json** controls logging and the OpenTelemetry collector endpoint:

```json
{
  "Otel": { "Endpoint": "http://localhost:4317" }
}
```

**Environment variables** for secrets (do not commit these):

| Variable | Purpose |
|----------|---------|
| `Anthropic__ApiKey` | Claude API key for market analysis |
| `Attom__ApiKey` | ATTOM Data API key for property records |

**appsettings.Development.json** adds CORS origins and dev-friendly log levels:

```json
{
  "Cors": { "AllowedOrigins": ["http://localhost:3000"] }
}
```

### Docker

```bash
cd apps/api
docker build -t realestatestar-api .
docker run -p 8080:8080 realestatestar-api
```

## API Endpoints

### Create CMA Job

```
POST /agents/{agentId}/cma
```

Accepts a lead submission and starts the CMA pipeline asynchronously. Rate limited to 10 requests per hour per agent.

**Request body (Lead model):**

| Field | Type | Required | Validation |
|-------|------|----------|-----------|
| `firstName` | string | Yes | Max 100 chars |
| `lastName` | string | Yes | Max 100 chars |
| `email` | string | Yes | Valid email, max 254 chars |
| `phone` | string | Yes | Valid phone, max 30 chars |
| `address` | string | Yes | Max 300 chars |
| `city` | string | Yes | Max 100 chars |
| `state` | string | Yes | Exactly 2 chars (state abbreviation) |
| `zip` | string | Yes | US zip: `12345` or `12345-6789` |
| `timeline` | string | Yes | Max 50 chars (e.g., "ASAP", "1-3 months", "Just curious") |
| `beds` | int | No | |
| `baths` | int | No | |
| `sqft` | int | No | |
| `notes` | string | No | Max 2000 chars |

**Response (202 Accepted):**

```json
{
  "jobId": "a1b2c3d4-...",
  "status": "processing"
}
```

**Error (400 Validation):**

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "errors": {
    "Zip": ["Zip must be a valid US zip code (e.g. 07081 or 07081-1234)."]
  }
}
```

### Get Job Status

```
GET /agents/{agentId}/cma/{jobId}/status
```

Poll for pipeline progress. Returns `Cache-Control: no-cache`.

**Response (200 OK):**

```json
{
  "status": "analyzing",
  "step": 4,
  "totalSteps": 9,
  "message": "Analyzing market trends...",
  "errorMessage": null
}
```

**Response (404 Not Found):**

```json
{
  "title": "Job not found",
  "detail": "No CMA job with ID 'abc' exists for agent 'xyz'.",
  "status": 404
}
```

### List Agent Leads

```
GET /agents/{agentId}/leads?skip=0&take=50
```

Returns paginated lead list for an agent. `take` is capped at 100.

**Response (200 OK):**

```json
[
  {
    "id": "a1b2c3d4-...",
    "name": "Jane Doe",
    "address": "123 Main St, Springfield, NJ 07081",
    "timeline": "1-3 months",
    "cmaStatus": "complete",
    "submittedAt": "2026-03-09T14:30:00Z",
    "driveLink": "https://drive.google.com/..."
  }
]
```

### SignalR Hub -- Real-Time Progress

```
/hubs/cma-progress
```

Clients join a group by `jobId` to receive `StatusUpdate` messages as the pipeline advances:

```json
{
  "status": "generatingpdf",
  "step": 5,
  "totalSteps": 9,
  "message": "Generating your personalized report..."
}
```

### Health Checks

| Endpoint | Purpose | Checks |
|----------|---------|--------|
| `GET /health/live` | Liveness probe | None (returns 200 if process is running) |
| `GET /health/ready` | Readiness probe | `gws` CLI availability, Claude API reachability |

Readiness response includes per-check status and duration:

```json
{
  "status": "Healthy",
  "checks": [
    { "name": "gws_cli", "status": "Healthy", "duration": 12.3 },
    { "name": "claude_api", "status": "Healthy", "duration": 85.1 }
  ]
}
```

## Usage Examples

### cURL

```bash
# Create a CMA job
curl -X POST http://localhost:5000/agents/jenise-buckalew/cma \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "John",
    "lastName": "Smith",
    "email": "john@example.com",
    "phone": "555-123-4567",
    "address": "123 Main St",
    "city": "Wayne",
    "state": "NJ",
    "zip": "07470",
    "timeline": "3-6 months",
    "notes": "Interested in selling"
  }'

# Response: { "jobId": "abc-123", "status": "processing" }

# Poll job status
curl http://localhost:5000/agents/jenise-buckalew/cma/abc-123/status

# List leads with pagination
curl "http://localhost:5000/agents/jenise-buckalew/leads?skip=0&take=10"

# Health checks
curl http://localhost:5000/health/live    # Liveness
curl http://localhost:5000/health/ready   # Readiness with dependency checks
```

### SignalR (JavaScript)

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:5000/hubs/cma-progress")
    .build();

connection.on("StatusUpdate", (update) => {
    console.log(`Step ${update.step}/${update.totalSteps}: ${update.message}`);
});

await connection.start();
await connection.invoke("JoinJob", "abc-123");
```

## CMA Pipeline

When a lead is submitted, the pipeline executes 9 steps. Steps 2 and 3 run in parallel.

```
1. Load Agent Config     Read agent profile from config/agents/{agentId}.json
         |
    2. Search Comps ----+---- 3. Research Lead      (parallel)
         |              |
         +--------------+
         |
4. Analyze (Claude)      AI-powered market analysis and pricing
         |
5. Generate PDF          QuestPDF report with comps, analysis, branding
         |
6. Organize Drive        Create Google Drive folder, upload PDF + Lead Brief doc
         |
7. Send Email            Gmail with PDF attachment via gws CLI
         |
8. Log to Sheet          Append row to agent's tracking spreadsheet
         |
9. Complete              Mark job done, record metrics
```

### Report Types

The `timeline` field determines report depth:

| Timeline | Report Type | Description |
|----------|------------|-------------|
| "Just curious" | Lean | Quick overview with fewer comps |
| "6-12 months", "3-6 months" | Standard | Full analysis with market trends |
| "1-3 months", "ASAP" | Comprehensive | Detailed report with conversation starters |

## Architecture

### Multi-Tenant Model

Each agent is a tenant with a JSON config file at `config/agents/{agent-id}.json`. The `AgentConfigService` loads configs by agent ID with path traversal protection (validates the ID contains no path separators).

### Comp Sources

The `CompAggregator` fetches comparable sales from four sources in parallel and deduplicates results:

| Source | Service Class |
|--------|--------------|
| Zillow | `ZillowCompSource` |
| Realtor.com | `RealtorComCompSource` |
| Redfin | `RedfinCompSource` |
| ATTOM Data | `AttomDataCompSource` |

### Service Registration

All services are registered as singletons in `Program.cs`:

- `IAgentConfigService` -- agent profile loader
- `ICompSource` (x4) -- comp data sources
- `CompAggregator` -- parallel fetch and dedup
- `ILeadResearchService` -- property record research
- `IAnalysisService` (`ClaudeAnalysisService`) -- AI market analysis
- `ICmaPdfGenerator` -- QuestPDF report generator
- `IGwsService` -- Google Workspace operations (Drive, Gmail, Sheets, Docs)
- `CmaPipeline` -- orchestrator
- `ICmaJobStore` (`InMemoryCmaJobStore`) -- in-memory job store with 10K entry limit

## Security

### Input Validation

The `Lead` model uses `System.ComponentModel.DataAnnotations` with strict constraints: string length limits on all fields, regex validation on zip codes, email format validation, and phone format validation. The API returns RFC 7807 Problem Details for validation failures.

### Path Traversal Protection

`AgentConfigService` validates that agent IDs do not contain path separator characters, preventing directory traversal attacks when loading config files.

### Command Injection Protection

`GwsService` uses `Process.ArgumentList` (not string interpolation) when spawning the `gws` CLI, preventing shell injection.

### Rate Limiting

| Policy | Scope | Limit |
|--------|-------|-------|
| Global | Per IP | 100 requests / minute |
| `cma-create` | Per agent ID | 10 requests / hour |

Exceeded limits return `429 Too Many Requests`.

### CORS

Configured via `Cors:AllowedOrigins` in appsettings. Defaults to `http://localhost:3000`. Credentials are allowed (required for SignalR).

### Correlation IDs

The `CorrelationIdMiddleware` attaches a unique correlation ID to every request, propagated through logs for end-to-end tracing.

### Error Handling

A global exception handler returns RFC 7807 `ProblemDetails` for unhandled exceptions (no stack traces leaked). All responses include an `X-Api-Version: 1.0` header.

## Observability

### Structured Logging

Serilog with request logging enriched by agent ID and correlation ID. Log levels configured in appsettings.

### OpenTelemetry Tracing

Distributed traces exported via OTLP (gRPC to `localhost:4317`). The pipeline creates:

- A root span `CmaPipeline.Execute` per job with tags for job ID, agent ID, and address
- Child spans for each pipeline step (`LoadAgentConfig`, `SearchingComps`, `Analyzing`, `GeneratingPdf`, `OrganizingDrive`, `SendingEmail`, `Logging`)
- Automatic ASP.NET Core and HTTP client instrumentation

### Business Metrics

Custom metrics exported via OpenTelemetry:

| Metric | Type | Description |
|--------|------|-------------|
| `cma.created` | Counter | Jobs created (tagged by `agent.id`) |
| `cma.completed` | Counter | Jobs completed successfully |
| `cma.failed` | Counter | Jobs that failed |
| `cma.duration` | Histogram (ms) | Total pipeline duration |
| `cma.step.duration` | Histogram (ms) | Per-step duration (tagged by `step`) |

### Health Checks

- **Liveness** (`/health/live`): Returns 200 if the process is running. Used by container orchestrators to detect deadlocks.
- **Readiness** (`/health/ready`): Checks `gws` CLI and Claude API. Used to gate traffic until dependencies are available.

## Observability Stack (Docker)

Start the full observability stack:

```bash
cd apps/api
docker compose -f docker-compose.observability.yml up -d
```

| Service | URL | Credentials |
|---------|-----|-------------|
| Grafana | http://localhost:3001 | admin / admin |
| Prometheus | http://localhost:9090 | -- |
| OTel Collector | localhost:4317 (gRPC), :4318 (HTTP) | -- |

The stack includes:

- **OpenTelemetry Collector** -- receives traces and metrics from the API, exports to Prometheus
- **Prometheus** -- scrapes the collector's Prometheus exporter on port 8889
- **Grafana** -- pre-provisioned dashboards for CMA pipeline metrics (throughput, duration, failure rate, per-step latency)

## Testing

Run all tests:

```bash
cd apps/api
dotnet test
```

The test suite contains 17 test files covering:

| Area | Tests |
|------|-------|
| Endpoints | CMA endpoint request/response validation |
| Health | Health check behavior |
| Integration | Full pipeline and middleware pipeline tests |
| Middleware | Correlation ID propagation |
| Models | CmaJob state machine, report type selection |
| Services | AgentConfigService, ClaudeAnalysisService, CompAggregator, GwsService, InMemoryCmaJobStore, CmaPdfGenerator, LeadResearchService, CmaPipeline |

## Adding New Endpoints

This API uses the **REPR (Request-Endpoint-Response) pattern** with auto-registration. Each endpoint is a self-contained class that implements `IEndpoint`.

### 1. Create a Response type

```csharp
// Models/Responses/MyResponse.cs
namespace RealEstateStar.Api.Models.Responses;

public record MyResponse
{
    public required string Value { get; init; }
}
```

### 2. Create the Endpoint

```csharp
// Endpoints/MyEndpoint.cs
namespace RealEstateStar.Api.Endpoints;

public class MyEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/my-route", Handle);

    private static IResult Handle(string param, IMyService service, CancellationToken ct)
    {
        // handler logic
        return Results.Ok(new MyResponse { Value = "hello" });
    }
}
```

No registration code needed -- `app.MapEndpoints()` auto-discovers all `IEndpoint` implementations via reflection.

## Project Structure

```
apps/api/
  Dockerfile
  docker-compose.observability.yml
  RealEstateStar.Api/
    Program.cs                          # Middleware, DI setup, MapEndpoints()
    appsettings.json
    appsettings.Development.json
    Diagnostics/
      CmaDiagnostics.cs                 # ActivitySource + Meters
      OpenTelemetryExtensions.cs        # OTel registration helpers
    Endpoints/
      IEndpoint.cs                       # REPR endpoint interface
      EndpointExtensions.cs              # MapEndpoints() auto-registration
      CreateCmaEndpoint.cs               # POST /agents/{agentId}/cma
      GetCmaStatusEndpoint.cs            # GET  /agents/{agentId}/cma/{jobId}/status
      GetLeadsEndpoint.cs                # GET  /agents/{agentId}/leads
    Health/
      ClaudeApiHealthCheck.cs
      GwsCliHealthCheck.cs
    Hubs/
      CmaProgressHub.cs                 # SignalR hub
    Logging/
      LoggingExtensions.cs              # Serilog configuration
    Middleware/
      CorrelationIdMiddleware.cs
      AgentIdEnricher.cs                # Serilog enricher for agent context
    Models/
      AgentConfig.cs                     # Agent profile model
      CmaAnalysis.cs                     # Analysis result model
      CmaJob.cs                          # Job entity + status enum + report type
      Comp.cs                            # Comparable sale model
      Lead.cs                            # Input model with DataAnnotations
      LeadResearch.cs                    # Property research model
      StatusMessages.cs                  # Pipeline status message constants
      Responses/
        CreateCmaResponse.cs             # POST /cma response record
        CmaStatusResponse.cs             # GET /status response record
        LeadSummaryResponse.cs           # GET /leads response record
    Services/
      AgentConfigService.cs             # Multi-tenant config loader
      CmaPipeline.cs                    # 9-step orchestrator
      ICmaJobStore.cs / InMemoryCmaJobStore.cs
      Analysis/
        ClaudeAnalysisService.cs        # Anthropic Claude integration
      Comps/
        CompAggregator.cs               # Parallel fetch + dedup
        ZillowCompSource.cs
        RealtorComCompSource.cs
        RedfinCompSource.cs
        AttomDataCompSource.cs
      Gws/
        GwsService.cs                   # Google Workspace CLI wrapper
      Pdf/
        CmaPdfGenerator.cs              # QuestPDF report builder
      Research/
        LeadResearchService.cs          # Property record research
  RealEstateStar.Api.Tests/
    Endpoints/                           # HTTP endpoint tests
    Health/                              # Health check tests
    Integration/                         # Pipeline + middleware integration tests
    Middleware/                           # Correlation ID tests
    Models/                              # CmaJob unit tests
    Services/                            # Service-level unit tests
  infra/
    otel-collector-config.yaml
    prometheus.yml
    grafana/
      provisioning/                      # Datasource + dashboard provisioning
      dashboards/                        # Pre-built Grafana dashboards
```
