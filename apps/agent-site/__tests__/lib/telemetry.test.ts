import { describe, it, expect, vi, beforeEach, afterAll } from "vitest";
import { trackFormEvent } from "../../lib/telemetry";

describe("trackFormEvent", () => {
  const originalEnv = process.env;

  beforeEach(() => {
    vi.restoreAllMocks();
    process.env = { ...originalEnv };
  });

  afterAll(() => {
    process.env = originalEnv;
  });

  it("sends POST to /telemetry with event data", () => {
    process.env.NEXT_PUBLIC_API_URL = "https://api.example.com";
    const fetchSpy = vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response());

    trackFormEvent("form.viewed", "agent-1");

    expect(fetchSpy).toHaveBeenCalledWith(
      "https://api.example.com/telemetry",
      expect.objectContaining({
        method: "POST",
        keepalive: true,
      })
    );
  });

  it("does nothing when API URL is not configured", () => {
    delete process.env.NEXT_PUBLIC_API_URL;
    delete process.env.LEAD_API_URL;
    const fetchSpy = vi.spyOn(globalThis, "fetch");

    trackFormEvent("form.viewed", "agent-1");

    expect(fetchSpy).not.toHaveBeenCalled();
  });

  it("includes errorType when provided", () => {
    process.env.NEXT_PUBLIC_API_URL = "https://api.example.com";
    const fetchSpy = vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response());

    trackFormEvent("form.failed", "agent-1", "timeout");

    const body = JSON.parse((fetchSpy.mock.calls[0][1] as RequestInit).body as string);
    expect(body.errorType).toBe("timeout");
  });

  it("falls back to LEAD_API_URL when NEXT_PUBLIC_API_URL is not set", () => {
    delete process.env.NEXT_PUBLIC_API_URL;
    process.env.LEAD_API_URL = "https://lead.example.com";
    const fetchSpy = vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response());

    trackFormEvent("form.submitted", "agent-2");

    expect(fetchSpy).toHaveBeenCalledWith(
      "https://lead.example.com/telemetry",
      expect.objectContaining({ method: "POST" })
    );
  });

  it("includes event and agentId in request body", () => {
    process.env.NEXT_PUBLIC_API_URL = "https://api.example.com";
    const fetchSpy = vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response());

    trackFormEvent("form.succeeded", "agent-3");

    const body = JSON.parse((fetchSpy.mock.calls[0][1] as RequestInit).body as string);
    expect(body.event).toBe("form.succeeded");
    expect(body.agentId).toBe("agent-3");
  });

  it("does not throw when fetch rejects", async () => {
    process.env.NEXT_PUBLIC_API_URL = "https://api.example.com";
    vi.spyOn(globalThis, "fetch").mockRejectedValue(new Error("network error"));

    expect(() => trackFormEvent("form.viewed", "agent-1")).not.toThrow();
    // Allow the promise to settle — the .catch(() => {}) swallows the rejection
    await new Promise((r) => setTimeout(r, 0));
  });

  it("sends Content-Type application/json header", () => {
    process.env.NEXT_PUBLIC_API_URL = "https://api.example.com";
    const fetchSpy = vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response());

    trackFormEvent("form.started", "agent-1");

    expect(fetchSpy).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({
        headers: { "Content-Type": "application/json" },
      })
    );
  });
});
