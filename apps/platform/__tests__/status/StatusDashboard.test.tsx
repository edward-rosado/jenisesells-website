import { render, screen, waitFor, act } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { StatusDashboard } from "@/app/status/StatusDashboard";

const mockHealthy = {
  status: "Healthy",
  entries: {
    "claude-api": { status: "Healthy", duration: "00:00:00.234" },
    "gws-cli": { status: "Healthy", duration: "00:00:00.012" },
    "google-drive": { status: "Healthy", duration: "00:00:00.456" },
    "scraper-api": { status: "Healthy", duration: "00:00:00.789" },
    turnstile: { status: "Healthy", duration: "00:00:00.100" },
  },
};

const mockDegraded = {
  status: "Degraded",
  entries: {
    "claude-api": { status: "Healthy", duration: "00:00:00.234" },
    "gws-cli": {
      status: "Degraded",
      duration: "00:00:05.000",
      description: "gws CLI not found",
    },
  },
};

beforeEach(() => {
  global.fetch = vi.fn();
});

afterEach(() => {
  vi.useRealTimers();
});

describe("StatusDashboard", () => {
  it("renders all healthy services with green indicators", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => mockHealthy,
    });
    render(<StatusDashboard />);
    await waitFor(() => {
      expect(screen.getByText("All Systems Operational")).toBeInTheDocument();
    });
    expect(screen.getByTestId("status-claude-api")).toHaveAttribute(
      "data-status",
      "Healthy"
    );
  });

  it("renders degraded status with yellow indicator and description", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => mockDegraded,
    });
    render(<StatusDashboard />);
    await waitFor(() => {
      expect(screen.getByText("Degraded Performance")).toBeInTheDocument();
    });
    expect(screen.getByText("gws CLI not found")).toBeInTheDocument();
  });

  it("renders error state when API is unreachable", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockRejectedValueOnce(
      new Error("Network error")
    );
    render(<StatusDashboard />);
    await waitFor(() => {
      expect(screen.getByText("Unable to reach API")).toBeInTheDocument();
    });
  });

  it("shows loading state initially", () => {
    (fetch as ReturnType<typeof vi.fn>).mockReturnValueOnce(
      new Promise(() => {})
    );
    render(<StatusDashboard />);
    expect(screen.getByTestId("status-loading")).toBeInTheDocument();
  });

  it("displays response time for each service", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => mockHealthy,
    });
    render(<StatusDashboard />);
    await waitFor(() => {
      expect(screen.getByText("234ms")).toBeInTheDocument();
    });
  });

  it("auto-refreshes every 30 seconds", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    (fetch as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({ ok: true, json: async () => mockHealthy })
      .mockResolvedValueOnce({ ok: true, json: async () => mockDegraded });

    render(<StatusDashboard />);

    await waitFor(() => {
      expect(screen.getByText("All Systems Operational")).toBeInTheDocument();
    });

    await vi.advanceTimersByTimeAsync(30_000);

    await waitFor(() => {
      expect(screen.getByText("Degraded Performance")).toBeInTheDocument();
    });
  });

  it("renders error state when API returns non-ok status", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: false,
      status: 503,
    });
    render(<StatusDashboard />);
    await waitFor(() => {
      expect(screen.getByTestId("status-error")).toBeInTheDocument();
    });
  });

  it("renders service disruption for Unhealthy overall status", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ status: "Unhealthy", entries: {} }),
    });
    render(<StatusDashboard />);
    await waitFor(() => {
      expect(screen.getByText("Service Disruption")).toBeInTheDocument();
    });
  });
});
