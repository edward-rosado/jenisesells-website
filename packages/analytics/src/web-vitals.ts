import type { Metric } from "web-vitals";

/**
 * Initializes Core Web Vitals collection and logs them via console.debug.
 * Only runs in the browser (no-ops during SSR).
 *
 * The backend can be extended to accept web vitals as telemetry events.
 * For now, metrics are logged to the console for local debugging.
 */
export function initWebVitals(apiUrl: string, agentId: string): void {
  if (typeof window === "undefined") return;

  // apiUrl and agentId reserved for future backend reporting
  void apiUrl;
  void agentId;

  import("web-vitals").then(({ onCLS, onLCP, onFCP, onTTFB, onINP }) => {
    const report = (metric: Metric) => {
      console.debug(`[WebVitals] ${metric.name}: ${metric.value}`);
    };
    onCLS(report);
    onLCP(report);
    onFCP(report);
    onTTFB(report);
    onINP(report);
  });
}
