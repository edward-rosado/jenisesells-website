"use client";
import type { UptimeSample } from "./useHealthCheck";

interface UptimeTrackerProps {
  samples: UptimeSample[];
  maxBars?: number;
}

function barColor(status: UptimeSample["status"]): string {
  switch (status) {
    case "Healthy":
      return "bg-emerald-500";
    case "Degraded":
      return "bg-yellow-500";
    case "Unhealthy":
      return "bg-red-500";
    case "Error":
      return "bg-red-700";
  }
}

function barTooltip(sample: UptimeSample): string {
  const time = sample.time.toLocaleTimeString([], {
    hour: "2-digit",
    minute: "2-digit",
  });
  return `${time} — ${sample.status}`;
}

export function UptimeTracker({ samples, maxBars = 30 }: UptimeTrackerProps) {
  // Pad with empty slots on the left if fewer than maxBars samples
  const padCount = Math.max(0, maxBars - samples.length);

  return (
    <div data-testid="uptime-tracker">
      <div className="flex items-center justify-between mb-1.5">
        <span className="text-xs text-gray-400">Session Uptime</span>
        <span className="text-xs text-gray-500">
          {samples.length > 0
            ? `${samples.filter((s) => s.status === "Healthy").length}/${samples.length} checks OK`
            : "Waiting for data…"}
        </span>
      </div>
      <div className="flex gap-0.5" role="img" aria-label="Uptime tracker showing recent health check results">
        {Array.from({ length: padCount }).map((_, i) => (
          <div
            key={`pad-${i}`}
            className="flex-1 h-6 rounded-sm bg-gray-800"
          />
        ))}
        {samples.map((sample, i) => (
          <div
            key={`sample-${i}`}
            className={`flex-1 h-6 rounded-sm ${barColor(sample.status)} transition-colors`}
            title={barTooltip(sample)}
          />
        ))}
      </div>
    </div>
  );
}
