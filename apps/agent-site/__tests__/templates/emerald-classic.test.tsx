/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { EmeraldClassic } from "@/templates/emerald-classic";
import { AGENT, CONTENT, CONTENT_ALL_DISABLED } from "../components/fixtures";
import type { AgentConfig, AgentContent } from "@/lib/types";

// Mock next/script so Analytics scripts render as queryable elements
vi.mock("next/script", () => ({
  __esModule: true,
  default: ({ id, src }: { id?: string; src: string }) => (
    <script data-testid={id} data-src={src} />
  ),
}));

describe("EmeraldClassic template", () => {
  it("always renders the Nav", () => {
    render(<EmeraldClassic agent={AGENT} content={CONTENT} />);
    expect(screen.getByRole("navigation", { name: "Main navigation" })).toBeInTheDocument();
  });

  it("always renders the Footer", () => {
    render(<EmeraldClassic agent={AGENT} content={CONTENT} />);
    expect(screen.getByRole("contentinfo")).toBeInTheDocument();
  });

  it("renders Hero section when enabled", () => {
    render(<EmeraldClassic agent={AGENT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 1, name: "Sell Your Home Fast" })).toBeInTheDocument();
  });

  it("does not render Hero when disabled", () => {
    render(<EmeraldClassic agent={AGENT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { level: 1 })).not.toBeInTheDocument();
  });

  it("renders StatsBar when enabled and items exist", () => {
    render(<EmeraldClassic agent={AGENT} content={CONTENT} />);
    expect(screen.getByText("150+")).toBeInTheDocument();
    expect(screen.getByText("Homes Sold")).toBeInTheDocument();
  });

  it("does not render StatsBar when disabled", () => {
    render(<EmeraldClassic agent={AGENT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByText("Homes Sold")).not.toBeInTheDocument();
  });

  it("does not render StatsBar when enabled but items is empty", () => {
    const contentEmptyStats: AgentContent = {
      ...CONTENT,
      sections: {
        ...CONTENT.sections,
        stats: { enabled: true, data: { items: [] } },
      },
    };
    render(<EmeraldClassic agent={AGENT} content={contentEmptyStats} />);
    expect(screen.queryByText("Homes Sold")).not.toBeInTheDocument();
  });

  it("renders Services section when enabled", () => {
    render(<EmeraldClassic agent={AGENT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 2, name: "What I Do for You" })).toBeInTheDocument();
  });

  it("does not render Services when disabled", () => {
    render(<EmeraldClassic agent={AGENT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { name: "What I Do for You" })).not.toBeInTheDocument();
  });

  it("renders HowItWorks when enabled", () => {
    render(<EmeraldClassic agent={AGENT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 2, name: "How It Works" })).toBeInTheDocument();
  });

  it("does not render HowItWorks when disabled", () => {
    render(<EmeraldClassic agent={AGENT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { name: "How It Works" })).not.toBeInTheDocument();
  });

  it("renders SoldHomes when enabled and items exist", () => {
    render(<EmeraldClassic agent={AGENT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 2, name: "Recently Sold" })).toBeInTheDocument();
  });

  it("does not render SoldHomes when disabled", () => {
    render(<EmeraldClassic agent={AGENT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { name: "Recently Sold" })).not.toBeInTheDocument();
  });

  it("does not render SoldHomes when enabled but items is empty", () => {
    const contentEmptySold: AgentContent = {
      ...CONTENT,
      sections: {
        ...CONTENT.sections,
        sold_homes: { enabled: true, data: { items: [] } },
      },
    };
    render(<EmeraldClassic agent={AGENT} content={contentEmptySold} />);
    expect(screen.queryByRole("heading", { name: "Recently Sold" })).not.toBeInTheDocument();
  });

  it("renders Testimonials when enabled and items exist", () => {
    render(<EmeraldClassic agent={AGENT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 2, name: "What My Clients Say" })).toBeInTheDocument();
  });

  it("does not render Testimonials when disabled", () => {
    render(<EmeraldClassic agent={AGENT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { name: "What My Clients Say" })).not.toBeInTheDocument();
  });

  it("does not render Testimonials when enabled but items is empty", () => {
    const contentEmptyTestimonials: AgentContent = {
      ...CONTENT,
      sections: {
        ...CONTENT.sections,
        testimonials: { enabled: true, data: { items: [] } },
      },
    };
    render(<EmeraldClassic agent={AGENT} content={contentEmptyTestimonials} />);
    expect(screen.queryByRole("heading", { name: "What My Clients Say" })).not.toBeInTheDocument();
  });

  it("renders CmaSection when enabled", () => {
    render(<EmeraldClassic agent={AGENT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 2, name: "What's Your Home Worth?" })).toBeInTheDocument();
  });

  it("does not render CmaSection when disabled", () => {
    render(<EmeraldClassic agent={AGENT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { name: "What's Your Home Worth?" })).not.toBeInTheDocument();
  });

  it("renders About when enabled", () => {
    render(<EmeraldClassic agent={AGENT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 2, name: "About Jane Smith" })).toBeInTheDocument();
  });

  it("does not render About when disabled", () => {
    render(<EmeraldClassic agent={AGENT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { name: /About/ })).not.toBeInTheDocument();
  });

  it("renders Analytics scripts when tracking is configured", () => {
    const agentWithTracking: AgentConfig = {
      ...AGENT,
      integrations: {
        ...AGENT.integrations,
        tracking: { google_analytics_id: "G-TEST123" },
      },
    };
    const { getByTestId } = render(<EmeraldClassic agent={agentWithTracking} content={CONTENT} />);
    expect(getByTestId("ga4-config")).toBeTruthy();
  });

  it("does not render Analytics scripts when no tracking is configured", () => {
    const { container } = render(<EmeraldClassic agent={AGENT} content={CONTENT} />);
    expect(container.querySelectorAll("[data-testid='gtm-script']").length).toBe(0);
    expect(container.querySelectorAll("[data-testid='ga4-config']").length).toBe(0);
    expect(container.querySelectorAll("[data-testid='meta-pixel']").length).toBe(0);
  });

  it("renders all sections when all enabled", () => {
    render(<EmeraldClassic agent={AGENT} content={CONTENT} />);
    // Check that all major section headings are present
    expect(screen.getByRole("heading", { name: "Sell Your Home Fast" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "What I Do for You" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "How It Works" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Recently Sold" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "What My Clients Say" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "What's Your Home Worth?" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "About Jane Smith" })).toBeInTheDocument();
  });
});
