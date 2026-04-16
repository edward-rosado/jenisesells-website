import { describe, it, expect } from "vitest";
import type { DefaultContent } from "@/features/templates/types";

// Static imports — each module is loaded once per test run.
// Vitest tree-shakes unused exports, but the named `defaultContent` export
// is specifically what we are validating here.
import { defaultContent as emeraldClassic } from "@/features/templates/emerald-classic";
import { defaultContent as modernMinimal } from "@/features/templates/modern-minimal";
import { defaultContent as warmCommunity } from "@/features/templates/warm-community";
import { defaultContent as luxuryEstate } from "@/features/templates/luxury-estate";
import { defaultContent as urbanLoft } from "@/features/templates/urban-loft";
import { defaultContent as newBeginnings } from "@/features/templates/new-beginnings";
import { defaultContent as lightLuxury } from "@/features/templates/light-luxury";
import { defaultContent as countryEstate } from "@/features/templates/country-estate";
import { defaultContent as coastalLiving } from "@/features/templates/coastal-living";
import { defaultContent as commercial } from "@/features/templates/commercial";

const ALL_TEMPLATES: { name: string; content: DefaultContent }[] = [
  { name: "emerald-classic", content: emeraldClassic },
  { name: "modern-minimal", content: modernMinimal },
  { name: "warm-community", content: warmCommunity },
  { name: "luxury-estate", content: luxuryEstate },
  { name: "urban-loft", content: urbanLoft },
  { name: "new-beginnings", content: newBeginnings },
  { name: "light-luxury", content: lightLuxury },
  { name: "country-estate", content: countryEstate },
  { name: "coastal-living", content: coastalLiving },
  { name: "commercial", content: commercial },
];

/** Recursively collect all leaf string values from a plain object */
function collectStringValues(obj: unknown, path = ""): { path: string; value: unknown }[] {
  if (obj === null || obj === undefined) return [];
  if (typeof obj === "string") return [{ path, value: obj }];
  if (typeof obj === "object") {
    const results: { path: string; value: unknown }[] = [];
    for (const [key, val] of Object.entries(obj as Record<string, unknown>)) {
      results.push(...collectStringValues(val, path ? `${path}.${key}` : key));
    }
    return results;
  }
  return [{ path, value: obj }];
}

describe("defaultContent exports", () => {
  describe("all 10 templates export a defaultContent object", () => {
    it("every template has a non-null defaultContent export", () => {
      for (const { name, content } of ALL_TEMPLATES) {
        expect(content, `${name} must export defaultContent`).toBeDefined();
        expect(typeof content, `${name}.defaultContent must be an object`).toBe("object");
        expect(content, `${name}.defaultContent must not be null`).not.toBeNull();
      }
    });
  });

  describe("every defaultContent satisfies the DefaultContent type contract", () => {
    it("each defaultContent has a hero section (all templates render a hero)", () => {
      for (const { name, content } of ALL_TEMPLATES) {
        expect(
          content.hero,
          `${name}.defaultContent.hero must be defined — every template renders a hero section`,
        ).toBeDefined();
      }
    });

    it("every hero has a non-empty title string", () => {
      for (const { name, content } of ALL_TEMPLATES) {
        const title = content.hero?.title;
        expect(
          typeof title,
          `${name}.defaultContent.hero.title must be a string`,
        ).toBe("string");
        expect(
          (title as string).trim().length,
          `${name}.defaultContent.hero.title must not be empty`,
        ).toBeGreaterThan(0);
      }
    });

    it("every hero has a non-empty ctaText string", () => {
      for (const { name, content } of ALL_TEMPLATES) {
        const ctaText = content.hero?.ctaText;
        expect(
          typeof ctaText,
          `${name}.defaultContent.hero.ctaText must be a string`,
        ).toBe("string");
        expect(
          (ctaText as string).trim().length,
          `${name}.defaultContent.hero.ctaText must not be empty`,
        ).toBeGreaterThan(0);
      }
    });
  });

  describe("all string values in every defaultContent are non-empty", () => {
    it("no defaultContent field contains an empty string", () => {
      for (const { name, content } of ALL_TEMPLATES) {
        const leaves = collectStringValues(content);
        for (const { path, value } of leaves) {
          expect(
            (value as string).trim().length,
            `${name}.defaultContent.${path} must not be an empty string`,
          ).toBeGreaterThan(0);
        }
      }
    });
  });

  describe("type safety — defaultContent is assignable to DefaultContent", () => {
    it("all exports are structurally compatible with DefaultContent", () => {
      // This test exercises the type at runtime: every top-level key must be
      // one of the keys defined in DefaultContent. Unknown keys fail here.
      const knownKeys: Array<keyof DefaultContent> = [
        "hero",
        "stats",
        "features",
        "steps",
        "gallery",
        "testimonials",
        "profiles",
        "contact",
        "about",
        "marquee",
      ];

      for (const { name, content } of ALL_TEMPLATES) {
        for (const key of Object.keys(content)) {
          expect(
            knownKeys,
            `${name}.defaultContent has unknown key "${key}" not in DefaultContent interface`,
          ).toContain(key);
        }
      }
    });

    it("templates with marquee sections include a marquee defaultContent entry", () => {
      // luxury-estate, light-luxury, and commercial render the marquee section
      const marqueeTemplates = ["luxury-estate", "light-luxury", "commercial"];
      for (const { name, content } of ALL_TEMPLATES) {
        if (marqueeTemplates.includes(name)) {
          expect(
            content.marquee,
            `${name} renders a MarqueeBanner — defaultContent.marquee must be defined`,
          ).toBeDefined();
        }
      }
    });
  });
});
