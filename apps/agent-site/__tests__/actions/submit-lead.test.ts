import { describe, it, expect, vi, beforeEach } from "vitest";
import type { LeadFormData } from "@real-estate-star/shared-types";

vi.mock("@/lib/turnstile", () => ({
  validateTurnstile: vi.fn(),
}));

vi.mock("@/lib/hmac", () => ({
  signAndForward: vi.fn(),
}));

vi.mock("@sentry/nextjs", () => ({
  captureException: vi.fn(),
}));

const validFormData: LeadFormData = {
  leadTypes: ["selling"],
  firstName: "Jane",
  lastName: "Doe",
  email: "jane@example.com",
  phone: "555-555-5555",
  timeline: "asap",
  seller: { address: "123 Main", city: "Newark", state: "NJ", zip: "07101" },
  marketingConsent: { optedIn: true, consentText: "I consent", channels: ["email"] },
};

describe("submitLead", () => {
  beforeEach(() => {
    vi.resetModules();
    vi.clearAllMocks();
  });

  it("returns fake success when honeypot field is filled (no API call)", async () => {
    const { validateTurnstile } = await import("@/lib/turnstile");
    const { signAndForward } = await import("@/lib/hmac");
    const { submitLead } = await import("@/actions/submit-lead");

    const result = await submitLead(
      "agent-123",
      { email: "bot@spam.com", website: "http://spam.com" } as LeadFormData,
      "some-token",
    );

    expect(result).toEqual({ leadId: "fake-id", status: "received" });
    expect(validateTurnstile).not.toHaveBeenCalled();
    expect(signAndForward).not.toHaveBeenCalled();
  });

  it("returns error when Turnstile verification fails", async () => {
    const { validateTurnstile } = await import("@/lib/turnstile");
    const { signAndForward } = await import("@/lib/hmac");
    const { submitLead } = await import("@/actions/submit-lead");

    (validateTurnstile as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: false, code: "SEC-004", detail: "test rejection" });

    const result = await submitLead(
      "agent-123",
      { email: "test@example.com" } as LeadFormData,
      "bad-token",
    );

    expect(result).toEqual({ error: "Verification failed [SEC-004]: test rejection" });
    expect(signAndForward).not.toHaveBeenCalled();
  });

  it("returns leadId on successful submission", async () => {
    const { validateTurnstile } = await import("@/lib/turnstile");
    const { signAndForward } = await import("@/lib/hmac");
    const { submitLead } = await import("@/actions/submit-lead");

    (validateTurnstile as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: true });
    (signAndForward as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ leadId: "lead-abc-123", status: "received" }),
    });

    const result = await submitLead("agent-123", validFormData, "valid-token");

    expect(result).toEqual({ leadId: "lead-abc-123", status: "received" });
    expect(validateTurnstile).toHaveBeenCalledWith("valid-token");

    const expectedPayload = JSON.stringify({
      leadType: "Seller",
      firstName: "Jane",
      lastName: "Doe",
      email: "jane@example.com",
      phone: "555-555-5555",
      timeline: "asap",
      notes: undefined,
      buyer: undefined,
      seller: { address: "123 Main", city: "Newark", state: "NJ", zip: "07101" },
      marketingConsent: { optedIn: true, consentText: "I consent", channels: ["email"] },
    });
    expect(signAndForward).toHaveBeenCalledWith("agent-123", expectedPayload);
  });

  it("maps leadTypes correctly", async () => {
    const { validateTurnstile } = await import("@/lib/turnstile");
    const { signAndForward } = await import("@/lib/hmac");
    const { submitLead } = await import("@/actions/submit-lead");

    (validateTurnstile as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: true });
    (signAndForward as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ leadId: "lead-1", status: "received" }),
    });

    await submitLead("agent-123", { ...validFormData, leadTypes: ["buying", "selling"] }, "token");

    const payload = JSON.parse((signAndForward as ReturnType<typeof vi.fn>).mock.calls[0][1]);
    expect(payload.leadType).toBe("Both");
  });

  it("returns API error with status and body when API responds with non-ok status", async () => {
    const { validateTurnstile } = await import("@/lib/turnstile");
    const { signAndForward } = await import("@/lib/hmac");
    const { submitLead } = await import("@/actions/submit-lead");

    (validateTurnstile as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: true });
    (signAndForward as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: false,
      status: 400,
      statusText: "Bad Request",
      text: async () => '{"errors":{"Email":["Invalid email"]}}',
    });

    const result = await submitLead("agent-123", validFormData, "valid-token");

    expect(result.error).toContain("API error [400]");
    expect(result.error).toContain("Invalid email");
  });

  it("returns generic error when signAndForward throws", async () => {
    const { validateTurnstile } = await import("@/lib/turnstile");
    const { signAndForward } = await import("@/lib/hmac");
    const { submitLead } = await import("@/actions/submit-lead");

    const networkError = new TypeError("fetch failed");
    (validateTurnstile as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: true });
    (signAndForward as ReturnType<typeof vi.fn>).mockRejectedValueOnce(networkError);

    const result = await submitLead("agent-123", validFormData, "valid-token");

    expect(result).toEqual({ error: "Something went wrong. Please try again." });
  });
});
