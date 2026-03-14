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

import PrivacyPage from "@/app/privacy/page";

describe("PrivacyPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockLoadAgentConfig.mockResolvedValue(AGENT);
    mockLoadLegalContent.mockResolvedValue({ above: undefined, below: undefined });
  });

  it("renders Privacy Policy heading", async () => {
    const page = await PrivacyPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    expect(screen.getByRole("heading", { level: 1, name: /Privacy Policy/i })).toBeInTheDocument();
  });

  it("displays agent name", async () => {
    const page = await PrivacyPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    expect(screen.getAllByText(/Jane Smith/).length).toBeGreaterThanOrEqual(1);
  });

  it("displays agent email", async () => {
    const page = await PrivacyPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    expect(screen.getAllByText(/jane@example\.com/).length).toBeGreaterThanOrEqual(1);
  });

  it("includes CCPA section", async () => {
    const page = await PrivacyPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    expect(screen.getByRole("heading", { name: /CCPA/i })).toBeInTheDocument();
  });

  it("includes effective date", async () => {
    const page = await PrivacyPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    expect(screen.getAllByText(/March 13, 2026/).length).toBeGreaterThanOrEqual(1);
  });

  it("renders custom_above markdown when loadLegalContent returns above content", async () => {
    mockLoadLegalContent.mockResolvedValue({ above: "## Important Notice", below: undefined });
    const page = await PrivacyPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    expect(screen.getByRole("heading", { level: 2, name: "Important Notice" })).toBeInTheDocument();
  });

  it("renders custom_below markdown when provided", async () => {
    mockLoadLegalContent.mockResolvedValue({ above: undefined, below: "## Additional Info" });
    const page = await PrivacyPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    expect(screen.getByRole("heading", { level: 2, name: "Additional Info" })).toBeInTheDocument();
  });

  it("calls notFound() when loadAgentConfig rejects", async () => {
    mockLoadAgentConfig.mockRejectedValue(new Error("not found"));
    await expect(
      PrivacyPage({ searchParams: Promise.resolve({ agentId: "bad" }) })
    ).rejects.toThrow("NOT_FOUND");
    expect(mockCaptureException).toHaveBeenCalled();
    expect(mockNotFound).toHaveBeenCalled();
  });

  it("renders with AGENT_MINIMAL (no brokerage, no service_areas)", async () => {
    mockLoadAgentConfig.mockResolvedValue(AGENT_MINIMAL);
    const page = await PrivacyPage({ searchParams: Promise.resolve({ agentId: "minimal" }) });
    render(page);
    expect(screen.getByRole("heading", { level: 1, name: /Privacy Policy/i })).toBeInTheDocument();
    expect(screen.getAllByText(/Bob Jones/).length).toBeGreaterThanOrEqual(1);
  });

  it("displays brokerage name when present", async () => {
    const page = await PrivacyPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    expect(screen.getAllByText(/Best Homes Realty/).length).toBeGreaterThanOrEqual(1);
  });

  it("displays service areas when present", async () => {
    const page = await PrivacyPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    expect(screen.getAllByText(/Hoboken/).length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText(/Jersey City/).length).toBeGreaterThanOrEqual(1);
  });
});
