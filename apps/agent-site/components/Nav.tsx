import type { AgentConfig } from "@/lib/types";

interface NavProps {
  agent: AgentConfig;
}

export function Nav({ agent }: NavProps) {
  const { identity } = agent;
  return (
    <nav
      className="fixed top-0 w-full z-50 px-10 py-3 flex items-center justify-between"
      style={{ backgroundColor: "var(--color-primary)" }}
    >
      <div className="flex items-center gap-3">
        <span className="text-sm font-semibold tracking-wide" style={{ color: "var(--color-accent)" }}>
          {identity.tagline?.toUpperCase() || identity.name.toUpperCase()}
        </span>
      </div>
      <div className="flex items-center gap-5">
        {identity.email && (
          <a href={`mailto:${identity.email}`} className="text-white text-sm hidden md:block">
            {identity.email}
          </a>
        )}
        {identity.phone && (
          <a
            href={`tel:${identity.phone}`}
            className="px-5 py-2 rounded-full text-sm font-bold"
            style={{ backgroundColor: "var(--color-accent)", color: "var(--color-primary)" }}
          >
            {identity.phone}
          </a>
        )}
      </div>
    </nav>
  );
}
