import { describe, it, expect, vi, beforeEach } from "vitest";
import { resetRateLimits } from "@/features/shared/rate-limit";

const mockPost = vi.fn();
const mockGet = vi.fn();
vi.mock("@real-estate-star/api-client", () => ({
  createApiClient: () => ({ POST: mockPost, GET: mockGet }),
}));

vi.mock("@/features/shared/hmac", () => ({
  signRequest: vi.fn().mockResolvedValue({
    headers: { "X-API-Key": "test", "X-Signature": "test", "X-Timestamp": "123" },
    signal: new AbortController().signal,
    cleanup: vi.fn(),
  }),
  getApiUrl: async () => "http://test-api",
}));

describe("requestOptOut", () => {
  beforeEach(async () => {
    vi.resetModules();
    vi.clearAllMocks();
    await resetRateLimits();
  });

  it("returns ok:true when API responds with success", async () => {
    mockPost.mockResolvedValueOnce({
      data: {},
      error: null,
      response: { ok: true, status: 200 },
    });
    const { requestOptOut } = await import("@/features/privacy/privacy");

    const result = await requestOptOut("agent-123", "user@example.com", "token-abc");

    expect(result).toEqual({ ok: true });
    expect(mockPost).toHaveBeenCalledWith(
      "/agents/{agentId}/leads/opt-out",
      expect.objectContaining({
        params: { path: { agentId: "agent-123" } },
        body: { email: "user@example.com", token: "token-abc" },
      }),
    );
  });

  it("returns error when API responds with non-ok status", async () => {
    mockPost.mockResolvedValueOnce({
      data: null,
      error: { title: "Bad Request" },
      response: { ok: false, status: 400 },
    });
    const { requestOptOut } = await import("@/features/privacy/privacy");

    const result = await requestOptOut("agent-123", "user@example.com", "token-abc");

    expect(result).toEqual({ ok: false, error: "Something went wrong. Please try again." });
  });

  it("returns error when signRequest throws", async () => {
    const { signRequest } = await import("@/features/shared/hmac");
    (signRequest as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error("network error"));
    const { requestOptOut } = await import("@/features/privacy/privacy");

    const result = await requestOptOut("agent-123", "user@example.com", "token-abc");

    expect(result).toEqual({ ok: false, error: "Something went wrong. Please try again." });
  });
});

describe("requestDeletion", () => {
  beforeEach(async () => {
    vi.resetModules();
    vi.clearAllMocks();
    await resetRateLimits();
  });

  it("returns ok:true when API responds with success", async () => {
    mockPost.mockResolvedValueOnce({
      data: {},
      error: null,
      response: { ok: true, status: 200 },
    });
    const { requestDeletion } = await import("@/features/privacy/privacy");

    const result = await requestDeletion("agent-123", "user@example.com");

    expect(result).toEqual({ ok: true });
    expect(mockPost).toHaveBeenCalledWith(
      "/agents/{agentId}/leads/request-deletion",
      expect.objectContaining({
        params: { path: { agentId: "agent-123" } },
        body: { email: "user@example.com" },
      }),
    );
  });

  it("returns error when API responds with non-ok status", async () => {
    mockPost.mockResolvedValueOnce({
      data: null,
      error: { title: "Bad Request" },
      response: { ok: false, status: 400 },
    });
    const { requestDeletion } = await import("@/features/privacy/privacy");

    const result = await requestDeletion("agent-123", "user@example.com");

    expect(result).toEqual({ ok: false, error: "Something went wrong. Please try again." });
  });

  it("returns error when signRequest throws", async () => {
    const { signRequest } = await import("@/features/shared/hmac");
    (signRequest as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error("network error"));
    const { requestDeletion } = await import("@/features/privacy/privacy");

    const result = await requestDeletion("agent-123", "user@example.com");

    expect(result).toEqual({ ok: false, error: "Something went wrong. Please try again." });
  });
});

describe("requestSubscribe", () => {
  beforeEach(async () => {
    vi.resetModules();
    vi.clearAllMocks();
    await resetRateLimits();
  });

  it("returns ok:true when API responds with success", async () => {
    mockPost.mockResolvedValueOnce({
      data: {},
      error: null,
      response: { ok: true, status: 200 },
    });
    const { requestSubscribe } = await import("@/features/privacy/privacy");

    const result = await requestSubscribe("agent-123", "user@example.com", "token-abc");

    expect(result).toEqual({ ok: true });
    expect(mockPost).toHaveBeenCalledWith(
      "/agents/{agentId}/leads/subscribe",
      expect.objectContaining({
        params: { path: { agentId: "agent-123" } },
        body: { email: "user@example.com", token: "token-abc" },
      }),
    );
  });

  it("returns error when API responds with non-ok status", async () => {
    mockPost.mockResolvedValueOnce({
      data: null,
      error: { title: "Bad Request" },
      response: { ok: false, status: 400 },
    });
    const { requestSubscribe } = await import("@/features/privacy/privacy");

    const result = await requestSubscribe("agent-123", "user@example.com", "token-abc");

    expect(result).toEqual({ ok: false, error: "Something went wrong. Please try again." });
  });

  it("returns error when signRequest throws", async () => {
    const { signRequest } = await import("@/features/shared/hmac");
    (signRequest as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error("network error"));
    const { requestSubscribe } = await import("@/features/privacy/privacy");

    const result = await requestSubscribe("agent-123", "user@example.com", "token-abc");

    expect(result).toEqual({ ok: false, error: "Something went wrong. Please try again." });
  });
});

