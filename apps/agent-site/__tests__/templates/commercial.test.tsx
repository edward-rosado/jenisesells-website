/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { Commercial } from "@/templates/commercial";
import { ACCOUNT, ACCOUNT_BROKER_ONLY, ACCOUNT_BROKERAGE_ONLY, AGENT_PROP, CONTENT, CONTENT_ALL_DISABLED, CONTENT_WITH_MARQUEE } from "../components/fixtures";

vi.mock("next/script", () => ({
  __esModule: true,
  default: ({ id, src }: { id?: string; src: string }) => (
    <script data-testid={id} data-src={src} />
  ),
}));

vi.mock("@/hooks/useParallax", () => ({ useParallax: vi.fn() }));
vi.mock("@/hooks/useScrollReveal", () => ({ useScrollReveal: vi.fn(() => true) }));
vi.mock("@/hooks/useReducedMotion", () => ({ useReducedMotion: vi.fn(() => false) }));

describe("Commercial template", () => {
  it("always renders the Nav", () => {
    render(<Commercial account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByRole("navigation", { name: "Main navigation" })).toBeInTheDocument();
  });

  it("always renders the Footer", () => {
    render(<Commercial account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByRole("contentinfo")).toBeInTheDocument();
  });

  it("renders Hero section when enabled", () => {
    render(<Commercial account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
  });

  it("does not render Hero when disabled", () => {
    render(<Commercial account={ACCOUNT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { level: 1 })).not.toBeInTheDocument();
  });

  it("renders all sections when all enabled", () => {
    render(<Commercial account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
    expect(screen.getByText("150+")).toBeInTheDocument();
    expect(screen.getByText("Market Analysis")).toBeInTheDocument();
    expect(screen.getByText("Submit Info")).toBeInTheDocument();
    expect(screen.getByText("$750,000")).toBeInTheDocument();
    expect(screen.getByText("Amazing service!")).toBeInTheDocument();
    expect(screen.getByText(/About Jane Smith/)).toBeInTheDocument();
  });

  it("does not render disabled sections", () => {
    render(<Commercial account={ACCOUNT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { level: 1 })).not.toBeInTheDocument();
    expect(screen.queryByText("Homes Sold")).not.toBeInTheDocument();
  });

  it("uses agent prop identity when provided", () => {
    render(<Commercial account={ACCOUNT} content={CONTENT} agent={AGENT_PROP} />);
    expect(screen.getByRole("heading", { name: /About Explicit Agent/ })).toBeInTheDocument();
  });

  it("falls back to broker name when no agent", () => {
    render(<Commercial account={ACCOUNT_BROKER_ONLY} content={CONTENT} />);
    expect(screen.getByRole("heading", { name: /About Sam Broker/ })).toBeInTheDocument();
  });

  it("falls back to brokerage name when no agent or broker", () => {
    render(<Commercial account={ACCOUNT_BROKERAGE_ONLY} content={CONTENT} />);
    expect(screen.getByRole("heading", { name: /About Brokerage LLC/ })).toBeInTheDocument();
  });

  it("renders MarqueeBanner when marquee is enabled with items", () => {
    render(<Commercial account={ACCOUNT} content={CONTENT_WITH_MARQUEE} />);
    expect(screen.getAllByText("LUXURY HOMES MAGAZINE").length).toBeGreaterThanOrEqual(1);
  });
});
