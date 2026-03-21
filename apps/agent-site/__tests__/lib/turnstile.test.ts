import { describe, it, expect, vi, beforeEach } from "vitest";

describe("validateTurnstile", () => {
  beforeEach(() => {
    vi.resetModules();
    global.fetch = vi.fn();
    process.env.TURNSTILE_SECRET_KEY = "test-secret";
  });

  it("returns true when Cloudflare responds with success", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ success: true }),
    });
    const { validateTurnstile } = await import("@/lib/turnstile");
    const result = await validateTurnstile("test-token");
    expect(result).toBe(true);
  });

  it("returns false when Cloudflare responds with failure", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ success: false }),
    });
    const { validateTurnstile } = await import("@/lib/turnstile");
    const result = await validateTurnstile("bad-token");
    expect(result).toBe(false);
  });

  it("returns false and logs error when fetch throws", async () => {
    const spy = vi.spyOn(console, "error").mockImplementation(() => {});
    (fetch as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error("net"));
    const { validateTurnstile } = await import("@/lib/turnstile");
    const result = await validateTurnstile("token");
    expect(result).toBe(false);
    expect(spy).toHaveBeenCalledWith("[SEC-001] Turnstile validation error:", expect.any(Error));
    spy.mockRestore();
  });

  it("returns false when TURNSTILE_SECRET_KEY is not set", async () => {
    delete process.env.TURNSTILE_SECRET_KEY;
    const { validateTurnstile } = await import("@/lib/turnstile");
    const result = await validateTurnstile("token");
    expect(result).toBe(false);
  });

  it("aborts fetch after 15 seconds and returns false", async () => {
    vi.useFakeTimers();
    const spy = vi.spyOn(console, "error").mockImplementation(() => {});
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
    expect(result).toBe(false);
    // DOMException is not a subclass of Error in all environments — use anything() for AbortError
    expect(spy).toHaveBeenCalledWith("[SEC-001] Turnstile validation error:", expect.anything());

    spy.mockRestore();
    vi.useRealTimers();
  });
});
