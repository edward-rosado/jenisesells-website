/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { LuxuryEstate } from "@/templates/luxury-estate";
import { ACCOUNT, CONTENT, CONTENT_ALL_DISABLED, ACCOUNT_BROKER_ONLY, ACCOUNT_BROKERAGE_ONLY, AGENT_PROP, CONTENT_WITH_MARQUEE } from "../components/fixtures";

vi.mock("next/script", () => ({
  __esModule: true,
  default: ({ id, src }: { id?: string; src: string }) => (
    <script data-testid={id} data-src={src} />
  ),
}));

vi.mock("@/hooks/useParallax", () => ({ useParallax: vi.fn() }));
vi.mock("@/hooks/useScrollReveal", () => ({ useScrollReveal: vi.fn(() => true) }));
vi.mock("@/hooks/useReducedMotion", () => ({ useReducedMotion: vi.fn(() => false) }));

// Mock matchMedia for SoldCarousel's useSyncExternalStore
Object.defineProperty(window, "matchMedia", {
  writable: true,
  value: vi.fn().mockImplementation((query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  })),
});

describe("LuxuryEstate template", () => {
  it("always renders the Nav", () => {
    render(<LuxuryEstate account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByRole("navigation", { name: "Main navigation" })).toBeInTheDocument();
  });

  it("always renders the Footer", () => {
    render(<LuxuryEstate account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByRole("contentinfo")).toBeInTheDocument();
  });

  it("renders Hero section when enabled", () => {
    render(<LuxuryEstate account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
  });

  it("does not render Hero when disabled", () => {
    render(<LuxuryEstate account={ACCOUNT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { level: 1 })).not.toBeInTheDocument();
  });

  it("renders all sections when all enabled", () => {
    render(<LuxuryEstate account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
    expect(screen.getByText("150+")).toBeInTheDocument();
    expect(screen.getByText("Market Analysis")).toBeInTheDocument();
    expect(screen.getByText("Submit Info")).toBeInTheDocument();
    expect(screen.getByText("$750,000")).toBeInTheDocument();
    expect(screen.getByText(/Amazing service!/)).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 2, name: "Jane Smith" })).toBeInTheDocument();
  });

  it("does not render disabled sections", () => {
    render(<LuxuryEstate account={ACCOUNT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { level: 1 })).not.toBeInTheDocument();
    expect(screen.queryByText("Homes Sold")).not.toBeInTheDocument();
  });

  it("uses agent prop identity when provided", () => {
    render(<LuxuryEstate account={ACCOUNT} content={CONTENT} agent={AGENT_PROP} />);
    expect(screen.getByRole("heading", { level: 2, name: "Explicit Agent" })).toBeInTheDocument();
  });

  it("falls back to broker name when no agent", () => {
    render(<LuxuryEstate account={ACCOUNT_BROKER_ONLY} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 2, name: "Sam Broker" })).toBeInTheDocument();
  });

  it("falls back to brokerage name when no agent or broker", () => {
    render(<LuxuryEstate account={ACCOUNT_BROKERAGE_ONLY} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 2, name: "Brokerage LLC" })).toBeInTheDocument();
  });

  it("renders MarqueeBanner when marquee is enabled with items", () => {
    render(<LuxuryEstate account={ACCOUNT} content={CONTENT_WITH_MARQUEE} />);
    expect(screen.getAllByText("LUXURY HOMES MAGAZINE").length).toBeGreaterThanOrEqual(1);
  });
});
