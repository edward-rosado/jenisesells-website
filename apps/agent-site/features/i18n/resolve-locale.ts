/**
 * Resolves the best locale for a visitor given their cookie preference,
 * Accept-Language header, and the agent's supported locale codes.
 *
 * Priority: cookie override > Accept-Language match > "en" fallback
 */
export function resolveLocale(
  acceptLanguageHeader: string | null,
  cookieLocale: string | undefined,
  agentLocaleCodes: string[],
): string {
  // If agent only supports one locale, short-circuit
  if (agentLocaleCodes.length <= 1) {
    return agentLocaleCodes[0] ?? "en";
  }

  const supported = new Set(agentLocaleCodes);

  // 1. Cookie override — if it's a supported locale, use it
  if (cookieLocale && supported.has(cookieLocale)) {
    return cookieLocale;
  }

  // 2. Parse Accept-Language header and find best match
  if (acceptLanguageHeader) {
    const preferred = parseAcceptLanguage(acceptLanguageHeader);
    for (const { code } of preferred) {
      // Exact match (e.g. "es")
      if (supported.has(code)) return code;
      // Primary subtag match (e.g. "es-MX" -> "es")
      const primary = code.split("-")[0];
      if (primary !== code && supported.has(primary)) return primary;
    }
  }

  // 3. Fallback to English, or first supported locale
  return supported.has("en") ? "en" : agentLocaleCodes[0] ?? "en";
}

interface AcceptLanguageEntry {
  code: string;
  quality: number;
}

/** Parse an Accept-Language header into sorted (by quality, desc) entries */
function parseAcceptLanguage(header: string): AcceptLanguageEntry[] {
  const entries: AcceptLanguageEntry[] = [];

  for (const part of header.split(",")) {
    const trimmed = part.trim();
    if (!trimmed) continue;

    const [langTag, ...params] = trimmed.split(";");
    const code = langTag.trim().toLowerCase();
    if (!code || code === "*") continue;

    let quality = 1.0;
    for (const param of params) {
      const match = param.trim().match(/^q\s*=\s*([\d.]+)$/);
      if (match) {
        quality = parseFloat(match[1]);
        if (Number.isNaN(quality)) quality = 0;
        break;
      }
    }

    entries.push({ code, quality });
  }

  // Sort by quality descending, stable
  entries.sort((a, b) => b.quality - a.quality);
  return entries;
}
