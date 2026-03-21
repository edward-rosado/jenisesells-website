"use server";

import { signAndForward } from "@/lib/hmac";

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
    response = await signAndForward(agentId, body, `agents/${agentId}/leads/delete-request`);
  } catch {
    return { ok: false, error: "Something went wrong. Please try again." };
  }
  if (!response.ok) return { ok: false, error: "Something went wrong. Please try again." };
  return { ok: true };
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
