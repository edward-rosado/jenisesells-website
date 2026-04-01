/** Maps human-readable language names to BCP 47 locale codes */
const LANGUAGE_TO_LOCALE: Record<string, string> = {
  English: "en",
  Spanish: "es",
  Portuguese: "pt",
  French: "fr",
  Chinese: "zh",
  Mandarin: "zh",
  Korean: "ko",
  Vietnamese: "vi",
  Tagalog: "tl",
  Arabic: "ar",
  Hindi: "hi",
};

const LOCALE_TO_LANGUAGE: Record<string, string> = Object.fromEntries(
  Object.entries(LANGUAGE_TO_LOCALE).map(([lang, code]) => [code, lang]),
);

export const SUPPORTED_LOCALES = Object.values(LANGUAGE_TO_LOCALE);

/** Convert a language name ("Spanish") to its BCP 47 code ("es"). Returns "en" for unknown. */
export function languageToLocale(language: string): string {
  return LANGUAGE_TO_LOCALE[language] ?? "en";
}

/** Convert a BCP 47 code ("es") to its language name ("Spanish"). Returns "English" for unknown. */
export function localeToLanguage(locale: string): string {
  return LOCALE_TO_LANGUAGE[locale] ?? "English";
}
