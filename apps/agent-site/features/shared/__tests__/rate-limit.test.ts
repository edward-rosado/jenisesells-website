import { describe, it, expect, beforeEach, vi } from "vitest";
import { checkRateLimit, resetRateLimits } from "@/features/shared/rate-limit";

describe("checkRateLimit", () => {
  beforeEach(async () => {
    await resetRateLimits();
  });

  it("allows requests under the limit", async () => {
    const result = await checkRateLimit("test", "user@example.com", 3, 60_000);
    expect(result.allowed).toBe(true);
  });

  it("allows exactly maxRequests calls", async () => {
    for (let i = 0; i < 5; i++) {
      const result = await checkRateLimit("test", "user@example.com", 5, 60_000);
      expect(result.allowed).toBe(true);
    }
  });

  it("blocks after exceeding maxRequests", async () => {
    for (let i = 0; i < 3; i++) {
      await checkRateLimit("test", "user@example.com", 3, 60_000);
    }
    const result = await checkRateLimit("test", "user@example.com", 3, 60_000);
    expect(result.allowed).toBe(false);
    expect(result.retryAfterMs).toBeGreaterThan(0);
    expect(result.retryAfterMs).toBeLessThanOrEqual(60_000);
  });

  it("isolates different actions", async () => {
    for (let i = 0; i < 3; i++) {
      await checkRateLimit("action-a", "user@example.com", 3, 60_000);
    }
    // Different action should still be allowed
    const result = await checkRateLimit("action-b", "user@example.com", 3, 60_000);
    expect(result.allowed).toBe(true);
  });

  it("isolates different identifiers", async () => {
    for (let i = 0; i < 3; i++) {
      await checkRateLimit("test", "user1@example.com", 3, 60_000);
    }
    // Different user should still be allowed
    const result = await checkRateLimit("test", "user2@example.com", 3, 60_000);
    expect(result.allowed).toBe(true);
  });

  it("allows requests after window expires", async () => {
    vi.useFakeTimers();
    try {
      for (let i = 0; i < 3; i++) {
        await checkRateLimit("test", "user@example.com", 3, 60_000);
      }
      // Blocked
      expect((await checkRateLimit("test", "user@example.com", 3, 60_000)).allowed).toBe(false);

      // Advance past window
      vi.advanceTimersByTime(61_000);

      // Now allowed again
      const result = await checkRateLimit("test", "user@example.com", 3, 60_000);
      expect(result.allowed).toBe(true);
    } finally {
      vi.useRealTimers();
    }
  });

  it("resetRateLimits clears all state", async () => {
    for (let i = 0; i < 3; i++) {
      await checkRateLimit("test", "user@example.com", 3, 60_000);
    }
    expect((await checkRateLimit("test", "user@example.com", 3, 60_000)).allowed).toBe(false);

    await resetRateLimits();

    expect((await checkRateLimit("test", "user@example.com", 3, 60_000)).allowed).toBe(true);
  });

  it("returns retryAfterMs when blocked", async () => {
    vi.useFakeTimers();
    try {
      for (let i = 0; i < 3; i++) {
        await checkRateLimit("test", "user@example.com", 3, 60_000);
      }
      vi.advanceTimersByTime(10_000); // 10s into the window

      const result = await checkRateLimit("test", "user@example.com", 3, 60_000);
      expect(result.allowed).toBe(false);
      // Should tell us to wait ~50s (60s window - 10s elapsed)
      expect(result.retryAfterMs).toBeGreaterThan(49_000);
      expect(result.retryAfterMs).toBeLessThanOrEqual(50_001);
    } finally {
      vi.useRealTimers();
    }
  });
});
