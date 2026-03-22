import { describe, it, expect } from "vitest";
import { buildCssVariableStyle } from "../src/branding";

describe("buildCssVariableStyle", () => {
  it("returns CSS variable map for full branding config", () => {
    const result = buildCssVariableStyle({
      primary_color: "#1B5E20",
      secondary_color: "#2E7D32",
      accent_color: "#C8A951",
      font_family: "Segoe UI",
    });
    expect(result["--color-primary"]).toBe("#1B5E20");
    expect(result["--color-secondary"]).toBe("#2E7D32");
    expect(result["--color-accent"]).toBe("#C8A951");
    expect(result["--font-family"]).toBe("'Segoe UI'");
  });

  it("uses defaults for missing branding fields", () => {
    const result = buildCssVariableStyle({});
    expect(result["--color-primary"]).toBe("#1B5E20");
    expect(result["--color-secondary"]).toBe("#2E7D32");
    expect(result["--color-accent"]).toBe("#C8A951");
    expect(result["--font-family"]).toBe("'Segoe UI'");
  });

  it("falls back to default color for invalid hex", () => {
    const result = buildCssVariableStyle({ primary_color: "not-a-color" });
    expect(result["--color-primary"]).toBe("#1B5E20");
  });

  it("falls back to default font for unsafe font family", () => {
    const result = buildCssVariableStyle({ font_family: "Comic<script>" });
    expect(result["--font-family"]).toBe("'Segoe UI'");
  });

  it("accepts valid hex colors", () => {
    const result = buildCssVariableStyle({ primary_color: "#AABBCC" });
    expect(result["--color-primary"]).toBe("#AABBCC");
  });

  it("accepts font families with commas and dashes", () => {
    const result = buildCssVariableStyle({ font_family: "Arial, sans-serif" });
    expect(result["--font-family"]).toBe("'Arial, sans-serif'");
  });
});
