/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { Nav, DEFAULT_NAV_ITEMS } from "@/components/Nav";
import { AGENT, AGENT_MINIMAL, CONTENT } from "./fixtures";
import type { ContactMethod, NavItem } from "@/lib/types";

const mockPathname = vi.fn(() => "/");
vi.mock("next/navigation", () => ({ usePathname: () => mockPathname() }));

describe("Nav", () => {
  // --- Fallback behavior (no content props — legacy path) ---

  it("renders the agent tagline in uppercase when tagline is present", () => {
    render(<Nav agent={AGENT} />);
    expect(screen.getByText("YOUR DREAM HOME AWAITS")).toBeInTheDocument();
  });

  it("falls back to agent name in uppercase when tagline is absent", () => {
    render(<Nav agent={AGENT_MINIMAL} />);
    expect(screen.getByText("BOB JONES")).toBeInTheDocument();
  });

  it("renders email link from fallback identity", () => {
    render(<Nav agent={AGENT} />);
    const emailLinks = screen.getAllByRole("link", { name: /jane@example\.com/ });
    expect(emailLinks.length).toBeGreaterThanOrEqual(1);
    expect(emailLinks[0]).toHaveAttribute("href", "mailto:jane@example.com");
  });

  it("renders phone links from fallback identity", () => {
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

  it("renders office phone link from fallback identity", () => {
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

  // --- Content-driven nav ---

  it("renders nav items from content.navigation", () => {
    const customNav = {
      items: [
        { label: "Custom Link 1", section: "custom-1" },
        { label: "Custom Link 2", section: "custom-2" },
      ],
    };
    const { container } = render(<Nav agent={AGENT} navigation={customNav} />);
    const desktopLinks = container.querySelector(".nav-desktop-links") as HTMLElement;
    const links = desktopLinks.querySelectorAll("a");
    expect(links).toHaveLength(2);
    expect(links[0].textContent).toBe("Custom Link 1");
    expect(links[0]).toHaveAttribute("href", "#custom-1");
    expect(links[1].textContent).toBe("Custom Link 2");
  });

  it("renders contact info from contactInfo prop", () => {
    const contacts: ContactMethod[] = [
      { type: "phone", value: "(999) 111-2222", label: "Cell", is_preferred: true },
      { type: "email", value: "test@test.com", label: "Work Email", is_preferred: false },
    ];
    const { container } = render(<Nav agent={AGENT} contactInfo={contacts} />);
    const drawerContact = container.querySelector(".drawer-contact") as HTMLElement;
    const hrefs = Array.from(drawerContact.querySelectorAll("a")).map((l) => l.getAttribute("href"));
    expect(hrefs).toContain("tel:9991112222");
    expect(hrefs).toContain("mailto:test@test.com");
  });

  it("renders phone with extension in drawer", () => {
    const contacts: ContactMethod[] = [
      { type: "phone", value: "(732) 251-2500", ext: "714", label: "Office Phone", is_preferred: false },
    ];
    const { container } = render(<Nav agent={AGENT} contactInfo={contacts} />);
    const drawerContact = container.querySelector(".drawer-contact") as HTMLElement;
    const link = drawerContact.querySelector("a") as HTMLElement;
    expect(link).toHaveAttribute("href", "tel:7322512500,714");
    expect(link.textContent).toContain("ext 714");
  });

  it("preferred phone gets accent background in drawer", () => {
    const contacts: ContactMethod[] = [
      { type: "phone", value: "555-111-0000", label: "Cell", is_preferred: true },
      { type: "phone", value: "555-222-0000", label: "Office", is_preferred: false },
    ];
    const { container } = render(<Nav agent={AGENT} contactInfo={contacts} />);
    const drawerContact = container.querySelector(".drawer-contact") as HTMLElement;
    const links = drawerContact.querySelectorAll("a");
    // Preferred phone gets accent background
    expect((links[0] as HTMLElement).style.background).toBe("var(--color-accent)");
    // Non-preferred gets subdued
    expect((links[1] as HTMLElement).style.background).toBe("rgba(255, 255, 255, 0.1)");
  });

  it("uses DEFAULT_NAV_ITEMS when navigation prop is not provided", () => {
    const { container } = render(<Nav agent={AGENT} />);
    const desktopLinks = container.querySelector(".nav-desktop-links") as HTMLElement;
    const links = desktopLinks.querySelectorAll("a");
    expect(links).toHaveLength(DEFAULT_NAV_ITEMS.length);
    expect(links[0].textContent).toBe(DEFAULT_NAV_ITEMS[0].label);
  });

  it("drawer also renders custom nav items from content", () => {
    const customNav = {
      items: [
        { label: "Alpha", section: "alpha" },
        { label: "Beta", section: "beta" },
        { label: "Gamma", section: "gamma" },
      ],
    };
    const { container } = render(<Nav agent={AGENT} navigation={customNav} />);
    const drawerLinks = container.querySelector(".drawer-nav-links") as HTMLElement;
    const links = drawerLinks.querySelectorAll("a");
    expect(links).toHaveLength(3);
    expect(links[0].textContent).toBe("Alpha");
    expect(links[2].textContent).toBe("Gamma");
  });

  // --- Logo and branding ---

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

  it("logo links to homepage", () => {
    const agentWithLogo = {
      ...AGENT,
      branding: { ...AGENT.branding, logo_url: "/images/logo.png" },
    };
    render(<Nav agent={agentWithLogo} />);
    const img = screen.getByRole("img");
    const homeLink = img.closest("a");
    expect(homeLink).toHaveAttribute("href", "/");
  });

  it("does not render brokerage name in nav (logo is sufficient)", () => {
    render(<Nav agent={AGENT} />);
    expect(screen.queryByText("Best Homes Realty")).not.toBeInTheDocument();
  });

  // --- Accessibility ---

  it("has nav landmark role", () => {
    render(<Nav agent={AGENT} />);
    expect(screen.getByRole("navigation")).toBeInTheDocument();
  });

  it("nav has aria-label for accessibility", () => {
    render(<Nav agent={AGENT} />);
    expect(screen.getByRole("navigation")).toHaveAttribute("aria-label", "Main navigation");
  });

  it("renders drawer as dialog with aria-modal", () => {
    const { container } = render(<Nav agent={AGENT} />);
    const drawer = container.querySelector(".nav-drawer") as HTMLElement;
    expect(drawer).toHaveAttribute("role", "dialog");
    expect(drawer).toHaveAttribute("aria-modal", "true");
    expect(drawer).toHaveAttribute("aria-label", "Navigation menu");
  });

  it("sets aria-expanded on hamburger button", () => {
    render(<Nav agent={AGENT} />);
    const hamburger = screen.getByLabelText("Menu");

    expect(hamburger).toHaveAttribute("aria-expanded", "false");
    fireEvent.click(hamburger);
    expect(hamburger).toHaveAttribute("aria-expanded", "true");
    fireEvent.click(hamburger);
    expect(hamburger).toHaveAttribute("aria-expanded", "false");
  });

  // --- Hamburger and drawer behavior ---

  it("renders hamburger menu button (hidden on desktop via CSS)", () => {
    render(<Nav agent={AGENT} />);
    const hamburger = screen.getByLabelText("Menu");
    expect(hamburger).toBeInTheDocument();
    expect(hamburger.tagName).toBe("BUTTON");
  });

  it("renders section links in the drawer", () => {
    const { container } = render(<Nav agent={AGENT} />);
    const drawer = container.querySelector(".nav-drawer") as HTMLElement;
    expect(drawer.querySelectorAll("a[href*='#']")).toHaveLength(6);
  });

  it("toggles drawer open and closed on hamburger click", () => {
    const { container } = render(<Nav agent={AGENT} />);
    const hamburger = screen.getByLabelText("Menu");
    const drawer = container.querySelector(".nav-drawer") as HTMLElement;

    fireEvent.click(hamburger);
    expect(drawer.style.visibility).toBe("visible");

    fireEvent.click(hamburger);
    expect(drawer.style.visibility).toBe("hidden");
  });

  it("closes drawer when a section link is clicked", () => {
    const { container } = render(<Nav agent={AGENT} />);
    const hamburger = screen.getByLabelText("Menu");

    fireEvent.click(hamburger);

    const drawer = container.querySelector(".nav-drawer") as HTMLElement;
    expect(drawer.style.visibility).toBe("visible");

    const drawerLink = drawer.querySelector("a[href*='#']") as HTMLElement;
    fireEvent.click(drawerLink);

    expect(drawer.style.visibility).toBe("hidden");
  });

  it("closes drawer when overlay is clicked", () => {
    const { container } = render(<Nav agent={AGENT} />);
    const hamburger = screen.getByLabelText("Menu");

    fireEvent.click(hamburger);

    const overlays = Array.from(container.querySelectorAll("div")).filter(
      (el) => el.style.position === "fixed" && el.style.zIndex === "1050"
    );
    expect(overlays).toHaveLength(1);
    fireEvent.click(overlays[0]);

    const drawer = container.querySelector(".nav-drawer") as HTMLElement;
    expect(drawer.style.visibility).toBe("hidden");
  });

  it("closes drawer when Escape key is pressed", () => {
    const { container } = render(<Nav agent={AGENT} />);
    const hamburger = screen.getByLabelText("Menu");

    fireEvent.click(hamburger);
    const drawer = container.querySelector(".nav-drawer") as HTMLElement;
    expect(drawer.style.visibility).toBe("visible");

    fireEvent.keyDown(document, { key: "Escape" });
    expect(drawer.style.visibility).toBe("hidden");
  });

  it("does not close drawer on non-Escape keys", () => {
    const { container } = render(<Nav agent={AGENT} />);
    const hamburger = screen.getByLabelText("Menu");

    fireEvent.click(hamburger);
    const drawer = container.querySelector(".nav-drawer") as HTMLElement;
    expect(drawer.style.visibility).toBe("visible");

    fireEvent.keyDown(document, { key: "Tab" });
    expect(drawer.style.visibility).toBe("visible");
  });

  it("focuses first link when drawer opens", () => {
    const { container } = render(<Nav agent={AGENT} />);
    const hamburger = screen.getByLabelText("Menu");

    fireEvent.click(hamburger);
    const drawer = container.querySelector(".nav-drawer") as HTMLElement;
    const firstLink = drawer.querySelector("a") as HTMLElement;
    expect(document.activeElement).toBe(firstLink);
  });

  // --- Routing ---

  it("uses hash-only links on the homepage", () => {
    mockPathname.mockReturnValue("/");
    const { container } = render(<Nav agent={AGENT} />);
    const desktopLinks = container.querySelector(".nav-desktop-links") as HTMLElement;
    const link = desktopLinks.querySelector("a");
    expect(link).toHaveAttribute("href", "#services");
  });

  it("uses absolute links with / prefix on non-homepage", () => {
    mockPathname.mockReturnValue("/terms");
    const { container } = render(<Nav agent={AGENT} />);
    const desktopLinks = container.querySelector(".nav-desktop-links") as HTMLElement;
    const link = desktopLinks.querySelector("a");
    expect(link).toHaveAttribute("href", "/#services");
  });

  it("tagline links to homepage", () => {
    render(<Nav agent={AGENT} />);
    const tagline = screen.getByText("YOUR DREAM HOME AWAITS");
    const homeLink = tagline.closest("a");
    expect(homeLink).toHaveAttribute("href", "/");
  });

  // --- Desktop links ---

  it("desktop link changes color on hover", () => {
    const { container } = render(<Nav agent={AGENT} />);
    const desktopLinks = container.querySelector(".nav-desktop-links") as HTMLElement;
    const link = desktopLinks.querySelector("a") as HTMLElement;

    fireEvent.mouseEnter(link);
    expect(link.style.color).toBe("var(--color-accent)");

    fireEvent.mouseLeave(link);
    expect(link.style.color).toBe("rgba(255, 255, 255, 0.85)");
  });

  it("renders desktop section links", () => {
    const { container } = render(<Nav agent={AGENT} />);
    const desktopLinks = container.querySelector(".nav-desktop-links");
    expect(desktopLinks).toBeInTheDocument();
    const links = desktopLinks?.querySelectorAll("a");
    expect(links?.length).toBe(6);
  });

  // --- Contact Me button ---

  it("renders Contact Me button for tablet view (hidden via CSS on other sizes)", () => {
    render(<Nav agent={AGENT} />);
    const contactBtn = screen.getByLabelText("Contact information");
    expect(contactBtn).toBeInTheDocument();
    expect(contactBtn.tagName).toBe("BUTTON");
    expect(contactBtn.textContent).toContain("Contact Me");
  });

  it("Contact Me button toggles drawer", () => {
    const { container } = render(<Nav agent={AGENT} />);
    const contactBtn = screen.getByLabelText("Contact information");
    const drawer = container.querySelector(".nav-drawer") as HTMLElement;

    fireEvent.click(contactBtn);
    expect(drawer.style.visibility).toBe("visible");

    fireEvent.click(contactBtn);
    expect(drawer.style.visibility).toBe("hidden");
  });

  it("drawer contains contact info section with phone, office, and email", () => {
    const { container } = render(<Nav agent={AGENT} />);
    const drawerContact = container.querySelector(".drawer-contact") as HTMLElement;
    expect(drawerContact).toBeInTheDocument();

    const links = drawerContact.querySelectorAll("a");
    const hrefs = Array.from(links).map((l) => l.getAttribute("href"));
    expect(hrefs).toContain("tel:5551234567");
    expect(hrefs).toContain("tel:7322512500");
    expect(hrefs).toContain("mailto:jane@example.com");
  });

  it("drawer nav links are inside .drawer-nav-links container", () => {
    const { container } = render(<Nav agent={AGENT} />);
    const navLinks = container.querySelector(".drawer-nav-links") as HTMLElement;
    expect(navLinks).toBeInTheDocument();
    expect(navLinks.querySelectorAll("a[href*='#']")).toHaveLength(6);
  });

  // --- Sync tests: desktop + drawer CMA labels match DEFAULT_NAV_ITEMS ---

  it("desktop nav CMA link label matches DEFAULT_NAV_ITEMS constant", () => {
    const { container } = render(<Nav agent={AGENT} />);
    const desktopLinks = container.querySelector(".nav-desktop-links") as HTMLElement;
    const cmaLink = Array.from(desktopLinks.querySelectorAll("a")).find(
      (a) => a.getAttribute("href")?.includes("#cma-form")
    );
    const expected = DEFAULT_NAV_ITEMS.find((s) => s.section === "cma-form")!.label;
    expect(cmaLink?.textContent).toBe(expected);
  });

  it("mobile drawer CMA link label matches DEFAULT_NAV_ITEMS constant", () => {
    const { container } = render(<Nav agent={AGENT} />);
    const drawerLinks = container.querySelector(".drawer-nav-links") as HTMLElement;
    const cmaLink = Array.from(drawerLinks.querySelectorAll("a")).find(
      (a) => a.getAttribute("href")?.includes("#cma-form")
    );
    const expected = DEFAULT_NAV_ITEMS.find((s) => s.section === "cma-form")!.label;
    expect(cmaLink?.textContent).toBe(expected);
  });

  it("DEFAULT_NAV_ITEMS CMA label is concise (fits nav bar)", () => {
    const cmaSection = DEFAULT_NAV_ITEMS.find((s) => s.section === "cma-form")!;
    expect(cmaSection.label.length).toBeLessThanOrEqual(20);
  });

  // --- Content-driven: nav items from CONTENT fixture match ---

  it("renders content.navigation items in desktop and drawer", () => {
    const { container } = render(
      <Nav agent={AGENT} navigation={CONTENT.navigation} contactInfo={CONTENT.contact_info} />
    );
    const desktopLinks = container.querySelector(".nav-desktop-links") as HTMLElement;
    const drawerLinks = container.querySelector(".drawer-nav-links") as HTMLElement;

    const contentItems = CONTENT.navigation!.items;
    expect(desktopLinks.querySelectorAll("a")).toHaveLength(contentItems.length);
    expect(drawerLinks.querySelectorAll("a")).toHaveLength(contentItems.length);

    // Verify labels match
    const desktopLabels = Array.from(desktopLinks.querySelectorAll("a")).map((a) => a.textContent);
    expect(desktopLabels).toEqual(contentItems.map((i) => i.label));
  });

  it("renders content.contact_info in drawer", () => {
    const { container } = render(
      <Nav agent={AGENT} navigation={CONTENT.navigation} contactInfo={CONTENT.contact_info} />
    );
    const drawerContact = container.querySelector(".drawer-contact") as HTMLElement;
    const hrefs = Array.from(drawerContact.querySelectorAll("a")).map((l) => l.getAttribute("href"));
    expect(hrefs).toContain("tel:5551234567");
    expect(hrefs).toContain("tel:7322512500,714");
    expect(hrefs).toContain("mailto:jane@example.com");
  });
});
