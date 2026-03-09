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
});
