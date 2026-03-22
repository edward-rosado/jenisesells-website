type FormEvent =
  | "form.viewed"
  | "form.started"
  | "form.submitted"
  | "form.succeeded"
  | "form.failed";

export function trackFormEvent(event: FormEvent, agentId: string, errorType?: string): void {
  const apiUrl = process.env.NEXT_PUBLIC_API_URL ?? process.env.LEAD_API_URL;
  if (!apiUrl) return;

  fetch(`${apiUrl}/telemetry`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ event, agentId, errorType }),
    keepalive: true,
  }).catch(() => {}); // fire-and-forget
}
