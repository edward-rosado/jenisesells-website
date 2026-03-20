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

vi.mock("@/lib/config", () => ({
  loadAccountConfig: (...args: unknown[]) => mockLoadAccountConfig(...args),
  loadLegalContent: (...args: unknown[]) => mockLoadLegalContent(...args),
}));
vi.mock("@/lib/nav-config", () => ({
  loadNavConfig: (...args: unknown[]) => mockLoadNavConfig(...args),
}));
vi.mock("next/navigation", () => ({ notFound: () => mockNotFound(), usePathname: () => "/privacy", useSearchParams: () => new URLSearchParams() }));
import PrivacyPage, { generateMetadata } from "@/app/privacy/page";

describe("generateMetadata (privacy)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockLoadAccountConfig.mockReturnValue(ACCOUNT);
    mockLoadNavConfig.mockReturnValue({ navigation: undefined, enabledSections: new Set() });
  });

  it("returns title with agent name when config loads", async () => {
    const meta = await generateMetadata({ searchParams: Promise.resolve({ accountId: "test" }) });
    expect(meta.title).toBe("Privacy Policy | Jane Smith");
  });

  it("returns fallback title when config fails", async () => {
    mockLoadAccountConfig.mockImplementation(() => { throw new Error("fail"); });
    const meta = await generateMetadata({ searchParams: Promise.resolve({ accountId: "bad" }) });
    expect(meta.title).toBe("Privacy Policy");
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

describe("PrivacyPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockLoadAccountConfig.mockReturnValue(ACCOUNT);
    mockLoadNavConfig.mockReturnValue({ navigation: undefined, enabledSections: new Set() });
    mockLoadLegalContent.mockReturnValue({ above: undefined, below: undefined });
  });

  it("renders Privacy Policy heading", async () => {
    const page = await PrivacyPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    expect(screen.getByRole("heading", { level: 1, name: /Privacy Policy/i })).toBeInTheDocument();
  });

  it("displays agent name", async () => {
    const page = await PrivacyPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    expect(screen.getAllByText(/Jane Smith/).length).toBeGreaterThanOrEqual(1);
  });

  it("displays agent email", async () => {
    const page = await PrivacyPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    expect(screen.getAllByText(/jane@example\.com/).length).toBeGreaterThanOrEqual(1);
  });

  it("includes CCPA section", async () => {
    const page = await PrivacyPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    expect(screen.getByRole("heading", { name: /CCPA/i })).toBeInTheDocument();
  });

  it("includes effective date", async () => {
    const page = await PrivacyPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    expect(screen.getAllByText(/March 13, 2026/).length).toBeGreaterThanOrEqual(1);
  });

  it("renders custom_above markdown when loadLegalContent returns above content", async () => {
    mockLoadLegalContent.mockReturnValue({ above: "## Important Notice", below: undefined });
    const page = await PrivacyPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    expect(screen.getByRole("heading", { level: 2, name: "Important Notice" })).toBeInTheDocument();
  });

  it("renders custom_below markdown when provided", async () => {
    mockLoadLegalContent.mockReturnValue({ above: undefined, below: "## Additional Info" });
    const page = await PrivacyPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    expect(screen.getByRole("heading", { level: 2, name: "Additional Info" })).toBeInTheDocument();
  });

  it("calls notFound() when loadAccountConfig rejects", async () => {
    mockLoadAccountConfig.mockImplementation(() => { throw new Error("not found"); });
    await expect(
      PrivacyPage({ searchParams: Promise.resolve({ accountId: "bad" }) })
    ).rejects.toThrow("NOT_FOUND");
    expect(mockNotFound).toHaveBeenCalled();
  });

  it("renders with ACCOUNT_MINIMAL (no brokerage, no service_areas)", async () => {
    mockLoadAccountConfig.mockReturnValue(ACCOUNT_MINIMAL);
    const page = await PrivacyPage({ searchParams: Promise.resolve({ accountId: "minimal" }) });
    render(page);
    expect(screen.getByRole("heading", { level: 1, name: /Privacy Policy/i })).toBeInTheDocument();
    expect(screen.getAllByText(/Bob Jones/).length).toBeGreaterThanOrEqual(1);
  });

  it("displays brokerage name when present", async () => {
    const page = await PrivacyPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    expect(screen.getAllByText(/Best Homes Realty/).length).toBeGreaterThanOrEqual(1);
  });

  it("displays service areas when present", async () => {
    const page = await PrivacyPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    expect(screen.getAllByText(/Hoboken/).length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText(/Jersey City/).length).toBeGreaterThanOrEqual(1);
  });

  it("omits brokerage name when brokerage.name is empty", async () => {
    const accountNoBrokerage = {
      ...ACCOUNT,
      brokerage: { ...ACCOUNT.brokerage, name: "" },
    };
    mockLoadAccountConfig.mockReturnValue(accountNoBrokerage);
    const page = await PrivacyPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    // Line 68: should render without " of <brokerage>" phrase
    expect(screen.queryByText(/of Best Homes Realty/)).not.toBeInTheDocument();
    // Line 105: should fall back to "Our affiliated brokerage"
    expect(screen.getByText(/Our affiliated brokerage/)).toBeInTheDocument();
  });

  describe("NJ-specific privacy content", () => {
    it("shows NJ Data Privacy Act reference for NJ agents", async () => {
      const page = await PrivacyPage({ searchParams: Promise.resolve({ accountId: "test" }) });
      render(page);
      expect(screen.getByText(/New Jersey Data Privacy Act/)).toBeInTheDocument();
    });

    it("shows NJ Residents heading for NJ agents", async () => {
      const page = await PrivacyPage({ searchParams: Promise.resolve({ accountId: "test" }) });
      render(page);
      expect(screen.getByRole("heading", { name: /New Jersey Residents/i })).toBeInTheDocument();
    });
  });

  describe("non-NJ state (dynamic state content)", () => {
    it("shows generic state privacy notice for non-NJ agents", async () => {
      mockLoadAccountConfig.mockReturnValue(ACCOUNT_MINIMAL);
      const page = await PrivacyPage({ searchParams: Promise.resolve({ accountId: "minimal" }) });
      render(page);
      expect(screen.getByRole("heading", { name: /Texas Residents/i })).toBeInTheDocument();
    });

    it("shows generic placeholder text for non-NJ agents", async () => {
      mockLoadAccountConfig.mockReturnValue(ACCOUNT_MINIMAL);
      const page = await PrivacyPage({ searchParams: Promise.resolve({ accountId: "minimal" }) });
      render(page);
      expect(screen.getByText(/Texas real estate laws and regulations apply/)).toBeInTheDocument();
    });

    it("does not show NJ Data Privacy Act reference for non-NJ agents", async () => {
      mockLoadAccountConfig.mockReturnValue(ACCOUNT_MINIMAL);
      const page = await PrivacyPage({ searchParams: Promise.resolve({ accountId: "minimal" }) });
      render(page);
      expect(screen.queryByText(/New Jersey Data Privacy Act/)).not.toBeInTheDocument();
    });
  });

  describe("broker/brokerage name fallback", () => {
    it("falls back to broker name when agent is absent", async () => {
      mockLoadAccountConfig.mockReturnValue(ACCOUNT_BROKER_ONLY);
      const page = await PrivacyPage({ searchParams: Promise.resolve({ accountId: "broker-only" }) });
      render(page);
      const main = screen.getByRole("main");
      expect(main.textContent).toContain("Sam Broker");
    });

    it("falls back to brokerage name when neither agent nor broker is defined", async () => {
      mockLoadAccountConfig.mockReturnValue(ACCOUNT_BROKERAGE_ONLY);
      const page = await PrivacyPage({ searchParams: Promise.resolve({ accountId: "brokerage-only" }) });
      render(page);
      const main = screen.getByRole("main");
      expect(main.textContent).toContain("Brokerage LLC");
    });

    it("uses broker name in generateMetadata when agent is absent", async () => {
      mockLoadAccountConfig.mockReturnValue(ACCOUNT_BROKER_ONLY);
      const meta = await generateMetadata({ searchParams: Promise.resolve({ accountId: "broker-only" }) });
      expect(meta.title).toBe("Privacy Policy | Sam Broker");
    });

    it("uses brokerage name in generateMetadata when neither agent nor broker", async () => {
      mockLoadAccountConfig.mockReturnValue(ACCOUNT_BROKERAGE_ONLY);
      const meta = await generateMetadata({ searchParams: Promise.resolve({ accountId: "brokerage-only" }) });
      expect(meta.title).toBe("Privacy Policy | Brokerage LLC");
    });
  });
});
