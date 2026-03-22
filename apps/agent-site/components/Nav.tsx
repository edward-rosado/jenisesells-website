"use client";

import { useState, useEffect, useRef } from "react";
import { usePathname, useSearchParams } from "next/navigation";
import Image from "next/image";
import Link from "next/link";
import type { AccountConfig, NavigationConfig, ContactMethod } from "@/features/config/types";
import { useFocusTrap } from "@/lib/use-focus-trap";
import { safeMailtoHref, safeTelHref } from "@/lib/safe-contact";

interface NavProps {
  account: AccountConfig;
  navigation?: NavigationConfig;
  /** Set of enabled section IDs (e.g. "features", "testimonials") — nav items linking to disabled sections are hidden */
  enabledSections?: Set<string>;
}

function OfficeIcon({ size = 14 }: { size?: number }) {
  return (
    <svg aria-hidden="true" width={size} height={size} fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24" style={{ flexShrink: 0 }}>
      <path d="M3 21h18M9 8h1M9 12h1M9 16h1M14 8h1M14 12h1M5 21V5a2 2 0 012-2h10a2 2 0 012 2v16" />
    </svg>
  );
}

function PhoneIcon({ size = 14 }: { size?: number }) {
  return (
    <svg aria-hidden="true" width={size} height={size} fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24" style={{ flexShrink: 0 }}>
      <path d="M22 16.92v3a2 2 0 01-2.18 2 19.79 19.79 0 01-8.63-3.07 19.5 19.5 0 01-6-6 19.79 19.79 0 01-3.07-8.67A2 2 0 014.11 2h3a2 2 0 012 1.72c.127.96.361 1.903.7 2.81a2 2 0 01-.45 2.11L8.09 9.91a16 16 0 006 6l1.27-1.27a2 2 0 012.11-.45c.907.339 1.85.573 2.81.7A2 2 0 0122 16.92z" />
    </svg>
  );
}

function EmailIcon({ size = 14 }: { size?: number }) {
  return (
    <svg aria-hidden="true" width={size} height={size} fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24" style={{ flexShrink: 0 }}>
      <rect x="2" y="4" width="20" height="16" rx="2" />
      <path d="M22 7l-10 7L2 7" />
    </svg>
  );
}

/** Default section nav links — used when content.navigation is not provided */
export const DEFAULT_NAV_ITEMS = [
  { label: "Stats", href: "#stats", enabled: false },
  { label: "Why Choose Me", href: "#features", enabled: true },
  { label: "How It Works", href: "#steps", enabled: true },
  { label: "Recent Sales", href: "#gallery", enabled: true },
  { label: "Testimonials", href: "#testimonials", enabled: true },
  { label: "Profiles", href: "#profiles", enabled: false },
  { label: "Ready to Move?", href: "#contact_form", enabled: true },
  { label: "About", href: "#about", enabled: true },
];

/** Build a tel: href from a phone value and optional extension */
function buildTelHref(value: string, ext?: string | null): string {
  return safeTelHref(value, ext ?? undefined);
}

/** Format a phone number with its extension for display */
function formatPhoneDisplay(value: string, ext?: string | null): string {
  return ext ? `${value} ext ${ext}` : value;
}

