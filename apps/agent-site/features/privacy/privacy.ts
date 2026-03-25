"use server";

import { createApiClient } from "@real-estate-star/api-client";
import { signRequest, getApiUrl } from "@/features/shared/hmac";
import { checkRateLimit } from "@/features/shared/rate-limit";

const VALID_AGENT_ID = /^[a-z0-9-]+$/;
const RATE_LIMIT_MAX = 5; // 5 requests per minute per email per action
const RATE_LIMIT_WINDOW_MS = 60_000;

export async function requestOptOut(
  agentId: string,
  email: string,
  token: string,
): Promise<{ ok: boolean; error?: string }> {
  if (!VALID_AGENT_ID.test(agentId)) return { ok: false, error: "Invalid request." };
  const { allowed } = await checkRateLimit("opt-out", email, RATE_LIMIT_MAX, RATE_LIMIT_WINDOW_MS);
  if (!allowed) return { ok: false, error: "Too many requests. Please try again later." };
  const body = JSON.stringify({ email, token });
  let cleanup: (() => void) | undefined;
  try {
    const { headers, signal, cleanup: c } = await signRequest(agentId, body);
    cleanup = c;
    const client = createApiClient(await getApiUrl());
    const { error, response } = await client.POST("/agents/{agentId}/leads/opt-out", {
      params: { path: { agentId } },
      body: { email, token },
      headers,
      init: { signal },
    });
    if (error || !response.ok) return { ok: false, error: "Something went wrong. Please try again." };
    return { ok: true };
  } catch {
    return { ok: false, error: "Something went wrong. Please try again." };
  } finally {
    cleanup?.();
  }
}

export async function requestDeletion(
  agentId: string,
  email: string,
): Promise<{ ok: boolean; error?: string }> {
  if (!VALID_AGENT_ID.test(agentId)) return { ok: false, error: "Invalid request." };
  const { allowed } = await checkRateLimit("deletion", email, RATE_LIMIT_MAX, RATE_LIMIT_WINDOW_MS);
  if (!allowed) return { ok: false, error: "Too many requests. Please try again later." };
  const body = JSON.stringify({ email });
  let cleanup: (() => void) | undefined;
  try {
    const { headers, signal, cleanup: c } = await signRequest(agentId, body);
    cleanup = c;
    const client = createApiClient(await getApiUrl());
    const { error, response } = await client.POST("/agents/{agentId}/leads/request-deletion", {
      params: { path: { agentId } },
      body: { email },
      headers,
      init: { signal },
    });
    if (error || !response.ok) return { ok: false, error: "Something went wrong. Please try again." };
    return { ok: true };
  } catch {
    return { ok: false, error: "Something went wrong. Please try again." };
  } finally {
    cleanup?.();
  }
}

export interface ExportData {
  email: string;
  name?: string;
  phone?: string;
  submittedAt?: string;
  propertyAddress?: string;
  source?: string;
  status?: string;
}

/**
 * Data export uses a DIFFERENT HMAC key derivation than the other endpoints.
 * signRequest derives key as hmacSecret:agentId.
 * requestExport signs with raw hmacSecret (no agentId suffix).
 * This is intentional — do NOT refactor to use signRequest.
 */
export async function requestExport(
  agentId: string,
  email: string,
): Promise<{ ok: boolean; data?: ExportData[]; error?: string }> {
  if (!VALID_AGENT_ID.test(agentId)) return { ok: false, error: "Invalid request." };
  const { allowed } = await checkRateLimit("export", email, RATE_LIMIT_MAX, RATE_LIMIT_WINDOW_MS);
  if (!allowed) return { ok: false, error: "Too many requests. Please try again later." };
  const apiKey = process.env.LEAD_API_KEY!;
  const hmacSecret = process.env.LEAD_HMAC_SECRET!;
  const timestamp = Math.floor(Date.now() / 1000).toString();
  const body = JSON.stringify({ email });
  const message = `${timestamp}.${body}`;

  const key = await crypto.subtle.importKey(
    "raw",
    new TextEncoder().encode(hmacSecret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
  const sig = await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(message));
  const signature = `sha256=${Array.from(new Uint8Array(sig))
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("")}`;

  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), 15_000);
  try {
    const client = createApiClient(await getApiUrl());
    const { data, error, response } = await client.GET("/agents/{agentId}/leads/export", {
      params: { path: { agentId }, query: { email } },
      headers: {
        "X-API-Key": apiKey,
        "X-Signature": signature,
        "X-Timestamp": timestamp,
      },
      init: { signal: controller.signal },
    });
    if (response.status === 404) return { ok: true, data: [] };
    if (error || !response.ok) return { ok: false, error: "Something went wrong. Please try again." };
    return { ok: true, data: (data ?? []) as ExportData[] };
  } catch {
    return { ok: false, error: "Something went wrong. Please try again." };
  } finally {
    clearTimeout(timeoutId);
  }
}

export async function requestSubscribe(
  agentId: string,
  email: string,
  token: string,
): Promise<{ ok: boolean; error?: string }> {
  if (!VALID_AGENT_ID.test(agentId)) return { ok: false, error: "Invalid request." };
  const { allowed } = await checkRateLimit("subscribe", email, RATE_LIMIT_MAX, RATE_LIMIT_WINDOW_MS);
  if (!allowed) return { ok: false, error: "Too many requests. Please try again later." };
  const body = JSON.stringify({ email, token });
  let cleanup: (() => void) | undefined;
  try {
    const { headers, signal, cleanup: c } = await signRequest(agentId, body);
    cleanup = c;
    const client = createApiClient(await getApiUrl());
    const { error, response } = await client.POST("/agents/{agentId}/leads/subscribe", {
      params: { path: { agentId } },
      body: { email, token },
      headers,
      init: { signal },
    });
    if (error || !response.ok) return { ok: false, error: "Something went wrong. Please try again." };
    return { ok: true };
  } catch {
    return { ok: false, error: "Something went wrong. Please try again." };
  } finally {
    cleanup?.();
  }
}
