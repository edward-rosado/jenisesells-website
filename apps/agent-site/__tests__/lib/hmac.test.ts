import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

describe("signAndForward", () => {
  const originalEnv = process.env;

  beforeEach(() => {
    vi.resetModules();
    global.fetch = vi.fn();
    process.env = {
      ...originalEnv,
      LEAD_API_KEY: "test-api-key",
      LEAD_HMAC_SECRET: "test-hmac-secret",
      LEAD_API_URL: "https://api.example.com",
    };
  });

  afterEach(() => {
    process.env = originalEnv;
  });

  it("calls the correct URL with POST method", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: true });
    const { signAndForward } = await import("@/lib/hmac");

    await signAndForward("agent-123", JSON.stringify({ email: "test@example.com" }));

    expect(fetch).toHaveBeenCalledWith(
      "https://api.example.com/agents/agent-123/leads",
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("includes X-API-Key header from environment", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: true });
    const { signAndForward } = await import("@/lib/hmac");

    await signAndForward("agent-123", JSON.stringify({ email: "test@example.com" }));

    const [, init] = (fetch as ReturnType<typeof vi.fn>).mock.calls[0];
    expect((init.headers as Record<string, string>)["X-API-Key"]).toBe("test-api-key");
  });

  it("includes X-Signature header with sha256= prefix", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: true });
    const { signAndForward } = await import("@/lib/hmac");

    await signAndForward("agent-123", JSON.stringify({ email: "test@example.com" }));

    const [, init] = (fetch as ReturnType<typeof vi.fn>).mock.calls[0];
    expect((init.headers as Record<string, string>)["X-Signature"]).toMatch(/^sha256=[0-9a-f]{64}$/);
  });

  it("includes X-Timestamp header as a unix timestamp string", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: true });
    const { signAndForward } = await import("@/lib/hmac");

    const before = Math.floor(Date.now() / 1000);
    await signAndForward("agent-123", JSON.stringify({ email: "test@example.com" }));
    const after = Math.floor(Date.now() / 1000);

    const [, init] = (fetch as ReturnType<typeof vi.fn>).mock.calls[0];
    const timestamp = parseInt((init.headers as Record<string, string>)["X-Timestamp"], 10);
    expect(timestamp).toBeGreaterThanOrEqual(before);
    expect(timestamp).toBeLessThanOrEqual(after);
  });

  it("same input produces same signature when timestamp is fixed", async () => {
    const mockDate = 1700000000000;
    vi.spyOn(Date, "now").mockReturnValue(mockDate);

    (fetch as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({ ok: true })
      .mockResolvedValueOnce({ ok: true });

    const { signAndForward } = await import("@/lib/hmac");
    const body = JSON.stringify({ email: "test@example.com" });

    await signAndForward("agent-123", body);
    await signAndForward("agent-123", body);

    const sig1 = ((fetch as ReturnType<typeof vi.fn>).mock.calls[0][1].headers as Record<string, string>)["X-Signature"];
    const sig2 = ((fetch as ReturnType<typeof vi.fn>).mock.calls[1][1].headers as Record<string, string>)["X-Signature"];

    expect(sig1).toBe(sig2);

    vi.restoreAllMocks();
  });

  it("different body produces different signature", async () => {
    const mockDate = 1700000000000;
    vi.spyOn(Date, "now").mockReturnValue(mockDate);

    (fetch as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({ ok: true })
      .mockResolvedValueOnce({ ok: true });

    const { signAndForward } = await import("@/lib/hmac");

    await signAndForward("agent-123", JSON.stringify({ email: "a@example.com" }));
    await signAndForward("agent-123", JSON.stringify({ email: "b@example.com" }));

    const sig1 = ((fetch as ReturnType<typeof vi.fn>).mock.calls[0][1].headers as Record<string, string>)["X-Signature"];
    const sig2 = ((fetch as ReturnType<typeof vi.fn>).mock.calls[1][1].headers as Record<string, string>)["X-Signature"];

    expect(sig1).not.toBe(sig2);

    vi.restoreAllMocks();
  });

  it("timestamp is included in the signed message (different timestamps produce different signatures for same body)", async () => {
    (fetch as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({ ok: true })
      .mockResolvedValueOnce({ ok: true });

    vi.spyOn(Date, "now").mockReturnValueOnce(1700000000000).mockReturnValueOnce(1700000001000);

    const { signAndForward } = await import("@/lib/hmac");
    const body = JSON.stringify({ email: "test@example.com" });

    await signAndForward("agent-123", body);
    await signAndForward("agent-123", body);

    const sig1 = ((fetch as ReturnType<typeof vi.fn>).mock.calls[0][1].headers as Record<string, string>)["X-Signature"];
    const sig2 = ((fetch as ReturnType<typeof vi.fn>).mock.calls[1][1].headers as Record<string, string>)["X-Signature"];

    expect(sig1).not.toBe(sig2);

    vi.restoreAllMocks();
  });

  it("forwards the raw body in the request", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: true });
    const { signAndForward } = await import("@/lib/hmac");

    const body = JSON.stringify({ email: "test@example.com" });
    await signAndForward("agent-123", body);

    const [, init] = (fetch as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(init.body).toBe(body);
  });

  it("returns the fetch response", async () => {
    const mockResponse = { ok: true, status: 201, json: async () => ({ leadId: "abc" }) };
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce(mockResponse);
    const { signAndForward } = await import("@/lib/hmac");

    const result = await signAndForward("agent-123", JSON.stringify({ email: "test@example.com" }));

    expect(result).toBe(mockResponse);
  });

  it("different agentId produces different signature (per-agent key derivation)", async () => {
    const mockDate = 1700000000000;
    vi.spyOn(Date, "now").mockReturnValue(mockDate);

    (fetch as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({ ok: true })
      .mockResolvedValueOnce({ ok: true });

    const { signAndForward } = await import("@/lib/hmac");
    const body = JSON.stringify({ email: "test@example.com" });

    await signAndForward("agent-aaa", body);
    await signAndForward("agent-bbb", body);

    const sig1 = ((fetch as ReturnType<typeof vi.fn>).mock.calls[0][1].headers as Record<string, string>)["X-Signature"];
    const sig2 = ((fetch as ReturnType<typeof vi.fn>).mock.calls[1][1].headers as Record<string, string>)["X-Signature"];

    expect(sig1).not.toBe(sig2);

    vi.restoreAllMocks();
  });

  it("uses provided path override instead of default leads path", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ ok: true });
    const { signAndForward } = await import("@/lib/hmac");

    await signAndForward("agent-123", JSON.stringify({ email: "test@example.com" }), "agents/agent-123/leads/opt-out");

    expect(fetch).toHaveBeenCalledWith(
      "https://api.example.com/agents/agent-123/leads/opt-out",
      expect.objectContaining({ method: "POST" }),
    );
  });
});
