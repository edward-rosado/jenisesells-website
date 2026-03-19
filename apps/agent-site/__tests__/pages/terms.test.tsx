/**
 * @vitest-environment jsdom
 */
import { vi, describe, it, expect, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { ACCOUNT, ACCOUNT_MINIMAL, ACCOUNT_BROKER_ONLY, ACCOUNT_BROKERAGE_ONLY } from "../components/fixtures";

const mockLoadAccountConfig = vi.fn();
const mockLoadLegalContent = vi.fn();
const mockLoadNavConfig = vi.fn();
const mockNotFound = vi.fn(() => { throw new Error("NOT_FOUND"); });
const mockCaptureException = vi.fn();

vi.mock("@/lib/config", () => ({
  loadAccountConfig: (...args: unknown[]) => mockLoadAccountConfig(...args),
  loadLegalContent: (...args: unknown[]) => mockLoadLegalContent(...args),
}));
vi.mock("@/lib/nav-config", () => ({
  loadNavConfig: (...args: unknown[]) => mockLoadNavConfig(...args),
}));
vi.mock("next/navigation", () => ({ notFound: () => mockNotFound(), usePathname: () => "/terms", useSearchParams: () => new URLSearchParams() }));
vi.mock("@sentry/nextjs", () => ({ captureException: (...args: unknown[]) => mockCaptureException(...args) }));

import TermsPage, { generateMetadata } from "@/app/terms/page";

beforeEach(() => {
  vi.clearAllMocks();
  mockLoadAccountConfig.mockReturnValue(ACCOUNT);
  mockLoadNavConfig.mockReturnValue({ navigation: undefined, enabledSections: new Set() });
  mockLoadLegalContent.mockReturnValue({ above: undefined, below: undefined });
});

describe("generateMetadata (terms)", () => {
  it("returns title with agent name when config loads", async () => {
    const meta = await generateMetadata({ searchParams: Promise.resolve({ accountId: "test" }) });
    expect(meta.title).toBe("Terms of Use | Jane Smith");
  });

  it("returns fallback title when config fails", async () => {
    mockLoadAccountConfig.mockImplementation(() => { throw new Error("fail"); });
    const meta = await generateMetadata({ searchParams: Promise.resolve({ accountId: "bad" }) });
    expect(meta.title).toBe("Terms of Use");
  });

  it("uses DEFAULT_AGENT_ID env var when accountId is absent", async () => {
    process.env.DEFAULT_AGENT_ID = "env-agent";
    await generateMetadata({ searchParams: Promise.resolve({}) });
    expect(mockLoadAccountConfig).toHaveBeenCalledWith("env-agent");
    delete process.env.DEFAULT_AGENT_ID;
  });

  it("falls back to jenise-buckalew when no accountId or env var", async () => {
    delete process.env.DEFAULT_AGENT_ID;
    await generateMetadata({ searchParams: Promise.resolve({}) });
    expect(mockLoadAccountConfig).toHaveBeenCalledWith("jenise-buckalew");
  });

  it("falls back to DEFAULT_AGENT_ID when accountId contains unsafe characters", async () => {
    process.env.DEFAULT_AGENT_ID = "env-agent";
    await generateMetadata({ searchParams: Promise.resolve({ accountId: "../../etc/passwd" }) });
    expect(mockLoadAccountConfig).toHaveBeenCalledWith("env-agent");
    delete process.env.DEFAULT_AGENT_ID;
  });

  it("falls back to jenise-buckalew when accountId is unsafe and no DEFAULT_AGENT_ID", async () => {
    delete process.env.DEFAULT_AGENT_ID;
    await generateMetadata({ searchParams: Promise.resolve({ accountId: "../../etc/passwd" }) });
    expect(mockLoadAccountConfig).toHaveBeenCalledWith("jenise-buckalew");
  });
});

describe("TermsPage", () => {
  it("renders Terms of Use heading", async () => {
    const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    expect(screen.getByRole("heading", { level: 1, name: /Terms of Use/i })).toBeInTheDocument();
  });

  it("displays agent name", async () => {
    const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    const matches = screen.getAllByText(/Jane Smith/);
    expect(matches.length).toBeGreaterThanOrEqual(1);
  });

  it("displays agent email", async () => {
    const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    const matches = screen.getAllByText(/jane@example\.com/);
    expect(matches.length).toBeGreaterThanOrEqual(1);
  });

  it("includes CMA disclaimer text", async () => {
    const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    const matches = screen.getAllByText(/Comparative Market Analysis/);
    expect(matches.length).toBeGreaterThanOrEqual(1);
  });

  it("includes fair housing commitment", async () => {
    const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    expect(screen.getByRole("heading", { name: /Fair Housing Commitment/i })).toBeInTheDocument();
  });

  it("includes NJ Fair Housing section for NJ agents", async () => {
    const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    expect(screen.getByRole("heading", { name: /New Jersey Fair Housing/i })).toBeInTheDocument();
  });

  it("includes NJ LAD protected classes for NJ agents", async () => {
    const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    const matches = screen.getAllByText(/gender identity or expression/);
    expect(matches.length).toBeGreaterThanOrEqual(1);
  });

  it("includes NJ Real Estate Commission section for NJ agents", async () => {
    const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    expect(screen.getByRole("heading", { name: /NJ Real Estate Commission/i })).toBeInTheDocument();
  });

  it("uses full state name — New Jersey not NJ", async () => {
    const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    const matches = screen.getAllByText(/New Jersey/);
    expect(matches.length).toBeGreaterThanOrEqual(1);
  });

  it("includes effective date", async () => {
    const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    const matches = screen.getAllByText(/March 13, 2026/);
    expect(matches.length).toBeGreaterThanOrEqual(1);
  });

  it("renders custom below content when provided", async () => {
    mockLoadLegalContent.mockReturnValue({ above: undefined, below: "## Custom Footer Content" });
    const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    expect(screen.getByText(/Custom Footer Content/)).toBeInTheDocument();
  });

  it("calls notFound() when agent config fails", async () => {
    mockLoadAccountConfig.mockImplementation(() => { throw new Error("Config not found"); });
    await expect(
      TermsPage({ searchParams: Promise.resolve({ accountId: "bad-agent" }) })
    ).rejects.toThrow("NOT_FOUND");
    expect(mockCaptureException).toHaveBeenCalled();
    expect(mockNotFound).toHaveBeenCalled();
  });

  it("renders with ACCOUNT_MINIMAL — covers absent license_id and brokerage branches", async () => {
    mockLoadAccountConfig.mockReturnValue(ACCOUNT_MINIMAL);
    const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "minimal" }) });
    render(page);
    expect(screen.getByRole("heading", { level: 1, name: /Terms of Use/i })).toBeInTheDocument();
    const matches = screen.getAllByText(/Bob Jones/);
    expect(matches.length).toBeGreaterThanOrEqual(1);
  });

  it("displays license_id when present", async () => {
    const agentWithLicense = {
      ...ACCOUNT,
      agent: { ...ACCOUNT.agent!, license_number: "12345" },
    };
    mockLoadAccountConfig.mockReturnValue(agentWithLicense);
    const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    const matches = screen.getAllByText(/12345/);
    expect(matches.length).toBeGreaterThanOrEqual(1);
  });

  it("renders NJ content without brokerage when absent", async () => {
    const njNoBrokerage = {
      ...ACCOUNT,
      brokerage: { ...ACCOUNT.brokerage, name: "" },
    };
    mockLoadAccountConfig.mockReturnValue(njNoBrokerage);
    const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    expect(screen.getByText(/New Jersey Real Estate Commission/)).toBeInTheDocument();
    expect(screen.queryByText(/Brokerage:/)).not.toBeInTheDocument();
  });

  it("displays brokerage_id when present", async () => {
    const agentWithBrokerageId = {
      ...ACCOUNT,
      brokerage: { ...ACCOUNT.brokerage, license_number: "BRK-99999" },
    };
    mockLoadAccountConfig.mockReturnValue(agentWithBrokerageId);
    const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    const matches = screen.getAllByText(/BRK-99999/);
    expect(matches.length).toBeGreaterThanOrEqual(1);
  });

  it("renders NJ content without brokerage license when license_number is empty", async () => {
    const njNoBrokerageLicense = {
      ...ACCOUNT,
      brokerage: { ...ACCOUNT.brokerage, license_number: "" },
    };
    mockLoadAccountConfig.mockReturnValue(njNoBrokerageLicense);
    const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    expect(screen.getByText(/NJ Real Estate Commission/)).toBeInTheDocument();
    // brokerage_id is falsy → no "(License #...)" text
    expect(screen.queryByText(/\(License #/)).not.toBeInTheDocument();
  });

  describe("non-NJ state (dynamic state content)", () => {
    it("shows generic state-specific notice for non-NJ agents", async () => {
      mockLoadAccountConfig.mockReturnValue(ACCOUNT_MINIMAL);
      const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "minimal" }) });
      render(page);
      expect(screen.getByRole("heading", { name: /State-Specific Notices \(Texas\)/i })).toBeInTheDocument();
    });

    it("shows generic placeholder text for non-NJ agents", async () => {
      mockLoadAccountConfig.mockReturnValue(ACCOUNT_MINIMAL);
      const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "minimal" }) });
      render(page);
      expect(screen.getByText(/Texas real estate laws and regulations apply/)).toBeInTheDocument();
    });

    it("does not show NJ Fair Housing section for non-NJ agents", async () => {
      mockLoadAccountConfig.mockReturnValue(ACCOUNT_MINIMAL);
      const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "minimal" }) });
      render(page);
      expect(screen.queryByRole("heading", { name: /New Jersey Fair Housing/i })).not.toBeInTheDocument();
    });

    it("does not show NJ Real Estate Commission section for non-NJ agents", async () => {
      mockLoadAccountConfig.mockReturnValue(ACCOUNT_MINIMAL);
      const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "minimal" }) });
      render(page);
      expect(screen.queryByRole("heading", { name: /NJ Real Estate Commission/i })).not.toBeInTheDocument();
    });

    it("does not include NJ LAD reference for non-NJ agents", async () => {
      mockLoadAccountConfig.mockReturnValue(ACCOUNT_MINIMAL);
      const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "minimal" }) });
      render(page);
      expect(screen.queryByText(/New Jersey Law Against Discrimination/)).not.toBeInTheDocument();
    });
  });

  describe("broker/brokerage name fallback", () => {
    it("falls back to broker name when agent is absent", async () => {
      mockLoadAccountConfig.mockReturnValue(ACCOUNT_BROKER_ONLY);
      const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "broker-only" }) });
      render(page);
      const main = screen.getByRole("main");
      expect(main.textContent).toContain("Sam Broker");
    });

    it("falls back to brokerage name when neither agent nor broker is defined", async () => {
      mockLoadAccountConfig.mockReturnValue(ACCOUNT_BROKERAGE_ONLY);
      const page = await TermsPage({ searchParams: Promise.resolve({ accountId: "brokerage-only" }) });
      render(page);
      const main = screen.getByRole("main");
      expect(main.textContent).toContain("Brokerage LLC");
    });

    it("uses broker name in generateMetadata when agent is absent", async () => {
      mockLoadAccountConfig.mockReturnValue(ACCOUNT_BROKER_ONLY);
      const meta = await generateMetadata({ searchParams: Promise.resolve({ accountId: "broker-only" }) });
      expect(meta.title).toBe("Terms of Use | Sam Broker");
    });

    it("uses brokerage name in generateMetadata when neither agent nor broker", async () => {
      mockLoadAccountConfig.mockReturnValue(ACCOUNT_BROKERAGE_ONLY);
      const meta = await generateMetadata({ searchParams: Promise.resolve({ accountId: "brokerage-only" }) });
      expect(meta.title).toBe("Terms of Use | Brokerage LLC");
    });
  });
});