describe("requestExport", () => {
  beforeEach(async () => {
    vi.resetModules();
    vi.clearAllMocks();
    await resetRateLimits();
    vi.stubEnv("LEAD_API_KEY", "test-api-key");
    vi.stubEnv("LEAD_HMAC_SECRET", "test-secret");
    vi.stubEnv("LEAD_API_URL", "http://test-api");
  });

  it("returns ok:true with data when API responds with success", async () => {
    const exportData = [{ email: "user@example.com", name: "User" }];
    mockGet.mockResolvedValueOnce({
      data: exportData,
      error: null,
      response: { ok: true, status: 200 },
    });
    const { requestExport } = await import("@/features/privacy/privacy");

    const result = await requestExport("agent-123", "user@example.com");

    expect(result).toEqual({ ok: true, data: exportData });
    expect(mockGet).toHaveBeenCalledWith(
      "/agents/{agentId}/leads/export",
      expect.objectContaining({
        params: { path: { agentId: "agent-123" }, query: { email: "user@example.com" } },
      }),
    );
  });

  it("returns ok:true with empty array when API responds with 404", async () => {
    mockGet.mockResolvedValueOnce({
      data: null,
      error: null,
      response: { ok: false, status: 404 },
    });
    const { requestExport } = await import("@/features/privacy/privacy");

    const result = await requestExport("agent-123", "user@example.com");

    expect(result).toEqual({ ok: true, data: [] });
  });

  it("returns error when API responds with non-ok status", async () => {
    mockGet.mockResolvedValueOnce({
      data: null,
      error: { title: "Internal Server Error" },
      response: { ok: false, status: 500 },
    });
    const { requestExport } = await import("@/features/privacy/privacy");

    const result = await requestExport("agent-123", "user@example.com");

    expect(result).toEqual({ ok: false, error: "Something went wrong. Please try again." });
  });

  it("returns error when GET throws", async () => {
    mockGet.mockRejectedValueOnce(new Error("network error"));
    const { requestExport } = await import("@/features/privacy/privacy");

    const result = await requestExport("agent-123", "user@example.com");

    expect(result).toEqual({ ok: false, error: "Something went wrong. Please try again." });
  });
});

describe("rate limiting", () => {
  beforeEach(async () => {
    vi.resetModules();
    vi.clearAllMocks();
    await resetRateLimits();
  });

  it("blocks requestOptOut after 5 rapid calls", async () => {
    mockPost.mockResolvedValue({ data: {}, error: null, response: { ok: true, status: 200 } });
    const { requestOptOut } = await import("@/features/privacy/privacy");

    for (let i = 0; i < 5; i++) {
      const result = await requestOptOut("agent-123", "spam@example.com", "token");
      expect(result.ok).toBe(true);
    }
    const blocked = await requestOptOut("agent-123", "spam@example.com", "token");
    expect(blocked).toEqual({ ok: false, error: "Too many requests. Please try again later." });
  });

  it("blocks requestDeletion after 5 rapid calls", async () => {
    mockPost.mockResolvedValue({ data: {}, error: null, response: { ok: true, status: 200 } });
    const { requestDeletion } = await import("@/features/privacy/privacy");

    for (let i = 0; i < 5; i++) {
      await requestDeletion("agent-123", "spam@example.com");
    }
    const blocked = await requestDeletion("agent-123", "spam@example.com");
    expect(blocked).toEqual({ ok: false, error: "Too many requests. Please try again later." });
  });

  it("blocks requestExport after 5 rapid calls", async () => {
    vi.stubEnv("LEAD_API_KEY", "test-api-key");
    vi.stubEnv("LEAD_HMAC_SECRET", "test-secret");
    mockGet.mockResolvedValue({ data: [], error: null, response: { ok: true, status: 200 } });
    const { requestExport } = await import("@/features/privacy/privacy");

    for (let i = 0; i < 5; i++) {
      await requestExport("agent-123", "spam@example.com");
    }
    const blocked = await requestExport("agent-123", "spam@example.com");
    expect(blocked).toEqual({ ok: false, error: "Too many requests. Please try again later." });
  });

  it("blocks requestSubscribe after 5 rapid calls", async () => {
    mockPost.mockResolvedValue({ data: {}, error: null, response: { ok: true, status: 200 } });
    const { requestSubscribe } = await import("@/features/privacy/privacy");

    for (let i = 0; i < 5; i++) {
      await requestSubscribe("agent-123", "spam@example.com", "token");
    }
    const blocked = await requestSubscribe("agent-123", "spam@example.com", "token");
    expect(blocked).toEqual({ ok: false, error: "Too many requests. Please try again later." });
  });

  it("allows different emails even when one is blocked", async () => {
    mockPost.mockResolvedValue({ data: {}, error: null, response: { ok: true, status: 200 } });
    const { requestOptOut } = await import("@/features/privacy/privacy");

    for (let i = 0; i < 5; i++) {
      await requestOptOut("agent-123", "spam@example.com", "token");
    }
    // Different email should still work
    const result = await requestOptOut("agent-123", "legit@example.com", "token");
    expect(result.ok).toBe(true);
  });
});
