import { createApiClient } from "@real-estate-star/api-client";

export async function signAndForward(agentId: string, body: string, path?: string): Promise<Response> {
  const apiKey = process.env.LEAD_API_KEY!;
  const hmacSecret = process.env.LEAD_HMAC_SECRET!;
  const apiUrl = process.env.LEAD_API_URL!;
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

  const endpoint = path ?? `agents/${agentId}/leads`;
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), 15_000);
  try {
    const client = createApiClient(apiUrl);
    const result = await client.POST(`/${endpoint}` as never, {
      body: JSON.parse(body),
      headers: {
        "Content-Type": "application/json",
        "X-API-Key": apiKey,
        "X-Signature": signature,
        "X-Timestamp": timestamp,
      },
      init: { signal: controller.signal },
    });
    return result.response;
  } finally {
    clearTimeout(timeoutId);
  }
}
