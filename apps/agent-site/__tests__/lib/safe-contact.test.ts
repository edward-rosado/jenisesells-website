import { describe, it, expect } from "vitest";
import { safeMailtoHref, safeTelHref } from "@/lib/safe-contact";

describe("safeMailtoHref", () => {
  it("returns mailto: for valid email", () => {
    expect(safeMailtoHref("test@example.com")).toBe("mailto:test@example.com");
  });

  it("returns # for empty string", () => {
    expect(safeMailtoHref("")).toBe("#");
  });

  it("returns # for email with spaces", () => {
    expect(safeMailtoHref("test @example.com")).toBe("#");
  });

  it("returns # for email with colon (protocol injection)", () => {
    expect(safeMailtoHref("javascript:alert(1)")).toBe("#");
  });

  it("returns # for email missing @", () => {
    expect(safeMailtoHref("notanemail")).toBe("#");
  });

  it("returns # for email missing TLD", () => {
    expect(safeMailtoHref("user@host")).toBe("#");
  });
});

describe("safeTelHref", () => {
  it("returns tel: with digits only", () => {
    expect(safeTelHref("(555) 123-4567")).toBe("tel:5551234567");
  });

  it("returns # for empty phone", () => {
    expect(safeTelHref("")).toBe("#");
  });

  it("returns # for non-digit phone", () => {
    expect(safeTelHref("abc")).toBe("#");
  });

  it("appends extension when provided", () => {
    expect(safeTelHref("555-1234", "123")).toBe("tel:5551234,123");
  });

  it("strips non-digits from extension", () => {
    expect(safeTelHref("555-1234", "ext 42")).toBe("tel:5551234,42");
  });

  it("handles extension with no digits gracefully", () => {
    expect(safeTelHref("555-1234", "")).toBe("tel:5551234");
  });
});
