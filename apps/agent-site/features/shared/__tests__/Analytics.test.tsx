import { render } from "@testing-library/react";
import { Analytics, trackCmaConversion } from "@/features/shared/Analytics";
import type { AccountTracking } from "@/features/config/types";

// Mock next/script to render a simple element we can query
vi.mock("next/script", () => ({
  __esModule: true,
  default: ({ id, src, children }: { id?: string; src?: string; children?: string }) => (
    <script data-testid={id} data-src={src ?? ""} data-children={children ?? ""} />
  ),
}));

describe("Analytics", () => {
  it("renders nothing when tracking is undefined", () => {
    const { container } = render(<Analytics />);
    expect(container.innerHTML).toBe("");
  });

  it("renders GTM script when gtm_container_id is provided", () => {
    const tracking: AccountTracking = { gtm_container_id: "GTM-ABC123" };
    const { getByTestId } = render(<Analytics tracking={tracking} />);
    const script = getByTestId("gtm-script");
    expect(script.getAttribute("data-src")).toContain("GTM-ABC123");
  });

  it("renders GA4 scripts when google_analytics_id is provided without GTM", () => {
    const tracking: AccountTracking = { google_analytics_id: "G-XYZ789" };
    const { getByTestId } = render(<Analytics tracking={tracking} />);
    const config = getByTestId("ga4-config");
    expect(config.getAttribute("data-children")).toContain("G-XYZ789");
  });

  it("does not render GA4 when GTM is also present", () => {
    const tracking: AccountTracking = {
      google_analytics_id: "G-XYZ789",
      gtm_container_id: "GTM-ABC123",
    };
    const { queryByTestId } = render(<Analytics tracking={tracking} />);
    expect(queryByTestId("ga4-config")).toBeNull();
    expect(queryByTestId("gtm-script")).not.toBeNull();
  });

  it("renders Meta Pixel script when meta_pixel_id is provided", () => {
    const tracking: AccountTracking = { meta_pixel_id: "123456789" };
    const { getByTestId } = render(<Analytics tracking={tracking} />);
    const script = getByTestId("meta-pixel");
    expect(script.getAttribute("data-children")).toContain("123456789");
  });

  it("renders all scripts when all tracking IDs are present (GTM takes precedence over GA4)", () => {
    const tracking: AccountTracking = {
      gtm_container_id: "GTM-ABC123",
      google_analytics_id: "G-XYZ789",
      meta_pixel_id: "123456789",
    };
    const { getByTestId, queryByTestId } = render(<Analytics tracking={tracking} />);
    expect(getByTestId("gtm-script")).toBeTruthy();
    expect(queryByTestId("ga4-config")).toBeNull(); // GTM takes precedence
    expect(getByTestId("meta-pixel")).toBeTruthy();
  });

  it("rejects unsafe tracking IDs (script injection prevention)", () => {
    const tracking: AccountTracking = {
      gtm_container_id: "<script>alert(1)</script>",
      google_analytics_id: "G-VALID",
    };
    const { queryByTestId } = render(<Analytics tracking={tracking} />);
    // GTM should be rejected due to unsafe chars, GA4 should render
    expect(queryByTestId("gtm-script")).toBeNull();
    expect(queryByTestId("ga4-config")).not.toBeNull();
  });

  it("handles empty string tracking IDs gracefully", () => {
    const tracking: AccountTracking = {
      gtm_container_id: "",
      google_analytics_id: "",
      meta_pixel_id: "",
    };
    const { container } = render(<Analytics tracking={tracking} />);
    expect(container.querySelectorAll("script").length).toBe(0);
  });
});

describe("trackCmaConversion", () => {
  let mockGtag: ReturnType<typeof vi.fn>;
  let mockFbq: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    mockGtag = vi.fn();
    mockFbq = vi.fn();
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (window as any).gtag = mockGtag;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (window as any).fbq = mockFbq;
  });

  afterEach(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    delete (window as any).gtag;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    delete (window as any).fbq;
  });

  it("does nothing when tracking is undefined", () => {
    trackCmaConversion(undefined);
    expect(mockGtag).not.toHaveBeenCalled();
    expect(mockFbq).not.toHaveBeenCalled();
  });

  it("fires Google Ads conversion event", () => {
    trackCmaConversion({
      google_ads_id: "AW-12345",
      google_ads_conversion_label: "abc123",
    });
    expect(mockGtag).toHaveBeenCalledWith("event", "conversion", {
      send_to: "AW-12345/abc123",
    });
  });

  it("fires GA4 custom event when google_analytics_id is set", () => {
    trackCmaConversion({ google_analytics_id: "G-XYZ789" });
    expect(mockGtag).toHaveBeenCalledWith("event", "cma_form_submit", {
      event_category: "lead_generation",
      event_label: "cma_request",
    });
  });

  it("fires GA4 custom event when gtm_container_id is set", () => {
    trackCmaConversion({ gtm_container_id: "GTM-ABC123" });
    expect(mockGtag).toHaveBeenCalledWith("event", "cma_form_submit", {
      event_category: "lead_generation",
      event_label: "cma_request",
    });
  });

  it("fires Meta Pixel Lead event", () => {
    trackCmaConversion({ meta_pixel_id: "123456789" });
    expect(mockFbq).toHaveBeenCalledWith("track", "Lead");
  });

  it("fires all conversion events when all tracking is configured", () => {
    trackCmaConversion({
      google_ads_id: "AW-12345",
      google_ads_conversion_label: "abc123",
      google_analytics_id: "G-XYZ789",
      meta_pixel_id: "123456789",
    });
    expect(mockGtag).toHaveBeenCalledWith("event", "conversion", {
      send_to: "AW-12345/abc123",
    });
    expect(mockGtag).toHaveBeenCalledWith("event", "cma_form_submit", {
      event_category: "lead_generation",
      event_label: "cma_request",
    });
    expect(mockFbq).toHaveBeenCalledWith("track", "Lead");
  });

  it("does not fire Google Ads when ads_id is unsafe", () => {
    trackCmaConversion({
      google_ads_id: "bad<script>",
      google_ads_conversion_label: "abc123",
    });
    expect(mockGtag).not.toHaveBeenCalled();
  });

  it("does not fire Meta Pixel when fbq is not a function", () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    delete (window as any).fbq;
    trackCmaConversion({ meta_pixel_id: "123456789" });
    // No error thrown, just silently skipped
  });

  it("does not fire Google Ads when gtag is not a function", () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    delete (window as any).gtag;
    trackCmaConversion({
      google_ads_id: "AW-12345",
      google_ads_conversion_label: "abc123",
    });
    // No error thrown
  });

  it("does not fire Google Ads when conversion label is missing", () => {
    trackCmaConversion({
      google_ads_id: "AW-12345",
    });
    // gtag should NOT be called for conversion (missing label)
    // but also no GA4 or GTM configured, so no calls at all
    expect(mockGtag).not.toHaveBeenCalled();
  });
});
