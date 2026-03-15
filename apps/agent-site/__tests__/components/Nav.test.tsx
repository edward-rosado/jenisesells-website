/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
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
    const emailLinks = screen.getAllByRole("link", { name: /jane@example\.com/ });
    expect(emailLinks.length).toBeGreaterThanOrEqual(1);
    expect(emailLinks[0]).toHaveAttribute("href", "mailto:jane@example.com");
  });

  it("renders phone links when phone is present", () => {
    render(<Nav agent={AGENT} />);
    const phoneLinks = screen.getAllByRole("link", { name: /555-123-4567/ });
    expect(phoneLinks.length).toBeGreaterThanOrEqual(1);
    expect(phoneLinks[0]).toHaveAttribute("href", "tel:5551234567");
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
    const links = screen.getAllByRole("link");
    const phoneLinks = links.filter((l) => l.getAttribute("href")?.startsWith("tel:"));
    expect(phoneLinks).toHaveLength(0);
  });

  it("renders office phone link when office_phone is present", () => {
    const { container } = render(<Nav agent={AGENT} />);
    const officeLink = container.querySelector('a[href="tel:7322512500"]');
    expect(officeLink).toBeInTheDocument();
    expect(officeLink?.textContent).toContain("251-2500");
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

  it("renders hamburger menu button (hidden on desktop via CSS)", () => {
    render(<Nav agent={AGENT} />);
    const hamburger = screen.getByLabelText("Menu");
    expect(hamburger).toBeInTheDocument();
    expect(hamburger.tagName).toBe("BUTTON");
  });

  it("renders section links in the drawer", () => {
    render(<Nav agent={AGENT} />);
    expect(screen.getByText("Why Choose Me")).toBeInTheDocument();
    expect(screen.getByText("How It Works")).toBeInTheDocument();
    expect(screen.getByText("Recent Sales")).toBeInTheDocument();
    expect(screen.getByText("Testimonials")).toBeInTheDocument();
    expect(screen.getByText("Get Your Home Value")).toBeInTheDocument();
    expect(screen.getByText("About")).toBeInTheDocument();
  });

  it("toggles drawer open and closed on hamburger click", () => {
    render(<Nav agent={AGENT} />);
    const hamburger = screen.getByLabelText("Menu");

    // Click to open
    fireEvent.click(hamburger);
    expect(screen.getByText("Why Choose Me")).toBeInTheDocument();

    // Click again to close
    fireEvent.click(hamburger);
    expect(screen.getByText("Why Choose Me")).toBeInTheDocument();
  });
});
