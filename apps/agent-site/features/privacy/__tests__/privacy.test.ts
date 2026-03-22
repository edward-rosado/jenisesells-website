import { describe, it, expect, vi, beforeEach } from "vitest";

vi.mock("@/features/lead-capture/hmac", () => ({
  signAndForward: vi.fn(),
}));

describe("requestOptOut", () => {
  beforeEach(() => {
    vi.resetModules();
    vi.clearAllMocks();
  });

  it("returns ok:true when API responds with success", async () => {
    const { signAndForward } = await import("@/features/lead-capture/hmac");
    (signAndForward as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: true });
    const { requestOptOut } = await import("@/features/privacy/privacy");

    const result = await requestOptOut("agent-123", "user@example.com", "token-abc");

    expect(result).toEqual({ ok: true });
    expect(signAndForward).toHaveBeenCalledWith(
      "agent-123",
      JSON.stringify({ email: "user@example.com", token: "token-abc" }),
      "agents/agent-123/leads/opt-out",
    );
  });

  it("returns error when API responds with non-ok status", async () => {
    const { signAndForward } = await import("@/features/lead-capture/hmac");
    (signAndForward as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: false, status: 400 });
    const { requestOptOut } = await import("@/features/privacy/privacy");

    const result = await requestOptOut("agent-123", "user@example.com", "token-abc");

    expect(result).toEqual({ ok: false, error: "Something went wrong. Please try again." });
  });

  it("returns error when signAndForward throws", async () => {
    const { signAndForward } = await import("@/features/lead-capture/hmac");
    (signAndForward as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error("network error"));
    const { requestOptOut } = await import("@/features/privacy/privacy");

    const result = await requestOptOut("agent-123", "user@example.com", "token-abc");

    expect(result).toEqual({ ok: false, error: "Something went wrong. Please try again." });
  });
});

describe("requestDeletion", () => {
  beforeEach(() => {
    vi.resetModules();
    vi.clearAllMocks();
  });

  it("returns ok:true when API responds with success", async () => {
    const { signAndForward } = await import("@/features/lead-capture/hmac");
    (signAndForward as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: true });
    const { requestDeletion } = await import("@/features/privacy/privacy");

    const result = await requestDeletion("agent-123", "user@example.com");

    expect(result).toEqual({ ok: true });
    expect(signAndForward).toHaveBeenCalledWith(
      "agent-123",
      JSON.stringify({ email: "user@example.com" }),
      "agents/agent-123/leads/request-deletion",
    );
  });

  it("returns error when API responds with non-ok status", async () => {
    const { signAndForward } = await import("@/features/lead-capture/hmac");
    (signAndForward as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: false, status: 400 });
    const { requestDeletion } = await import("@/features/privacy/privacy");

    const result = await requestDeletion("agent-123", "user@example.com");

    expect(result).toEqual({ ok: false, error: "Something went wrong. Please try again." });
  });

  it("returns error when signAndForward throws", async () => {
    const { signAndForward } = await import("@/features/lead-capture/hmac");
    (signAndForward as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error("network error"));
    const { requestDeletion } = await import("@/features/privacy/privacy");

    const result = await requestDeletion("agent-123", "user@example.com");

    expect(result).toEqual({ ok: false, error: "Something went wrong. Please try again." });
  });
});

describe("requestSubscribe", () => {
  beforeEach(() => {
    vi.resetModules();
    vi.clearAllMocks();
  });

  it("returns ok:true when API responds with success", async () => {
    const { signAndForward } = await import("@/features/lead-capture/hmac");
    (signAndForward as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: true });
    const { requestSubscribe } = await import("@/features/privacy/privacy");

    const result = await requestSubscribe("agent-123", "user@example.com", "token-abc");

    expect(result).toEqual({ ok: true });
    expect(signAndForward).toHaveBeenCalledWith(
      "agent-123",
      JSON.stringify({ email: "user@example.com", token: "token-abc" }),
      "agents/agent-123/leads/subscribe",
    );
  });

  it("returns error when API responds with non-ok status", async () => {
    const { signAndForward } = await import("@/features/lead-capture/hmac");
    (signAndForward as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: false, status: 400 });
    const { requestSubscribe } = await import("@/features/privacy/privacy");

    const result = await requestSubscribe("agent-123", "user@example.com", "token-abc");

    expect(result).toEqual({ ok: false, error: "Something went wrong. Please try again." });
  });

  it("returns error when signAndForward throws", async () => {
    const { signAndForward } = await import("@/features/lead-capture/hmac");
    (signAndForward as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error("network error"));
    const { requestSubscribe } = await import("@/features/privacy/privacy");

    const result = await requestSubscribe("agent-123", "user@example.com", "token-abc");

    expect(result).toEqual({ ok: false, error: "Something went wrong. Please try again." });
  });
});
