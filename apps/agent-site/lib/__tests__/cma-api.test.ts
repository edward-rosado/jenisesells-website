/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { submitCmaRequest, connectToProgress } from "@/lib/cma-api";
import type { CmaSubmitRequest, CmaStatusUpdate } from "@/lib/cma-api";

// --- Mock @microsoft/signalr ---
const mockOn = vi.fn();
const mockStart = vi.fn();
const mockInvoke = vi.fn();
const mockStop = vi.fn();
const mockBuild = vi.fn();

vi.mock("@microsoft/signalr", () => {
  const mockConnection = {
    on: (...args: unknown[]) => mockOn(...args),
    start: () => mockStart(),
    invoke: (...args: unknown[]) => mockInvoke(...args),
    stop: () => mockStop(),
  };

  class MockHubConnectionBuilder {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() {
      mockBuild();
      return mockConnection;
    }
  }

  return {
    HubConnectionBuilder: MockHubConnectionBuilder,
    LogLevel: { Warning: 3 },
  };
});

const SAMPLE_REQUEST: CmaSubmitRequest = {
  firstName: "Alice",
  lastName: "Test",
  email: "alice@test.com",
  phone: "555-111-2222",
  address: "1 Test St",
  city: "Hoboken",
  state: "NJ",
  zip: "07030",
  timeline: "asap",
};

describe("submitCmaRequest", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("sends POST with JSON body to the correct API endpoint", async () => {
    const mockFetch = vi.mocked(fetch);
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve({ jobId: "abc-123", status: "processing" }),
    } as Response);

    await submitCmaRequest("test-agent", SAMPLE_REQUEST);

    expect(mockFetch).toHaveBeenCalledWith(
      "http://localhost:5135/agents/test-agent/cma",
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify(SAMPLE_REQUEST),
      },
    );
  });

  it("returns the parsed response on success", async () => {
    const mockFetch = vi.mocked(fetch);
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve({ jobId: "abc-123", status: "processing" }),
    } as Response);

    const result = await submitCmaRequest("test-agent", SAMPLE_REQUEST);
    expect(result).toEqual({ jobId: "abc-123", status: "processing" });
  });

  it("encodes agent ID in the URL", async () => {
    const mockFetch = vi.mocked(fetch);
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve({ jobId: "x", status: "processing" }),
    } as Response);

    await submitCmaRequest("agent with spaces", SAMPLE_REQUEST);
    expect(mockFetch).toHaveBeenCalledWith(
      expect.stringContaining("agent%20with%20spaces"),
      expect.any(Object),
    );
  });

  it("throws on non-ok response with status code", async () => {
    const mockFetch = vi.mocked(fetch);
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 422,
      text: () => Promise.resolve("Validation failed"),
    } as Response);

    await expect(submitCmaRequest("test-agent", SAMPLE_REQUEST))
      .rejects.toThrow("CMA submission failed (422)");
  });

  it("throws on non-ok response when body read fails", async () => {
    const mockFetch = vi.mocked(fetch);
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 500,
      text: () => Promise.reject(new Error("read error")),
    } as Response);

    await expect(submitCmaRequest("test-agent", SAMPLE_REQUEST))
      .rejects.toThrow("CMA submission failed (500)");
  });

  it("propagates network errors", async () => {
    const mockFetch = vi.mocked(fetch);
    mockFetch.mockRejectedValueOnce(new Error("Network failure"));

    await expect(submitCmaRequest("test-agent", SAMPLE_REQUEST))
      .rejects.toThrow("Network failure");
  });
});

describe("connectToProgress", () => {
  beforeEach(() => {
    mockOn.mockReset();
    mockStart.mockReset();
    mockInvoke.mockReset();
    mockStop.mockReset();
    mockBuild.mockReset();
    mockStart.mockResolvedValue(undefined);
    mockInvoke.mockResolvedValue(undefined);
    mockStop.mockResolvedValue(undefined);
  });

  it("builds a SignalR connection with the correct hub URL", () => {
    connectToProgress("job-1", vi.fn(), vi.fn());
    expect(mockBuild).toHaveBeenCalled();
  });

  it("registers StatusUpdate listener before connecting", () => {
    connectToProgress("job-1", vi.fn(), vi.fn());
    expect(mockOn).toHaveBeenCalledWith("StatusUpdate", expect.any(Function));
  });

  it("joins the job group after connecting", async () => {
    connectToProgress("job-1", vi.fn(), vi.fn());

    // Wait for start + invoke chain
    await vi.waitFor(() => {
      expect(mockInvoke).toHaveBeenCalledWith("JoinJob", "job-1");
    });
  });

  it("calls onUpdate when StatusUpdate is received", () => {
    const onUpdate = vi.fn();
    connectToProgress("job-1", onUpdate, vi.fn());

    // Get the listener callback that was registered
    const statusCallback = mockOn.mock.calls[0][1] as (update: CmaStatusUpdate) => void;
    const update: CmaStatusUpdate = {
      status: "SearchingComps",
      step: 2,
      totalSteps: 9,
      message: "Searching...",
    };
    statusCallback(update);

    expect(onUpdate).toHaveBeenCalledWith(update);
  });

  it("calls onError when start fails", async () => {
    const onError = vi.fn();
    mockStart.mockRejectedValueOnce(new Error("Connection refused"));

    connectToProgress("job-1", vi.fn(), onError);

    await vi.waitFor(() => {
      expect(onError).toHaveBeenCalledWith(expect.objectContaining({ message: "Connection refused" }));
    });
  });

  it("calls onError with generic message for non-Error failures", async () => {
    const onError = vi.fn();
    mockStart.mockRejectedValueOnce("string error");

    connectToProgress("job-1", vi.fn(), onError);

    await vi.waitFor(() => {
      expect(onError).toHaveBeenCalledWith(
        expect.objectContaining({ message: "Failed to connect to progress hub" }),
      );
    });
  });

  it("returns a dispose function that stops the connection", () => {
    const dispose = connectToProgress("job-1", vi.fn(), vi.fn());
    dispose();
    expect(mockStop).toHaveBeenCalled();
  });

  it("swallows errors when stop fails during cleanup", () => {
    mockStop.mockRejectedValueOnce(new Error("Already stopped"));
    const dispose = connectToProgress("job-1", vi.fn(), vi.fn());
    // Should not throw
    expect(() => dispose()).not.toThrow();
  });
});
