"use client";
import { useState, useEffect } from "react";

interface HealthEntry {
  status: "Healthy" | "Degraded" | "Unhealthy";
  duration?: string;
  description?: string;
}
export interface HealthResponse {
  status: "Healthy" | "Degraded" | "Unhealthy";
  entries: Record<string, HealthEntry>;
}
export type FetchState =
  | { kind: "loading" }
  | { kind: "success"; data: HealthResponse }
  | { kind: "error"; message: string };

const REFRESH_INTERVAL = 30_000;

export function useHealthCheck(apiUrl: string): FetchState {
  const [state, setState] = useState<FetchState>({ kind: "loading" });

  useEffect(() => {
    let cancelled = false;

    const doFetch = async () => {
      try {
        const res = await fetch(`${apiUrl}/health/ready`, { cache: "no-store" });
        if (cancelled) return;
        if (!res.ok) {
          setState({ kind: "error", message: `API returned ${res.status}` });
          return;
        }
        const data: HealthResponse = await res.json();
        if (!cancelled) setState({ kind: "success", data });
      } catch (err) {
        if (!cancelled)
          setState({
            kind: "error",
            message: err instanceof Error ? err.message : "Unknown error",
          });
      }
    };

    doFetch();
    const id = setInterval(doFetch, REFRESH_INTERVAL);
    return () => {
      cancelled = true;
      clearInterval(id);
    };
  }, [apiUrl]);

  return state;
}
