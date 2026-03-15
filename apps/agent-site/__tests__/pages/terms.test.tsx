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

import TermsPage, { generateMetadata } from "@/app/terms/page";

beforeEach(() => {
  vi.clearAllMocks();
  mockLoadAgentConfig.mockReturnValue(AGENT);
  mockLoadLegalContent.mockReturnValue({ above: undefined, below: undefined });
});

describe("generateMetadata (terms)", () => {
  it("returns title with agent name when config loads", async () => {
    const meta = await generateMetadata({ searchParams: Promise.resolve({ agentId: "test" }) });
    expect(meta.title).toBe("Terms of Use | Jane Smith");
  });

  it("returns fallback title when config fails", async () => {
    mockLoadAgentConfig.mockImplementation(() => { throw new Error("fail"); });
    const meta = await generateMetadata({ searchParams: Promise.resolve({ agentId: "bad" }) });
    expect(meta.title).toBe("Terms of Use");
  });

  it("uses DEFAULT_AGENT_ID env var when agentId is absent", async () => {
    process.env.DEFAULT_AGENT_ID = "env-agent";
    await generateMetadata({ searchParams: Promise.resolve({}) });
    expect(mockLoadAgentConfig).toHaveBeenCalledWith("env-agent");
    delete process.env.DEFAULT_AGENT_ID;
  });

  it("falls back to jenise-buckalew when no agentId or env var", async () => {
    delete process.env.DEFAULT_AGENT_ID;
    await generateMetadata({ searchParams: Promise.resolve({}) });
    expect(mockLoadAgentConfig).toHaveBeenCalledWith("jenise-buckalew");
  });
});

describe("TermsPage", () => {
  it("renders Terms of Use heading", async () => {
    const page = await TermsPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    expect(screen.getByRole("heading", { level: 1, name: /Terms of Use/i })).toBeInTheDocument();
  });

  it("displays agent name", async () => {
    const page = await TermsPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    const matches = screen.getAllByText(/Jane Smith/);
    expect(matches.length).toBeGreaterThanOrEqual(1);
  });

  it("displays agent email", async () => {
    const page = await TermsPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    const matches = screen.getAllByText(/jane@example\.com/);
    expect(matches.length).toBeGreaterThanOrEqual(1);
  });

  it("includes CMA disclaimer text", async () => {
    const page = await TermsPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    const matches = screen.getAllByText(/Comparative Market Analysis/);
    expect(matches.length).toBeGreaterThanOrEqual(1);
  });

  it("includes fair housing commitment", async () => {
    const page = await TermsPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    expect(screen.getByRole("heading", { name: /Fair Housing Commitment/i })).toBeInTheDocument();
  });

  it("includes NJ Fair Housing Act reference", async () => {
    const page = await TermsPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    expect(screen.getByRole("heading", { name: /New Jersey Fair Housing/i })).toBeInTheDocument();
  });

  it("includes NJ LAD protected classes", async () => {
    const page = await TermsPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    const content = screen.getByText(/gender identity or expression/);
    expect(content).toBeInTheDocument();
  });

  it("includes licensing information section", async () => {
    const page = await TermsPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    expect(screen.getByRole("heading", { name: /Licensing Information/i })).toBeInTheDocument();
  });

  it("uses full state name — New Jersey not NJ", async () => {
    const page = await TermsPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    const matches = screen.getAllByText(/New Jersey/);
    expect(matches.length).toBeGreaterThanOrEqual(1);
  });

  it("includes effective date", async () => {
    const page = await TermsPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    const matches = screen.getAllByText(/March 13, 2026/);
    expect(matches.length).toBeGreaterThanOrEqual(1);
  });

  it("renders custom below content when provided", async () => {
    mockLoadLegalContent.mockReturnValue({ above: undefined, below: "## Custom Footer Content" });
    const page = await TermsPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    expect(screen.getByText(/Custom Footer Content/)).toBeInTheDocument();
  });

  it("calls notFound() when agent config fails", async () => {
    mockLoadAgentConfig.mockImplementation(() => { throw new Error("Config not found"); });
    await expect(
      TermsPage({ searchParams: Promise.resolve({ agentId: "bad-agent" }) })
    ).rejects.toThrow("NOT_FOUND");
    expect(mockCaptureException).toHaveBeenCalled();
    expect(mockNotFound).toHaveBeenCalled();
  });

  it("renders with AGENT_MINIMAL — covers absent license_id and brokerage branches", async () => {
    mockLoadAgentConfig.mockReturnValue(AGENT_MINIMAL);
    const page = await TermsPage({ searchParams: Promise.resolve({ agentId: "minimal" }) });
    render(page);
    expect(screen.getByRole("heading", { level: 1, name: /Terms of Use/i })).toBeInTheDocument();
    const matches = screen.getAllByText(/Bob Jones/);
    expect(matches.length).toBeGreaterThanOrEqual(1);
  });

  it("displays license_id when present", async () => {
    const agentWithLicense = {
      ...AGENT,
      identity: { ...AGENT.identity, license_id: "12345" },
    };
    mockLoadAgentConfig.mockReturnValue(agentWithLicense);
    const page = await TermsPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    const matches = screen.getAllByText(/12345/);
    expect(matches.length).toBeGreaterThanOrEqual(1);
  });

  it("displays brokerage_id when present", async () => {
    const agentWithBrokerageId = {
      ...AGENT,
      identity: { ...AGENT.identity, brokerage_id: "BRK-99999" },
    };
    mockLoadAgentConfig.mockReturnValue(agentWithBrokerageId);
    const page = await TermsPage({ searchParams: Promise.resolve({ agentId: "test" }) });
    render(page);
    const matches = screen.getAllByText(/BRK-99999/);
    expect(matches.length).toBeGreaterThanOrEqual(1);
  });
});
