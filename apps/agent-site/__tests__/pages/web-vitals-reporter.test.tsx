/**
 * @vitest-environment jsdom
 */
import { vi, describe, it, expect, beforeEach } from "vitest";
import { render } from "@testing-library/react";

const mockInitWebVitals = vi.fn();

vi.mock("@real-estate-star/analytics", () => ({
  initWebVitals: (...args: unknown[]) => mockInitWebVitals(...args),
  reportError: vi.fn(),
}));

import { WebVitalsReporter } from "@/app/WebVitalsReporter";

describe("WebVitalsReporter", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders nothing (returns null)", () => {
    const { container } = render(<WebVitalsReporter />);
    expect(container.firstChild).toBeNull();
  });

  it("calls initWebVitals on mount", () => {
    render(<WebVitalsReporter />);
    expect(mockInitWebVitals).toHaveBeenCalledTimes(1);
  });

  it("passes apiUrl from NEXT_PUBLIC_API_URL env var", () => {
    const ORIGINAL_ENV = process.env;
    process.env = { ...ORIGINAL_ENV, NEXT_PUBLIC_API_URL: "https://api.example.com" };
    render(<WebVitalsReporter />);
    expect(mockInitWebVitals).toHaveBeenCalledWith("https://api.example.com", "");
    process.env = ORIGINAL_ENV;
  });

  it("passes empty string for apiUrl when env var is absent", () => {
    const ORIGINAL_ENV = process.env;
    process.env = { ...ORIGINAL_ENV };
    delete process.env.NEXT_PUBLIC_API_URL;
    render(<WebVitalsReporter />);
    expect(mockInitWebVitals).toHaveBeenCalledWith("", "");
    process.env = ORIGINAL_ENV;
  });
});
