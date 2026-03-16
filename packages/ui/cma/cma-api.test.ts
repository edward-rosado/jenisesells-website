import { describe, it, expect, vi, beforeEach } from "vitest";
import { submitCma } from "./cma-api";
import type { CmaSubmitRequest } from "@real-estate-star/shared-types";

const API_BASE = "https://api.real-estate-star.com";
const AGENT_ID = "jenise-buckalew";

function makeRequest(overrides?: Partial<CmaSubmitRequest>): CmaSubmitRequest {
  return {
    firstName: "Jane",
    lastName: "Doe",
    email: "jane@example.com",
    phone: "555-0100",
    address: "123 Main St",
    city: "Newark",
    state: "NJ",
    zip: "07101",
    timeline: "asap",
    ...overrides,
  };
}

describe("submitCma", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("posts to the correct URL with encoded agentId", async () => {
    const fetchSpy = vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(JSON.stringify({ jobId: "job-1", status: "processing" }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );

    await submitCma(API_BASE, "agent with spaces", makeRequest());

    expect(fetchSpy).toHaveBeenCalledWith(
      `${API_BASE}/agents/agent%20with%20spaces/cma`,
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("sends JSON body with correct headers", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(JSON.stringify({ jobId: "job-1", status: "processing" }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );

    const request = makeRequest({ beds: 3, notes: "Nice house" });
    await submitCma(API_BASE, AGENT_ID, request);

    const [, options] = vi.mocked(fetch).mock.calls[0];
    expect(options?.headers).toEqual({
      "Content-Type": "application/json",
      Accept: "application/json",
    });
    expect(JSON.parse(options?.body as string)).toEqual(request);
  });

  it("returns jobId and status on success", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(JSON.stringify({ jobId: "abc-123", status: "processing" }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );

    const result = await submitCma(API_BASE, AGENT_ID, makeRequest());

    expect(result).toEqual({ jobId: "abc-123", status: "processing" });
  });

  it("throws on HTTP 400 with status in message", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response("Validation error", { status: 400 }),
    );
    const consoleSpy = vi.spyOn(console, "error").mockImplementation(() => {});

    await expect(submitCma(API_BASE, AGENT_ID, makeRequest())).rejects.toThrow(
      "CMA submission failed (400)",
    );
    expect(consoleSpy).toHaveBeenCalledWith(
      "[CMA-001] API error body:",
      "Validation error",
    );
  });

  it("throws on HTTP 500", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response("Internal Server Error", { status: 500 }),
    );
    vi.spyOn(console, "error").mockImplementation(() => {});

    await expect(submitCma(API_BASE, AGENT_ID, makeRequest())).rejects.toThrow(
      "CMA submission failed (500)",
    );
  });

  it("does not log when error body is empty", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response("", { status: 500 }),
    );
    const consoleSpy = vi.spyOn(console, "error").mockImplementation(() => {});

    await expect(submitCma(API_BASE, AGENT_ID, makeRequest())).rejects.toThrow();
    expect(consoleSpy).not.toHaveBeenCalled();
  });

  it("handles response.text() rejection gracefully", async () => {
    const badResponse = new Response(null, { status: 500 });
    vi.spyOn(badResponse, "text").mockRejectedValue(new Error("read failed"));
    vi.spyOn(globalThis, "fetch").mockResolvedValue(badResponse);
    const consoleSpy = vi.spyOn(console, "error").mockImplementation(() => {});

    await expect(submitCma(API_BASE, AGENT_ID, makeRequest())).rejects.toThrow(
      "CMA submission failed (500)",
    );
    expect(consoleSpy).not.toHaveBeenCalled();
  });

  it("uses apiBaseUrl parameter (not process.env)", async () => {
    const customBase = "http://localhost:9999";
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(JSON.stringify({ jobId: "j1", status: "processing" }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );

    await submitCma(customBase, AGENT_ID, makeRequest());

    const [url] = vi.mocked(fetch).mock.calls[0];
    expect(url).toBe(`${customBase}/agents/${AGENT_ID}/cma`);
  });
});
