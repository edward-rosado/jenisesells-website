import { describe, it, expect } from "vitest";
import { resolveLocale } from "../resolve-locale";

describe("resolveLocale", () => {
  describe("cookie override", () => {
    it("returns cookie locale when it is supported", () => {
      expect(resolveLocale("en", "es", ["en", "es"])).toBe("es");
    });

    it("ignores cookie locale when it is not supported", () => {
      expect(resolveLocale("es", "fr", ["en", "es"])).toBe("es");
    });

    it("falls through when cookie is undefined", () => {
      expect(resolveLocale("es", undefined, ["en", "es"])).toBe("es");
    });

    it("falls through when cookie is empty string", () => {
      expect(resolveLocale("es", "", ["en", "es"])).toBe("es");
    });
  });

  describe("Accept-Language parsing", () => {
    it("picks the highest quality language that is supported", () => {
      expect(
        resolveLocale("es;q=0.9,en;q=0.8", undefined, ["en", "es"]),
      ).toBe("es");
    });

    it("uses implicit q=1.0 when no quality value is specified", () => {
      expect(
        resolveLocale("es,en;q=0.8", undefined, ["en", "es"]),
      ).toBe("es");
    });

    it("respects quality ordering regardless of header position", () => {
      expect(
        resolveLocale("en;q=0.5,es;q=0.9", undefined, ["en", "es"]),
      ).toBe("es");
    });

    it("returns first supported match when qualities are equal", () => {
      // Both q=1.0, but "fr" appears first in header; only "es" is supported
      expect(
        resolveLocale("fr,es", undefined, ["en", "es"]),
      ).toBe("es");
    });
  });

  describe("primary subtag fallback", () => {
    it('matches "es-MX" to "es" via primary subtag', () => {
      expect(resolveLocale("es-MX", undefined, ["en", "es"])).toBe("es");
    });

    it('matches "pt-BR" to "pt" via primary subtag', () => {
      expect(resolveLocale("pt-BR", undefined, ["en", "pt"])).toBe("pt");
    });

    it("prefers exact match over subtag fallback", () => {
      // "es" exact match should win over "es-MX" subtag
      expect(resolveLocale("es", undefined, ["en", "es"])).toBe("es");
    });
  });

  describe("wildcard handling", () => {
    it('skips wildcard "*" entries', () => {
      expect(resolveLocale("*,es;q=0.5", undefined, ["en", "es"])).toBe("es");
    });

    it('falls back to "en" when only wildcard is present', () => {
      expect(resolveLocale("*", undefined, ["en", "es"])).toBe("en");
    });
  });

  describe("fallback to English", () => {
    it('returns "en" when Accept-Language is null', () => {
      expect(resolveLocale(null, undefined, ["en", "es"])).toBe("en");
    });

    it('returns "en" when Accept-Language contains only unsupported languages', () => {
      expect(resolveLocale("de,ja", undefined, ["en", "es"])).toBe("en");
    });

    it('returns "en" when Accept-Language is empty string', () => {
      expect(resolveLocale("", undefined, ["en", "es"])).toBe("en");
    });
  });

  describe("agent with single locale", () => {
    it('returns the only supported locale (short-circuit)', () => {
      expect(resolveLocale("es", "es", ["en"])).toBe("en");
    });

    it('returns "en" when agent locales array is empty', () => {
      expect(resolveLocale("es", undefined, [])).toBe("en");
    });
  });

  describe("agent without English support", () => {
    it("returns first supported locale when en is not in the list", () => {
      expect(resolveLocale("de", undefined, ["es", "fr"])).toBe("es");
    });
  });

  describe("edge cases", () => {
    it("lowercases language tags from Accept-Language", () => {
      expect(resolveLocale("ES", undefined, ["en", "es"])).toBe("es");
    });

    it("handles whitespace in Accept-Language header", () => {
      expect(
        resolveLocale("  es ; q=0.9 , en ; q=0.8 ", undefined, ["en", "es"]),
      ).toBe("es");
    });

    it("defaults to q=1.0 when quality param does not match digit pattern", () => {
      // "es;q=abc" — the regex requires digits, so "q=abc" does not match;
      // quality stays at the default 1.0, same as "en" (implicit 1.0).
      // Both are q=1.0, "es" appears first in the header, so "es" wins.
      expect(
        resolveLocale("es;q=abc,en", undefined, ["en", "es"]),
      ).toBe("es");
    });
  });
});
