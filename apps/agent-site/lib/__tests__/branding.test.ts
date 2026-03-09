import { describe, it, expect } from "vitest";
import { buildCssVariables } from "../branding";
import type { AgentBranding } from "../types";

describe("buildCssVariables", () => {
  it("should generate CSS variables from branding config", () => {
    const branding: AgentBranding = {
      primary_color: "#1B5E20",
      secondary_color: "#2E7D32",
      accent_color: "#C8A951",
      font_family: "Segoe UI",
    };
    const css = buildCssVariables(branding);
    expect(css).toContain("--color-primary: #1B5E20");
    expect(css).toContain("--color-secondary: #2E7D32");
    expect(css).toContain("--color-accent: #C8A951");
    expect(css).toContain("--font-family: 'Segoe UI'");
  });

  it("should use defaults for missing values", () => {
    const css = buildCssVariables({});
    expect(css).toContain("--color-primary: #1B5E20");
    expect(css).toContain("--font-family: 'Segoe UI'");
  });

  it("should sanitize malicious color values", () => {
    const branding: AgentBranding = {
      primary_color: "red; background: url(evil)",
      accent_color: "#C8A951",
    };
    const css = buildCssVariables(branding);
    // Malicious primary should fall back to default
    expect(css).toContain("--color-primary: #1B5E20");
    // Valid accent should pass through
    expect(css).toContain("--color-accent: #C8A951");
  });

  it("should sanitize malicious font_family values", () => {
    const branding: AgentBranding = {
      font_family: "Segoe UI'; behavior:url(evil.htc); x: '",
    };
    const css = buildCssVariables(branding);
    // Malicious font should fall back to default
    expect(css).toContain("--font-family: 'Segoe UI'");
  });
});
