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
        <a href={`tel:${identity.phone}`} style={{ color: "var(--color-accent)" }}>{identity.phone}</a>
        {" | "}
        <a href={`mailto:${identity.email}`} style={{ color: "var(--color-accent)" }}>{identity.email}</a>
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
          aria-hidden="true"
          width="20"
          height="20"
          viewBox="0 0 24 24"
          fill="currentColor"
          xmlns="http://www.w3.org/2000/svg"
        >
          <rect x="1" y="1" width="22" height="22" rx="2" stroke="currentColor" strokeWidth="1.5" fill="none" />
          <text x="12" y="16.5" textAnchor="middle" fontSize="11" fontWeight="bold" fill="currentColor">=</text>
        </svg>
        <span>Equal Housing Opportunity</span>
      </div>
      <p className="mt-2 text-xs opacity-40">
        &copy; {new Date().getFullYear()} {identity.name}. All rights reserved.
      </p>
    </footer>
  );
}
