"use client";
import { useHealthCheck, type HealthEntry } from "./useHealthCheck";
import { UptimeTracker } from "./UptimeTracker";

const API_URL =
  process.env.NEXT_PUBLIC_API_URL ?? "https://api.real-estate-star.com";

function parseDuration(duration: string): string {
  const match = duration.match(/(\d+):(\d+):(\d+)\.(\d+)/);
  if (!match) return duration;
  const [, h, m, s, ms] = match;
  const totalMs = +h * 3600000 + +m * 60000 + +s * 1000 + +ms;
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

function overallColor(status: string): string {
  switch (status) {
    case "Healthy":
      return "text-emerald-400";
    case "Degraded":
      return "text-yellow-400";
    default:
      return "text-red-400";
  }
}

function statusDot(status: string): string {
  switch (status) {
    case "Healthy":
      return "bg-emerald-500";
    case "Degraded":
      return "bg-yellow-500";
    default:
      return "bg-red-500";
  }
}

function formatLastActivity(value: unknown): string {
  if (value === "never") return "Never";
  if (typeof value === "string") {
    const date = new Date(value);
    if (!isNaN(date.getTime())) {
      const seconds = Math.floor((Date.now() - date.getTime()) / 1000);
      if (seconds < 60) return `${seconds}s ago`;
      if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
      return `${Math.floor(seconds / 3600)}h ago`;
    }
  }
  return String(value);
}

interface ServiceCardProps {
  name: string;
  entry: HealthEntry;
}

function ServiceCard({ name, entry }: ServiceCardProps) {
  return (
    <div
      data-testid={`status-${name}`}
      data-status={entry.status}
      className="flex items-center justify-between p-3 rounded-lg bg-gray-800/60 border border-gray-700/50"
    >
      <div className="flex items-center gap-3">
        <span className={`w-2.5 h-2.5 rounded-full ${statusDot(entry.status)}`} />
        <span className="font-medium text-sm">{name}</span>
      </div>
      <div className="flex items-center gap-4 text-xs text-gray-400">
        {entry.description && (
          <span className="text-yellow-400">{entry.description}</span>
        )}
        {entry.duration && <span>{parseDuration(entry.duration)}</span>}
      </div>
    </div>
  );
}

interface WorkerCardProps {
  name: string;
  entry: HealthEntry;
}

function WorkerCard({ name, entry }: WorkerCardProps) {
  const data = entry.data ?? {};

  // Extract per-worker metrics from the data dictionary
  const workers = ["LeadProcessingWorker", "CmaProcessingWorker", "HomeSearchProcessingWorker"];
  const workerMetrics = workers.map((w) => ({
    name: w.replace("ProcessingWorker", "").replace("Worker", ""),
    queueDepth: (data[`${w}.queueDepth`] as number) ?? 0,
    lastActivity: data[`${w}.lastActivity`],
  }));

  return (
    <div
      data-testid={`status-${name}`}
      data-status={entry.status}
      className="rounded-lg bg-gray-800/60 border border-gray-700/50 overflow-hidden"
    >
      <div className="flex items-center justify-between p-3 border-b border-gray-700/50">
        <div className="flex items-center gap-3">
          <span className={`w-2.5 h-2.5 rounded-full ${statusDot(entry.status)}`} />
          <span className="font-medium text-sm">Background Workers</span>
        </div>
        <span className="text-xs text-gray-400">
          {entry.description}
        </span>
      </div>
      <div className="divide-y divide-gray-700/30">
        {workerMetrics.map((w) => (
          <div
            key={w.name}
            data-testid={`worker-${w.name}`}
            className="flex items-center justify-between px-4 py-2.5"
          >
            <span className="text-sm text-gray-300">{w.name}</span>
            <div className="flex items-center gap-4 text-xs text-gray-400">
              <span>
                Queue: <span className={w.queueDepth > 0 ? "text-yellow-400" : "text-gray-500"}>{w.queueDepth}</span>
              </span>
              <span>
                Last: {formatLastActivity(w.lastActivity)}
              </span>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function SectionLabel({ children }: { children: React.ReactNode }) {
  return (
    <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-2 mt-6 first:mt-0">
      {children}
    </h3>
  );
}

export function StatusDashboard() {
  const { current, error, loading, history } = useHealthCheck(API_URL);

  if (loading) {
    return <div data-testid="status-loading">Checking services…</div>;
  }

  if (error && !current) {
    return (
      <div data-testid="status-error" className="text-red-400">
        <p>Unable to reach API</p>
        <p className="text-sm text-gray-500">{error}</p>
      </div>
    );
  }

  if (!current) return null;

  // Split entries into core services vs background workers
  const coreEntries: [string, HealthEntry][] = [];
  const workerEntries: [string, HealthEntry][] = [];

  for (const [name, entry] of Object.entries(current.entries)) {
    if (name === "background_workers") {
      workerEntries.push([name, entry]);
    } else {
      coreEntries.push([name, entry]);
    }
  }

  return (
    <div>
      {/* Overall status + uptime */}
      <div className="mb-6">
        <div className="flex items-center gap-3 mb-4">
          <span className={`w-3 h-3 rounded-full ${statusDot(current.status)}`} />
          <h2 className={`text-2xl font-bold ${overallColor(current.status)}`}>
            {overallLabel(current.status)}
          </h2>
        </div>
        <UptimeTracker samples={history} />
      </div>

      {/* Core services */}
      {coreEntries.length > 0 && (
        <div>
          <SectionLabel>Core Services</SectionLabel>
          <div className="space-y-2">
            {coreEntries.map(([name, entry]) => (
              <ServiceCard key={name} name={name} entry={entry} />
            ))}
          </div>
        </div>
      )}

      {/* Background workers */}
      {workerEntries.length > 0 && (
        <div>
          <SectionLabel>Background Processing</SectionLabel>
          <div className="space-y-2">
            {workerEntries.map(([name, entry]) => (
              <WorkerCard key={name} name={name} entry={entry} />
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
