import { render } from "@testing-library/react";
import { describe, it, expect, afterEach, vi } from "vitest";

describe("GA4Script", () => {
  const originalEnv = process.env;

  afterEach(() => {
    process.env = originalEnv;
    vi.resetModules();
  });

  it("renders nothing when NEXT_PUBLIC_GA4_ID is not set", async () => {
    process.env = { ...originalEnv, NEXT_PUBLIC_GA4_ID: undefined };
    const { GA4Script } = await import("../GA4Script");
    const { container } = render(<GA4Script />);
    expect(container).toBeEmptyDOMElement();
  });

  it("renders script elements when NEXT_PUBLIC_GA4_ID is set", async () => {
    process.env = { ...originalEnv, NEXT_PUBLIC_GA4_ID: "G-TEST12345" };
    const { GA4Script } = await import("../GA4Script");
    // GA4Script renders two <script> elements (async loader + inline config).
    // jsdom may not preserve <script> elements in the container since React
    // treats script tags differently. We verify by checking the rendered output
    // via the container's innerHTML or script query.
    const { container } = render(<GA4Script />);
    const scripts = container.querySelectorAll("script");
    // Expect at least one script element rendered (loader or config)
    expect(scripts.length).toBeGreaterThanOrEqual(1);
    // Verify at least one script references the GA4 ID
    const hasGa4Id = Array.from(scripts).some(
      (s) => s.getAttribute("src")?.includes("G-TEST12345") || s.innerHTML.includes("G-TEST12345")
    );
    expect(hasGa4Id).toBe(true);
  });

  it("includes the GA4 measurement ID in the rendered output", async () => {
    process.env = { ...originalEnv, NEXT_PUBLIC_GA4_ID: "G-MYID999" };
    const { GA4Script } = await import("../GA4Script");
    const { container } = render(<GA4Script />);
    const scripts = container.querySelectorAll("script");
    const hasId = Array.from(scripts).some(
      (s) => s.getAttribute("src")?.includes("G-MYID999") || s.innerHTML.includes("G-MYID999")
    );
    expect(hasId).toBe(true);
  });
});
