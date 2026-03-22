"use server";

import * as Sentry from "@sentry/nextjs";
import type { LeadFormData } from "@real-estate-star/shared-types";
import { validateTurnstile } from "@/lib/turnstile";
import { signAndForward } from "@/lib/hmac";

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

  try {
    const body = JSON.stringify(toApiPayload(formData));
    const response = await signAndForward(agentId, body);

    if (!response.ok) {
      const text = await response.text().catch(() => "");
      return { error: `API error [${response.status}]: ${text || response.statusText}` };
    }
    return response.json();
  } catch (error) {
    Sentry.captureException(error);
    return { error: "Something went wrong. Please try again." };
  }
}
