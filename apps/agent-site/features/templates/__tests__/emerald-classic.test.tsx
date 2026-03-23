/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { EmeraldClassic } from "@/features/templates/emerald-classic";
import { ACCOUNT, ACCOUNT_BROKER_ONLY, ACCOUNT_BROKERAGE_ONLY, AGENT_PROP, CONTENT, CONTENT_ALL_DISABLED } from "../../../__tests__/fixtures";
import type { ContentConfig } from "@/features/config/types";

vi.mock("@/features/shared/useScrollReveal", () => ({ useScrollReveal: vi.fn(() => true) }));
vi.mock("@/features/shared/useReducedMotion", () => ({ useReducedMotion: vi.fn(() => false) }));

describe("EmeraldClassic template", () => {
  it("always renders the Nav", () => {
    render(<EmeraldClassic account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByRole("navigation", { name: "Main navigation" })).toBeInTheDocument();
  });

  it("always renders the Footer", () => {
    render(<EmeraldClassic account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByRole("contentinfo")).toBeInTheDocument();
  });

  it("renders Hero section when enabled", () => {
    render(<EmeraldClassic account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 1, name: "Sell Your Home Fast" })).toBeInTheDocument();
  });

  it("does not render Hero when disabled", () => {
    render(<EmeraldClassic account={ACCOUNT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { level: 1 })).not.toBeInTheDocument();
  });

  it("renders StatsBar when enabled and items exist", () => {
    render(<EmeraldClassic account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByText("150+")).toBeInTheDocument();
    expect(screen.getByText("Homes Sold")).toBeInTheDocument();
  });

  it("does not render StatsBar when disabled", () => {
    render(<EmeraldClassic account={ACCOUNT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByText("Homes Sold")).not.toBeInTheDocument();
  });

  it("does not render StatsBar when enabled but items is empty", () => {
    const contentEmptyStats: ContentConfig = {
      ...CONTENT,
      pages: {
        ...CONTENT.pages,
        home: {
          ...CONTENT.pages.home,
          sections: {
            ...CONTENT.pages.home.sections,
            stats: { enabled: true, data: { items: [] } },
          },
        },
      },
    };
    render(<EmeraldClassic account={ACCOUNT} content={contentEmptyStats} />);
    expect(screen.queryByText("Homes Sold")).not.toBeInTheDocument();
  });

  it("renders Services section when enabled", () => {
    render(<EmeraldClassic account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 2, name: "What I Do for You" })).toBeInTheDocument();
  });

  it("does not render Services when disabled", () => {
    render(<EmeraldClassic account={ACCOUNT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { name: "What I Do for You" })).not.toBeInTheDocument();
  });

  it("renders HowItWorks when enabled", () => {
    render(<EmeraldClassic account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 2, name: "How It Works" })).toBeInTheDocument();
  });

  it("does not render HowItWorks when disabled", () => {
    render(<EmeraldClassic account={ACCOUNT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { name: "How It Works" })).not.toBeInTheDocument();
  });

  it("renders SoldHomes when enabled and items exist", () => {
    render(<EmeraldClassic account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 2, name: "Recently Sold" })).toBeInTheDocument();
  });

  it("does not render SoldHomes when disabled", () => {
    render(<EmeraldClassic account={ACCOUNT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { name: "Recently Sold" })).not.toBeInTheDocument();
  });

  it("does not render SoldHomes when enabled but items is empty", () => {
    const contentEmptySold: ContentConfig = {
      ...CONTENT,
      pages: {
        ...CONTENT.pages,
        home: {
          ...CONTENT.pages.home,
          sections: {
            ...CONTENT.pages.home.sections,
            gallery: { enabled: true, data: { items: [] } },
          },
        },
      },
    };
    render(<EmeraldClassic account={ACCOUNT} content={contentEmptySold} />);
    expect(screen.queryByRole("heading", { name: "Recently Sold" })).not.toBeInTheDocument();
  });

  it("renders Testimonials when enabled and items exist", () => {
    render(<EmeraldClassic account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 2, name: "What My Clients Say" })).toBeInTheDocument();
  });

  it("does not render Testimonials when disabled", () => {
    render(<EmeraldClassic account={ACCOUNT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { name: "What My Clients Say" })).not.toBeInTheDocument();
  });

  it("does not render Testimonials when enabled but items is empty", () => {
    const contentEmptyTestimonials: ContentConfig = {
      ...CONTENT,
      pages: {
        ...CONTENT.pages,
        home: {
          ...CONTENT.pages.home,
          sections: {
            ...CONTENT.pages.home.sections,
            testimonials: { enabled: true, data: { items: [] } },
          },
        },
      },
    };
    render(<EmeraldClassic account={ACCOUNT} content={contentEmptyTestimonials} />);
    expect(screen.queryByRole("heading", { name: "What My Clients Say" })).not.toBeInTheDocument();
  });

  it("renders CmaSection when enabled", () => {
    render(<EmeraldClassic account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 2, name: "What's Your Home Worth?" })).toBeInTheDocument();
  });

  it("does not render CmaSection when disabled", () => {
    render(<EmeraldClassic account={ACCOUNT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { name: "What's Your Home Worth?" })).not.toBeInTheDocument();
  });

  it("renders About when enabled", () => {
    render(<EmeraldClassic account={ACCOUNT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 2, name: "About Jane Smith" })).toBeInTheDocument();
  });

  it("does not render About when disabled", () => {
    render(<EmeraldClassic account={ACCOUNT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { name: /About/ })).not.toBeInTheDocument();
  });

  it("renders all sections when all enabled", () => {
    render(<EmeraldClassic account={ACCOUNT} content={CONTENT} />);
    // Check that all major section headings are present
    expect(screen.getByRole("heading", { name: "Sell Your Home Fast" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "What I Do for You" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "How It Works" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Recently Sold" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "What My Clients Say" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "What's Your Home Worth?" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "About Jane Smith" })).toBeInTheDocument();
  });

  it("uses agent prop identity when provided", () => {
    render(<EmeraldClassic account={ACCOUNT} content={CONTENT} agent={AGENT_PROP} />);
    expect(screen.getByRole("heading", { name: /About Explicit Agent/ })).toBeInTheDocument();
  });

  it("falls back to broker name when no agent", () => {
    render(<EmeraldClassic account={ACCOUNT_BROKER_ONLY} content={CONTENT} />);
    expect(screen.getByRole("heading", { name: /About Sam Broker/ })).toBeInTheDocument();
  });

  it("falls back to brokerage name when no agent or broker", () => {
    render(<EmeraldClassic account={ACCOUNT_BROKERAGE_ONLY} content={CONTENT} />);
    expect(screen.getByRole("heading", { name: /About Brokerage LLC/ })).toBeInTheDocument();
  });
});
