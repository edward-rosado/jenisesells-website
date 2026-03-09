const BASE_DOMAINS = ["realestatestar.com", "localhost"];
const RESERVED_SUBDOMAINS = ["www", "api", "portal", "app", "admin"];

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

  // Custom domain — look up in domain map (future)
  return null;
}
