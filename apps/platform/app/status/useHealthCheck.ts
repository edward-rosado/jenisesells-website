"use client";
import { useState, useEffect, useRef, useCallback } from "react";

export interface HealthEntry {
  status: "Healthy" | "Degraded" | "Unhealthy";
  duration?: string;
  description?: string;
  data?: Record<string, unknown>;
}

export interface HealthResponse {
  status: "Healthy" | "Degraded" | "Unhealthy";
  entries: Record<string, HealthEntry>;
}

export interface UptimeSample {
  time: Date;
  status: "Healthy" | "Degraded" | "Unhealthy" | "Error";
}

export interface HealthState {
  current: HealthResponse | null;
  error: string | null;
  loading: boolean;
  history: UptimeSample[];
}

const REFRESH_INTERVAL = 30_000;
const MAX_HISTORY = 30; // ~15 minutes of samples

export function useHealthCheck(apiUrl: string): HealthState {
  const [state, setState] = useState<HealthState>({
    current: null,
    error: null,
    loading: true,
    history: [],
  });
  const historyRef = useRef<UptimeSample[]>([]);

  const doFetch = useCallback(async () => {
    try {
      const res = await fetch(`${apiUrl}/health/ready`, { cache: "no-store" });
      if (!res.ok) {
        const sample: UptimeSample = { time: new Date(), status: "Error" };
        historyRef.current = [...historyRef.current, sample].slice(-MAX_HISTORY);
        setState({
          current: null,
          error: `API returned ${res.status}`,
          loading: false,
          history: historyRef.current,
        });
        return;
      }
      const data: HealthResponse = await res.json();
      const sample: UptimeSample = { time: new Date(), status: data.status };
      historyRef.current = [...historyRef.current, sample].slice(-MAX_HISTORY);
      setState({
        current: data,
        error: null,
        loading: false,
        history: historyRef.current,
      });
    } catch (err) {
      const sample: UptimeSample = { time: new Date(), status: "Error" };
      historyRef.current = [...historyRef.current, sample].slice(-MAX_HISTORY);
      setState({
        current: null,
        error: err instanceof Error ? err.message : "Unknown error",
        loading: false,
        history: historyRef.current,
      });
    }
  }, [apiUrl]);

  useEffect(() => {
    let cancelled = false;
    const wrappedFetch = async () => {
      if (cancelled) return;
      await doFetch();
    };

    wrappedFetch();
    const id = setInterval(wrappedFetch, REFRESH_INTERVAL);
    return () => {
      cancelled = true;
      clearInterval(id);
    };
  }, [doFetch]);

  return state;
}
