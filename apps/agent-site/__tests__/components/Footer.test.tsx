/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { Footer } from "@/components/sections/Footer";
import { AGENT, AGENT_MINIMAL } from "./fixtures";

describe("Footer", () => {
  it("renders the agent name", () => {
    render(<Footer agent={AGENT} />);
    expect(screen.getAllByText(/Jane Smith/).length).toBeGreaterThan(0);
  });

  it("renders the title when present", () => {
    render(<Footer agent={AGENT} />);
    expect(screen.getByText(/Jane Smith, REALTOR/)).toBeInTheDocument();
  });

  it("does not render title separator when title is absent", () => {
    render(<Footer agent={AGENT_MINIMAL} />);
    // Should just be the name without a comma
    expect(screen.getByText("Bob Jones")).toBeInTheDocument();
    expect(screen.queryByText(/Bob Jones,/)).not.toBeInTheDocument();
  });

  it("renders brokerage when present", () => {
    render(<Footer agent={AGENT} />);
    expect(screen.getByText("Best Homes Realty")).toBeInTheDocument();
  });

  it("does not render brokerage when absent", () => {
    render(<Footer agent={AGENT_MINIMAL} />);
    expect(screen.queryByText("Best Homes Realty")).not.toBeInTheDocument();
  });

  it("renders phone as a tel link", () => {
    render(<Footer agent={AGENT} />);
    const phoneLink = screen.getByRole("link", { name: "555-123-4567" });
    expect(phoneLink).toHaveAttribute("href", "tel:555-123-4567");
  });

  it("renders email as a mailto link", () => {
    render(<Footer agent={AGENT} />);
    const emailLink = screen.getByRole("link", { name: "jane@example.com" });
    expect(emailLink).toHaveAttribute("href", "mailto:jane@example.com");
  });

  it("renders service areas when present", () => {
    render(<Footer agent={AGENT} />);
    expect(screen.getByText(/Hoboken/)).toBeInTheDocument();
    expect(screen.getByText(/Jersey City/)).toBeInTheDocument();
  });

  it("does not render service areas section when absent", () => {
    render(<Footer agent={AGENT_MINIMAL} />);
    expect(screen.queryByText(/Serving/)).not.toBeInTheDocument();
  });

  it("renders languages when agent has more than one language", () => {
    render(<Footer agent={AGENT} />);
    expect(screen.getByText(/English/)).toBeInTheDocument();
    expect(screen.getByText(/Spanish/)).toBeInTheDocument();
  });

  it("does not render language line for single language", () => {
    const agentSingleLang = {
      ...AGENT,
      identity: { ...AGENT.identity, languages: ["English"] },
    };
    render(<Footer agent={agentSingleLang} />);
    // The language paragraph should not appear for a single language
    expect(screen.queryByText("English · Spanish")).not.toBeInTheDocument();
    expect(screen.queryByText("English")).not.toBeInTheDocument();
  });

  it("does not render language line when languages is undefined", () => {
    render(<Footer agent={AGENT_MINIMAL} />);
    expect(screen.queryByText(/·/)).not.toBeInTheDocument();
  });

  it("renders copyright with current year and agent name", () => {
    render(<Footer agent={AGENT} />);
    const currentYear = new Date().getFullYear().toString();
    expect(screen.getByText(new RegExp(currentYear))).toBeInTheDocument();
    expect(screen.getByText(/All rights reserved/)).toBeInTheDocument();
  });

  it("renders a footer element", () => {
    const { container } = render(<Footer agent={AGENT} />);
    expect(container.querySelector("footer")).toBeInTheDocument();
  });
});
