import { describe, it, expect } from "vitest";
import { buildCssVariableStyle } from "../branding";
import type { AccountBranding } from "../types";

describe("buildCssVariableStyle", () => {
  it("should generate CSS variables from branding config", () => {
    const branding: AccountBranding = {
      primary_color: "#1B5E20",
      secondary_color: "#2E7D32",
      accent_color: "#C8A951",
      font_family: "Segoe UI",
    };
    const style = buildCssVariableStyle(branding);
    expect(style["--color-primary"]).toBe("#1B5E20");
    expect(style["--color-secondary"]).toBe("#2E7D32");
    expect(style["--color-accent"]).toBe("#C8A951");
    expect(style["--font-family"]).toBe("'Segoe UI'");
  });

  it("should use defaults for missing values", () => {
    const style = buildCssVariableStyle({});
    expect(style["--color-primary"]).toBe("#1B5E20");
    expect(style["--font-family"]).toBe("'Segoe UI'");
  });

  it("should sanitize malicious color values", () => {
    const branding: AccountBranding = {
      primary_color: "red; background: url(evil)",
      accent_color: "#C8A951",
    };
    const style = buildCssVariableStyle(branding);
    expect(style["--color-primary"]).toBe("#1B5E20");
    expect(style["--color-accent"]).toBe("#C8A951");
  });

  it("should sanitize malicious font_family values", () => {
    const branding: AccountBranding = {
      font_family: "Segoe UI'; behavior:url(evil.htc); x: '",
    };
    const style = buildCssVariableStyle(branding);
    expect(style["--font-family"]).toBe("'Segoe UI'");
  });
});
