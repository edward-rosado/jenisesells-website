/**
 * @vitest-environment node
 */
import { describe, it, expect } from "vitest";
import { getTemplate, TEMPLATES } from "@/templates/index";
import { EmeraldClassic } from "@/templates/emerald-classic";

describe("getTemplate", () => {
  it("returns EmeraldClassic for 'emerald-classic'", () => {
    const Template = getTemplate("emerald-classic");
    expect(Template).toBe(EmeraldClassic);
  });

  it("falls back to EmeraldClassic for an unknown template name", () => {
    const Template = getTemplate("nonexistent-template");
    expect(Template).toBe(EmeraldClassic);
  });

  it("falls back to EmeraldClassic for an empty string", () => {
    const Template = getTemplate("");
    expect(Template).toBe(EmeraldClassic);
  });

  it("falls back to EmeraldClassic for a numeric-looking string", () => {
    const Template = getTemplate("123");
    expect(Template).toBe(EmeraldClassic);
  });

  it("TEMPLATES registry contains emerald-classic key", () => {
    expect("emerald-classic" in TEMPLATES).toBe(true);
  });

  it("TEMPLATES registry maps emerald-classic to EmeraldClassic component", () => {
    expect(TEMPLATES["emerald-classic"]).toBe(EmeraldClassic);
  });

  it("returns the same reference on repeated calls", () => {
    const a = getTemplate("emerald-classic");
    const b = getTemplate("emerald-classic");
    expect(a).toBe(b);
  });

  it("unknown template returns the same EmeraldClassic reference every time", () => {
    const a = getTemplate("foo");
    const b = getTemplate("bar");
    expect(a).toBe(b);
    expect(a).toBe(EmeraldClassic);
  });
});
