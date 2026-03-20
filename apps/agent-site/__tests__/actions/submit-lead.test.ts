import { describe, it, expect, vi, beforeEach } from "vitest";

vi.mock("@/lib/turnstile", () => ({
  validateTurnstile: vi.fn(),
}));

vi.mock("@/lib/hmac", () => ({
  signAndForward: vi.fn(),
}));

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
      { email: "bot@spam.com", website: "http://spam.com" } as any,
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

    (validateTurnstile as ReturnType<typeof vi.fn>).mockResolvedValueOnce(false);

    const result = await submitLead(
      "agent-123",
      { email: "test@example.com" } as any,
      "bad-token",
    );

    expect(result).toEqual({ error: "Verification failed. Please try again." });
    expect(signAndForward).not.toHaveBeenCalled();
  });

  it("returns leadId on successful submission", async () => {
    const { validateTurnstile } = await import("@/lib/turnstile");
    const { signAndForward } = await import("@/lib/hmac");
    const { submitLead } = await import("@/actions/submit-lead");

    (validateTurnstile as ReturnType<typeof vi.fn>).mockResolvedValueOnce(true);
    (signAndForward as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ leadId: "lead-abc-123", status: "received" }),
    });

    const result = await submitLead(
      "agent-123",
      { email: "buyer@example.com", firstName: "Jane" } as any,
      "valid-token",
    );

    expect(result).toEqual({ leadId: "lead-abc-123", status: "received" });
    expect(validateTurnstile).toHaveBeenCalledWith("valid-token");
    expect(signAndForward).toHaveBeenCalledWith(
      "agent-123",
      JSON.stringify({ email: "buyer@example.com", firstName: "Jane" }),
    );
  });

  it("returns generic error message when API responds with non-ok status", async () => {
    const { validateTurnstile } = await import("@/lib/turnstile");
    const { signAndForward } = await import("@/lib/hmac");
    const { submitLead } = await import("@/actions/submit-lead");

    (validateTurnstile as ReturnType<typeof vi.fn>).mockResolvedValueOnce(true);
    (signAndForward as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: false,
      status: 500,
    });

    const result = await submitLead(
      "agent-123",
      { email: "test@example.com" } as any,
      "valid-token",
    );

    expect(result).toEqual({ error: "Something went wrong. Please try again." });
  });
});
