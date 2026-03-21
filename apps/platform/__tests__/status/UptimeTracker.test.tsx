import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { UptimeTracker } from "@/app/status/UptimeTracker";
import type { UptimeSample } from "@/app/status/useHealthCheck";

function makeSample(
  status: UptimeSample["status"],
  minutesAgo = 0
): UptimeSample {
  return {
    time: new Date(Date.now() - minutesAgo * 60_000),
    status,
  };
}

describe("UptimeTracker", () => {
  it("renders waiting message when no samples", () => {
    render(<UptimeTracker samples={[]} />);
    expect(screen.getByText("Waiting for data…")).toBeInTheDocument();
  });

  it("renders check count for samples", () => {
    const samples = [makeSample("Healthy"), makeSample("Healthy")];
    render(<UptimeTracker samples={samples} />);
    expect(screen.getByText("2/2 checks OK")).toBeInTheDocument();
  });

  it("counts only Healthy samples in OK count", () => {
    const samples = [
      makeSample("Healthy"),
      makeSample("Unhealthy"),
      makeSample("Healthy"),
    ];
    render(<UptimeTracker samples={samples} />);
    expect(screen.getByText("2/3 checks OK")).toBeInTheDocument();
  });

  it("renders correct number of bars including padding", () => {
    const samples = [makeSample("Healthy")];
    const { container } = render(
      <UptimeTracker samples={samples} maxBars={5} />
    );
    const tracker = container.querySelector('[data-testid="uptime-tracker"]');
    const bars = tracker!.querySelectorAll('[class*="flex-1"]');
    expect(bars).toHaveLength(5); // 4 padding + 1 sample
  });

  it("renders all bars as samples when full", () => {
    const samples = Array.from({ length: 5 }, (_, i) =>
      makeSample("Healthy", i)
    );
    const { container } = render(
      <UptimeTracker samples={samples} maxBars={5} />
    );
    const tracker = container.querySelector('[data-testid="uptime-tracker"]');
    const bars = tracker!.querySelectorAll('[class*="flex-1"]');
    // All 5 should be sample bars (emerald), no padding (gray-800)
    const sampleBars = tracker!.querySelectorAll('[class*="bg-emerald-500"]');
    expect(sampleBars).toHaveLength(5);
    expect(bars).toHaveLength(5);
  });

  it("uses red for unhealthy and error samples", () => {
    const samples = [makeSample("Unhealthy"), makeSample("Error")];
    const { container } = render(
      <UptimeTracker samples={samples} maxBars={2} />
    );
    const tracker = container.querySelector('[data-testid="uptime-tracker"]');
    expect(tracker!.querySelectorAll('[class*="bg-red-500"]')).toHaveLength(1);
    expect(tracker!.querySelectorAll('[class*="bg-red-700"]')).toHaveLength(1);
  });

  it("uses yellow for degraded samples", () => {
    const samples = [makeSample("Degraded")];
    const { container } = render(
      <UptimeTracker samples={samples} maxBars={1} />
    );
    const tracker = container.querySelector('[data-testid="uptime-tracker"]');
    expect(
      tracker!.querySelectorAll('[class*="bg-yellow-500"]')
    ).toHaveLength(1);
  });

  it("includes tooltip with time and status", () => {
    const samples = [makeSample("Healthy")];
    const { container } = render(
      <UptimeTracker samples={samples} maxBars={1} />
    );
    const bar = container.querySelector('[class*="bg-emerald-500"]');
    expect(bar!.getAttribute("title")).toContain("Healthy");
  });

  it("has accessible role and label", () => {
    render(<UptimeTracker samples={[]} />);
    expect(
      screen.getByRole("img", { name: /uptime tracker/i })
    ).toBeInTheDocument();
  });
});