export function Nav({ account, navigation, enabledSections }: NavProps) {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const { branding } = account;
  const hamburgerRef = useRef<HTMLButtonElement>(null);
  const contactBtnRef = useRef<HTMLButtonElement>(null);
  // drawerRef removed — useFocusTrap handles drawer focus management
  const focusTrapRef = useFocusTrap(drawerOpen);
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const isHome = pathname === "/";
  const qs = searchParams?.toString() ?? "";

  // Resolve identity for display
  const displayName = account.agent?.name ?? account.broker?.name ?? account.brokerage.name;
  const tagline = account.agent?.tagline;

  function toggleDrawer() {
    setDrawerOpen((prev) => !prev);
  }

  function closeDrawer() {
    setDrawerOpen(false);
  }

  // Escape key closes drawer
  useEffect(() => {
    if (!drawerOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") setDrawerOpen(false);
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [drawerOpen]);

  // useFocusTrap handles initial focus and return-focus on close

  const navItems = navigation?.items ?? DEFAULT_NAV_ITEMS;
  const enabledItems = navItems.filter((item) => {
    if (!item.enabled) return false;
    // If enabledSections is provided, filter out hash links to missing sections
    if (enabledSections && item.href.startsWith("#")) {
      const sectionId = item.href.slice(1);
      return enabledSections.has(sectionId);
    }
    return true;
  });
  const qsSuffix = qs ? `?${qs}` : "";
  const sections = enabledItems.map((item) => ({
    label: item.label,
    href: item.href,
  }));

  // Resolve contact methods from account.contact_info or fallback
  const contacts = account.contact_info ?? buildFallbackContacts(account);
  const preferredPhone = contacts.find((c) => c.type === "phone" && c.is_preferred)
    ?? contacts.find((c) => c.type === "phone");
  const emails = contacts.filter((c) => c.type === "email");
  const phones = contacts.filter((c) => c.type === "phone");

  return (
    <>
      <style>{`
        html { scroll-behavior: smooth; }
        section[id] { scroll-margin-top: 80px; }

        /* Tablet: 769-1024px — section links visible, contact pills hidden, "Contact Me" button shown */
        @media (max-width: 1024px) {
          .nav-contact { display: none !important; }
          .nav-contact-btn { display: flex !important; }
          .nav-logo { height: 50px !important; }
          .nav-desktop-links a { font-size: 12px !important; padding: 6px 6px !important; }
        }

        /* Mobile + iPad Air: <=834px — section links hidden, hamburger shown, "Contact Me" button hidden */
        @media (max-width: 834px) {
          .nav-desktop-links { display: none !important; }
          .nav-contact-btn { display: none !important; }
          .nav-mobile-call { display: flex !important; }
          .nav-hamburger { display: block !important; }
          .nav-logo { height: 32px !important; }
          section { padding-top: 35px !important; padding-bottom: 35px !important; padding-left: 16px !important; padding-right: 16px !important; }
          section:first-of-type { padding-top: 70px !important; }
          section h1 { font-size: 28px !important; }
          section h2 { font-size: 22px !important; }
          section p { font-size: 15px !important; }
        }

        /* Drawer nav links: hidden on tablet/desktop (section links already in nav bar) */
        @media (min-width: 835px) {
          .drawer-nav-links { display: none !important; }
        }
      `}</style>
      <nav
        aria-label="Main navigation"
        style={{
          background: "var(--color-primary)",
          padding: "10px 24px",
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          position: "fixed",
          width: "100%",
          top: 0,
          zIndex: 1000,
          boxSizing: "border-box",
        }}
      >
        <Link href={isHome ? `${qsSuffix}#hero` : `/${qsSuffix}`} style={{ display: "flex", alignItems: "center", gap: "10px", textDecoration: "none" }}>
          {branding.logo_url ? (
            <div style={{ background: "white", borderRadius: "6px", padding: "4px 8px", display: "flex", alignItems: "center" }}>
              <Image
                src={branding.logo_url}
                alt={account.brokerage.name || "Brokerage logo"}
                width={240}
                height={60}
                className="nav-logo"
                style={{ height: "60px", width: "auto" }}
                priority
              />
            </div>
          ) : (
            <span
              className="nav-tagline"
              style={{ color: "var(--color-accent)", fontSize: "14px", fontWeight: 600, letterSpacing: "1px" }}
            >
              {tagline?.toUpperCase() || displayName.toUpperCase()}
            </span>
          )}
        </Link>

        {/* Desktop section links */}
        <div className="nav-desktop-links" style={{ display: "flex", alignItems: "center", gap: "4px", flex: 1, justifyContent: "center" }}>
          {sections.map((s) => (
            <a
              key={s.href}
              href={s.href}
              style={{
                color: "rgba(255,255,255,0.85)",
                fontSize: "13px",
                fontWeight: 500,
                padding: "6px 10px",
                borderRadius: "6px",
                textDecoration: "none",
                whiteSpace: "nowrap",
                transition: "color 0.2s",
              }}
              onMouseEnter={(e) => { e.currentTarget.style.color = "var(--color-accent)"; }}
              onMouseLeave={(e) => { e.currentTarget.style.color = "rgba(255,255,255,0.85)"; }}
            >
              {s.label}
            </a>
          ))}
        </div>

        <div style={{ display: "flex", alignItems: "center", gap: "12px" }}>
          {/* Desktop contact links — visible >1024px */}
          <div className="nav-contact" style={{ display: "flex", alignItems: "center", gap: "8px" }}>
            {emails[0] && (
              <a
                href={safeMailtoHref(emails[0].value)}
                aria-label={`Email ${emails[0].value}`}
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  gap: "6px",
                  background: "rgba(255,255,255,0.1)",
                  color: "white",
                  padding: "8px 16px",
                  borderRadius: "25px",
                  fontWeight: 600,
                  fontSize: "13px",
                  whiteSpace: "nowrap",
                  textDecoration: "none",
                  border: "1px solid rgba(255,255,255,0.2)",
                  minHeight: "44px",
                  boxSizing: "border-box",
                }}
              >
                <EmailIcon /> {emails[0].value}
              </a>
            )}
            {preferredPhone && (
              <a
                href={buildTelHref(preferredPhone.value, preferredPhone.ext)}
                aria-label={`Call ${formatPhoneDisplay(preferredPhone.value, preferredPhone.ext)}`}
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  gap: "6px",
                  background: "var(--color-accent)",
                  color: "var(--color-primary)",
                  padding: "8px 16px",
                  borderRadius: "25px",
                  fontWeight: 700,
                  fontSize: "14px",
                  whiteSpace: "nowrap",
                  textDecoration: "none",
                  minHeight: "44px",
                  boxSizing: "border-box",
                }}
              >
                <PhoneIcon /> {preferredPhone.value}
              </a>
            )}
          </div>

          {/* Tablet "Contact Me" button — visible 769-1024px */}
          <button
            ref={contactBtnRef}
            className="nav-contact-btn"
            onClick={toggleDrawer}
            aria-label="Contact information"
            aria-expanded={drawerOpen}
            aria-controls="nav-drawer"
            style={{
              display: "none",
              alignItems: "center",
              gap: "6px",
              background: "var(--color-accent)",
              color: "var(--color-primary)",
              padding: "8px 18px",
              borderRadius: "25px",
              fontWeight: 700,
              fontSize: "13px",
              border: "none",
              cursor: "pointer",
              whiteSpace: "nowrap",
              minHeight: "44px",
            }}
          >
            <PhoneIcon /> Contact Me
          </button>

          {/* Mobile call button — visible <=768px */}
          {preferredPhone && (
            <a
              href={buildTelHref(preferredPhone.value, preferredPhone.ext)}
              aria-label={`Call ${formatPhoneDisplay(preferredPhone.value, preferredPhone.ext)}`}
              className="nav-mobile-call"
              style={{
                display: "none",
                alignItems: "center",
                gap: "6px",
                background: "var(--color-accent)",
                color: "var(--color-primary)",
                padding: "6px 14px",
                borderRadius: "20px",
                fontWeight: 700,
                fontSize: "13px",
                textDecoration: "none",
                whiteSpace: "nowrap",
              }}
            >
              <PhoneIcon /> {preferredPhone.value}
            </a>
          )}

          {/* Hamburger button — visible <=768px */}
          <button
            ref={hamburgerRef}
            className="nav-hamburger"
            onClick={toggleDrawer}
            aria-label={drawerOpen ? "Close menu" : "Open menu"}
            aria-expanded={drawerOpen}
            aria-controls="nav-drawer"
            style={{
              display: "none",
              background: "none",
              border: "none",
              cursor: "pointer",
              padding: "8px",
              minWidth: "44px",
              minHeight: "44px",
              zIndex: 1100,
            }}
          >
            <span style={{
              display: "block", width: "26px", height: "3px", background: "var(--color-accent)",
              margin: "5px 0", borderRadius: "3px", transition: "all 0.3s",
              transform: drawerOpen ? "rotate(45deg) translate(5px, 6px)" : "none",
            }} />
            <span style={{
              display: "block", width: "26px", height: "3px", background: "var(--color-accent)",
              margin: "5px 0", borderRadius: "3px", transition: "all 0.3s",
              opacity: drawerOpen ? 0 : 1,
            }} />
            <span style={{
              display: "block", width: "26px", height: "3px", background: "var(--color-accent)",
              margin: "5px 0", borderRadius: "3px", transition: "all 0.3s",
              transform: drawerOpen ? "rotate(-45deg) translate(5px, -6px)" : "none",
            }} />
          </button>
        </div>
      </nav>

      {/* Overlay */}
      {drawerOpen && (
        <div
          aria-hidden="true"
          onClick={closeDrawer}
          style={{
            position: "fixed",
            top: 0,
            left: 0,
            width: "100%",
            height: "100%",
            background: "rgba(0,0,0,0.5)",
            zIndex: 1050,
          }}
        />
      )}

      {/* Drawer — on mobile: nav links + contact; on tablet: contact only */}
      <div
        ref={focusTrapRef}
        id="nav-drawer"
        className="nav-drawer"
        role="dialog"
        aria-modal="true"
        aria-label="Navigation menu"
        style={{
          position: "fixed",
          top: 0,
          right: drawerOpen ? "0" : "-300px",
          width: "280px",
          height: "100%",
          background: "var(--color-primary)",
          zIndex: 1060,
          padding: "80px 30px 30px",
          transition: "right 0.3s ease, visibility 0.3s ease",
          overflowY: "auto",
          boxShadow: drawerOpen ? "-4px 0 20px rgba(0,0,0,0.3)" : "none",
          visibility: drawerOpen ? "visible" : "hidden",
        }}
      >
        {/* Section nav links — hidden on tablet via CSS (already in nav bar) */}
        <div className="drawer-nav-links">
          {sections.map((s) => (
            <a
              key={s.href}
              href={s.href}
              onClick={closeDrawer}
              style={{
                display: "block",
                color: "white",
                fontSize: "16px",
                padding: "14px 0",
                borderBottom: "1px solid rgba(255,255,255,0.15)",
                textDecoration: "none",
              }}
            >
              {s.label}
            </a>
          ))}
        </div>

        {/* Contact info — always visible in drawer */}
        <div className="drawer-contact">
          {phones.map((phone, i) => (
            <a
              key={`phone-${i}`}
              href={buildTelHref(phone.value, phone.ext)}
              aria-label={`Call ${formatPhoneDisplay(phone.value, phone.ext)}`}
              style={{
                display: "flex",
                alignItems: "center",
                gap: "10px",
                background: phone.is_preferred ? "var(--color-accent)" : "rgba(255,255,255,0.1)",
                color: phone.is_preferred ? "var(--color-primary)" : "var(--color-accent)",
                padding: "12px 18px",
                borderRadius: "10px",
                fontWeight: phone.is_preferred ? 700 : 600,
                fontSize: phone.is_preferred ? "15px" : "14px",
                marginTop: i === 0 ? "20px" : "8px",
                textDecoration: "none",
                justifyContent: "center",
                ...(phone.is_preferred ? {} : { border: "1px solid rgba(255,255,255,0.2)" }),
              }}
            >
              {phone.label?.includes("Office") ? <OfficeIcon size={18} /> : <PhoneIcon size={18} />}
              {formatPhoneDisplay(phone.value, phone.ext)}
            </a>
          ))}

          {emails.map((email, i) => (
            <a
              key={`email-${i}`}
              href={safeMailtoHref(email.value)}
              aria-label={`Email ${email.value}`}
              style={{
                display: "flex",
                alignItems: "center",
                gap: "10px",
                background: "rgba(255,255,255,0.1)",
                color: "white",
                padding: "12px 18px",
                borderRadius: "10px",
                fontWeight: 600,
                fontSize: "14px",
                marginTop: "8px",
                textDecoration: "none",
                justifyContent: "center",
                border: "1px solid rgba(255,255,255,0.2)",
              }}
            >
              <EmailIcon size={18} />
              {email.value}
            </a>
          ))}
        </div>
      </div>
    </>
  );
}

/** Build fallback ContactMethod[] from account when contact_info is not provided */
function buildFallbackContacts(account: AccountConfig): ContactMethod[] {
  const contacts: ContactMethod[] = [];
  const agent = account.agent;
  if (agent?.phone) {
    contacts.push({ type: "phone", value: agent.phone, label: "Cell Phone", is_preferred: true });
  }
  if (account.brokerage.office_phone) {
    const extMatch = account.brokerage.office_phone.match(/ext\s*(\d+)/i);
    const phoneValue = account.brokerage.office_phone.replace(/\s*ext\s*\d+/i, "").trim();
    contacts.push({ type: "phone", value: phoneValue, ext: extMatch?.[1] ?? null, label: "Office Phone", is_preferred: false });
  }
  if (agent?.email) {
    contacts.push({ type: "email", value: agent.email, label: "Email", is_preferred: false });
  }
  return contacts;
}
