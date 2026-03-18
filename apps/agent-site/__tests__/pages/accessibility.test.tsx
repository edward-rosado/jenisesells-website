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
vi.mock("next/navigation", () => ({ notFound: () => mockNotFound(), usePathname: () => "/accessibility", useSearchParams: () => new URLSearchParams() }));
vi.mock("@sentry/nextjs", () => ({ captureException: (...args: unknown[]) => mockCaptureException(...args) }));

import AccessibilityPage, { generateMetadata } from "@/app/accessibility/page";

describe("generateMetadata (accessibility)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockLoadAccountConfig.mockReturnValue(ACCOUNT);
    mockLoadNavConfig.mockReturnValue({ navigation: undefined, enabledSections: new Set() });
  });

  it("returns title with agent name when config loads", async () => {
    const meta = await generateMetadata({ searchParams: Promise.resolve({ accountId: "test" }) });
    expect(meta.title).toBe("Accessibility | Jane Smith");
  });

  it("returns fallback title when config fails", async () => {
    mockLoadAccountConfig.mockImplementation(() => { throw new Error("fail"); });
    const meta = await generateMetadata({ searchParams: Promise.resolve({ accountId: "bad" }) });
    expect(meta.title).toBe("Accessibility");
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
});

describe("AccessibilityPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockLoadAccountConfig.mockReturnValue(ACCOUNT);
    mockLoadNavConfig.mockReturnValue({ navigation: undefined, enabledSections: new Set() });
    mockLoadLegalContent.mockReturnValue({ above: undefined, below: undefined });
  });

  it("renders Accessibility Statement heading", async () => {
    const page = await AccessibilityPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    expect(screen.getByRole("heading", { level: 1, name: /Accessibility Statement/i })).toBeInTheDocument();
  });

  it("mentions WCAG 2.1 Level AA", async () => {
    const page = await AccessibilityPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    expect(screen.getByText(/WCAG\) 2\.1 Level AA/)).toBeInTheDocument();
  });

  it("displays agent contact email in page content", async () => {
    const page = await AccessibilityPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    const emailLinks = screen.getAllByText(/jane@example\.com/);
    // At least one is in the page content (not just Nav)
    expect(emailLinks.length).toBeGreaterThanOrEqual(2);
  });

  it("includes known limitations section", async () => {
    const page = await AccessibilityPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    expect(screen.getByRole("heading", { name: /Known Limitations/i })).toBeInTheDocument();
  });

  it("includes effective date", async () => {
    const page = await AccessibilityPage({ searchParams: Promise.resolve({ accountId: "test" }) });
    render(page);
    // Effective date appears in mixed content (bold + text), use a function matcher
    const main = screen.getByRole("main");
    expect(main.textContent).toContain("March 13, 2026");
  });

  it("calls notFound() when agent config fails", async () => {
    mockLoadAccountConfig.mockImplementation(() => { throw new Error("not found"); });
    await expect(
      AccessibilityPage({ searchParams: Promise.resolve({ accountId: "bad" }) })
    ).rejects.toThrow("NOT_FOUND");
    expect(mockCaptureException).toHaveBeenCalled();
    expect(mockNotFound).toHaveBeenCalled();
  });

  it("renders with ACCOUNT_MINIMAL", async () => {
    mockLoadAccountConfig.mockReturnValue(ACCOUNT_MINIMAL);
    const page = await AccessibilityPage({ searchParams: Promise.resolve({ accountId: "minimal" }) });
    render(page);
    expect(screen.getByRole("heading", { level: 1, name: /Accessibility Statement/i })).toBeInTheDocument();
    const main = screen.getByRole("main");
    expect(main.textContent).toContain("Bob Jones");
  });

  it("falls back to broker name when agent is absent", async () => {
    mockLoadAccountConfig.mockReturnValue(ACCOUNT_BROKER_ONLY);
    const page = await AccessibilityPage({ searchParams: Promise.resolve({ accountId: "broker-only" }) });
    render(page);
    const main = screen.getByRole("main");
    expect(main.textContent).toContain("Sam Broker");
  });

  it("falls back to brokerage name when neither agent nor broker is defined", async () => {
    mockLoadAccountConfig.mockReturnValue(ACCOUNT_BROKERAGE_ONLY);
    const page = await AccessibilityPage({ searchParams: Promise.resolve({ accountId: "brokerage-only" }) });
    render(page);
    const main = screen.getByRole("main");
    expect(main.textContent).toContain("Brokerage LLC");
  });

  it("uses broker name in generateMetadata when agent is absent", async () => {
    mockLoadAccountConfig.mockReturnValue(ACCOUNT_BROKER_ONLY);
    const meta = await generateMetadata({ searchParams: Promise.resolve({ accountId: "broker-only" }) });
    expect(meta.title).toBe("Accessibility | Sam Broker");
  });

  it("uses brokerage name in generateMetadata when neither agent nor broker", async () => {
    mockLoadAccountConfig.mockReturnValue(ACCOUNT_BROKERAGE_ONLY);
    const meta = await generateMetadata({ searchParams: Promise.resolve({ accountId: "brokerage-only" }) });
    expect(meta.title).toBe("Accessibility | Brokerage LLC");
  });
});
