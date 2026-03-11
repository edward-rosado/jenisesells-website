/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useCmaSubmit } from "@/lib/useCmaSubmit";
import type { CmaSubmitRequest, CmaStatusUpdate } from "@/lib/cma-api";

// --- Mock cma-api ---
const mockSubmitCmaRequest = vi.fn();
const mockConnectToProgress = vi.fn();

vi.mock("@/lib/cma-api", () => ({
  submitCmaRequest: (...args: unknown[]) => mockSubmitCmaRequest(...args),
  connectToProgress: (...args: unknown[]) => mockConnectToProgress(...args),
}));

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

describe("useCmaSubmit", () => {
  const mockDispose = vi.fn();

  beforeEach(() => {
    mockSubmitCmaRequest.mockReset();
    mockConnectToProgress.mockReset();
    mockDispose.mockReset();
    mockConnectToProgress.mockReturnValue(mockDispose);
  });

  it("starts in idle phase", () => {
    const { result } = renderHook(() => useCmaSubmit());
    expect(result.current.state.phase).toBe("idle");
    expect(result.current.state.statusUpdate).toBeNull();
    expect(result.current.state.errorMessage).toBeNull();
  });

  it("transitions to submitting phase on submit", async () => {
    // Make submitCmaRequest hang so we can observe the submitting phase
    let resolveSubmit!: (value: { jobId: string; status: string }) => void;
    mockSubmitCmaRequest.mockReturnValueOnce(
      new Promise((resolve) => { resolveSubmit = resolve; }),
    );

    const { result } = renderHook(() => useCmaSubmit());

    act(() => {
      result.current.submit("agent-1", SAMPLE_REQUEST);
    });

    expect(result.current.state.phase).toBe("submitting");

    // Clean up
    await act(async () => {
      resolveSubmit({ jobId: "j1", status: "processing" });
    });
  });

  it("transitions to tracking phase after successful submit", async () => {
    mockSubmitCmaRequest.mockResolvedValueOnce({ jobId: "j1", status: "processing" });

    const { result } = renderHook(() => useCmaSubmit());

    await act(async () => {
      await result.current.submit("agent-1", SAMPLE_REQUEST);
    });

    expect(result.current.state.phase).toBe("tracking");
    expect(mockConnectToProgress).toHaveBeenCalledWith("j1", expect.any(Function), expect.any(Function));
  });

  it("transitions to error phase when submit fails", async () => {
    mockSubmitCmaRequest.mockRejectedValueOnce(new Error("Network failure"));

    const { result } = renderHook(() => useCmaSubmit());

    await act(async () => {
      await result.current.submit("agent-1", SAMPLE_REQUEST);
    });

    expect(result.current.state.phase).toBe("error");
    expect(result.current.state.errorMessage).toBe("Network failure");
  });

  it("transitions to error with fallback message for non-Error rejections", async () => {
    mockSubmitCmaRequest.mockRejectedValueOnce("string error");

    const { result } = renderHook(() => useCmaSubmit());

    await act(async () => {
      await result.current.submit("agent-1", SAMPLE_REQUEST);
    });

    expect(result.current.state.phase).toBe("error");
    expect(result.current.state.errorMessage).toBe("Submission failed");
  });

  it("updates state when SignalR sends a progress update", async () => {
    mockSubmitCmaRequest.mockResolvedValueOnce({ jobId: "j1", status: "processing" });

    const { result } = renderHook(() => useCmaSubmit());

    await act(async () => {
      await result.current.submit("agent-1", SAMPLE_REQUEST);
    });

    // Get the onUpdate callback passed to connectToProgress
    const onUpdate = mockConnectToProgress.mock.calls[0][1] as (u: CmaStatusUpdate) => void;

    act(() => {
      onUpdate({
        status: "SearchingComps",
        step: 2,
        totalSteps: 9,
        message: "Searching MLS databases...",
      });
    });

    expect(result.current.state.phase).toBe("tracking");
    expect(result.current.state.statusUpdate).toEqual({
      status: "SearchingComps",
      step: 2,
      totalSteps: 9,
      message: "Searching MLS databases...",
    });
  });

  it("transitions to complete phase when status is Complete", async () => {
    mockSubmitCmaRequest.mockResolvedValueOnce({ jobId: "j1", status: "processing" });

    const { result } = renderHook(() => useCmaSubmit());

    await act(async () => {
      await result.current.submit("agent-1", SAMPLE_REQUEST);
    });

    const onUpdate = mockConnectToProgress.mock.calls[0][1] as (u: CmaStatusUpdate) => void;

    act(() => {
      onUpdate({
        status: "Complete",
        step: 9,
        totalSteps: 9,
        message: "Your report has been sent to your email!",
      });
    });

    expect(result.current.state.phase).toBe("complete");
    expect(mockDispose).toHaveBeenCalled();
  });

  it("transitions to error phase when status is Failed", async () => {
    mockSubmitCmaRequest.mockResolvedValueOnce({ jobId: "j1", status: "processing" });

    const { result } = renderHook(() => useCmaSubmit());

    await act(async () => {
      await result.current.submit("agent-1", SAMPLE_REQUEST);
    });

    const onUpdate = mockConnectToProgress.mock.calls[0][1] as (u: CmaStatusUpdate) => void;

    act(() => {
      onUpdate({
        status: "Failed",
        step: 3,
        totalSteps: 9,
        message: "An error occurred",
        errorMessage: "Pipeline failed",
      });
    });

    expect(result.current.state.phase).toBe("error");
    expect(result.current.state.errorMessage).toBe("Pipeline failed");
    expect(mockDispose).toHaveBeenCalled();
  });

  it("uses fallback error message when Failed status has no errorMessage", async () => {
    mockSubmitCmaRequest.mockResolvedValueOnce({ jobId: "j1", status: "processing" });

    const { result } = renderHook(() => useCmaSubmit());

    await act(async () => {
      await result.current.submit("agent-1", SAMPLE_REQUEST);
    });

    const onUpdate = mockConnectToProgress.mock.calls[0][1] as (u: CmaStatusUpdate) => void;

    act(() => {
      onUpdate({
        status: "Failed",
        step: 3,
        totalSteps: 9,
        message: "An error occurred",
      });
    });

    expect(result.current.state.errorMessage).toBe("Report generation failed");
  });

  it("transitions to error when SignalR connection fails", async () => {
    mockSubmitCmaRequest.mockResolvedValueOnce({ jobId: "j1", status: "processing" });

    const { result } = renderHook(() => useCmaSubmit());

    await act(async () => {
      await result.current.submit("agent-1", SAMPLE_REQUEST);
    });

    const onError = mockConnectToProgress.mock.calls[0][2] as (e: Error) => void;

    act(() => {
      onError(new Error("WebSocket closed"));
    });

    expect(result.current.state.phase).toBe("error");
    expect(result.current.state.errorMessage).toBe("WebSocket closed");
  });

  it("reset returns to idle and disposes connection", async () => {
    mockSubmitCmaRequest.mockResolvedValueOnce({ jobId: "j1", status: "processing" });

    const { result } = renderHook(() => useCmaSubmit());

    await act(async () => {
      await result.current.submit("agent-1", SAMPLE_REQUEST);
    });

    act(() => {
      result.current.reset();
    });

    expect(result.current.state.phase).toBe("idle");
    expect(result.current.state.statusUpdate).toBeNull();
    expect(result.current.state.errorMessage).toBeNull();
    expect(mockDispose).toHaveBeenCalled();
  });

  it("cleans up previous connection when submitting again", async () => {
    mockSubmitCmaRequest.mockResolvedValue({ jobId: "j1", status: "processing" });
    const dispose1 = vi.fn();
    const dispose2 = vi.fn();
    mockConnectToProgress.mockReturnValueOnce(dispose1).mockReturnValueOnce(dispose2);

    const { result } = renderHook(() => useCmaSubmit());

    await act(async () => {
      await result.current.submit("agent-1", SAMPLE_REQUEST);
    });

    await act(async () => {
      await result.current.submit("agent-1", SAMPLE_REQUEST);
    });

    expect(dispose1).toHaveBeenCalled();
  });

  it("cleans up connection on unmount", async () => {
    mockSubmitCmaRequest.mockResolvedValueOnce({ jobId: "j1", status: "processing" });

    const { result, unmount } = renderHook(() => useCmaSubmit());

    await act(async () => {
      await result.current.submit("agent-1", SAMPLE_REQUEST);
    });

    unmount();
    expect(mockDispose).toHaveBeenCalled();
  });
});
