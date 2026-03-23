"use server";

/**
 * Compute HMAC-SHA256 signature headers for agent API calls.
 * Returns the signed headers — callers pass them to the typed API client.
 *
 * Key derivation: hmacSecret:agentId (per-agent isolation).
 */
export async function signRequest(agentId: string, body: string): Promise<{
  headers: Record<string, string>;
  signal: AbortSignal;
  cleanup: () => void;
}> {
  const apiKey = process.env.LEAD_API_KEY!;
  const hmacSecret = process.env.LEAD_HMAC_SECRET!;
  const timestamp = Math.floor(Date.now() / 1000).toString();
  const message = `${timestamp}.${body}`;

  const derivedSecret = `${hmacSecret}:${agentId}`;
  const key = await crypto.subtle.importKey(
    "raw",
    new TextEncoder().encode(derivedSecret),
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

  return {
    headers: {
      "X-API-Key": apiKey,
      "X-Signature": signature,
      "X-Timestamp": timestamp,
    },
    signal: controller.signal,
    cleanup: () => clearTimeout(timeoutId),
  };
}

/** Get the API base URL from environment. */
export async function getApiUrl(): Promise<string> {
  return process.env.LEAD_API_URL!;
}
