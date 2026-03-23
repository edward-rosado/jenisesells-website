import { render, act } from "@testing-library/react";
import { describe, it, expect, vi, afterEach } from "vitest";

vi.mock("@sentry/nextjs", () => ({
  captureException: vi.fn(),
}));

vi.mock("@real-estate-star/analytics", () => ({
  setErrorReporter: vi.fn(),
  reportError: vi.fn(),
}));

describe("SentryInit", () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it("calls setErrorReporter on mount", async () => {
    const { setErrorReporter } = await import("@real-estate-star/analytics");
    const { SentryInit } = await import("../SentryInit");

    await act(async () => {
      render(<SentryInit />);
    });

    expect(setErrorReporter).toHaveBeenCalledOnce();
  });

  it("wires the reporter to Sentry.captureException", async () => {
    const analytics = await import("@real-estate-star/analytics");
    const sentry = await import("@sentry/nextjs");
    const { SentryInit } = await import("../SentryInit");

    // Capture the reporter function passed to setErrorReporter
    let capturedReporter: ((error: unknown, context?: Record<string, string>) => void) | undefined;
    vi.mocked(analytics.setErrorReporter).mockImplementation((fn) => {
      capturedReporter = fn;
    });

    await act(async () => {
      render(<SentryInit />);
    });

    expect(capturedReporter).toBeDefined();

    // Invoke it and verify Sentry is called
    const err = new Error("test");
    capturedReporter!(err, { key: "val" });
    expect(sentry.captureException).toHaveBeenCalledWith(err, { extra: { key: "val" } });
  });

  it("renders nothing visible", async () => {
    const { SentryInit } = await import("../SentryInit");
    const { container } = render(<SentryInit />);
    expect(container).toBeEmptyDOMElement();
  });
});
