"use server";

import { signAndForward } from "@/features/lead-capture/hmac";

export async function requestOptOut(
  agentId: string,
  email: string,
  token: string,
): Promise<{ ok: boolean; error?: string }> {
  const body = JSON.stringify({ email, token });
  let response: Response;
  try {
    response = await signAndForward(agentId, body, `agents/${agentId}/leads/opt-out`);
  } catch {
    return { ok: false, error: "Something went wrong. Please try again." };
  }
  if (!response.ok) return { ok: false, error: "Something went wrong. Please try again." };
  return { ok: true };
}

export async function requestDeletion(
  agentId: string,
  email: string,
): Promise<{ ok: boolean; error?: string }> {
  const body = JSON.stringify({ email });
  let response: Response;
  try {
    response = await signAndForward(agentId, body, `agents/${agentId}/leads/request-deletion`);
  } catch {
    return { ok: false, error: "Something went wrong. Please try again." };
  }
  if (!response.ok) return { ok: false, error: "Something went wrong. Please try again." };
  return { ok: true };
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

export async function requestExport(
  agentId: string,
  email: string,
): Promise<{ ok: boolean; data?: ExportData[]; error?: string }> {
  const apiKey = process.env.LEAD_API_KEY!;
  const hmacSecret = process.env.LEAD_HMAC_SECRET!;
  const apiUrl = process.env.LEAD_API_URL!;
  const timestamp = Math.floor(Date.now() / 1000).toString();
  const body = "";
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

  const encodedEmail = encodeURIComponent(email);
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), 15_000);
  let response: Response;
  try {
    response = await fetch(
      `${apiUrl}/agents/${agentId}/leads/export?email=${encodedEmail}`,
      {
        method: "GET",
        headers: {
          "X-API-Key": apiKey,
          "X-Signature": signature,
          "X-Timestamp": timestamp,
        },
        signal: controller.signal,
      },
    );
  } catch {
    return { ok: false, error: "Something went wrong. Please try again." };
  } finally {
    clearTimeout(timeoutId);
  }

  if (response.status === 404) return { ok: true, data: [] };
  if (!response.ok) return { ok: false, error: "Something went wrong. Please try again." };

  try {
    const data = (await response.json()) as ExportData[];
    return { ok: true, data };
  } catch {
    return { ok: false, error: "Failed to parse response data." };
  }
}

export async function requestSubscribe(
  agentId: string,
  email: string,
  token: string,
): Promise<{ ok: boolean; error?: string }> {
  const body = JSON.stringify({ email, token });
  let response: Response;
  try {
    response = await signAndForward(agentId, body, `agents/${agentId}/leads/subscribe`);
  } catch {
    return { ok: false, error: "Something went wrong. Please try again." };
  }
  if (!response.ok) return { ok: false, error: "Something went wrong. Please try again." };
  return { ok: true };
}
