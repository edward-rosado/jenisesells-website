/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { Nav } from "@/components/Nav";
import { AGENT, AGENT_MINIMAL } from "./fixtures";

describe("Nav", () => {
  it("renders the agent tagline in uppercase when tagline is present", () => {
    render(<Nav agent={AGENT} />);
    expect(screen.getByText("YOUR DREAM HOME AWAITS")).toBeInTheDocument();
  });

  it("falls back to agent name in uppercase when tagline is absent", () => {
    render(<Nav agent={AGENT_MINIMAL} />);
    expect(screen.getByText("BOB JONES")).toBeInTheDocument();
  });

  it("renders email link when email is present", () => {
    render(<Nav agent={AGENT} />);
    const emailLink = screen.getByRole("link", { name: "jane@example.com" });
    expect(emailLink).toHaveAttribute("href", "mailto:jane@example.com");
  });

  it("renders phone CTA button when phone is present", () => {
    render(<Nav agent={AGENT} />);
    const phoneLink = screen.getByRole("link", { name: /555-123-4567/ });
    expect(phoneLink).toHaveAttribute("href", "tel:5551234567");
  });

  it("does not render email link when email is absent", () => {
    const agentNoEmail = {
      ...AGENT_MINIMAL,
      identity: { ...AGENT_MINIMAL.identity, email: "" },
    };
    render(<Nav agent={agentNoEmail} />);
    expect(screen.queryByRole("link", { name: /[@]/ })).not.toBeInTheDocument();
  });

  it("does not render phone link when phone is absent", () => {
    const agentNoPhone = {
      ...AGENT_MINIMAL,
      identity: { ...AGENT_MINIMAL.identity, phone: "" },
    };
    render(<Nav agent={agentNoPhone} />);
    expect(screen.queryByRole("link", { name: /tel:/ })).not.toBeInTheDocument();
  });

  it("renders office phone link when office_phone is present", () => {
    render(<Nav agent={AGENT} />);
    const officeLink = screen.getByRole("link", { name: /251-2500/ });
    expect(officeLink).toHaveAttribute("href", "tel:7322512500");
  });

  it("does not render office phone link when office_phone is absent", () => {
    render(<Nav agent={AGENT_MINIMAL} />);
    const links = screen.getAllByRole("link");
    const officeLinks = links.filter((l) => l.getAttribute("href")?.startsWith("tel:") && l.textContent?.includes("251"));
    expect(officeLinks).toHaveLength(0);
  });

  it("renders logo when logo_url is present", () => {
    const agentWithLogo = {
      ...AGENT,
      branding: { ...AGENT.branding, logo_url: "/images/logo.png" },
    };
    render(<Nav agent={agentWithLogo} />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Best Homes Realty");
  });

  it("does not render logo when logo_url is absent", () => {
    render(<Nav agent={AGENT} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("has nav landmark role", () => {
    render(<Nav agent={AGENT} />);
    expect(screen.getByRole("navigation")).toBeInTheDocument();
  });

  it("nav has aria-label for accessibility", () => {
    render(<Nav agent={AGENT} />);
    expect(screen.getByRole("navigation")).toHaveAttribute("aria-label", "Main navigation");
  });

  it("uses 'Brokerage logo' as image alt text when brokerage name is absent", () => {
    const agentWithLogoNoBrokerage = {
      ...AGENT_MINIMAL,
      branding: { ...AGENT_MINIMAL.branding, logo_url: "/images/logo.png" },
      identity: { ...AGENT_MINIMAL.identity, brokerage: undefined },
    };
    render(<Nav agent={agentWithLogoNoBrokerage as never} />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Brokerage logo");
  });
});
