"use client";

import { useState, useCallback, useRef, useEffect } from "react";
import type { CmaSubmitRequest, CmaStatusUpdate } from "./cma-api";
import { submitCmaRequest, connectToProgress } from "./cma-api";

export type CmaSubmitPhase = "idle" | "submitting" | "tracking" | "complete" | "error";

export interface CmaSubmitState {
  phase: CmaSubmitPhase;
  /** Current status update from SignalR (null until first update arrives) */
  statusUpdate: CmaStatusUpdate | null;
  /** Error message for display */
  errorMessage: string | null;
}

export interface UseCmaSubmitReturn {
  state: CmaSubmitState;
  submit: (agentId: string, request: CmaSubmitRequest) => Promise<void>;
  reset: () => void;
}

const INITIAL_STATE: CmaSubmitState = {
  phase: "idle",
  statusUpdate: null,
  errorMessage: null,
};

export function useCmaSubmit(): UseCmaSubmitReturn {
  const [state, setState] = useState<CmaSubmitState>(INITIAL_STATE);
  const disposeRef = useRef<(() => void) | null>(null);

  // Clean up SignalR connection on unmount
  useEffect(() => {
    return () => {
      disposeRef.current?.();
    };
  }, []);

  const submit = useCallback(async (agentId: string, request: CmaSubmitRequest) => {
    // Clean up any previous connection
    disposeRef.current?.();
    disposeRef.current = null;

    setState({ phase: "submitting", statusUpdate: null, errorMessage: null });

    let response;
    try {
      response = await submitCmaRequest(agentId, request);
    } catch (err) {
      setState({
        phase: "error",
        statusUpdate: null,
        errorMessage: err instanceof Error ? err.message : "Submission failed",
      });
      return;
    }

    setState({ phase: "tracking", statusUpdate: null, errorMessage: null });

    const dispose = connectToProgress(
      response.jobId,
      (update) => {
        if (update.status === "Complete") {
          setState({ phase: "complete", statusUpdate: update, errorMessage: null });
          disposeRef.current?.();
          disposeRef.current = null;
        } else if (update.status === "Failed") {
          setState({
            phase: "error",
            statusUpdate: update,
            errorMessage: update.errorMessage ?? "Report generation failed",
          });
          disposeRef.current?.();
          disposeRef.current = null;
        } else {
          setState({ phase: "tracking", statusUpdate: update, errorMessage: null });
        }
      },
      (err) => {
        setState({
          phase: "error",
          statusUpdate: null,
          errorMessage: err.message,
        });
      },
    );

    disposeRef.current = dispose;
  }, []);

  const reset = useCallback(() => {
    disposeRef.current?.();
    disposeRef.current = null;
    setState(INITIAL_STATE);
  }, []);

  return { state, submit, reset };
}
