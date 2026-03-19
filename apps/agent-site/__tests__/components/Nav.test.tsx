/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { Nav, DEFAULT_NAV_ITEMS } from "@/components/Nav";
import { ACCOUNT, ACCOUNT_MINIMAL, ACCOUNT_BROKER_ONLY, ACCOUNT_BROKERAGE_ONLY, CONTENT } from "./fixtures";

const mockPathname = vi.fn(() => "/");
const mockSearchParams = vi.fn(() => new URLSearchParams());
vi.mock("next/navigation", () => ({
  usePathname: () => mockPathname(),
  useSearchParams: () => mockSearchParams(),
}));

describe("Nav", () => {
  // --- Fallback behavior (no content props — legacy path) ---

  it("renders the agent tagline in uppercase when tagline is present", () => {
    render(<Nav account={ACCOUNT} />);
    expect(screen.getByText("YOUR DREAM HOME AWAITS")).toBeInTheDocument();
  });

  it("falls back to agent name in uppercase when tagline is absent", () => {
    render(<Nav account={ACCOUNT_MINIMAL} />);
    expect(screen.getByText("BOB JONES")).toBeInTheDocument();
  });

  it("renders email link from fallback identity", () => {
    render(<Nav account={ACCOUNT} />);
    const emailLinks = screen.getAllByRole("link", { name: /jane@example\.com/ });
    expect(emailLinks.length).toBeGreaterThanOrEqual(1);
    expect(emailLinks[0]).toHaveAttribute("href", "mailto:jane@example.com");
  });

  it("renders phone links from fallback identity", () => {
    render(<Nav account={ACCOUNT} />);
    const phoneLinks = screen.getAllByRole("link", { name: /555-123-4567/ });
    expect(phoneLinks.length).toBeGreaterThanOrEqual(1);
    expect(phoneLinks[0]).toHaveAttribute("href", "tel:5551234567");
  });

  it("does not render email link when email is absent", () => {
    const accountNoEmail = {
      ...ACCOUNT_MINIMAL,
      agent: { ...ACCOUNT_MINIMAL.agent!, email: "" },
    };
    render(<Nav account={accountNoEmail} />);
    expect(screen.queryByRole("link", { name: /[@]/ })).not.toBeInTheDocument();
  });

  it("does not render phone link when phone is absent", () => {
    const accountNoPhone = {
      ...ACCOUNT_MINIMAL,
      agent: { ...ACCOUNT_MINIMAL.agent!, phone: "" },
    };
    render(<Nav account={accountNoPhone} />);
    const links = screen.getAllByRole("link");
    const phoneLinks = links.filter((l) => l.getAttribute("href")?.startsWith("tel:"));
    expect(phoneLinks).toHaveLength(0);
  });

  it("renders office phone link from contact_info", () => {
    const { container } = render(<Nav account={ACCOUNT} />);
    // ACCOUNT.contact_info has office phone with ext 714
    const officeLink = container.querySelector('a[href="tel:7322512500,714"]');
    expect(officeLink).toBeInTheDocument();
    expect(officeLink?.textContent).toContain("251-2500");
  });

  it("does not render office phone link when office_phone is absent", () => {
    render(<Nav account={ACCOUNT_MINIMAL} />);
    const links = screen.getAllByRole("link");
    const officeLinks = links.filter((l) => l.getAttribute("href")?.startsWith("tel:") && l.textContent?.includes("251"));
    expect(officeLinks).toHaveLength(0);
  });

  // --- Content-driven nav ---

  it("renders nav items from content.navigation", () => {
    const customNav = {
      items: [
        { label: "Custom Link 1", href: "#custom-1", enabled: true },
        { label: "Custom Link 2", href: "#custom-2", enabled: true },
      ],
    };
    const { container } = render(<Nav account={ACCOUNT} navigation={customNav} />);
    const desktopLinks = container.querySelector(".nav-desktop-links") as HTMLElement;
    const links = desktopLinks.querySelectorAll("a");
    expect(links).toHaveLength(2);
    expect(links[0].textContent).toBe("Custom Link 1");
    expect(links[0]).toHaveAttribute("href", "#custom-1");
    expect(links[1].textContent).toBe("Custom Link 2");
  });

  it("passes through non-hash hrefs without prefix or query suffix", () => {
    mockPathname.mockReturnValue("/");
    const customNav = {
      items: [
        { label: "Blog", href: "/blog", enabled: true },
        { label: "Home", href: "#hero", enabled: true },
      ],
    };
    const { container } = render(<Nav account={ACCOUNT} navigation={customNav} />);
    const desktopLinks = container.querySelector(".nav-desktop-links") as HTMLElement;
    const links = desktopLinks.querySelectorAll("a");
    expect(links[0]).toHaveAttribute("href", "/blog");
    expect(links[1]).toHaveAttribute("href", "#hero");
  });

  it("renders contact info from account.contact_info", () => {
    const accountWithContacts = {
      ...ACCOUNT,
      contact_info: [
        { type: "phone" as const, value: "(999) 111-2222", label: "Cell", is_preferred: true },
        { type: "email" as const, value: "test@test.com", label: "Work Email", is_preferred: false },
      ],
    };
    const { container } = render(<Nav account={accountWithContacts} />);
    const drawerContact = container.querySelector(".drawer-contact") as HTMLElement;
    const hrefs = Array.from(drawerContact.querySelectorAll("a")).map((l) => l.getAttribute("href"));
    expect(hrefs).toContain("tel:9991112222");
    expect(hrefs).toContain("mailto:test@test.com");
  });

  it("renders phone with extension in drawer", () => {
    const accountWithExt = {
      ...ACCOUNT,
      contact_info: [
        { type: "phone" as const, value: "(732) 251-2500", ext: "714", label: "Office Phone", is_preferred: false },
      ],
    };
    const { container } = render(<Nav account={accountWithExt} />);
    const drawerContact = container.querySelector(".drawer-contact") as HTMLElement;
    const link = drawerContact.querySelector("a") as HTMLElement;
    expect(link).toHaveAttribute("href", "tel:7322512500,714");
    expect(link.textContent).toContain("ext 714");
  });

  it("preferred phone gets accent background in drawer", () => {
    const accountWithPhones = {
      ...ACCOUNT,
      contact_info: [
        { type: "phone" as const, value: "555-111-0000", label: "Cell", is_preferred: true },
        { type: "phone" as const, value: "555-222-0000", label: "Office", is_preferred: false },
      ],
    };
    const { container } = render(<Nav account={accountWithPhones} />);
    const drawerContact = container.querySelector(".drawer-contact") as HTMLElement;
    const links = drawerContact.querySelectorAll("a");
    // Preferred phone gets accent background
    expect((links[0] as HTMLElement).style.background).toBe("var(--color-accent)");
    // Non-preferred gets subdued
    expect((links[1] as HTMLElement).style.background).toBe("rgba(255, 255, 255, 0.1)");
  });

  it("uses DEFAULT_NAV_ITEMS when navigation prop is not provided", () => {
    const { container } = render(<Nav account={ACCOUNT} />);
    const desktopLinks = container.querySelector(".nav-desktop-links") as HTMLElement;
    const links = desktopLinks.querySelectorAll("a");
    const enabledDefaults = DEFAULT_NAV_ITEMS.filter((i) => i.enabled);
    expect(links).toHaveLength(enabledDefaults.length);
    expect(links[0].textContent).toBe(enabledDefaults[0].label);
  });

  it("filters nav items by enabledSections when provided", () => {
    const customNav = {
      items: [
        { label: "Features", href: "#features", enabled: true },
        { label: "Stats", href: "#stats", enabled: true },
        { label: "Gallery", href: "#gallery", enabled: true },
      ],
    };
    // Only features and gallery are enabled sections — stats should be hidden
    const enabledSections = new Set(["features", "gallery"]);
    const { container } = render(<Nav account={ACCOUNT} navigation={customNav} enabledSections={enabledSections} />);
    const desktopLinks = container.querySelector(".nav-desktop-links") as HTMLElement;
    const links = desktopLinks.querySelectorAll("a");
    expect(links).toHaveLength(2);
    expect(links[0].textContent).toBe("Features");
    expect(links[1].textContent).toBe("Gallery");
  });

  it("drawer also renders custom nav items from content", () => {
    const customNav = {
      items: [
        { label: "Alpha", href: "#alpha", enabled: true },
        { label: "Beta", href: "#beta", enabled: true },
        { label: "Gamma", href: "#gamma", enabled: true },
      ],
    };
    const { container } = render(<Nav account={ACCOUNT} navigation={customNav} />);
    const drawerLinks = container.querySelector(".drawer-nav-links") as HTMLElement;
    const links = drawerLinks.querySelectorAll("a");
    expect(links).toHaveLength(3);
    expect(links[0].textContent).toBe("Alpha");
    expect(links[2].textContent).toBe("Gamma");
  });

  // --- Logo and branding ---

  it("renders logo when logo_url is present", () => {
    const accountWithLogo = {
      ...ACCOUNT,
      branding: { ...ACCOUNT.branding, logo_url: "/images/logo.png" },
    };
    render(<Nav account={accountWithLogo} />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Best Homes Realty");
  });

  it("does not render logo when logo_url is absent", () => {
    render(<Nav account={ACCOUNT} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("uses 'Brokerage logo' as image alt text when brokerage name is absent", () => {
    const accountWithLogoNoBrokerage = {
      ...ACCOUNT_MINIMAL,
      branding: { ...ACCOUNT_MINIMAL.branding, logo_url: "/images/logo.png" },
      brokerage: { ...ACCOUNT_MINIMAL.brokerage, name: undefined as unknown as string },
    };
    render(<Nav account={accountWithLogoNoBrokerage as never} />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Brokerage logo");
  });

  it("logo links to #hero on home page", () => {
    const accountWithLogo = {
      ...ACCOUNT,
      branding: { ...ACCOUNT.branding, logo_url: "/images/logo.png" },
    };
    render(<Nav account={accountWithLogo} />);
    const img = screen.getByRole("img");
    const homeLink = img.closest("a");
    expect(homeLink).toHaveAttribute("href", "#hero");
  });

  it("does not render brokerage name in nav (logo is sufficient)", () => {
    render(<Nav account={ACCOUNT} />);
    expect(screen.queryByText("Best Homes Realty")).not.toBeInTheDocument();
  });

  // --- Accessibility ---

  it("has nav landmark role", () => {
    render(<Nav account={ACCOUNT} />);
    expect(screen.getByRole("navigation")).toBeInTheDocument();
  });

  it("nav has aria-label for accessibility", () => {
    render(<Nav account={ACCOUNT} />);
    expect(screen.getByRole("navigation")).toHaveAttribute("aria-label", "Main navigation");
  });

  it("renders drawer as dialog with aria-modal", () => {
    const { container } = render(<Nav account={ACCOUNT} />);
    const drawer = container.querySelector(".nav-drawer") as HTMLElement;
    expect(drawer).toHaveAttribute("role", "dialog");
    expect(drawer).toHaveAttribute("aria-modal", "true");
    expect(drawer).toHaveAttribute("aria-label", "Navigation menu");
  });

  it("sets aria-expanded on hamburger button", () => {
    render(<Nav account={ACCOUNT} />);
    const hamburger = screen.getByLabelText("Open menu");

    expect(hamburger).toHaveAttribute("aria-expanded", "false");
    fireEvent.click(hamburger);
    expect(hamburger).toHaveAttribute("aria-expanded", "true");
    fireEvent.click(hamburger);
    expect(hamburger).toHaveAttribute("aria-expanded", "false");
  });

  // --- Hamburger and drawer behavior ---

  it("renders hamburger menu button (hidden on desktop via CSS)", () => {
    render(<Nav account={ACCOUNT} />);
    const hamburger = screen.getByLabelText("Open menu");
    expect(hamburger).toBeInTheDocument();
    expect(hamburger.tagName).toBe("BUTTON");
  });

  it("renders section links in the drawer", () => {
    const { container } = render(<Nav account={ACCOUNT} />);
    const drawer = container.querySelector(".nav-drawer") as HTMLElement;
    expect(drawer.querySelectorAll("a[href*='#']")).toHaveLength(6);
  });

  it("toggles drawer open and closed on hamburger click", () => {
    const { container } = render(<Nav account={ACCOUNT} />);
    const hamburger = screen.getByLabelText("Open menu");
    const drawer = container.querySelector(".nav-drawer") as HTMLElement;

    fireEvent.click(hamburger);
    expect(drawer.style.visibility).toBe("visible");

    fireEvent.click(hamburger);
    expect(drawer.style.visibility).toBe("hidden");
  });

  it("closes drawer when a section link is clicked", () => {
    const { container } = render(<Nav account={ACCOUNT} />);
    const hamburger = screen.getByLabelText("Open menu");

    fireEvent.click(hamburger);

    const drawer = container.querySelector(".nav-drawer") as HTMLElement;
    expect(drawer.style.visibility).toBe("visible");

    const drawerLink = drawer.querySelector("a[href*='#']") as HTMLElement;
    fireEvent.click(drawerLink);

    expect(drawer.style.visibility).toBe("hidden");
  });

  it("closes drawer when overlay is clicked", () => {
    const { container } = render(<Nav account={ACCOUNT} />);
    const hamburger = screen.getByLabelText("Open menu");

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
    const { container } = render(<Nav account={ACCOUNT} />);
    const hamburger = screen.getByLabelText("Open menu");

    fireEvent.click(hamburger);
    const drawer = container.querySelector(".nav-drawer") as HTMLElement;
    expect(drawer.style.visibility).toBe("visible");

    fireEvent.keyDown(document, { key: "Escape" });
    expect(drawer.style.visibility).toBe("hidden");
  });

  it("does not close drawer on non-Escape keys", () => {
    const { container } = render(<Nav account={ACCOUNT} />);
    const hamburger = screen.getByLabelText("Open menu");

    fireEvent.click(hamburger);
    const drawer = container.querySelector(".nav-drawer") as HTMLElement;
    expect(drawer.style.visibility).toBe("visible");

    fireEvent.keyDown(document, { key: "Tab" });
    expect(drawer.style.visibility).toBe("visible");
  });

  it("focuses first link when drawer opens", () => {
    const { container } = render(<Nav account={ACCOUNT} />);
    const hamburger = screen.getByLabelText("Open menu");

    fireEvent.click(hamburger);
    const drawer = container.querySelector(".nav-drawer") as HTMLElement;
    const firstLink = drawer.querySelector("a") as HTMLElement;
    expect(document.activeElement).toBe(firstLink);
  });

  // --- Routing ---

  it("uses hash-only links on the homepage", () => {
    mockPathname.mockReturnValue("/");
    const { container } = render(<Nav account={ACCOUNT} />);
    const desktopLinks = container.querySelector(".nav-desktop-links") as HTMLElement;
    const link = desktopLinks.querySelector("a");
    expect(link).toHaveAttribute("href", "#features");
  });

  it("uses same-page anchor links on non-homepage (no / prefix)", () => {
    mockPathname.mockReturnValue("/terms");
    const { container } = render(<Nav account={ACCOUNT} />);
    const desktopLinks = container.querySelector(".nav-desktop-links") as HTMLElement;
    const link = desktopLinks.querySelector("a");
    expect(link).toHaveAttribute("href", "#features");
  });

  it("uses same-page anchor links on agent sub-pages", () => {
    mockPathname.mockReturnValue("/agents/agent-b");
    mockSearchParams.mockReturnValue(new URLSearchParams("accountId=test-brokerage"));
    const { container } = render(<Nav account={ACCOUNT} />);
    const desktopLinks = container.querySelector(".nav-desktop-links") as HTMLElement;
    const link = desktopLinks.querySelector("a");
    expect(link).toHaveAttribute("href", "#features");
    mockSearchParams.mockReturnValue(new URLSearchParams());
  });

  it("tagline links to #hero on home page", () => {
    mockPathname.mockReturnValue("/");
    render(<Nav account={ACCOUNT} />);
    const tagline = screen.getByText("YOUR DREAM HOME AWAITS");
    const homeLink = tagline.closest("a");
    expect(homeLink).toHaveAttribute("href", "#hero");
  });

  it("logo links to homepage from sub-page", () => {
    mockPathname.mockReturnValue("/agents/agent-a");
    const accountWithLogo = {
      ...ACCOUNT,
      branding: { ...ACCOUNT.branding, logo_url: "/images/logo.png" },
    };
    render(<Nav account={accountWithLogo} />);
    const img = screen.getByRole("img");
    const homeLink = img.closest("a");
    expect(homeLink).toHaveAttribute("href", "/");
    mockPathname.mockReturnValue("/");
  });

  // --- Desktop links ---

  it("desktop link changes color on hover", () => {
    const { container } = render(<Nav account={ACCOUNT} />);
    const desktopLinks = container.querySelector(".nav-desktop-links") as HTMLElement;
    const link = desktopLinks.querySelector("a") as HTMLElement;

    fireEvent.mouseEnter(link);
    expect(link.style.color).toBe("var(--color-accent)");

    fireEvent.mouseLeave(link);
    expect(link.style.color).toBe("rgba(255, 255, 255, 0.85)");
  });

  it("renders desktop section links", () => {
    const { container } = render(<Nav account={ACCOUNT} />);
    const desktopLinks = container.querySelector(".nav-desktop-links");
    expect(desktopLinks).toBeInTheDocument();
    const links = desktopLinks?.querySelectorAll("a");
    expect(links?.length).toBe(6);
  });

  // --- Contact Me button ---

  it("renders Contact Me button for tablet view (hidden via CSS on other sizes)", () => {
    render(<Nav account={ACCOUNT} />);
    const contactBtn = screen.getByLabelText("Contact information");
    expect(contactBtn).toBeInTheDocument();
    expect(contactBtn.tagName).toBe("BUTTON");
    expect(contactBtn.textContent).toContain("Contact Me");
  });

  it("Contact Me button toggles drawer", () => {
    const { container } = render(<Nav account={ACCOUNT} />);
    const contactBtn = screen.getByLabelText("Contact information");
    const drawer = container.querySelector(".nav-drawer") as HTMLElement;

    fireEvent.click(contactBtn);
    expect(drawer.style.visibility).toBe("visible");

    fireEvent.click(contactBtn);
    expect(drawer.style.visibility).toBe("hidden");
  });

  it("drawer contains contact info section with phone, office, and email", () => {
    const { container } = render(<Nav account={ACCOUNT} />);
    const drawerContact = container.querySelector(".drawer-contact") as HTMLElement;
    expect(drawerContact).toBeInTheDocument();

    const links = drawerContact.querySelectorAll("a");
    const hrefs = Array.from(links).map((l) => l.getAttribute("href"));
    expect(hrefs).toContain("tel:5551234567");
    expect(hrefs).toContain("tel:7322512500,714"); // ACCOUNT has office phone with ext 714
    expect(hrefs).toContain("mailto:jane@example.com");
  });

  it("drawer nav links are inside .drawer-nav-links container", () => {
    const { container } = render(<Nav account={ACCOUNT} />);
    const navLinks = container.querySelector(".drawer-nav-links") as HTMLElement;
    expect(navLinks).toBeInTheDocument();
    expect(navLinks.querySelectorAll("a[href*='#']")).toHaveLength(6);
  });

  // --- Sync tests: desktop + drawer contact_form labels match DEFAULT_NAV_ITEMS ---

  it("desktop nav contact_form link label matches DEFAULT_NAV_ITEMS constant", () => {
    const { container } = render(<Nav account={ACCOUNT} />);
    const desktopLinks = container.querySelector(".nav-desktop-links") as HTMLElement;
    const contactFormLink = Array.from(desktopLinks.querySelectorAll("a")).find(
      (a) => a.getAttribute("href")?.includes("#contact_form")
    );
    const expected = DEFAULT_NAV_ITEMS.find((s) => s.href === "#contact_form")!.label;
    expect(contactFormLink?.textContent).toBe(expected);
  });

  it("mobile drawer contact_form link label matches DEFAULT_NAV_ITEMS constant", () => {
    const { container } = render(<Nav account={ACCOUNT} />);
    const drawerLinks = container.querySelector(".drawer-nav-links") as HTMLElement;
    const contactFormLink = Array.from(drawerLinks.querySelectorAll("a")).find(
      (a) => a.getAttribute("href")?.includes("#contact_form")
    );
    const expected = DEFAULT_NAV_ITEMS.find((s) => s.href === "#contact_form")!.label;
    expect(contactFormLink?.textContent).toBe(expected);
  });

  it("DEFAULT_NAV_ITEMS contact_form label is concise (fits nav bar)", () => {
    const contactFormItem = DEFAULT_NAV_ITEMS.find((s) => s.href === "#contact_form")!;
    expect(contactFormItem.label.length).toBeLessThanOrEqual(20);
  });

  // --- Content-driven: nav items from CONTENT fixture match ---

  it("renders content.navigation items in desktop and drawer", () => {
    const { container } = render(
      <Nav account={ACCOUNT} navigation={CONTENT.navigation} />
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

  it("renders account.contact_info in drawer", () => {
    const { container } = render(
      <Nav account={ACCOUNT} navigation={CONTENT.navigation} />
    );
    const drawerContact = container.querySelector(".drawer-contact") as HTMLElement;
    const hrefs = Array.from(drawerContact.querySelectorAll("a")).map((l) => l.getAttribute("href"));
    expect(hrefs).toContain("tel:5551234567");
    expect(hrefs).toContain("tel:7322512500,714");
    expect(hrefs).toContain("mailto:jane@example.com");
  });

  // --- Fallback contacts: buildFallbackContacts branch coverage ---

  it("builds fallback contacts with office phone without extension", () => {
    // No contact_info → triggers buildFallbackContacts; office_phone has no "ext" → extMatch is null
    const accountNoContactInfo = {
      ...ACCOUNT,
      contact_info: undefined,
      brokerage: { ...ACCOUNT.brokerage, office_phone: "(732) 555-0000" },
    };
    const { container } = render(<Nav account={accountNoContactInfo} />);
    const drawerContact = container.querySelector(".drawer-contact") as HTMLElement;
    const hrefs = Array.from(drawerContact.querySelectorAll("a")).map((l) => l.getAttribute("href"));
    // Office phone without extension: tel: with digits only, no comma
    expect(hrefs).toContain("tel:7325550000");
    // Also has agent cell and email from fallback
    expect(hrefs).toContain("tel:5551234567");
    expect(hrefs).toContain("mailto:jane@example.com");
  });

  it("falls back to broker name when no agent is defined", () => {
    render(<Nav account={ACCOUNT_BROKER_ONLY} />);
    expect(screen.getByText("SAM BROKER")).toBeInTheDocument();
  });

  it("falls back to brokerage name when no agent or broker is defined", () => {
    render(<Nav account={ACCOUNT_BROKERAGE_ONLY} />);
    expect(screen.getByText("BROKERAGE LLC")).toBeInTheDocument();
  });
});
