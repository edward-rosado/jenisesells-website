import { initializeFaro } from "@grafana/faro-web-sdk";
import { TracingInstrumentation } from "@grafana/faro-web-tracing";

export function initFaro() {
  const url = process.env.NEXT_PUBLIC_FARO_COLLECTOR_URL;
  if (!url) return;

  initializeFaro({
    url,
    app: { name: "agent-site", version: "1.0.0" },
    instrumentations: [
      new TracingInstrumentation({
        instrumentationOptions: {
          propagateTraceHeaderCorsUrls: [/\/api\//],
        },
      }),
    ],
    sessionTracking: { enabled: true },
  });
}
