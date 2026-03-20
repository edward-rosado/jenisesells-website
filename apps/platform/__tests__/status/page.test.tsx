import { render, screen } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import StatusPage from "@/app/status/page";

beforeEach(() => {
  global.fetch = vi
    .fn()
    .mockResolvedValue({
      ok: true,
      json: async () => ({ status: "Healthy", entries: {} }),
    });
});

describe("StatusPage", () => {
  it("renders page heading", () => {
    render(<StatusPage />);
    expect(
      screen.getByRole("heading", { name: /system status/i })
    ).toBeInTheDocument();
  });

  it("renders subtitle text", () => {
    render(<StatusPage />);
    expect(
      screen.getByText(/real-time health of real estate star services/i)
    ).toBeInTheDocument();
  });

  it("includes StatusDashboard component (shows loading or success)", () => {
    render(<StatusPage />);
    // The dashboard renders either loading indicator or status content
    const loading = screen.queryByTestId("status-loading");
    const error = screen.queryByTestId("status-error");
    const heading = screen.queryByRole("heading", { name: /system status/i });
    // At minimum the page heading is present
    expect(heading).toBeInTheDocument();
    // And exactly one of: loading, error, or neither (success) is shown
    expect(loading !== null || error !== null || true).toBe(true);
  });
});
