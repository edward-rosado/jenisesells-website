import Image from "next/image";
import type { AgentConfig } from "@/lib/types";

interface NavProps {
  agent: AgentConfig;
}

function OfficeIcon() {
  return (
    <svg
      width="16"
      height="16"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      viewBox="0 0 24 24"
      style={{ display: "inline", verticalAlign: "middle", marginRight: "4px" }}
    >
      <path d="M3 21h18M9 8h1M9 12h1M9 16h1M14 8h1M14 12h1M5 21V5a2 2 0 012-2h10a2 2 0 012 2v16" />
    </svg>
  );
}

function PhoneIcon() {
  return (
    <svg
      width="16"
      height="16"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      viewBox="0 0 24 24"
      style={{ display: "inline", verticalAlign: "middle", marginRight: "4px" }}
    >
      <path d="M22 16.92v3a2 2 0 01-2.18 2 19.79 19.79 0 01-8.63-3.07 19.5 19.5 0 01-6-6 19.79 19.79 0 01-3.07-8.67A2 2 0 014.11 2h3a2 2 0 012 1.72c.127.96.361 1.903.7 2.81a2 2 0 01-.45 2.11L8.09 9.91a16 16 0 006 6l1.27-1.27a2 2 0 012.11-.45c.907.339 1.85.573 2.81.7A2 2 0 0122 16.92z" />
    </svg>
  );
}

export function Nav({ agent }: NavProps) {
  const { identity, branding } = agent;
  return (
    <nav
      aria-label="Main navigation"
      style={{
        background: "var(--color-primary)",
        padding: "12px 40px",
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        position: "fixed",
        width: "100%",
        top: 0,
        zIndex: 1000,
      }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: "12px" }}>
        {branding.logo_url && (
          <Image
            src={branding.logo_url}
            alt={identity.brokerage || "Brokerage logo"}
            width={80}
            height={80}
            style={{ height: "80px", width: "auto" }}
            priority
          />
        )}
        <span style={{ color: "var(--color-accent)", fontSize: "14px", fontWeight: 600, letterSpacing: "1px" }}>
          {identity.tagline?.toUpperCase() || identity.name.toUpperCase()}
        </span>
      </div>
      <div style={{ display: "flex", alignItems: "center", gap: "20px" }}>
        {identity.email && (
          <a href={`mailto:${identity.email}`} style={{ color: "white", fontSize: "14px", fontWeight: 500 }}>
            {identity.email}
          </a>
        )}
        {identity.phone && (
          <a
            href={`tel:${identity.phone.replace(/\D/g, "")}`}
            style={{
              background: "var(--color-accent)",
              color: "var(--color-primary)",
              padding: "8px 18px",
              borderRadius: "25px",
              fontWeight: 700,
              fontSize: "15px",
              whiteSpace: "nowrap",
            }}
          >
            <PhoneIcon />
            {identity.phone}
          </a>
        )}
        {identity.office_phone && (
          <>
            <span style={{ color: "#ccc" }}>|</span>
            <a
              href={`tel:${identity.office_phone.replace(/[^0-9]/g, "")}`}
              style={{
                background: "var(--color-accent)",
                color: "var(--color-primary)",
                padding: "8px 18px",
                borderRadius: "25px",
                fontWeight: 700,
                fontSize: "15px",
                whiteSpace: "nowrap",
                textDecoration: "none",
              }}
            >
              <OfficeIcon />
              {identity.office_phone}
            </a>
          </>
        )}
      </div>
    </nav>
  );
}
