import type { AgentConfig } from "@/lib/types";

interface FooterProps {
  agent: AgentConfig;
}

function formatServiceAreas(areas: string[], state: string): string {
  const names = areas.map((a) => a.replace(/ County$/i, ""));
  if (names.length <= 2) {
    return `Serving ${names.join(" & ")} Counties, ${state}`;
  }
  const last = names.pop();
  return `Serving ${names.join(", ")} & ${last} Counties, ${state}`;
}

export function Footer({ agent }: FooterProps) {
  const { identity, location } = agent;
  return (
    <footer
      style={{
        background: "#1B5E20",
        color: "white",
        padding: "40px",
        textAlign: "center",
      }}
    >
      <p
        style={{
          fontSize: "22px",
          fontWeight: 700,
          color: "#C8A951",
          marginBottom: "5px",
        }}
      >
        {identity.name}{identity.title ? `, ${identity.title}` : ""}
      </p>
      <p
        style={{
          fontSize: "20px",
          fontWeight: 700,
          color: "white",
          marginBottom: "3px",
        }}
      >
        {identity.brokerage}
      </p>
      <p
        style={{
          fontSize: "14px",
          color: "white",
          marginBottom: "15px",
        }}
      >
        {identity.title || "Licensed Real Estate Salesperson"}{identity.license_id && ` | NJ License #${identity.license_id}`}
      </p>
      <p style={{ fontSize: "14px", color: "white", marginBottom: "3px" }}>
        <a
          href={`tel:${identity.phone.replace(/\D/g, "")}`}
          aria-label={`Call ${identity.name}`}
          style={{ color: "white", textDecoration: "none" }}
        >
          Cell: {identity.phone}
        </a>
        {identity.office_phone && (
          <>
            {"  |  "}
            <a
              href={`tel:${identity.office_phone.replace(/[^0-9]/g, "")}`}
              aria-label="Call office"
              style={{ color: "white", textDecoration: "none" }}
            >
              {identity.office_phone}
            </a>
          </>
        )}
      </p>
      <p style={{ fontSize: "14px", color: "white", marginBottom: "5px" }}>
        <a
          href={`mailto:${identity.email}`}
          aria-label={`Email ${identity.name}`}
          style={{ color: "white", textDecoration: "none" }}
        >
          {identity.email}
        </a>
      </p>
      {location.service_areas && location.service_areas.length > 0 && (
        <p style={{ fontSize: "13px", color: "rgba(255,255,255,0.8)", marginTop: "15px" }}>
          {formatServiceAreas(location.service_areas, location.state)}
        </p>
      )}
      <div
        style={{
          marginTop: "20px",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          gap: "8px",
          fontSize: "11px",
          color: "rgba(255,255,255,0.7)",
        }}
      >
        <svg
          aria-label="Equal Housing Opportunity logo"
          role="img"
          width="20"
          height="20"
          viewBox="0 0 64 64"
          fill="currentColor"
          xmlns="http://www.w3.org/2000/svg"
        >
          <path d="M32 4L2 30h10v28h40V30h10L32 4zm-12 48V28h24v24H20z" />
          <rect x="24" y="32" width="16" height="4" />
          <rect x="24" y="40" width="16" height="4" />
        </svg>
        <span>Equal Housing Opportunity</span>
      </div>
      <p
        style={{
          fontSize: "11px",
          color: "rgba(255,255,255,0.7)",
          marginTop: "20px",
          maxWidth: "700px",
          marginLeft: "auto",
          marginRight: "auto",
        }}
      >
        The information on this website is for general informational purposes only. {identity.name}, Licensed Real Estate Salesperson{identity.license_id && ` (NJ License #${identity.license_id})`}, is affiliated with {identity.brokerage}{location.office_address && `, ${location.office_address}`}.{identity.office_phone && ` ${identity.office_phone}.`} All information deemed reliable but not guaranteed.
      </p>
      <nav
        aria-label="Legal links"
        style={{
          marginTop: "16px",
          display: "flex",
          justifyContent: "center",
          gap: "16px",
          fontSize: "11px",
          color: "rgba(255,255,255,0.7)",
        }}
      >
        <a href="/privacy" style={{ color: "rgba(255,255,255,0.7)", textDecoration: "underline" }}>Privacy Policy</a>
        <a href="/terms" style={{ color: "rgba(255,255,255,0.7)", textDecoration: "underline" }}>Terms of Use</a>
        <a href="/accessibility" style={{ color: "rgba(255,255,255,0.7)", textDecoration: "underline" }}>Accessibility</a>
      </nav>
      <p
        style={{
          marginTop: "8px",
          fontSize: "11px",
          color: "rgba(255,255,255,0.7)",
          opacity: 0.6,
        }}
      >
        &copy; {new Date().getFullYear()} {identity.name}. All rights reserved.
      </p>
    </footer>
  );
}
