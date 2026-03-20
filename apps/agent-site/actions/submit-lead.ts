"use server";

import { validateTurnstile } from "@/lib/turnstile";
import { signAndForward } from "@/lib/hmac";

export async function submitLead(
  agentId: string,
  formData: Record<string, unknown>,
  turnstileToken: string,
): Promise<{ leadId?: string; status?: string; error?: string }> {
  // Honeypot: filled means bot
  if (formData.website) return { leadId: "fake-id", status: "received" };

  const isHuman = await validateTurnstile(turnstileToken);
  if (!isHuman) return { error: "Verification failed. Please try again." };

  const body = JSON.stringify(formData);
  const response = await signAndForward(agentId, body);

  if (!response.ok) return { error: "Something went wrong. Please try again." };
  return response.json();
}
