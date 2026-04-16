/**
 * @vitest-environment node
 */
import { vi, describe, it, expect } from "vitest";

// Mock config.ts so tests don't depend on the generated config-registry fixture data
vi.mock("@/features/config/config", () => ({
  loadLocalizedContent: (handle: string, locale: string) => {
    if (handle === "jenise-buckalew" && locale === "en") {
      return { pages: { home: { sections: {} } } };
    }
    if (handle === "jenise-buckalew" && locale === "es") {
      return { pages: { home: { sections: {} } }, _locale: "es" };
    }
    // Simulate missing account
    throw new Error(`Account not found: ${handle}`);
  },
}));

import { loadContent } from "@/features/config/hybrid-loader";

describe("loadContent", () => {
  describe("preview env", () => {
    it("returns bundled content for a known handle + en locale", async () => {
      const result = await loadContent("jenise-buckalew", "en", "preview");

      expect(result.source).toBe("bundled");
      expect(result.content).not.toBeNull();
      expect(result.content?.pages.home.sections).toBeDefined();
    });

    it("returns bundled content for a known handle + es locale", async () => {
      const result = await loadContent("jenise-buckalew", "es", "preview");

      expect(result.source).toBe("bundled");
      expect(result.content).not.toBeNull();
    });

    it("returns null content for an unknown handle", async () => {
      const result = await loadContent("nonexistent-agent", "en", "preview");

      expect(result.source).toBe("bundled");
      expect(result.content).toBeNull();
    });

    it("returns null content for an invalid handle (special chars)", async () => {
      const result = await loadContent("../../../etc/passwd", "en", "preview");

      expect(result.source).toBe("bundled");
      expect(result.content).toBeNull();
    });
  });

  describe("production env (KV stub — falls back to bundled)", () => {
    it("returns bundled content for a known handle (stub fallback)", async () => {
      const result = await loadContent("jenise-buckalew", "en", "production");

      expect(result.source).toBe("bundled");
      expect(result.content).not.toBeNull();
    });

    it("returns null content for an unknown handle via stub fallback", async () => {
      const result = await loadContent("nonexistent-agent", "en", "production");

      expect(result.source).toBe("bundled");
      expect(result.content).toBeNull();
    });
  });
});
