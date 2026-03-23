import { render, act } from "@testing-library/react";
import { describe, it, expect, vi, afterEach } from "vitest";

vi.mock("@real-estate-star/analytics", () => ({
  initWebVitals: vi.fn(),
}));

describe("WebVitalsReporter", () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it("calls initWebVitals on mount", async () => {
    const { initWebVitals } = await import("@real-estate-star/analytics");
    const { WebVitalsReporter } = await import("../WebVitalsReporter");

    await act(async () => {
      render(<WebVitalsReporter />);
    });

    expect(initWebVitals).toHaveBeenCalledOnce();
    expect(initWebVitals).toHaveBeenCalledWith(expect.any(String), "platform");
  });

  it("renders nothing visible", async () => {
    const { WebVitalsReporter } = await import("../WebVitalsReporter");
    const { container } = render(<WebVitalsReporter />);
    expect(container).toBeEmptyDOMElement();
  });
});
