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
      {identity.brokerage && <p className="text-sm opacity-80">{identity.brokerage}</p>}
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
      <p className="mt-6 text-xs opacity-40">
        &copy; {new Date().getFullYear()} {identity.name}. All rights reserved.
      </p>
    </footer>
  );
}
