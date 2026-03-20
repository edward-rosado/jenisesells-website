import { render, screen, waitFor } from "@testing-library/react";
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

const mockWithWorkers = {
  status: "Healthy",
  entries: {
    "claude-api": { status: "Healthy", duration: "00:00:00.234" },
    background_workers: {
      status: "Healthy",
      description: "All workers active or idle",
      duration: "00:00:00.001",
      data: {
        "LeadProcessingWorker.queueDepth": 0,
        "LeadProcessingWorker.lastActivity": "2026-03-20T12:00:00Z",
        "CmaProcessingWorker.queueDepth": 2,
        "CmaProcessingWorker.lastActivity": "2026-03-20T12:00:00Z",
        "HomeSearchProcessingWorker.queueDepth": 0,
        "HomeSearchProcessingWorker.lastActivity": "never",
      },
    },
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

const mockUnhealthyWorkers = {
  status: "Unhealthy",
  entries: {
    background_workers: {
      status: "Unhealthy",
      description: "Stuck workers: LeadProcessingWorker: 5 queued, never active",
      duration: "00:00:00.001",
      data: {
        "LeadProcessingWorker.queueDepth": 5,
        "LeadProcessingWorker.lastActivity": "never",
        "CmaProcessingWorker.queueDepth": 0,
        "CmaProcessingWorker.lastActivity": "never",
        "HomeSearchProcessingWorker.queueDepth": 0,
        "HomeSearchProcessingWorker.lastActivity": "never",
      },
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

  it("renders uptime tracker with session history", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => mockHealthy,
    });
    render(<StatusDashboard />);
    await waitFor(() => {
      expect(screen.getByText("1/1 checks OK")).toBeInTheDocument();
    });
    expect(screen.getByTestId("uptime-tracker")).toBeInTheDocument();
  });

  it("separates core services from background workers", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => mockWithWorkers,
    });
    render(<StatusDashboard />);
    await waitFor(() => {
      expect(screen.getByText("Core Services")).toBeInTheDocument();
    });
    expect(screen.getByText("Background Processing")).toBeInTheDocument();
    expect(screen.getByTestId("status-claude-api")).toBeInTheDocument();
    expect(screen.getByTestId("status-background_workers")).toBeInTheDocument();
  });

  it("renders worker details with queue depth and last activity", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => mockWithWorkers,
    });
    render(<StatusDashboard />);
    await waitFor(() => {
      expect(screen.getByTestId("worker-Lead")).toBeInTheDocument();
    });
    expect(screen.getByTestId("worker-Cma")).toBeInTheDocument();
    expect(screen.getByTestId("worker-HomeSearch")).toBeInTheDocument();
  });

  it("shows queue depth with yellow highlight when non-zero", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => mockWithWorkers,
    });
    render(<StatusDashboard />);
    await waitFor(() => {
      expect(screen.getByTestId("worker-Cma")).toBeInTheDocument();
    });
    // Cma has queueDepth 2 — should have yellow text
    const cmaWorker = screen.getByTestId("worker-Cma");
    const yellowSpan = cmaWorker.querySelector(".text-yellow-400");
    expect(yellowSpan).not.toBeNull();
    expect(yellowSpan!.textContent).toBe("2");
  });

  it("shows Never for workers that have no activity", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => mockWithWorkers,
    });
    render(<StatusDashboard />);
    await waitFor(() => {
      expect(screen.getByTestId("worker-HomeSearch")).toBeInTheDocument();
    });
    const hsWorker = screen.getByTestId("worker-HomeSearch");
    expect(hsWorker.textContent).toContain("Never");
  });

  it("renders unhealthy workers with stuck description", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => mockUnhealthyWorkers,
    });
    render(<StatusDashboard />);
    await waitFor(() => {
      expect(screen.getByText("Service Disruption")).toBeInTheDocument();
    });
    expect(
      screen.getByText(/Stuck workers: LeadProcessingWorker/)
    ).toBeInTheDocument();
  });
});
