# @real-estate-star/api-client

Typed API client generated from the OpenAPI specification. Uses `openapi-fetch` for type-safe HTTP calls with automatic correlation ID injection.

## Usage — Platform (Shared Instance)

The platform app uses a shared API client instance defined in `@/lib/api`:

```typescript
import { api } from "@/lib/api";

const { data, error } = await api.GET("/health/ready");
```

The shared instance is initialized once per page load with `createApiClient(baseUrl)`, and correlation IDs are automatically injected.

## Usage — Agent Site (Per-Request HMAC Headers)

The agent site creates a new client instance per request to pass HMAC authentication headers:

```typescript
import { createApiClient } from "@real-estate-star/api-client";

const client = createApiClient(baseUrl);
const { data, error } = await client.POST("/path" as never, {
  headers: {
    "X-Signature": signature,
    "X-Timestamp": timestamp,
    "X-API-Key": apiKey,
  },
  body: payload,
});
```

## Correlation IDs

Every API request automatically includes an `X-Correlation-ID` header (UUID v4). The correlation ID flows through:

- **Client**: Generated on first request
- **API**: Captured by `CorrelationIdMiddleware`, stored in Serilog LogContext
- **Backend Pipelines**: Propagated through enrichment and notification workers
- **Grafana**: Queryable via `correlation_id` field for end-to-end tracing

## Regenerating Types Locally

First, start the API:

```bash
dotnet run --project apps/api/RealEstateStar.Api
```

Then, regenerate the types:

```bash
npm run generate --workspace=packages/api-client
```

This pulls the OpenAPI spec from the running API and regenerates `src/types.ts`.

## CI Auto-Update

The `api-client` GitHub Actions workflow automatically regenerates types whenever the API build produces a new OpenAPI spec. No manual regeneration needed on main.
