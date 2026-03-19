/**
 * @vitest-environment node
 */
import { describe, it, expect, vi } from "vitest";

class MockNextResponse {
  headers: Map<string, string>;
  constructor() {
    this.headers = new Map();
  }
  static next() {
    return new MockNextResponse();
  }
}

vi.mock("next/server", () => ({
  NextResponse: MockNextResponse,
}));

import { applySecurityHeaders, safeCspUrl } from "@/lib/security-headers";

describe("applySecurityHeaders", () => {
  it("sets X-Content-Type-Options to nosniff", () => {
    const response = new MockNextResponse() as never;
    applySecurityHeaders(response);
    expect((response as MockNextResponse).headers.get("X-Content-Type-Options")).toBe("nosniff");
  });

  it("sets Referrer-Policy to strict-origin-when-cross-origin", () => {
    const response = new MockNextResponse() as never;
    applySecurityHeaders(response);
    expect((response as MockNextResponse).headers.get("Referrer-Policy")).toBe("strict-origin-when-cross-origin");
  });

  it("sets Strict-Transport-Security with max-age and includeSubDomains", () => {
    const response = new MockNextResponse() as never;
    applySecurityHeaders(response);
    const hsts = (response as MockNextResponse).headers.get("Strict-Transport-Security");
    expect(hsts).toContain("max-age=63072000");
    expect(hsts).toContain("includeSubDomains");
  });
});

describe("safeCspUrl", () => {
  it("returns empty string for undefined", () => {
    expect(safeCspUrl(undefined)).toBe("");
  });

  it("returns empty string for empty string", () => {
    expect(safeCspUrl("")).toBe("");
  });

  it("returns the URL for a valid https URL", () => {
    expect(safeCspUrl("https://api.example.com")).toBe("https://api.example.com");
  });

  it("returns the URL for a valid https URL with port", () => {
    expect(safeCspUrl("https://api.example.com:8443")).toBe("https://api.example.com:8443");
  });

  it("returns the URL for localhost http", () => {
    expect(safeCspUrl("http://localhost")).toBe("http://localhost");
  });

  it("returns the URL for localhost http with port", () => {
    expect(safeCspUrl("http://localhost:3000")).toBe("http://localhost:3000");
  });

  it("returns empty string for an http non-localhost URL", () => {
    expect(safeCspUrl("http://example.com")).toBe("");
  });

  it("returns empty string for a javascript: URL", () => {
    expect(safeCspUrl("javascript:alert(1)")).toBe("");
  });

  it("returns empty string for a URL with path characters that fail the regex", () => {
    expect(safeCspUrl("https://example.com/path")).toBe("");
  });
});
