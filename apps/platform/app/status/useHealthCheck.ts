"use client";
import { useState, useEffect, useCallback } from "react";

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

  const fetchHealth = useCallback(async () => {
    try {
      const res = await fetch(`${apiUrl}/health/ready`, { cache: "no-store" });
      if (!res.ok) {
        setState({ kind: "error", message: `API returned ${res.status}` });
        return;
      }
      const data: HealthResponse = await res.json();
      setState({ kind: "success", data });
    } catch (err) {
      setState({
        kind: "error",
        message: err instanceof Error ? err.message : "Unknown error",
      });
    }
  }, [apiUrl]);

  useEffect(() => {
    fetchHealth();
    const id = setInterval(fetchHealth, REFRESH_INTERVAL);
    return () => clearInterval(id);
  }, [fetchHealth]);

  return state;
}
