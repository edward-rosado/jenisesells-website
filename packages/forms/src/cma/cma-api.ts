/**
 * CMA API client — handles form submission to the .NET backend.
 * No SignalR dependency — progress tracking is app-specific.
 */

import type {
  CmaSubmitRequest,
  CmaSubmitResponse,
} from "@real-estate-star/domain";

/**
 * Submit a CMA request to the API.
 * Returns the jobId for optional progress tracking by the consuming app.
 */
export async function submitCma(
  apiBaseUrl: string,
  agentId: string,
  request: CmaSubmitRequest,
): Promise<CmaSubmitResponse> {
  const url = `${apiBaseUrl}/agents/${encodeURIComponent(agentId)}/cma`;
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
    const detail = text ? `: ${text}` : "";
    throw new Error(`CMA submission failed (${response.status})${detail}`);
  }

  return (await response.json()) as CmaSubmitResponse;
}
