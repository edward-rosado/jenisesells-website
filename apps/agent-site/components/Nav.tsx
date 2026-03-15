"use client";

import { useState, useEffect, useRef } from "react";
import Image from "next/image";
import type { AgentConfig } from "@/lib/types";

interface NavProps {
  agent: AgentConfig;
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

export function Nav({ agent }: NavProps) {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const { identity, branding } = agent;
  const hamburgerRef = useRef<HTMLButtonElement>(null);
  const drawerRef = useRef<HTMLDivElement>(null);

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

  // Focus management: focus first link on open, return to hamburger on close
  useEffect(() => {
    if (drawerOpen) {
      const firstFocusable = drawerRef.current?.querySelector<HTMLElement>(
        'a, button, [tabindex]:not([tabindex="-1"])'
      );
      firstFocusable?.focus();
    } else {
      hamburgerRef.current?.focus();
    }
  }, [drawerOpen]);

  const sections = [
    { label: "Why Choose Me", href: "#services" },
    { label: "How It Works", href: "#how-it-works" },
    { label: "Recent Sales", href: "#sold" },
    { label: "Testimonials", href: "#testimonials" },
    { label: "Get Your Home Value", href: "#cma-form" },
    { label: "About", href: "#about" },
  ];

  return (
    <>
      <style>{`
        html { scroll-behavior: smooth; }
        section[id] { scroll-margin-top: 70px; }
        @media (max-width: 768px) {
          .nav-contact { display: none !important; }
          .nav-mobile-call { display: flex !important; }
          .nav-hamburger { display: block !important; }
          .nav-logo { height: 32px !important; }
          section { padding-top: 35px !important; padding-bottom: 35px !important; padding-left: 16px !important; padding-right: 16px !important; }
          section:first-of-type { padding-top: 70px !important; }
          section h1 { font-size: 28px !important; }
          section h2 { font-size: 22px !important; }
          section p { font-size: 15px !important; }
        }
      `}</style>
      <nav
        aria-label="Main navigation"
        style={{
          background: "var(--color-primary)",
          padding: "10px 16px",
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
        <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
          {branding.logo_url ? (
            <div style={{ background: "white", borderRadius: "6px", padding: "4px 8px", display: "flex", alignItems: "center" }}>
              <Image
                src={branding.logo_url}
                alt={identity.brokerage || "Brokerage logo"}
                width={160}
                height={40}
                className="nav-logo"
                style={{ height: "40px", width: "auto" }}
                priority
              />
            </div>
          ) : (
            <span
              className="nav-tagline"
              style={{ color: "var(--color-accent)", fontSize: "14px", fontWeight: 600, letterSpacing: "1px" }}
            >
              {identity.tagline?.toUpperCase() || identity.name.toUpperCase()}
            </span>
          )}
          {identity.brokerage && (
            <span
              className="nav-brokerage"
              style={{ color: "rgba(255,255,255,0.85)", fontSize: "11px", fontWeight: 500 }}
            >
              {identity.brokerage}
            </span>
          )}
        </div>

        <div style={{ display: "flex", alignItems: "center", gap: "12px" }}>
          {/* Desktop contact links */}
          <div className="nav-contact" style={{ display: "flex", alignItems: "center", gap: "8px" }}>
            {identity.email && (
              <a
                href={`mailto:${identity.email}`}
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
                <EmailIcon /> {identity.email}
              </a>
            )}
            {identity.phone && (
              <a
                href={`tel:${identity.phone.replace(/\D/g, "")}`}
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
                <PhoneIcon /> {identity.phone}
              </a>
            )}
          </div>

          {/* Mobile call button */}
          {identity.phone && (
            <a
              href={`tel:${identity.phone.replace(/\D/g, "")}`}
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
              <PhoneIcon /> {identity.phone}
            </a>
          )}

          {/* Hamburger button */}
          <button
            ref={hamburgerRef}
            className="nav-hamburger"
            onClick={toggleDrawer}
            aria-label="Menu"
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

      {/* Mobile drawer */}
      <div
        ref={drawerRef}
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

        {identity.phone && (
          <a
            href={`tel:${identity.phone.replace(/\D/g, "")}`}
            style={{
              display: "flex",
              alignItems: "center",
              gap: "10px",
              background: "var(--color-accent)",
              color: "var(--color-primary)",
              padding: "12px 18px",
              borderRadius: "10px",
              fontWeight: 700,
              fontSize: "15px",
              marginTop: "20px",
              textDecoration: "none",
              justifyContent: "center",
            }}
          >
            <PhoneIcon size={18} />
            {identity.phone}
          </a>
        )}

        {identity.office_phone && (
          <a
            href={`tel:${identity.office_phone.replace(/[^0-9]/g, "")}`}
            style={{
              display: "flex",
              alignItems: "center",
              gap: "10px",
              background: "rgba(255,255,255,0.1)",
              color: "var(--color-accent)",
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
            <OfficeIcon size={18} />
            {identity.office_phone}
          </a>
        )}

        {identity.email && (
          <a
            href={`mailto:${identity.email}`}
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
            {identity.email}
          </a>
        )}
      </div>
    </>
  );
}
