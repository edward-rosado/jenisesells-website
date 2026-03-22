/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { LightLuxury } from "@/features/templates/light-luxury";
import { ACCOUNT, CONTENT, CONTENT_ALL_DISABLED, ACCOUNT_BROKER_ONLY, ACCOUNT_BROKERAGE_ONLY, AGENT_PROP, CONTENT_WITH_MARQUEE } from "../../../__tests__/fixtures";

vi.mock("next/script", () => ({
  __esModule: true,
  default: ({ id, src }: { id?: string; src: string }) => (
    <script data-testid={id} data-src={src} />
  ),
}));

vi.mock("@/features/shared/useParallax", () => ({ useParallax: vi.fn() }));
vi.mock("@/features/shared/useScrollReveal", () => ({ useScrollReveal: vi.fn(() => true) }));
vi.mock("@/features/shared/useReducedMotion", () => ({ useReducedMotion: vi.fn(() => false) }));

describe("LightLuxury template", () => {
  it("always renders the Nav", () => {
    render(<LightLuxury account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByRole("navigation", { name: "Main navigation" })).toBeInTheDocument();
  });

  it("always renders the Footer", () => {
    render(<LightLuxury account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByRole("contentinfo")).toBeInTheDocument();
  });

  it("renders Hero section when enabled", () => {
    render(<LightLuxury account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
  });

  it("does not render Hero when disabled", () => {
    render(<LightLuxury account={ACCOUNT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { level: 1 })).not.toBeInTheDocument();
  });

  it("renders all sections when all enabled", () => {
    render(<LightLuxury account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
    expect(screen.getByText("150+")).toBeInTheDocument();
    expect(screen.getByText("Market Analysis")).toBeInTheDocument();
    expect(screen.getByText("Submit Info")).toBeInTheDocument();
    expect(screen.getByText("$750,000")).toBeInTheDocument();
    expect(screen.getByText("Amazing service!")).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 2, name: "Jane Smith" })).toBeInTheDocument();
  });

  it("does not render disabled sections", () => {
    render(<LightLuxury account={ACCOUNT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { level: 1 })).not.toBeInTheDocument();
    expect(screen.queryByText("Homes Sold")).not.toBeInTheDocument();
  });

  it("uses agent prop identity when provided", () => {
    render(<LightLuxury account={ACCOUNT} content={CONTENT} agent={AGENT_PROP} />);
    expect(screen.getByRole("heading", { level: 2, name: "Explicit Agent" })).toBeInTheDocument();
  });

  it("falls back to broker name when no agent", () => {
    render(<LightLuxury account={ACCOUNT_BROKER_ONLY} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 2, name: "Sam Broker" })).toBeInTheDocument();
  });

  it("falls back to brokerage name when no agent or broker", () => {
    render(<LightLuxury account={ACCOUNT_BROKERAGE_ONLY} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 2, name: "Brokerage LLC" })).toBeInTheDocument();
  });

  it("renders MarqueeBanner when marquee is enabled with items", () => {
    render(<LightLuxury account={ACCOUNT} content={CONTENT_WITH_MARQUEE} />);
    expect(screen.getAllByText("LUXURY HOMES MAGAZINE").length).toBeGreaterThanOrEqual(1);
  });
});
