import { customDomains, agentIds } from "./config-registry";

const BASE_DOMAINS = ["real-estate-star.com", "localhost"];
const RESERVED_SUBDOMAINS = ["www", "api", "portal", "platform", "app", "admin"];

export function extractAgentId(hostname: string): string | null {
  const host = hostname.split(":")[0]; // strip port

  for (const base of BASE_DOMAINS) {
    if (host === base) return null;
    if (host.endsWith(`.${base}`)) {
      const subdomain = host.slice(0, -(base.length + 1));
      if (RESERVED_SUBDOMAINS.includes(subdomain)) return null;
      if (subdomain.includes(".")) return null; // nested subdomain
      return subdomain;
    }
  }

  return null;
}

export function resolveAgentFromCustomDomain(hostname: string): string | null {
  const host = hostname.split(":")[0];
  return customDomains[host] ?? null;
}

export function isWwwCustomDomain(hostname: string): string | null {
  const host = hostname.split(":")[0];
  if (!host.startsWith("www.")) return null;
  const bare = host.slice(4);
  return customDomains[bare] ? bare : null;
}

export function getAgentIds(): Set<string> {
  return agentIds;
}
