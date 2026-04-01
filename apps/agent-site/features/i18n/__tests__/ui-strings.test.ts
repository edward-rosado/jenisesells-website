import { describe, it, expect } from "vitest";
import { getUiStrings } from "../ui-strings";
import type { UiStrings } from "../ui-strings";

describe("getUiStrings", () => {
  it("returns English strings for locale 'en'", () => {
    const strings = getUiStrings("en");
    expect(strings.contactMe).toBe("Contact Me");
    expect(strings.skipToMainContent).toBe("Skip to main content");
  });

  it("returns Spanish strings for locale 'es'", () => {
    const strings = getUiStrings("es");
    expect(strings.contactMe).toBe("Cont\u00e1ctame");
    expect(strings.skipToMainContent).toBe("Saltar al contenido principal");
  });

  it("returns English strings when locale is undefined", () => {
    const strings = getUiStrings(undefined);
    expect(strings.contactMe).toBe("Contact Me");
  });

  it("returns English strings for an unknown locale", () => {
    const strings = getUiStrings("xx");
    expect(strings.contactMe).toBe("Contact Me");
  });

  it("returns English strings for empty string locale", () => {
    const strings = getUiStrings("");
    expect(strings.contactMe).toBe("Contact Me");
  });

  describe("key parity between English and Spanish", () => {
    it("has the same keys in both languages", () => {
      const enKeys = Object.keys(getUiStrings("en")).sort();
      const esKeys = Object.keys(getUiStrings("es")).sort();
      expect(esKeys).toEqual(enKeys);
    });

    it("has no empty string values in English", () => {
      const en = getUiStrings("en");
      for (const [key, value] of Object.entries(en)) {
        expect(value, `English key "${key}" should not be empty`).not.toBe("");
      }
    });

    it("has no empty string values in Spanish", () => {
      const es = getUiStrings("es");
      for (const [key, value] of Object.entries(es)) {
        expect(value, `Spanish key "${key}" should not be empty`).not.toBe("");
      }
    });
  });

  describe("timeline keys", () => {
    it("English has whenLookingToBuy", () => {
      expect(getUiStrings("en").whenLookingToBuy).toBe(
        "When are you looking to buy?",
      );
    });

    it("English has whenLookingToSell", () => {
      expect(getUiStrings("en").whenLookingToSell).toBe(
        "When are you looking to sell?",
      );
    });

    it("English has whenLookingToBuyOrSell", () => {
      expect(getUiStrings("en").whenLookingToBuyOrSell).toBe(
        "When are you looking to buy/sell?",
      );
    });

    it("Spanish has whenLookingToBuy", () => {
      expect(getUiStrings("es").whenLookingToBuy).toBe(
        "\u00bfCu\u00e1ndo planea comprar?",
      );
    });

    it("Spanish has whenLookingToSell", () => {
      expect(getUiStrings("es").whenLookingToSell).toBe(
        "\u00bfCu\u00e1ndo planea vender?",
      );
    });

    it("Spanish has whenLookingToBuyOrSell", () => {
      expect(getUiStrings("es").whenLookingToBuyOrSell).toBe(
        "\u00bfCu\u00e1ndo planea comprar/vender?",
      );
    });
  });

  describe("UiStrings interface completeness", () => {
    it("all interface keys are present in English strings", () => {
      const en = getUiStrings("en");
      // Type assertion ensures compile-time check; runtime check below
      const typed: UiStrings = en;
      expect(Object.keys(typed).length).toBeGreaterThan(0);
    });

    it("English and Spanish have the same number of keys", () => {
      const enCount = Object.keys(getUiStrings("en")).length;
      const esCount = Object.keys(getUiStrings("es")).length;
      expect(esCount).toBe(enCount);
    });
  });
});
