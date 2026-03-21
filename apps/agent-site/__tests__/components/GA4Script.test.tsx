import { render } from "@testing-library/react";
import { GA4Script } from "@/components/GA4Script";

// Mock next/script to render a simple element we can query
vi.mock("next/script", () => ({
  __esModule: true,
  default: ({ id, src, children }: { id?: string; src?: string; children?: string }) => (
    <script data-testid={id ?? "gtag-src"} data-src={src ?? ""} data-children={children ?? ""} />
  ),
}));

describe("GA4Script", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("returns null when no measurementId is provided", () => {
    const { container } = render(<GA4Script />);
    expect(container.innerHTML).toBe("");
  });

  it("returns null when measurementId is provided but consent not granted", () => {
    const { container } = render(<GA4Script measurementId="G-ABC123" />);
    expect(container.innerHTML).toBe("");
  });

  it("returns null when consent is denied", () => {
    localStorage.setItem("analytics-consent", "denied");
    const { container } = render(<GA4Script measurementId="G-ABC123" />);
    expect(container.innerHTML).toBe("");
  });

  it("returns null when consent is pending (null in storage)", () => {
    // No item set — consent is null
    const { container } = render(<GA4Script measurementId="G-ABC123" />);
    expect(container.innerHTML).toBe("");
  });

  it("renders GA4 scripts when consent is granted", () => {
    localStorage.setItem("analytics-consent", "granted");
    const { getByTestId } = render(<GA4Script measurementId="G-ABC123" />);
    const initScript = getByTestId("ga4-byoa-init");
    expect(initScript.getAttribute("data-children")).toContain("G-ABC123");
  });

  it("includes the measurement ID in the gtag.js src", () => {
    localStorage.setItem("analytics-consent", "granted");
    const { getByTestId } = render(<GA4Script measurementId="G-XYZ789" />);
    const srcScript = getByTestId("gtag-src");
    expect(srcScript.getAttribute("data-src")).toContain("G-XYZ789");
  });

  it("rejects unsafe measurement IDs (script injection prevention)", () => {
    localStorage.setItem("analytics-consent", "granted");
    const { container } = render(<GA4Script measurementId="<script>alert(1)</script>" />);
    expect(container.innerHTML).toBe("");
  });

  it("rejects IDs without G- prefix", () => {
    localStorage.setItem("analytics-consent", "granted");
    const { container } = render(<GA4Script measurementId="UA-12345" />);
    expect(container.innerHTML).toBe("");
  });

  it("rejects empty string measurementId", () => {
    localStorage.setItem("analytics-consent", "granted");
    const { container } = render(<GA4Script measurementId="" />);
    expect(container.innerHTML).toBe("");
  });
});
