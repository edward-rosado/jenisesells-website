/**
 * @vitest-environment node
 */
import { vi, describe, it, expect } from "vitest";

vi.mock("@/features/config/nav-registry", () => ({
  navData: {
    "test-account": {
      navigation: { items: [{ label: "About", href: "#about", enabled: true }] },
      enabledSections: ["hero", "about", "contact_form"],
    },
  },
}));

import { loadNavConfig } from "@/features/config/nav-config";

describe("loadNavConfig", () => {
  it("returns navigation and enabledSections as Set for known handle", () => {
    const result = loadNavConfig("test-account");
    expect(result.navigation).toEqual({ items: [{ label: "About", href: "#about", enabled: true }] });
    expect(result.enabledSections).toBeInstanceOf(Set);
    expect(result.enabledSections.has("hero")).toBe(true);
    expect(result.enabledSections.has("about")).toBe(true);
    expect(result.enabledSections.has("contact_form")).toBe(true);
    expect(result.enabledSections.size).toBe(3);
  });

  it("returns empty defaults for unknown handle", () => {
    const result = loadNavConfig("unknown");
    expect(result.navigation).toBeUndefined();
    expect(result.enabledSections).toBeInstanceOf(Set);
    expect(result.enabledSections.size).toBe(0);
  });
});
