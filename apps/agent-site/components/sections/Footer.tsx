import type { AgentConfig } from "@/lib/types";

interface FooterProps {
  agent: AgentConfig;
}

export function Footer({ agent }: FooterProps) {
  const { identity, location } = agent;
  return (
    <footer className="py-10 px-10 text-center text-white" style={{ backgroundColor: "var(--color-primary)" }}>
      <p className="text-lg font-bold">
        {identity.name}{identity.title ? `, ${identity.title}` : ""}
      </p>
      <p className="text-sm opacity-80">
        {identity.brokerage}
        {identity.license_id && ` · Lic. #${identity.license_id}`}
      </p>
      <p className="mt-3 text-sm">
        <a href={`tel:${identity.phone}`} aria-label={`Call ${identity.name}`} style={{ color: "var(--color-accent)" }}>{identity.phone}</a>
        {" | "}
        <a href={`mailto:${identity.email}`} aria-label={`Email ${identity.name}`} style={{ color: "var(--color-accent)" }}>{identity.email}</a>
      </p>
      {location.service_areas && (
        <p className="mt-2 text-xs opacity-60">
          Serving {location.service_areas.join(" · ")}
        </p>
      )}
      {identity.languages && identity.languages.length > 1 && (
        <p className="mt-1 text-xs opacity-60">
          {identity.languages.join(" · ")}
        </p>
      )}
      <div className="mt-6 flex items-center justify-center gap-2 text-xs opacity-60">
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
      <p className="mt-2 text-xs opacity-40">
        &copy; {new Date().getFullYear()} {identity.name}. All rights reserved.
      </p>
    </footer>
  );
}
