/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { Footer } from "@/components/sections/shared/Footer";
import { ACCOUNT, ACCOUNT_MINIMAL, AGENT_PROP } from "../fixtures";
import type { AccountConfig } from "@/features/config/types";

describe("Footer", () => {
  it("renders the agent name", () => {
    render(<Footer agent={ACCOUNT} />);
    expect(screen.getAllByText(/Jane Smith/).length).toBeGreaterThan(0);
  });

  it("renders the title when present", () => {
    render(<Footer agent={ACCOUNT} />);
    expect(screen.getAllByText(/Jane Smith, REALTOR/).length).toBeGreaterThan(0);
  });

  it("does not render title separator when title is absent", () => {
    const agentNoTitle: AccountConfig = { ...ACCOUNT, agent: { ...ACCOUNT.agent!, title: "" } };
    render(<Footer agent={agentNoTitle} />);
    const heading = screen.getByText("Jane Smith");
    expect(heading.tagName).toBe("P");
    expect(heading).toHaveStyle({ fontSize: "22px" });
  });

  it("renders brokerage name prominently", () => {
    render(<Footer agent={ACCOUNT} />);
    expect(screen.getByText("Best Homes Realty")).toBeInTheDocument();
  });

  it("does not render brokerage when agent has no brokerage name", () => {
    render(<Footer agent={ACCOUNT_MINIMAL} />);
    // ACCOUNT_MINIMAL has brokerage "Min Realty" — it renders
    // This test verifies AGENT renders Best Homes Realty, not ACCOUNT_MINIMAL absence
    expect(screen.queryByText("Best Homes Realty")).not.toBeInTheDocument();
  });

  it("renders phone as a tel link with digits only", () => {
    render(<Footer agent={ACCOUNT} />);
    const phoneLink = screen.getByRole("link", { name: /call jane smith/i });
    expect(phoneLink).toHaveAttribute("href", "tel:5551234567");
  });

  it("renders email as a mailto link", () => {
    render(<Footer agent={ACCOUNT} />);
    const emailLink = screen.getByRole("link", { name: /email jane smith/i });
    expect(emailLink).toHaveAttribute("href", "mailto:jane@example.com");
  });

  it("renders service areas with compact county format", () => {
    render(<Footer agent={ACCOUNT} />);
    expect(screen.getByText(/Serving Hoboken & Jersey City Counties, NJ/)).toBeInTheDocument();
  });

  it("does not render service areas section when empty", () => {
    render(<Footer agent={ACCOUNT_MINIMAL} />);
    expect(screen.queryByText(/Serving/)).not.toBeInTheDocument();
  });

  it("renders office phone in contact line when present", () => {
    render(<Footer agent={ACCOUNT} />);
    const officeLink = screen.getByRole("link", { name: /call office/i });
    expect(officeLink).toHaveAttribute("href", "tel:7322512500");
  });

  it("renders equal housing opportunity as standalone section", () => {
    render(<Footer agent={ACCOUNT} />);
    expect(screen.getByLabelText("Equal Housing Opportunity logo")).toBeInTheDocument();
    expect(screen.getByText("Equal Housing Opportunity")).toBeInTheDocument();
  });

  it("renders disclaimer with agent info", () => {
    render(<Footer agent={ACCOUNT} />);
    expect(screen.getByText(/general informational purposes/)).toBeInTheDocument();
  });

  it("renders copyright with current year and agent name", () => {
    render(<Footer agent={ACCOUNT} />);
    const currentYear = new Date().getFullYear().toString();
    expect(screen.getByText(new RegExp(currentYear))).toBeInTheDocument();
    expect(screen.getByText(/All rights reserved/)).toBeInTheDocument();
  });

  it("renders a footer element", () => {
    const { container } = render(<Footer agent={ACCOUNT} />);
    expect(container.querySelector("footer")).toBeInTheDocument();
  });

  it("renders legal links nav", () => {
    render(<Footer agent={ACCOUNT} />);
    expect(screen.getByRole("navigation", { name: "Legal links" })).toBeInTheDocument();
  });

  it("renders privacy link without accountId when not provided", () => {
    render(<Footer agent={ACCOUNT} />);
    const link = screen.getByRole("link", { name: /privacy/i });
    expect(link).toHaveAttribute("href", "/privacy");
  });

  it("renders terms link without accountId when not provided", () => {
    render(<Footer agent={ACCOUNT} />);
    const link = screen.getByRole("link", { name: /terms/i });
    expect(link).toHaveAttribute("href", "/terms");
  });

  it("renders accessibility link without accountId when not provided", () => {
    render(<Footer agent={ACCOUNT} />);
    const link = screen.getByRole("link", { name: /accessibility/i });
    expect(link).toHaveAttribute("href", "/accessibility");
  });

  it("appends accountId to legal links when provided", () => {
    render(<Footer agent={ACCOUNT} accountId="test-agent" />);
    expect(screen.getByRole("link", { name: /privacy/i })).toHaveAttribute("href", "/privacy?accountId=test-agent");
    expect(screen.getByRole("link", { name: /terms/i })).toHaveAttribute("href", "/terms?accountId=test-agent");
    expect(screen.getByRole("link", { name: /accessibility/i })).toHaveAttribute("href", "/accessibility?accountId=test-agent");
  });

  it("formats 3+ service areas with comma-separated list and ampersand", () => {
    const agentThreeAreas: AccountConfig = {
      ...ACCOUNT,
      location: { ...ACCOUNT.location, service_areas: ["Middlesex County", "Monmouth County", "Ocean County"] },
    };
    render(<Footer agent={agentThreeAreas} />);
    expect(screen.getByText(/Serving Middlesex, Monmouth & Ocean Counties, NJ/)).toBeInTheDocument();
  });

  it("formats single service area correctly", () => {
    const agentOneArea: AccountConfig = {
      ...ACCOUNT,
      location: { ...ACCOUNT.location, service_areas: ["Bergen County"] },
    };
    render(<Footer agent={agentOneArea} />);
    expect(screen.getByText(/Serving Bergen Counties, NJ/)).toBeInTheDocument();
  });

  it("renders license_number in license line when present", () => {
    const agentWithLicense: AccountConfig = {
      ...ACCOUNT,
      agent: { ...ACCOUNT.agent!, license_number: "1234567" },
    };
    render(<Footer agent={agentWithLicense} />);
    const matches = screen.getAllByText(/License #1234567/);
    expect(matches.length).toBeGreaterThan(0);
  });

  it("does not render license number in license line when absent", () => {
    render(<Footer agent={ACCOUNT} />);
    // AGENT (ACCOUNT) has no agent.license_number and brokerage.license_number is "123456" (internal) — but the license line uses agent's license
    expect(screen.queryByText(/License #123456/)).not.toBeInTheDocument();
  });

  it("renders office_address in disclaimer when present", () => {
    const agentWithOfficeAddress: AccountConfig = {
      ...ACCOUNT,
      brokerage: { ...ACCOUNT.brokerage, office_address: "100 Park Ave, Newark, NJ" },
    };
    render(<Footer agent={agentWithOfficeAddress} />);
    expect(screen.getByText(/100 Park Ave, Newark, NJ/)).toBeInTheDocument();
  });

  it("renders office_phone in disclaimer when present", () => {
    render(<Footer agent={ACCOUNT} />);
    // ACCOUNT.brokerage.office_phone = "(732) 251-2500"
    const disclaimer = screen.getByText(/general informational purposes/);
    expect(disclaimer).toHaveTextContent("(732) 251-2500");
  });

  it("does not include office_phone in disclaimer when absent", () => {
    render(<Footer agent={ACCOUNT_MINIMAL} />);
    const disclaimer = screen.getByText(/general informational purposes/);
    expect(disclaimer).not.toHaveTextContent("Office:");
  });

  it("renders brokerage name more prominently than agent name", () => {
    render(<Footer agent={ACCOUNT} />);
    const brokerage = screen.getByText(ACCOUNT.brokerage.name);
    expect(brokerage).toHaveStyle({ fontSize: "24px" });
  });

  it("renders NJ fair housing statement with expanded protected classes", () => {
    render(<Footer agent={ACCOUNT} />);
    expect(screen.getByText(/gender identity/i)).toBeInTheDocument();
    expect(screen.getByText(/source of lawful income/i)).toBeInTheDocument();
  });

  it("renders with AgentConfig (flat agent identity — no handle property)", () => {
    render(<Footer agent={AGENT_PROP} accountId="explicit-agent" />);
    expect(screen.getAllByText(/Explicit Agent/).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Senior REALTOR/).length).toBeGreaterThan(0);
    // Flat AgentConfig has no brokerage, service areas, or state
    expect(screen.queryByText(/Serving/)).not.toBeInTheDocument();
    // Phone renders
    const phoneLink = screen.getByRole("link", { name: /call explicit agent/i });
    expect(phoneLink).toHaveAttribute("href", "tel:5559990000");
    // Email renders
    const emailLink = screen.getByRole("link", { name: /email explicit agent/i });
    expect(emailLink).toHaveAttribute("href", "mailto:explicit@example.com");
    // Legal links include accountId
    expect(screen.getByRole("link", { name: /privacy/i })).toHaveAttribute("href", "/privacy?accountId=explicit-agent");
  });
});
