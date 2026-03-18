import Script from "next/script";
import type { AccountTracking } from "@/lib/types";

interface AnalyticsProps {
  tracking?: AccountTracking;
}

// Sanitize tracking IDs to prevent script injection via config
const SAFE_ID = /^[A-Za-z0-9-]+$/;
function safeId(value: string | undefined): string | null {
  if (!value || !SAFE_ID.test(value)) return null;
  return value;
}

export function Analytics({ tracking }: AnalyticsProps) {
  if (!tracking) return null;

  const gtmId = safeId(tracking.gtm_container_id);
  const gaId = safeId(tracking.google_analytics_id);
  const pixelId = safeId(tracking.meta_pixel_id);

  return (
    <>
      {/* Google Tag Manager — loads GA4, Google Ads, and custom tags */}
      {gtmId && (
        <Script
          id="gtm-script"
          strategy="afterInteractive"
          src={`https://www.googletagmanager.com/gtm.js?id=${gtmId}`}
        />
      )}

      {/* Google Analytics 4 (standalone, when no GTM) */}
      {gaId && !gtmId && (
        <>
          <Script
            src={`https://www.googletagmanager.com/gtag/js?id=${gaId}`}
            strategy="afterInteractive"
          />
          <Script
            id="ga4-config"
            strategy="afterInteractive"
            src={`/scripts/ga4-init.js?id=${gaId}`}
          />
        </>
      )}

      {/* Meta/Facebook Pixel */}
      {pixelId && (
        <Script
          id="meta-pixel"
          strategy="afterInteractive"
          src={`/scripts/meta-pixel-init.js?id=${pixelId}`}
        />
      )}
    </>
  );
}

/**
 * Fire a conversion event for the CMA form submission.
 * Call this from CmaSection after a successful submission.
 */
export function trackCmaConversion(tracking?: AccountTracking) {
  if (!tracking || typeof window === "undefined") return;

  const w = window as unknown as Record<string, unknown>;

  // Google Ads conversion
  const adsId = safeId(tracking.google_ads_id);
  const adsLabel = safeId(tracking.google_ads_conversion_label);
  if (adsId && adsLabel && typeof w.gtag === "function") {
    (w.gtag as (...args: unknown[]) => void)("event", "conversion", {
      send_to: `${adsId}/${adsLabel}`,
    });
  }

  // GA4 custom event
  if ((tracking.google_analytics_id || tracking.gtm_container_id) && typeof w.gtag === "function") {
    (w.gtag as (...args: unknown[]) => void)("event", "cma_form_submit", {
      event_category: "lead_generation",
      event_label: "cma_request",
    });
  }

  // Meta Pixel Lead event
  if (tracking.meta_pixel_id && typeof w.fbq === "function") {
    (w.fbq as (...args: unknown[]) => void)("track", "Lead");
  }
}
