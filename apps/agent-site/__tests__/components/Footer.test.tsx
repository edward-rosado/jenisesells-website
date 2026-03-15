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
    const heading = screen.getByText("Bob Jones");
    expect(heading.tagName).toBe("P");
    expect(heading).toHaveStyle({ fontSize: "22px" });
  });

  it("renders brokerage name prominently", () => {
    render(<Footer agent={AGENT} />);
    expect(screen.getByText("Best Homes Realty")).toBeInTheDocument();
  });

  it("does not render brokerage when absent", () => {
    render(<Footer agent={AGENT_MINIMAL} />);
    expect(screen.queryByText(/Best Homes Realty/)).not.toBeInTheDocument();
  });

  it("renders phone as a tel link with digits only", () => {
    render(<Footer agent={AGENT} />);
    const phoneLink = screen.getByRole("link", { name: /call jane smith/i });
    expect(phoneLink).toHaveAttribute("href", "tel:5551234567");
  });

  it("renders email as a mailto link", () => {
    render(<Footer agent={AGENT} />);
    const emailLink = screen.getByRole("link", { name: /email jane smith/i });
    expect(emailLink).toHaveAttribute("href", "mailto:jane@example.com");
  });

  it("renders service areas with compact county format", () => {
    render(<Footer agent={AGENT} />);
    expect(screen.getByText(/Serving Hoboken & Jersey City Counties, NJ/)).toBeInTheDocument();
  });

  it("does not render service areas section when absent", () => {
    render(<Footer agent={AGENT_MINIMAL} />);
    expect(screen.queryByText(/Serving/)).not.toBeInTheDocument();
  });

  it("renders office phone in contact line when present", () => {
    const agentWithOffice = {
      ...AGENT,
      identity: { ...AGENT.identity, office_phone: "Office: (732) 251-2500" },
    };
    render(<Footer agent={agentWithOffice} />);
    const officeLink = screen.getByRole("link", { name: /call office/i });
    expect(officeLink).toHaveAttribute("href", "tel:7322512500");
  });

  it("renders equal housing opportunity as standalone section", () => {
    render(<Footer agent={AGENT} />);
    expect(screen.getByLabelText("Equal Housing Opportunity logo")).toBeInTheDocument();
    expect(screen.getByText("Equal Housing Opportunity")).toBeInTheDocument();
  });

  it("renders disclaimer with agent info", () => {
    render(<Footer agent={AGENT} />);
    expect(screen.getByText(/general informational purposes/)).toBeInTheDocument();
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

  it("renders legal links nav", () => {
    render(<Footer agent={AGENT} />);
    expect(screen.getByRole("navigation", { name: "Legal links" })).toBeInTheDocument();
  });

  it("renders privacy link", () => {
    render(<Footer agent={AGENT} />);
    const link = screen.getByRole("link", { name: /privacy/i });
    expect(link).toHaveAttribute("href", "/privacy");
  });

  it("renders terms link", () => {
    render(<Footer agent={AGENT} />);
    const link = screen.getByRole("link", { name: /terms/i });
    expect(link).toHaveAttribute("href", "/terms");
  });

  it("renders accessibility link", () => {
    render(<Footer agent={AGENT} />);
    const link = screen.getByRole("link", { name: /accessibility/i });
    expect(link).toHaveAttribute("href", "/accessibility");
  });

  it("formats 3+ service areas with comma-separated list and ampersand", () => {
    const agentThreeAreas = {
      ...AGENT,
      location: { ...AGENT.location, service_areas: ["Middlesex County", "Monmouth County", "Ocean County"] },
    };
    render(<Footer agent={agentThreeAreas} />);
    expect(screen.getByText(/Serving Middlesex, Monmouth & Ocean Counties, NJ/)).toBeInTheDocument();
  });

  it("formats single service area correctly", () => {
    const agentOneArea = {
      ...AGENT,
      location: { ...AGENT.location, service_areas: ["Bergen County"] },
    };
    render(<Footer agent={agentOneArea} />);
    expect(screen.getByText(/Serving Bergen Counties, NJ/)).toBeInTheDocument();
  });

  it("renders license_id in license line when present", () => {
    const agentWithLicense = {
      ...AGENT,
      identity: { ...AGENT.identity, license_id: "1234567" },
    };
    render(<Footer agent={agentWithLicense} />);
    // The license number appears in both the license line and the disclaimer — confirm at least one is present
    const matches = screen.getAllByText(/NJ License #1234567/);
    expect(matches.length).toBeGreaterThan(0);
  });

  it("does not render license number in license line when license_id is absent", () => {
    render(<Footer agent={AGENT} />);
    // AGENT has no license_id — neither the license line nor the disclaimer should have it
    expect(screen.queryByText(/NJ License #/)).not.toBeInTheDocument();
  });

  it("renders office_address in disclaimer when present", () => {
    const agentWithOfficeAddress = {
      ...AGENT,
      location: { ...AGENT.location, office_address: "100 Park Ave, Newark, NJ" },
    };
    render(<Footer agent={agentWithOfficeAddress} />);
    expect(screen.getByText(/100 Park Ave, Newark, NJ/)).toBeInTheDocument();
  });

  it("renders office_phone in disclaimer when present", () => {
    const agentWithOfficePhone = {
      ...AGENT,
      identity: { ...AGENT.identity, office_phone: "Office: (732) 251-2500" },
    };
    render(<Footer agent={agentWithOfficePhone} />);
    // The disclaimer paragraph includes the office phone
    const disclaimer = screen.getByText(/general informational purposes/);
    expect(disclaimer).toHaveTextContent("Office: (732) 251-2500");
  });

  it("does not include office_phone in disclaimer when absent", () => {
    render(<Footer agent={AGENT_MINIMAL} />);
    const disclaimer = screen.getByText(/general informational purposes/);
    expect(disclaimer).not.toHaveTextContent("Office:");
  });
});
