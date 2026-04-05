import type { SupportedLocale } from "./locale-map";
import { isSupportedLocale } from "./locale-map";

/**
 * Parse the Accept-Language header into an ordered list of locale codes.
 * Example: "es-MX,es;q=0.9,en;q=0.8" → ["es", "en"]
 */
export function parseAcceptLanguage(header: string): string[] {
  return header
    .split(",")
    .map((part) => {
      const [lang, qPart] = part.trim().split(";");
      const q = qPart?.trim().startsWith("q=")
        ? parseFloat(qPart.trim().slice(2))
        : 1;
      // Extract the primary language subtag (e.g., "es-MX" → "es")
      const code = lang.trim().split("-")[0].toLowerCase();
      return { code, q };
    })
    .filter(({ code, q }) => code && !isNaN(q))
    .sort((a, b) => b.q - a.q)
    .map(({ code }) => code);
}

/**
 * Resolve the best locale for the current request.
 *
 * Priority: cookie override → Accept-Language match → "en" fallback.
 * Only returns locales the agent actually supports.
 */
export function resolveLocale(
  acceptLanguageHeader: string | null,
  cookieLocale: string | null,
  agentLocales: SupportedLocale[],
): SupportedLocale {
  // 1. Cookie override — user explicitly chose a language
  if (cookieLocale && isSupportedLocale(cookieLocale) && agentLocales.includes(cookieLocale)) {
    return cookieLocale;
  }

  // 2. Best match from Accept-Language header
  if (acceptLanguageHeader) {
    const preferred = parseAcceptLanguage(acceptLanguageHeader);
    for (const code of preferred) {
      if (isSupportedLocale(code) && agentLocales.includes(code)) {
        return code;
      }
    }
  }

  // 3. Fallback to English (or first supported locale if English isn't supported)
  return agentLocales.includes("en") ? "en" : agentLocales[0];
}
