import { describe, it, expect, vi, beforeEach } from "vitest";
import type { LeadFormData } from "@real-estate-star/domain";

vi.mock("@/features/lead-capture/turnstile", () => ({
  validateTurnstile: vi.fn(),
}));

const mockPost = vi.fn();
vi.mock("@real-estate-star/api-client", () => ({
  createApiClient: () => ({ POST: mockPost, GET: vi.fn() }),
}));

vi.mock("@/features/shared/hmac", () => ({
  signRequest: vi.fn().mockResolvedValue({
    headers: { "X-API-Key": "test", "X-Signature": "test", "X-Timestamp": "123" },
    signal: new AbortController().signal,
    cleanup: vi.fn(),
  }),
  getApiUrl: async () => "http://test-api",
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
    const { validateTurnstile } = await import("@/features/lead-capture/turnstile");
    const { submitLead } = await import("@/features/lead-capture/submit-lead");

    const result = await submitLead(
      "agent-123",
      { email: "bot@spam.com", website: "http://spam.com" } as LeadFormData,
      "some-token",
    );

    expect(result).toEqual({ leadId: "fake-id", status: "received" });
    expect(validateTurnstile).not.toHaveBeenCalled();
    expect(mockPost).not.toHaveBeenCalled();
  });

  it("returns error when Turnstile verification fails", async () => {
    const { validateTurnstile } = await import("@/features/lead-capture/turnstile");
    const { submitLead } = await import("@/features/lead-capture/submit-lead");

    (validateTurnstile as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: false, code: "SEC-004", detail: "test rejection" });

    const result = await submitLead(
      "agent-123",
      { email: "test@example.com" } as LeadFormData,
      "bad-token",
    );

    expect(result).toEqual({ error: "Verification failed [SEC-004]: test rejection" });
    expect(mockPost).not.toHaveBeenCalled();
  });

  it("returns leadId on successful submission", async () => {
    const { validateTurnstile } = await import("@/features/lead-capture/turnstile");
    const { signRequest } = await import("@/features/shared/hmac");
    const { submitLead } = await import("@/features/lead-capture/submit-lead");

    (validateTurnstile as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: true });
    mockPost.mockResolvedValueOnce({
      data: { leadId: "lead-abc-123", status: "received" },
      error: null,
      response: { ok: true, status: 200, text: vi.fn() },
    });

    const result = await submitLead("agent-123", validFormData, "valid-token");

    expect(result).toEqual({ leadId: "lead-abc-123", status: "received" });
    expect(validateTurnstile).toHaveBeenCalledWith("valid-token");

    const expectedBody = JSON.stringify({
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
    expect(signRequest).toHaveBeenCalledWith("agent-123", expectedBody);
    expect(mockPost).toHaveBeenCalledWith(
      "/agents/{agentId}/leads",
      expect.objectContaining({ params: { path: { agentId: "agent-123" } } }),
    );
  });

  it("maps leadTypes correctly", async () => {
    const { validateTurnstile } = await import("@/features/lead-capture/turnstile");
    const { submitLead } = await import("@/features/lead-capture/submit-lead");

    (validateTurnstile as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: true });
    mockPost.mockResolvedValueOnce({
      data: { leadId: "lead-1", status: "received" },
      error: null,
      response: { ok: true, status: 200, text: vi.fn() },
    });

    await submitLead("agent-123", { ...validFormData, leadTypes: ["buying", "selling"] }, "token");

    const callBody = mockPost.mock.calls[0][1].body;
    expect(callBody.leadType).toBe("Both");
  });

  it("returns API error with status and body when API responds with non-ok status", async () => {
    const { validateTurnstile } = await import("@/features/lead-capture/turnstile");
    const { submitLead } = await import("@/features/lead-capture/submit-lead");

    (validateTurnstile as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: true });
    mockPost.mockResolvedValueOnce({
      data: null,
      error: { title: "Bad Request" },
      response: {
        ok: false,
        status: 400,
        statusText: "Bad Request",
        text: async () => '{"errors":{"Email":["Invalid email"]}}',
      },
    });

    const result = await submitLead("agent-123", validFormData, "valid-token");

    expect(result.error).toContain("API error [400]");
    expect(result.error).toContain("Invalid email");
  });

  it("returns generic error when signRequest throws", async () => {
    const { validateTurnstile } = await import("@/features/lead-capture/turnstile");
    const { signRequest } = await import("@/features/shared/hmac");
    const { submitLead } = await import("@/features/lead-capture/submit-lead");

    const networkError = new TypeError("fetch failed");
    (validateTurnstile as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: true });
    (signRequest as ReturnType<typeof vi.fn>).mockRejectedValueOnce(networkError);

    const result = await submitLead("agent-123", validFormData, "valid-token");

    expect(result).toEqual({ error: "Something went wrong. Please try again." });
  });
});
