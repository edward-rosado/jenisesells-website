/**
 * CMA API client — handles form submission and real-time progress via SignalR.
 *
 * The API endpoint is `POST /agents/{agentId}/cma` which returns a jobId.
 * Progress updates are pushed via the `/cma-progress` SignalR hub.
 */

import * as signalR from "@microsoft/signalr";

const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

/** Mirrors the .NET SubmitCmaRequest record */
export interface CmaSubmitRequest {
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  address: string;
  city: string;
  state: string;
  zip: string;
  timeline: string;
  beds?: number;
  baths?: number;
  sqft?: number;
  notes?: string;
}

/** Mirrors the .NET SubmitCmaResponse record */
export interface CmaSubmitResponse {
  jobId: string;
  status: string;
}

/** Mirrors the .NET GetStatusResponse record (also sent via SignalR) */
export interface CmaStatusUpdate {
  status: string;
  step: number;
  totalSteps: number;
  message: string;
  errorMessage?: string | null;
}

/**
 * Submit a CMA request to the API.
 * Returns the jobId for tracking progress.
 */
export async function submitCmaRequest(
  agentId: string,
  request: CmaSubmitRequest,
): Promise<CmaSubmitResponse> {
  const url = `${API_BASE}/agents/${encodeURIComponent(agentId)}/cma`;
  const response = await fetch(url, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Accept: "application/json",
    },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    const text = await response.text().catch(() => "");
    throw new Error(
      `CMA submission failed (${response.status})${text ? `: ${text}` : ""}`,
    );
  }

  return (await response.json()) as CmaSubmitResponse;
}

/**
 * Connect to the CMA progress SignalR hub and listen for status updates.
 * Returns a dispose function to clean up the connection.
 */
export function connectToProgress(
  jobId: string,
  onUpdate: (update: CmaStatusUpdate) => void,
  onError: (error: Error) => void,
): () => void {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${API_BASE}/cma-progress`)
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  connection.on("StatusUpdate", (update: CmaStatusUpdate) => {
    onUpdate(update);
  });

  connection
    .start()
    .then(() => connection.invoke("JoinJob", jobId))
    .catch((err: unknown) => {
      onError(
        err instanceof Error
          ? err
          : new Error("Failed to connect to progress hub"),
      );
    });

  return () => {
    connection.stop().catch(() => {
      // Swallow stop errors during cleanup
    });
  };
}
