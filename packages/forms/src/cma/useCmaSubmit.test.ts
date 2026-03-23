/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useCmaSubmit } from "./useCmaSubmit";
import * as cmaApi from "./cma-api";
import type { LeadFormData } from "@real-estate-star/domain";

const API_BASE = "https://api.real-estate-star.com";
const AGENT_ID = "jenise-buckalew";

function makeLeadData(): LeadFormData {
  return {
    leadTypes: ["selling"],
    firstName: "Jane",
    lastName: "Doe",
    email: "jane@example.com",
    phone: "555-0100",
    seller: {
      address: "123 Main St",
      city: "Newark",
      state: "NJ",
      zip: "07101",
    },
    timeline: "asap",
  };
}

describe("useCmaSubmit", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("starts in idle state", () => {
    const { result } = renderHook(() => useCmaSubmit(API_BASE));

    expect(result.current.state).toEqual({
      phase: "idle",
      jobId: null,
      errorMessage: null,
    });
  });

  it("transitions idle → submitting → submitted on success and returns true", async () => {
    vi.spyOn(cmaApi, "submitCma").mockResolvedValue({
      jobId: "job-123",
      status: "processing",
    });

    const { result } = renderHook(() => useCmaSubmit(API_BASE));
    let success: boolean = false;

    await act(async () => {
      success = await result.current.submit(AGENT_ID, makeLeadData());
    });

    expect(success).toBe(true);
    expect(result.current.state).toEqual({
      phase: "submitted",
      jobId: "job-123",
      errorMessage: null,
    });
  });

  it("transitions idle → submitting → error on failure and returns false", async () => {
    vi.spyOn(cmaApi, "submitCma").mockRejectedValue(
      new Error("CMA submission failed (500)"),
    );

    const { result } = renderHook(() => useCmaSubmit(API_BASE));
    let success: boolean = true;

    await act(async () => {
      success = await result.current.submit(AGENT_ID, makeLeadData());
    });

    expect(success).toBe(false);
    expect(result.current.state).toEqual({
      phase: "error",
      jobId: null,
      errorMessage: "CMA submission failed (500)",
    });
  });

  it("calls onError callback when submission fails", async () => {
    const error = new Error("Network failure");
    vi.spyOn(cmaApi, "submitCma").mockRejectedValue(error);
    const onError = vi.fn();

    const { result } = renderHook(() =>
      useCmaSubmit(API_BASE, { onError }),
    );

    await act(async () => {
      await result.current.submit(AGENT_ID, makeLeadData());
    });

    expect(onError).toHaveBeenCalledWith(error);
  });

  it("handles non-Error thrown values", async () => {
    vi.spyOn(cmaApi, "submitCma").mockRejectedValue("string error");
    const onError = vi.fn();

    const { result } = renderHook(() =>
      useCmaSubmit(API_BASE, { onError }),
    );

    await act(async () => {
      await result.current.submit(AGENT_ID, makeLeadData());
    });

    expect(result.current.state.errorMessage).toBe("Submission failed");
    expect(onError).toHaveBeenCalledWith(expect.any(Error));
  });

  it("reset returns to idle state", async () => {
    vi.spyOn(cmaApi, "submitCma").mockResolvedValue({
      jobId: "job-123",
      status: "processing",
    });

    const { result } = renderHook(() => useCmaSubmit(API_BASE));

    await act(async () => {
      await result.current.submit(AGENT_ID, makeLeadData());
    });
    expect(result.current.state.phase).toBe("submitted");

    act(() => {
      result.current.reset();
    });

    expect(result.current.state).toEqual({
      phase: "idle",
      jobId: null,
      errorMessage: null,
    });
  });

  it("passes apiBaseUrl to submitCma", async () => {
    const spy = vi.spyOn(cmaApi, "submitCma").mockResolvedValue({
      jobId: "j1",
      status: "processing",
    });

    const { result } = renderHook(() =>
      useCmaSubmit("http://localhost:5135"),
    );

    await act(async () => {
      await result.current.submit(AGENT_ID, makeLeadData());
    });

    expect(spy).toHaveBeenCalledWith(
      "http://localhost:5135",
      AGENT_ID,
      expect.objectContaining({ firstName: "Jane" }),
    );
  });

  it("maps LeadFormData to CmaSubmitRequest before submitting", async () => {
    const spy = vi.spyOn(cmaApi, "submitCma").mockResolvedValue({
      jobId: "j1",
      status: "processing",
    });

    const { result } = renderHook(() => useCmaSubmit(API_BASE));

    await act(async () => {
      await result.current.submit(AGENT_ID, makeLeadData());
    });

    const [, , request] = spy.mock.calls[0];
    expect(request).toEqual({
      firstName: "Jane",
      lastName: "Doe",
      email: "jane@example.com",
      phone: "555-0100",
      address: "123 Main St",
      city: "Newark",
      state: "NJ",
      zip: "07101",
      timeline: "asap",
      beds: undefined,
      baths: undefined,
      sqft: undefined,
      notes: undefined,
    });
  });
});
