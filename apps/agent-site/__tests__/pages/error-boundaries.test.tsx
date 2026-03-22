/**
 * @vitest-environment jsdom
 */
import { vi, describe, it, expect, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";

const mockReportError = vi.fn();

vi.mock("@real-estate-star/analytics", () => ({
  reportError: (...args: unknown[]) => mockReportError(...args),
  initWebVitals: vi.fn(),
}));

import ErrorBoundary from "@/app/error";
import GlobalError from "@/app/global-error";

describe("Error boundary (error.tsx)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  const testError = Object.assign(new globalThis.Error("Page blew up"));
  const mockReset = vi.fn();

  it("renders the error message and try-again button", () => {
    render(<ErrorBoundary error={testError} reset={mockReset} />);
    expect(screen.getByRole("alert")).toBeInTheDocument();
    expect(screen.getByText(/something went wrong/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /try again/i })).toBeInTheDocument();
  });

  it("calls reset when try-again is clicked", () => {
    render(<ErrorBoundary error={testError} reset={mockReset} />);
    fireEvent.click(screen.getByRole("button", { name: /try again/i }));
    expect(mockReset).toHaveBeenCalled();
  });

  it("calls reportError on mount with the error", () => {
    render(<ErrorBoundary error={testError} reset={mockReset} />);
    expect(mockReportError).toHaveBeenCalledWith(testError, { context: "page-error-boundary" });
  });

  it("calls reportError again when error changes", () => {
    const { rerender } = render(<ErrorBoundary error={testError} reset={mockReset} />);
    const newError = Object.assign(new globalThis.Error("Another error"));
    rerender(<ErrorBoundary error={newError} reset={mockReset} />);
    expect(mockReportError).toHaveBeenCalledTimes(2);
    expect(mockReportError).toHaveBeenLastCalledWith(newError, { context: "page-error-boundary" });
  });
});

describe("GlobalError boundary (global-error.tsx)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  const testError = Object.assign(new globalThis.Error("Global meltdown"));
  const mockReset = vi.fn();

  it("renders the error message and try-again button", () => {
    render(<GlobalError error={testError} reset={mockReset} />);
    expect(screen.getByRole("alert")).toBeInTheDocument();
    expect(screen.getByText(/something went wrong/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /try again/i })).toBeInTheDocument();
  });

  it("calls reset when try-again is clicked", () => {
    render(<GlobalError error={testError} reset={mockReset} />);
    fireEvent.click(screen.getByRole("button", { name: /try again/i }));
    expect(mockReset).toHaveBeenCalled();
  });

  it("calls reportError on mount with the error", () => {
    render(<GlobalError error={testError} reset={mockReset} />);
    expect(mockReportError).toHaveBeenCalledWith(testError, { context: "global-error-boundary" });
  });

  it("calls reportError again when error changes", () => {
    const { rerender } = render(<GlobalError error={testError} reset={mockReset} />);
    const newError = Object.assign(new globalThis.Error("Another global error"));
    rerender(<GlobalError error={newError} reset={mockReset} />);
    expect(mockReportError).toHaveBeenCalledTimes(2);
    expect(mockReportError).toHaveBeenLastCalledWith(newError, { context: "global-error-boundary" });
  });
});
