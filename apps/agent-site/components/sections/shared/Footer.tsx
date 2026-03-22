import type { AccountConfig, AgentConfig } from "@/features/config/types";
import { EqualHousingNotice } from "@real-estate-star/legal";
import { safeTelHref, safeMailtoHref } from "@/lib/safe-contact";

interface FooterProps {
  agent: AccountConfig | AgentConfig;
  accountId?: string;
}

function formatServiceAreas(areas: string[], state: string): string {
  const names = areas.map((a) => a.replace(/ County$/i, ""));
  if (names.length <= 2) {
    return `Serving ${names.join(" & ")} Counties, ${state}`;
  }
  const last = names.pop();
  return `Serving ${names.join(", ")} & ${last} Counties, ${state}`;
}

export function Footer({ agent, accountId }: FooterProps) {
  const qs = accountId ? `?accountId=${encodeURIComponent(accountId)}` : "";

  let name: string;
  let title: string | undefined;
  let phone: string;
  let email: string;
  let brokerageName: string;
  let licenseNumber: string | undefined;
  let officePhone: string | undefined;
  let officeAddress: string | undefined;
  let state: string;
  let serviceAreas: string[];

  if ("handle" in agent) {
    // AccountConfig
    name = agent.agent?.name ?? agent.broker?.name ?? agent.brokerage.name;
    title = agent.agent?.title ?? agent.broker?.title;
    phone = agent.agent?.phone ?? agent.contact_info?.find((c) => c.type === "phone")?.value ?? "";
    email = agent.agent?.email ?? agent.contact_info?.find((c) => c.type === "email")?.value ?? "";
    brokerageName = agent.brokerage.name;
    licenseNumber = agent.agent?.license_number;
    officePhone = agent.brokerage.office_phone;
    officeAddress = agent.brokerage.office_address;
    state = agent.location.state;
    serviceAreas = agent.location.service_areas;
  } else {
    // AgentConfig (flat)
    name = agent.name;
    title = agent.title;
    phone = agent.phone;
    email = agent.email;
    brokerageName = "";
    licenseNumber = agent.license_number;
    officePhone = undefined;
    officeAddress = undefined;
    state = "";
    serviceAreas = [];
  }

  return (
    <footer
      style={{
        background: "var(--color-primary)",
        color: "white",
        padding: "40px",
        textAlign: "center",
      }}
    >
      <p
        style={{
          fontSize: "22px",
          fontWeight: 700,
          color: "var(--color-accent)",
          marginBottom: "5px",
        }}
      >
        {name}{title ? `, ${title}` : ""}
      </p>
      {brokerageName && (
        <p
          style={{
            fontSize: "24px",
            fontWeight: 700,
            color: "white",
            marginBottom: "3px",
          }}
        >
          {brokerageName}
        </p>
      )}
      <p
        style={{
          fontSize: "14px",
          color: "white",
          marginBottom: "15px",
        }}
      >
        {title || "Licensed Real Estate Salesperson"}{licenseNumber && ` | License #${licenseNumber}`}
      </p>
      {phone && (
        <p style={{ fontSize: "14px", color: "white", marginBottom: "3px" }}>
          <a
            href={safeTelHref(phone)}
            aria-label={`Call ${name}`}
            style={{ color: "white", textDecoration: "none" }}
          >
            Cell: {phone}
          </a>
          {officePhone && (
            <>
              {"  |  "}
              <a
                href={safeTelHref(officePhone)}
                aria-label="Call office"
                style={{ color: "white", textDecoration: "none" }}
              >
                {officePhone}
              </a>
            </>
          )}
        </p>
      )}
      {email && (
        <p style={{ fontSize: "14px", color: "white", marginBottom: "5px" }}>
          <a
            href={safeMailtoHref(email)}
            aria-label={`Email ${name}`}
            style={{ color: "white", textDecoration: "none" }}
          >
            {email}
          </a>
        </p>
      )}
      {serviceAreas.length > 0 && state && (
        <p style={{ fontSize: "13px", color: "rgba(255,255,255,0.85)", marginTop: "15px" }}>
          {formatServiceAreas(serviceAreas, state)}
        </p>
      )}
      {state && (
        <div style={{ marginTop: "20px" }}>
          <EqualHousingNotice agentState={state} />
        </div>
      )}
      <p
        style={{
          fontSize: "12px",
          color: "rgba(255,255,255,0.85)",
          marginTop: "20px",
          maxWidth: "700px",
          marginLeft: "auto",
          marginRight: "auto",
        }}
      >
        The information on this website is for general informational purposes only. {name}, {title || "Licensed Real Estate Salesperson"}{licenseNumber && ` (License #${licenseNumber})`}{brokerageName && `, is affiliated with ${brokerageName}`}{officeAddress && `, ${officeAddress}`}.{officePhone && ` ${officePhone}.`} All information deemed reliable but not guaranteed.
      </p>
      <nav
        aria-label="Legal links"
        style={{
          marginTop: "16px",
          display: "flex",
          justifyContent: "center",
          gap: "16px",
          fontSize: "12px",
          color: "rgba(255,255,255,0.85)",
        }}
      >
        <a href={`/privacy${qs}`} style={{ color: "rgba(255,255,255,0.85)", textDecoration: "underline" }}>Privacy Policy</a>
        <a href={`/terms${qs}`} style={{ color: "rgba(255,255,255,0.85)", textDecoration: "underline" }}>Terms of Use</a>
        <a href={`/accessibility${qs}`} style={{ color: "rgba(255,255,255,0.85)", textDecoration: "underline" }}>Accessibility</a>
      </nav>
      <p
        style={{
          marginTop: "8px",
          fontSize: "12px",
          color: "rgba(255,255,255,0.85)",
        }}
      >
        &copy; {new Date().getFullYear()} {name}. All rights reserved.
      </p>
    </footer>
  );
}
