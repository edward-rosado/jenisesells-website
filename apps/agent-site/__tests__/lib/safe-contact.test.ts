/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { safeMailtoHref, safeTelHref } from "../../lib/safe-contact";

describe("safeMailtoHref", () => {
  it("returns mailto: for valid email", () => {
    expect(safeMailtoHref("agent@example.com")).toBe("mailto:agent@example.com");
  });
  it("returns # for empty string", () => {
    expect(safeMailtoHref("")).toBe("#");
  });
  it("returns # for javascript: attempt", () => {
    expect(safeMailtoHref("javascript:alert(1)")).toBe("#");
  });
  it("returns # for email with protocol prefix", () => {
    expect(safeMailtoHref("mailto:real@example.com")).toBe("#");
  });
  it("returns # for email with spaces", () => {
    expect(safeMailtoHref("has spaces@example.com")).toBe("#");
  });
});

describe("safeTelHref", () => {
  it("returns tel: for valid phone digits", () => {
    expect(safeTelHref("(732) 555-1234")).toBe("tel:7325551234");
  });
  it("returns tel: with extension", () => {
    expect(safeTelHref("(732) 555-1234", "5")).toBe("tel:7325551234,5");
  });
  it("returns # for empty string", () => {
    expect(safeTelHref("")).toBe("#");
  });
  it("strips non-digits", () => {
    expect(safeTelHref("+1-800-FLOWERS")).toBe("tel:1800");
  });
});
