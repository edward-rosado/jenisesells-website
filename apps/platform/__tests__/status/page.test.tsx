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

  it("includes StatusDashboard component in loading state", () => {
    render(<StatusPage />);
    // Dashboard starts in loading state before fetch resolves
    expect(screen.getByTestId("status-loading")).toBeInTheDocument();
  });
});
