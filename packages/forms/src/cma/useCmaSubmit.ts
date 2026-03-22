"use client";

import { useState, useCallback, useRef, useEffect } from "react";
import type { LeadFormData } from "@real-estate-star/domain";
import { submitCma } from "./cma-api";
import { mapToCmaRequest } from "./mapToCmaRequest";

export type CmaSubmitPhase = "idle" | "submitting" | "submitted" | "error";

export interface CmaSubmitState {
  phase: CmaSubmitPhase;
  /** The jobId returned by the API after successful submission */
  jobId: string | null;
  /** Error message for display */
  errorMessage: string | null;
}

export interface UseCmaSubmitReturn {
  state: CmaSubmitState;
  /** Returns true on success, false on error */
  submit: (agentId: string, data: LeadFormData) => Promise<boolean>;
  reset: () => void;
}

export interface UseCmaSubmitOptions {
  /** Called when submission fails — use for Sentry or other error reporting */
  onError?: (error: Error) => void;
}

const INITIAL_STATE: CmaSubmitState = {
  phase: "idle",
  jobId: null,
  errorMessage: null,
};

export function useCmaSubmit(
  apiBaseUrl: string,
  options?: UseCmaSubmitOptions,
): UseCmaSubmitReturn {
  const [state, setState] = useState<CmaSubmitState>(INITIAL_STATE);

  const onErrorRef = useRef(options?.onError);
  useEffect(() => {
    onErrorRef.current = options?.onError;
  }, [options?.onError]);

  const submit = useCallback(
    async (agentId: string, data: LeadFormData): Promise<boolean> => {
      setState({ phase: "submitting", jobId: null, errorMessage: null });

      try {
        const request = mapToCmaRequest(data);
        const response = await submitCma(apiBaseUrl, agentId, request);
        setState({
          phase: "submitted",
          jobId: response.jobId,
          errorMessage: null,
        });
        return true;
      } catch (err) {
        const error =
          err instanceof Error ? err : new Error("Submission failed");
        onErrorRef.current?.(error);
        setState({
          phase: "error",
          jobId: null,
          errorMessage: error.message,
        });
        return false;
      }
    },
    [apiBaseUrl],
  );

  const reset = useCallback(() => {
    setState(INITIAL_STATE);
  }, []);

  return { state, submit, reset };
}
