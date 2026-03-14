/**
 * @vitest-environment jsdom
 */
import { vi, describe, it, expect, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { AGENT, AGENT_MINIMAL } from "../components/fixtures";

const mockLoadAgentConfig = vi.fn();
const mockLoadLegalContent = vi.fn();
const mockNotFound = vi.fn(() => { throw new Error("NOT_FOUND"); });
const mockCaptureException = vi.fn();

vi.mock("@/lib/config", () => ({
  loadAgentConfig: (...args: unknown[]) => mockLoadAgentConfig(...args),
  loadLegalContent: (...args: unknown[]) => mockLoadLegalContent(...args),
}));
vi.mock("next/navigation", () => ({ notFound: () => mockNotFound() }));
vi.mock("@sentry/nextjs", () => ({ captureException: (...args: unknown[]) => mockCaptureException(...args) }));

import AccessibilityPage from "@/app/accessibility/page";

describe("AccessibilityPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockLoadAgentConfig.mockResolvedValue(AGENT);
    mockLoadLegalContent.mockResolvedValue({ above: undefined, below: undefined });
  });

  it("renders Accessibility Statement heading", async () => {
    const page = await AccessibilityPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    expect(screen.getByRole("heading", { level: 1, name: /Accessibility Statement/i })).toBeInTheDocument();
  });

  it("mentions WCAG 2.1 Level AA", async () => {
    const page = await AccessibilityPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    expect(screen.getByText(/WCAG\) 2\.1 Level AA/)).toBeInTheDocument();
  });

  it("displays agent contact email in page content", async () => {
    const page = await AccessibilityPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    const emailLinks = screen.getAllByText(/jane@example\.com/);
    // At least one is in the page content (not just Nav)
    expect(emailLinks.length).toBeGreaterThanOrEqual(2);
  });

  it("includes known limitations section", async () => {
    const page = await AccessibilityPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    expect(screen.getByRole("heading", { name: /Known Limitations/i })).toBeInTheDocument();
  });

  it("includes effective date", async () => {
    const page = await AccessibilityPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    // Effective date appears in mixed content (bold + text), use a function matcher
    const main = screen.getByRole("main");
    expect(main.textContent).toContain("March 13, 2026");
  });

  it("calls notFound() when agent config fails", async () => {
    mockLoadAgentConfig.mockRejectedValue(new Error("not found"));
    await expect(
      AccessibilityPage({ searchParams: Promise.resolve({ agentId: "bad" }) })
    ).rejects.toThrow("NOT_FOUND");
    expect(mockCaptureException).toHaveBeenCalled();
    expect(mockNotFound).toHaveBeenCalled();
  });

  it("renders with AGENT_MINIMAL", async () => {
    mockLoadAgentConfig.mockResolvedValue(AGENT_MINIMAL);
    const page = await AccessibilityPage({ searchParams: Promise.resolve({ agentId: "minimal" }) });
    render(page);
    expect(screen.getByRole("heading", { level: 1, name: /Accessibility Statement/i })).toBeInTheDocument();
    const main = screen.getByRole("main");
    expect(main.textContent).toContain("Bob Jones");
  });
});
