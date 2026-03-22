import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { trackEvent } from "../src/track-event";
import { EventType } from "../src/event-types";

describe("trackEvent", () => {
  let fetchSpy: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    fetchSpy = vi.fn().mockResolvedValue(new Response(null, { status: 200 }));
    vi.stubGlobal("fetch", fetchSpy);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("POSTs to the /telemetry endpoint with correct URL", async () => {
    await trackEvent("https://api.example.com", EventType.Viewed, "agent-123");

    expect(fetchSpy).toHaveBeenCalledOnce();
    expect(fetchSpy).toHaveBeenCalledWith(
      "https://api.example.com/telemetry",
      expect.objectContaining({ method: "POST" })
    );
  });

  it("sends JSON content-type header", async () => {
    await trackEvent("https://api.example.com", EventType.Submitted, "agent-abc");

    const [, options] = fetchSpy.mock.calls[0];
    expect(options.headers).toEqual({ "Content-Type": "application/json" });
  });

  it("sends event, agentId in the body", async () => {
    await trackEvent("https://api.example.com", EventType.Succeeded, "agent-xyz");

    const [, options] = fetchSpy.mock.calls[0];
    const body = JSON.parse(options.body);
    expect(body.event).toBe("Succeeded");
    expect(body.agentId).toBe("agent-xyz");
  });

  it("includes errorType in the body when provided", async () => {
    await trackEvent(
      "https://api.example.com",
      EventType.Failed,
      "agent-xyz",
      "validation_error"
    );

    const [, options] = fetchSpy.mock.calls[0];
    const body = JSON.parse(options.body);
    expect(body.errorType).toBe("validation_error");
  });

  it("omits errorType from body when not provided", async () => {
    await trackEvent("https://api.example.com", EventType.Viewed, "agent-xyz");

    const [, options] = fetchSpy.mock.calls[0];
    const body = JSON.parse(options.body);
    // errorType should be undefined (not present or explicitly undefined)
    expect(body.errorType).toBeUndefined();
  });

  it("resolves silently when fetch throws (fire-and-forget)", async () => {
    fetchSpy.mockRejectedValue(new Error("Network error"));

    // Must not throw
    await expect(
      trackEvent("https://api.example.com", EventType.Viewed, "agent-123")
    ).resolves.toBeUndefined();
  });

  it("resolves silently when fetch returns an error status", async () => {
    fetchSpy.mockResolvedValue(new Response(null, { status: 500 }));

    await expect(
      trackEvent("https://api.example.com", EventType.Viewed, "agent-123")
    ).resolves.toBeUndefined();
  });
});
