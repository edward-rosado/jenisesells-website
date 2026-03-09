import { describe, it, expect } from "vitest";
import { buildCssVariableStyle } from "../branding";
import type { AgentBranding } from "../types";

describe("buildCssVariableStyle", () => {
  it("returns a record with all four CSS variable keys", () => {
    const branding: AgentBranding = {
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

  it("falls back to defaults for all missing fields", () => {
    const style = buildCssVariableStyle({});
    expect(style["--color-primary"]).toBe("#1B5E20");
    expect(style["--color-secondary"]).toBe("#2E7D32");
    expect(style["--color-accent"]).toBe("#C8A951");
    expect(style["--font-family"]).toBe("'Segoe UI'");
  });

  it("sanitizes invalid hex colors and falls back to default", () => {
    const branding: AgentBranding = {
      primary_color: "expression(alert(1))",
      secondary_color: "#GGG111",
      accent_color: "#C8A951",
    };
    const style = buildCssVariableStyle(branding);
    expect(style["--color-primary"]).toBe("#1B5E20");
    expect(style["--color-secondary"]).toBe("#2E7D32");
    expect(style["--color-accent"]).toBe("#C8A951");
  });

  it("accepts uppercase hex colors", () => {
    const branding: AgentBranding = { primary_color: "#ABCDEF" };
    const style = buildCssVariableStyle(branding);
    expect(style["--color-primary"]).toBe("#ABCDEF");
  });

  it("accepts lowercase hex colors", () => {
    const branding: AgentBranding = { primary_color: "#abcdef" };
    const style = buildCssVariableStyle(branding);
    expect(style["--color-primary"]).toBe("#abcdef");
  });

  it("sanitizes font family with dangerous characters", () => {
    const branding: AgentBranding = {
      font_family: "Arial'; behavior:url(evil.htc)",
    };
    const style = buildCssVariableStyle(branding);
    expect(style["--font-family"]).toBe("'Segoe UI'");
  });

  it("accepts font family with hyphens and commas", () => {
    const branding: AgentBranding = {
      font_family: "Helvetica Neue, Arial, sans-serif",
    };
    const style = buildCssVariableStyle(branding);
    expect(style["--font-family"]).toBe("'Helvetica Neue, Arial, sans-serif'");
  });

  it("returns a plain object with exactly four keys", () => {
    const style = buildCssVariableStyle({});
    expect(Object.keys(style)).toHaveLength(4);
  });
});
