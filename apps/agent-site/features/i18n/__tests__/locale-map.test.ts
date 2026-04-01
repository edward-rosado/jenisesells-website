import { describe, it, expect } from "vitest";
import {
  languageToLocale,
  localeToLanguage,
  SUPPORTED_LOCALES,
} from "../locale-map";

describe("languageToLocale", () => {
  it.each([
    ["English", "en"],
    ["Spanish", "es"],
    ["Portuguese", "pt"],
    ["French", "fr"],
    ["Chinese", "zh"],
    ["Korean", "ko"],
    ["Vietnamese", "vi"],
    ["Tagalog", "tl"],
    ["Arabic", "ar"],
    ["Hindi", "hi"],
  ])('maps "%s" to "%s"', (language, expected) => {
    expect(languageToLocale(language)).toBe(expected);
  });

  it('maps "Mandarin" to "zh" (same as Chinese)', () => {
    expect(languageToLocale("Mandarin")).toBe("zh");
    expect(languageToLocale("Mandarin")).toBe(languageToLocale("Chinese"));
  });

  it('returns "en" for an unknown language', () => {
    expect(languageToLocale("Klingon")).toBe("en");
  });

  it('returns "en" for an empty string', () => {
    expect(languageToLocale("")).toBe("en");
  });

  it("is case-sensitive (lowercase does not match)", () => {
    expect(languageToLocale("english")).toBe("en");
    expect(languageToLocale("spanish")).toBe("en");
    expect(languageToLocale("ENGLISH")).toBe("en");
  });
});

describe("localeToLanguage", () => {
  it.each([
    ["en", "English"],
    ["es", "Spanish"],
    ["pt", "Portuguese"],
    ["fr", "French"],
    ["ko", "Korean"],
    ["vi", "Vietnamese"],
    ["tl", "Tagalog"],
    ["ar", "Arabic"],
    ["hi", "Hindi"],
  ])('maps "%s" to "%s"', (code, expected) => {
    expect(localeToLanguage(code)).toBe(expected);
  });

  it('maps "zh" to "Mandarin" (last writer wins for duplicate locale codes)', () => {
    // Both Chinese and Mandarin map to "zh", but Object.fromEntries
    // overwrites the earlier entry, so "zh" -> "Mandarin"
    expect(localeToLanguage("zh")).toBe("Mandarin");
  });

  it('returns "English" for an unknown locale code', () => {
    expect(localeToLanguage("xx")).toBe("English");
  });

  it('returns "English" for an empty string', () => {
    expect(localeToLanguage("")).toBe("English");
  });
});

describe("SUPPORTED_LOCALES", () => {
  it("contains all expected locale codes", () => {
    const expected = ["en", "es", "pt", "fr", "zh", "zh", "ko", "vi", "tl", "ar", "hi"];
    expect(SUPPORTED_LOCALES).toEqual(expected);
  });

  it("includes standard BCP 47 codes", () => {
    expect(SUPPORTED_LOCALES).toContain("en");
    expect(SUPPORTED_LOCALES).toContain("es");
    expect(SUPPORTED_LOCALES).toContain("zh");
  });

  it('contains "zh" twice (Chinese and Mandarin both map to zh)', () => {
    const zhCount = SUPPORTED_LOCALES.filter((c) => c === "zh").length;
    expect(zhCount).toBe(2);
  });

  it("has the expected total length (11 entries including duplicate zh)", () => {
    expect(SUPPORTED_LOCALES).toHaveLength(11);
  });
});
