import { render, screen, waitFor, act } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { StatusDashboard } from "@/features/status/StatusDashboard";

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
      json: async () => ({
        status: "Unhealthy",
        entries: {
          "scraper-api": { status: "Unhealthy", duration: "00:00:00.000", description: "Timeout" },
        },
      }),
    });
    render(<StatusDashboard />);
    await waitFor(() => {
      expect(screen.getByText("Service Disruption")).toBeInTheDocument();
    });
    expect(screen.getByTestId("status-scraper-api")).toHaveAttribute("data-status", "Unhealthy");
  });

  it("renders raw duration when format does not match HH:MM:SS.ms", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        status: "Healthy",
        entries: {
          "test-service": { status: "Healthy", duration: "not-a-duration" },
        },
      }),
    });
    render(<StatusDashboard />);
    await waitFor(() => {
      expect(screen.getByText("not-a-duration")).toBeInTheDocument();
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

  it("formats lastActivity as seconds ago when under 60s", async () => {
    const now = Date.now();
    vi.spyOn(Date, "now").mockReturnValue(now);
    const mockRecentActivity = {
      status: "Healthy",
      entries: {
        background_workers: {
          status: "Healthy",
          description: "All workers active or idle",
          duration: "00:00:00.001",
          data: {
            "LeadProcessingWorker.queueDepth": 0,
            "LeadProcessingWorker.lastActivity": new Date(now - 15_000).toISOString(),
            "CmaProcessingWorker.queueDepth": 0,
            "CmaProcessingWorker.lastActivity": new Date(now - 120_000).toISOString(),
            "HomeSearchProcessingWorker.queueDepth": 0,
            "HomeSearchProcessingWorker.lastActivity": new Date(now - 7_200_000).toISOString(),
          },
        },
      },
    };
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => mockRecentActivity,
    });
    render(<StatusDashboard />);
    await waitFor(() => {
      expect(screen.getByTestId("worker-Lead")).toBeInTheDocument();
    });
    // 15s ago → "15s ago"
    expect(screen.getByTestId("worker-Lead").textContent).toContain("15s ago");
    // 120s ago → "2m ago"
    expect(screen.getByTestId("worker-Cma").textContent).toContain("2m ago");
    // 7200s ago → "2h ago"
    expect(screen.getByTestId("worker-HomeSearch").textContent).toContain("2h ago");
    vi.restoreAllMocks();
  });

  it("shows raw value when lastActivity is not a date or 'never'", async () => {
    const mockNonDateActivity = {
      status: "Healthy",
      entries: {
        background_workers: {
          status: "Healthy",
          description: "All workers active or idle",
          duration: "00:00:00.001",
          data: {
            "LeadProcessingWorker.queueDepth": 0,
            "LeadProcessingWorker.lastActivity": "not-a-date",
            "CmaProcessingWorker.queueDepth": 0,
            "CmaProcessingWorker.lastActivity": undefined,
            "HomeSearchProcessingWorker.queueDepth": 0,
            "HomeSearchProcessingWorker.lastActivity": "never",
          },
        },
      },
    };
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => mockNonDateActivity,
    });
    render(<StatusDashboard />);
    await waitFor(() => {
      expect(screen.getByTestId("worker-Lead")).toBeInTheDocument();
    });
    // Invalid date string should be shown as-is
    const leadWorker = screen.getByTestId("worker-Lead");
    expect(leadWorker.textContent).toContain("not-a-date");
    // Undefined should become "undefined"
    const cmaWorker = screen.getByTestId("worker-Cma");
    expect(cmaWorker.textContent).toContain("undefined");
  });

  it("renders workers with no data property (fallback to empty object)", async () => {
    const mockNoData = {
      status: "Healthy",
      entries: {
        background_workers: {
          status: "Healthy",
          description: "Idle",
          duration: "00:00:00.001",
        },
      },
    };
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => mockNoData,
    });
    render(<StatusDashboard />);
    await waitFor(() => {
      expect(screen.getByTestId("worker-Lead")).toBeInTheDocument();
    });
    // All queue depths should be 0 (fallback)
    const leadWorker = screen.getByTestId("worker-Lead");
    expect(leadWorker.textContent).toContain("Queue:");
  });

  it("passes through unparseable duration strings", async () => {
    const mockBadDuration = {
      status: "Healthy",
      entries: {
        "claude-api": { status: "Healthy", duration: "fast" },
      },
    };
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => mockBadDuration,
    });
    render(<StatusDashboard />);
    await waitFor(() => {
      expect(screen.getByTestId("status-claude-api")).toBeInTheDocument();
    });
    expect(screen.getByText("fast")).toBeInTheDocument();
  });

  it("renders nothing when current is null after loading completes", async () => {
    // Mock the hook to return the defensive edge case
    const useHealthCheckModule = await import("@/features/status/useHealthCheck");
    const spy = vi.spyOn(useHealthCheckModule, "useHealthCheck").mockReturnValue({
      current: null,
      error: null,
      loading: false,
      history: [],
    });
    const { container } = render(<StatusDashboard />);
    // The component should render null (empty container)
    expect(container.firstChild).toBeNull();
    spy.mockRestore();
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

  it("shows error when a non-Error object is thrown during fetch", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockRejectedValueOnce("string-error");
    render(<StatusDashboard />);
    await waitFor(() => {
      expect(screen.getByText("Unable to reach API")).toBeInTheDocument();
    });
  });

  it("cancels interval fetch after component unmounts (exercises cancelled guard)", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    (fetch as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({ ok: true, json: async () => mockHealthy });

    const { unmount } = render(<StatusDashboard />);
    await waitFor(() => {
      expect(screen.getByText("All Systems Operational")).toBeInTheDocument();
    });

    unmount();
    // Advance past the 30s interval — wrappedFetch fires but cancelled=true, returns early
    await act(async () => {
      await vi.advanceTimersByTimeAsync(30_000);
    });
    // fetch should NOT have been called a second time
    expect(fetch as ReturnType<typeof vi.fn>).toHaveBeenCalledTimes(1);
  });
});
