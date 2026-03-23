"use server";

import type { LeadFormData } from "@real-estate-star/domain";
import { createApiClient } from "@real-estate-star/api-client";
import { validateTurnstile } from "./turnstile";
import { signRequest, getApiUrl } from "../shared/hmac";

function mapLeadType(leadTypes: string[]): string {
  const buying = leadTypes.includes("buying");
  const selling = leadTypes.includes("selling");
  if (buying && selling) return "Both";
  if (buying) return "Buyer";
  return "Seller";
}

function toApiPayload(formData: LeadFormData) {
  return {
    leadType: mapLeadType(formData.leadTypes),
    firstName: formData.firstName,
    lastName: formData.lastName,
    email: formData.email,
    phone: formData.phone,
    timeline: formData.timeline,
    notes: formData.notes,
    buyer: formData.buyer,
    seller: formData.seller,
    marketingConsent: formData.marketingConsent,
  };
}

export async function submitLead(
  agentId: string,
  formData: LeadFormData,
  turnstileToken: string,
): Promise<{ leadId?: string; status?: string; error?: string }> {
  // Honeypot: filled means bot (defense in depth — LeadForm also blocks bot submissions client-side)
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  if ((formData as any).website) return { leadId: "fake-id", status: "received" };

  const turnstile = await validateTurnstile(turnstileToken);
  if (!turnstile.ok) return { error: `Verification failed [${turnstile.code}]: ${turnstile.detail}` };

  const body = JSON.stringify(toApiPayload(formData));
  let cleanup: (() => void) | undefined;

  try {
    const { headers, signal, cleanup: c } = await signRequest(agentId, body);
    cleanup = c;
    const client = createApiClient(getApiUrl());
    const { data, error, response } = await client.POST("/agents/{agentId}/leads", {
      params: { path: { agentId } },
      body: toApiPayload(formData),
      headers,
      init: { signal },
    });

    if (error || !response.ok) {
      const text = await response.text().catch(() => "");
      return { error: `API error [${response.status}]: ${text || response.statusText} | Payload: ${body}` };
    }
    return (data ?? {}) as { leadId?: string; status?: string };
  } catch (err) {
    const Sentry = await import("@sentry/nextjs");
    Sentry.captureException(err);
    return { error: "Something went wrong. Please try again." };
  } finally {
    cleanup?.();
  }
}
