import { describe, it, expect, vi, beforeEach } from "vitest";

describe("validateTurnstile", () => {
  beforeEach(() => {
    vi.resetModules();
    global.fetch = vi.fn();
    process.env.TURNSTILE_SECRET_KEY = "test-secret";
  });

  it("returns ok when Cloudflare responds with success", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ success: true }),
    });
    const { validateTurnstile } = await import("@/lib/turnstile");
    const result = await validateTurnstile("test-token");
    expect(result).toEqual({ ok: true });
  });

  it("returns SEC-004 when Cloudflare responds with failure", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ success: false, "error-codes": ["invalid-input-response"], hostname: "example.com" }),
    });
    const { validateTurnstile } = await import("@/lib/turnstile");
    const result = await validateTurnstile("bad-token");
    expect(result).toMatchObject({ ok: false, code: "SEC-004" });
    expect(result.ok === false && result.detail).toContain("invalid-input-response");
  });

  it("returns SEC-001 when fetch throws", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error("net"));
    const { validateTurnstile } = await import("@/lib/turnstile");
    const result = await validateTurnstile("token");
    expect(result).toMatchObject({ ok: false, code: "SEC-001" });
  });

  it("returns SEC-002 when TURNSTILE_SECRET_KEY is not set", async () => {
    delete process.env.TURNSTILE_SECRET_KEY;
    const { validateTurnstile } = await import("@/lib/turnstile");
    const result = await validateTurnstile("token");
    expect(result).toEqual({ ok: false, code: "SEC-002", detail: "TURNSTILE_SECRET_KEY is not set" });
  });

  it("returns SEC-003 when token is empty", async () => {
    const { validateTurnstile } = await import("@/lib/turnstile");
    const result = await validateTurnstile("");
    expect(result).toEqual({ ok: false, code: "SEC-003", detail: "Turnstile token is empty" });
  });

  it("returns SEC-001 on abort after 15 seconds", async () => {
    vi.useFakeTimers();
    (fetch as ReturnType<typeof vi.fn>).mockImplementation(
      (_url: string, init?: RequestInit) =>
        new Promise((_resolve, reject) => {
          init?.signal?.addEventListener("abort", () => reject(new DOMException("The operation was aborted.", "AbortError")));
        }),
    );

    const { validateTurnstile } = await import("@/lib/turnstile");
    const promise = validateTurnstile("test-token");

    await vi.advanceTimersByTimeAsync(15_000);
    const result = await promise;
    expect(result).toMatchObject({ ok: false, code: "SEC-001" });

    vi.useRealTimers();
  });
});
