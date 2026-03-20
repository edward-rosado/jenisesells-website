"use client";
import { useHealthCheck } from "./useHealthCheck";

const API_URL =
  process.env.NEXT_PUBLIC_API_URL ?? "https://api.real-estate-star.com";

function parseDuration(duration: string): string {
  const match = duration.match(/(\d+):(\d+):(\d+)\.(\d+)/);
  if (!match) return duration;
  const [, h, m, s, ms] = match;
  const totalMs =
    +h * 3600000 + +m * 60000 + +s * 1000 + +ms;
  return `${totalMs}ms`;
}

function overallLabel(status: string): string {
  switch (status) {
    case "Healthy":
      return "All Systems Operational";
    case "Degraded":
      return "Degraded Performance";
    default:
      return "Service Disruption";
  }
}

function statusColor(status: string): string {
  switch (status) {
    case "Healthy":
      return "bg-green-500";
    case "Degraded":
      return "bg-yellow-500";
    default:
      return "bg-red-500";
  }
}

export function StatusDashboard() {
  const state = useHealthCheck(API_URL);

  if (state.kind === "loading") {
    return <div data-testid="status-loading">Checking services…</div>;
  }

  if (state.kind === "error") {
    return (
      <div data-testid="status-error" className="text-red-400">
        <p>Unable to reach API</p>
        <p className="text-sm text-gray-500">{state.message}</p>
      </div>
    );
  }

  const { data } = state;

  return (
    <div>
      <h2 className="text-2xl font-bold mb-4">{overallLabel(data.status)}</h2>
      <div className="space-y-3">
        {Object.entries(data.entries).map(([name, entry]) => (
          <div
            key={name}
            data-testid={`status-${name}`}
            data-status={entry.status}
            className="flex items-center justify-between p-3 rounded bg-gray-800"
          >
            <div className="flex items-center gap-3">
              <span
                className={`w-3 h-3 rounded-full ${statusColor(entry.status)}`}
              />
              <span className="font-medium">{name}</span>
            </div>
            <div className="flex items-center gap-4 text-sm text-gray-400">
              {entry.description && (
                <span className="text-yellow-400">{entry.description}</span>
              )}
              {entry.duration && <span>{parseDuration(entry.duration)}</span>}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
