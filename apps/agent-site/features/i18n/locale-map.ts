/** BCP 47 locale codes supported by the platform */
export const SUPPORTED_LOCALES = ["en", "es", "pt"] as const;
export type SupportedLocale = (typeof SUPPORTED_LOCALES)[number];

const LANGUAGE_TO_LOCALE: Record<string, SupportedLocale> = {
  English: "en",
  Spanish: "es",
  Portuguese: "pt",
};

const LOCALE_TO_LANGUAGE: Record<SupportedLocale, string> = {
  en: "English",
  es: "Español",
  pt: "Português",
};

/** Map a human-readable language name (from agent config) to a BCP 47 locale code */
export function languageToLocale(language: string): SupportedLocale | undefined {
  return LANGUAGE_TO_LOCALE[language];
}

/** Map a BCP 47 locale code to a display name (in the target language) */
export function localeToDisplayName(locale: SupportedLocale): string {
  return LOCALE_TO_LANGUAGE[locale] ?? locale;
}

/** Check if a string is a supported locale code */
export function isSupportedLocale(value: string): value is SupportedLocale {
  return SUPPORTED_LOCALES.includes(value as SupportedLocale);
}

/** Convert an array of language names (from agent config) to locale codes */
export function languagesToLocales(languages: string[]): SupportedLocale[] {
  const locales: SupportedLocale[] = [];
  for (const lang of languages) {
    const locale = languageToLocale(lang);
    if (locale) locales.push(locale);
  }
  return locales.length > 0 ? locales : ["en"];
}
