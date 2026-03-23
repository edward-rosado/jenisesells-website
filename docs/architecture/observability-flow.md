# Frontend Observability Flow

How telemetry, error tracking, and correlation IDs flow from frontend to backend.

```mermaid
flowchart TD
    subgraph Frontend ["Frontend Apps"]
        UserAction["User Action<br/>form submit, click, page load"]
        TrackEvent["trackEvent<br/>PascalCase: Viewed, Started,<br/>Submitted, Succeeded, Failed"]
        ReportError["reportError<br/>Wraps Sentry via<br/>injected reporter"]
        WebVitals["WebVitalsReporter<br/>LCP, CLS, INP, FCP, TTFB"]
        CorrelationID["createCorrelationId<br/>UUID v4, auto-injected<br/>via api-client"]
    end

    subgraph Backend ["Backend API"]
        TelemetryEndpoint["POST /telemetry<br/>FormEvent enum"]
        SentryCloud["Sentry Cloud<br/>Error tracking"]
        CorrelationMiddleware["CorrelationIdMiddleware<br/>X-Correlation-ID header"]
        GrafanaCloud["Grafana Cloud<br/>Traces + metrics"]
    end

    UserAction --> TrackEvent
    UserAction --> ReportError
    UserAction --> CorrelationID

    TrackEvent -->|"fire-and-forget"| TelemetryEndpoint
    ReportError -->|"lazy import"| SentryCloud
    WebVitals -->|"Core Web Vitals"| TelemetryEndpoint
    CorrelationID -->|"X-Correlation-ID"| CorrelationMiddleware

    TelemetryEndpoint --> GrafanaCloud
    CorrelationMiddleware -->|"Serilog LogContext"| GrafanaCloud
    ReportError -->|"correlationId in extra"| SentryCloud
```

## Correlation ID Flow

The `createApiClient()` from `@real-estate-star/api-client` auto-injects `X-Correlation-ID: crypto.randomUUID()` on every request. This correlation ID flows through the entire request lifecycle:

1. **Frontend**: Generated on first request via `createApiClient(baseUrl)` or shared `api` instance
2. **API**: Captured by `CorrelationIdMiddleware`, stored in Serilog LogContext
3. **Enrichment & Notification**: Propagated through worker pipelines via LogContext
4. **Grafana Dashboards**: Queryable via `correlation_id` field for end-to-end request tracing
```
