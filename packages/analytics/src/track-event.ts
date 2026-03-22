import { EventType } from "./event-types";

/**
 * Fire-and-forget POST to the /telemetry endpoint.
 * Failures are silently swallowed — telemetry must never break the app.
 */
export async function trackEvent(
  apiUrl: string,
  event: EventType,
  agentId: string,
  errorType?: string
): Promise<void> {
  try {
    await fetch(`${apiUrl}/telemetry`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ event, agentId, errorType }),
    });
  } catch {
    // Fire-and-forget — don't break the app if telemetry fails
  }
}
